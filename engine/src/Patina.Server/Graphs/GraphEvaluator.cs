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
    static readonly TimeSpan WorkerPatience = TimeSpan.FromSeconds(10);

    readonly Dictionary<string, Told> told = new(StringComparer.Ordinal);
    int untilTrim = TrimInterval;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ServiceLoop.RunAsync(logger, nameof(GraphEvaluator), LoopAsync, stoppingToken);

    async Task LoopAsync(CancellationToken stoppingToken) {
        while (true) {
            await sessions.WaitAsync(stoppingToken);
            while (await DrainAsync(stoppingToken)) {
                // Another pass: something may have changed while the last graph was evaluating.
            }
        }
    }

    /// <summary>Has the Worker write a node's picture at full size to the cache, and says which file that is.</summary>
    /// <exception cref="IpcException">The Worker holds no such picture.</exception>
    /// <exception cref="TimeoutException">No Worker came, or it did not answer.</exception>
    public async Task<GraphFullImage> ImageAsync(
        GraphSession session,
        int node,
        string port,
        CancellationToken cancellationToken
    ) {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(WorkerPatience);
        try {
            var connection = await worker.ConnectionAsync(patience.Token);
            var reply = await connection.SendAsync(
                new() { Type = IpcTypes.GraphImage, GraphImage = new(session.Id, node, port, store.Root) },
                cancellationToken
            );
            return reply.GraphFullImage!;
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new TimeoutException("No Worker is connected.");
        }
    }

    async Task<bool> DrainAsync(CancellationToken cancellationToken) {
        await ForgetClosedAsync(cancellationToken);
        var worked = false;
        foreach (var session in sessions.All) {
            if (await worker.WhenThereIsWorkAsync(session.Pending, cancellationToken) is not var (connection, work)) {
                continue;
            }

            worked = true;
            try {
                await EvaluateAsync(session, connection, work, cancellationToken);
            } catch (Exception e) when (e is IOException or TimeoutException) {
                // Nothing is lost: the next Worker gets the document and this evaluation runs again.
                ServerLog.GraphWorkerLost(logger, session.Path);
                await Task.Delay(LostWorkerPause, cancellationToken);
            } catch (Exception e) when (e is not OperationCanceledException) {
                // One graph's fault stays that graph's.
                ServerLog.GraphFailed(logger, session.Path, e.Message);
                session.Fail(work, e.Message);
            }
        }

        return worked;
    }

    async Task EvaluateAsync(
        GraphSession session,
        IpcConnection connection,
        GraphWork work,
        CancellationToken cancellationToken
    ) {
        var values = !work.Reload
            && told.GetValueOrDefault(session.Id) is { } held
            && ReferenceEquals(held.Connection, connection)
                ? GraphDocuments.Values(held.Document, work.Document)
                : null;
        if (values is { Count: 0 } && !work.Forced) {
            session.Settle(work);
            return;
        }

        if (values is null) {
            told.Remove(session.Id);
            var load = new GraphLoad(
                session.Id,
                GraphDocuments.Text(work.Document),
                session.Folder,
                session.ImageRoots,
                session.MeshMaps
            );
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
