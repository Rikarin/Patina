using Patina.Ipc;
using Vixen.NodeGraph;

namespace Patina.Server.Graphs;

/// <summary>
///     Turns documents into previews, one graph at a time. Only the newest revision of a graph counts, so a drag faster
///     than the GPU skips positions and never queues them. What the Worker holds is recorded per connection: a new
///     Worker has nothing, so it is sent the document; the same Worker is sent the numbers that changed, or the
///     document when something else did. A Worker that dies mid-evaluation costs a retry against the next one (ADR
///     0004).
/// </summary>
public sealed class GraphEvaluator(
    GraphSessions sessions,
    WorkerLink worker,
    PreviewStore store,
    ILogger<GraphEvaluator> logger
) : BackgroundService {
    const int PreviewSide = 256;
    const int TrimInterval = 64;
    static readonly TimeSpan LostWorkerPause = TimeSpan.FromMilliseconds(20);

    readonly Dictionary<string, Told> told = new(StringComparer.Ordinal);
    int untilTrim = TrimInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            while (true) {
                await sessions.WaitAsync(stoppingToken);
                while (await DrainAsync(stoppingToken)) {
                    // Another pass: something may have changed while the last graph was evaluating.
                }
            }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // Stopping: nothing to report.
        }
    }

    async Task<bool> DrainAsync(CancellationToken cancellationToken) {
        await ForgetClosedAsync(cancellationToken);
        var worked = false;
        foreach (var session in sessions.All) {
            if (session.Pending() is not { } work) {
                continue;
            }

            worked = true;
            try {
                await EvaluateAsync(session, work, cancellationToken);
            } catch (Exception e) when (e is IOException or TimeoutException) {
                // Nothing is lost: the next Worker gets the document and this evaluation runs again.
                ServerLog.GraphWorkerLost(logger, session.Path);
                await Task.Delay(LostWorkerPause, cancellationToken);
            } catch (IpcException e) {
                ServerLog.GraphFailed(logger, session.Path, e.Message);
                session.Fail(work, e.Message);
            }
        }

        return worked;
    }

    async Task EvaluateAsync(GraphSession session, GraphWork work, CancellationToken cancellationToken) {
        var connection = await worker.ConnectionAsync(cancellationToken);
        var values = told.GetValueOrDefault(session.Id) is { } held && ReferenceEquals(held.Connection, connection)
            ? GraphDocuments.Values(held.Document, work.Document)
            : null;
        if (values is { Count: 0 } && !work.Forced) {
            session.Settle(work);
            return;
        }

        if (values is null) {
            told.Remove(session.Id);
            var load = new GraphLoad(session.Id, GraphDocuments.Text(work.Document), session.Folder);
            await connection.SendAsync(new() { Type = IpcTypes.GraphLoad, GraphLoad = load }, cancellationToken);
        }

        var reply = await connection.SendAsync(
            new() {
                Type = IpcTypes.GraphEvaluate, GraphEvaluate = new(session.Id, values ?? [], PreviewSide, store.Root)
            },
            cancellationToken
        );
        told[session.Id] = new(connection, work.Document);
        session.Publish(work, reply.GraphResult!);
        TrimNow();
    }

    /// <summary>A drag leaves a preview per position behind, and none of them is worth keeping for long.</summary>
    void TrimNow() {
        if (--untilTrim > 0) {
            return;
        }

        untilTrim = TrimInterval;
        store.Trim(sessions.All.SelectMany(static session => session.PreviewHashes));
    }

    async Task ForgetClosedAsync(CancellationToken cancellationToken) {
        foreach (var id in sessions.TakeClosed()) {
            if (told.Remove(id, out var held)) {
                try {
                    await held.Connection.SendAsync(
                        new() { Type = IpcTypes.GraphClose, GraphClose = id },
                        cancellationToken
                    );
                } catch (Exception e) when (e is IOException or TimeoutException or IpcException) {
                    // A Worker that has gone holds nothing to forget.
                }
            }
        }
    }

    sealed record Told(IpcConnection Connection, NodeGraphAsset Document);
}
