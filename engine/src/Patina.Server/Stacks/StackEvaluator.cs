using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;

namespace Patina.Server.Stacks;

/// <summary>
///     Turns the journal into pixels, one stack at a time. Strokes are never skipped, since they are what the pixels
///     are, but the composite is: a drag faster than the GPU sends its stamps in bigger parts. What a Worker has been
///     told is recorded per connection, so a new Worker is sent the document and every stroke since the newest
///     checkpoint, and catches up to the same bytes (ADR 0009).
/// </summary>
public sealed class StackEvaluator(
    StackSessions sessions,
    WorkerLink worker,
    PreviewStore store,
    ServerIdentity identity,
    ILogger<StackEvaluator> logger
) : BackgroundService {
    const int PreviewSide = 512;
    const int StampsPerCheckpoint = 2048;
    const int CheckpointsKept = 3;
    static readonly TimeSpan LostWorkerPause = TimeSpan.FromMilliseconds(20);
    static readonly TimeSpan WorkerPatience = TimeSpan.FromSeconds(30);

    readonly Dictionary<string, Told> told = new(StringComparer.Ordinal);
    readonly Dictionary<string, Marks> marks = new(StringComparer.Ordinal);

    /// <summary>Writes the pixels beside the stack, then the stack.</summary>
    /// <exception cref="GraphFileChangedException">The file changed behind the session.</exception>
    /// <exception cref="TimeoutException">No Worker came.</exception>
    public async Task<StackEditResult> SaveAsync(
        StackSession session,
        bool force,
        CancellationToken cancellationToken
    ) {
        var text = session.PrepareSave(force);
        KeepBase(session);
        await ReachedAsync(session, cancellationToken);
        var connection = await ConnectionAsync(cancellationToken);
        await connection.SendAsync(
            new() { Type = IpcTypes.StackSave, StackSave = new(session.Id, null) },
            cancellationToken
        );
        return session.FinishSave(text);
    }

    /// <summary>A digest of each map, from what the strokes left or from nothing, and files when a folder is given.</summary>
    /// <exception cref="TimeoutException">No Worker came.</exception>
    /// <exception cref="IpcException">It would not evaluate.</exception>
    public async Task<IReadOnlyList<StackPixels>> PixelsAsync(
        StackSession session,
        bool clean,
        string? folder,
        CancellationToken cancellationToken
    ) {
        await ReachedAsync(session, cancellationToken);
        var connection = await ConnectionAsync(cancellationToken);
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.StackPixels, StackPixels = new(session.Id, clean, folder) },
            cancellationToken
        );
        return reply.StackPicture ?? [];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            while (true) {
                await sessions.WaitAsync(stoppingToken);
                while (await DrainAsync(stoppingToken)) {
                    // Another pass: something may have changed while the last stack was evaluating.
                }
            }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // Stopping: nothing to report.
        }
    }

    /// <summary>The newest checkpoint on the way to these strokes: an undone stroke is in none that can be used.</summary>
    static string? Newest(Marks held, IReadOnlyList<StackJournalEntry> standing) =>
        held.Checkpoints.LastOrDefault(mark => Prefix(mark.Standing, standing))?.Folder ?? held.Base;

    static bool Prefix(IReadOnlyList<long> start, IReadOnlyList<StackJournalEntry> standing) =>
        start.Count <= standing.Count && start.Select((id, index) => standing[index].Id == id).All(static same => same);

    async Task<IpcConnection> ConnectionAsync(CancellationToken cancellationToken) {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(WorkerPatience);
        try {
            return await worker.ConnectionAsync(patience.Token);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new TimeoutException("No Worker is connected.");
        }
    }

    /// <summary>Waits until the Worker's pixels are what the journal says, which a save and a read both need.</summary>
    static async Task ReachedAsync(StackSession session, CancellationToken cancellationToken) {
        session.Evaluate();
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(WorkerPatience);
        try {
            var sequence = session.Sequence;
            while (session.Pending() is not null) {
                await session.WaitAsync(sequence, patience.Token);
                sequence = session.Sequence;
            }
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new TimeoutException("No Worker reached this stack.");
        }
    }

    async Task<bool> DrainAsync(CancellationToken cancellationToken) {
        await ForgetClosedAsync(cancellationToken);
        var worked = 0;
        foreach (var session in sessions.All) {
            worked += await StepAsync(session, cancellationToken) ? 1 : 0;
        }

        return worked > 0;
    }

    /// <summary>Brings one stack up to date. A lost Worker costs a retry: its successor is told everything.</summary>
    async Task<bool> StepAsync(StackSession session, CancellationToken cancellationToken) {
        var ready = await worker.WhenThereIsWorkAsync(session.Pending, cancellationToken);
        if (ready is null) {
            return false;
        }

        try {
            await EvaluateAsync(session, ready.Value.Connection, ready.Value.Work, cancellationToken);
        } catch (Exception e) when (e is IOException or TimeoutException) {
            told.Remove(session.Id);
            ServerLog.GraphWorkerLost(logger, session.Path);
            session.Evaluate();
            await Task.Delay(LostWorkerPause, cancellationToken);
        } catch (IpcException e) {
            ServerLog.GraphFailed(logger, session.Path, e.Message);
            session.Fail(ready.Value.Work, e.Message);
        }

        return true;
    }

    async Task EvaluateAsync(
        StackSession session,
        IpcConnection connection,
        StackWork work,
        CancellationToken cancellationToken
    ) {
        var held = marks.TryGetValue(session.Id, out var known) ? known : marks[session.Id] = new();
        if (!told.TryGetValue(session.Id, out var state)
            || !ReferenceEquals(state.Connection, connection)
            || !Prefix(state.Base, work.Standing)) {
            state = new(connection);
            var load = new StackLoad(
                session.Id,
                session.Path,
                work.Document,
                session.Set,
                session.Folder,
                session.ImageRoots,
                null,
                Newest(held, work.Standing),
                session.Created
            );
            await connection.SendAsync(new() { Type = IpcTypes.StackLoad, StackLoad = load }, cancellationToken);
            state.Document = work.Document;

            // What a checkpoint holds, the Worker has: nothing before it is sent again.
            if (held.Checkpoints.LastOrDefault(mark => mark.Folder == load.Checkpoint) is { } start) {
                foreach (var id in start.Standing) {
                    state.Sent[id] = (start.Stamps[id], false);
                }

                state.Base = start.Standing;
            }
        }

        var parts = session.PartsAfter(work.Standing, state.Sent);
        await connection.SendAsync(
            new() {
                Type = IpcTypes.StackSync,
                StackSync = new(
                    session.Id,
                    work.Document == state.Document ? null : work.Document,
                    [..work.Standing.Select(static entry => entry.Id).Except(state.Base)],
                    parts,
                    work.Forget
                )
            },
            cancellationToken
        );
        state.Document = work.Document;
        told[session.Id] = state;
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.StackEvaluate, StackEvaluate = new(session.Id, PreviewSide, store.Root) },
            cancellationToken
        );
        session.Publish(work, reply.StackResult!);
        held.Stamps += parts.Sum(static part => part.Stamps.Count);
        await CheckpointAsync(session, connection, work, held, cancellationToken);
    }

    /// <summary>
    ///     Bounds what a new Worker has to stamp again. Only between strokes: a canvas in the middle of a drag is a
    ///     picture no journal entry describes.
    /// </summary>
    async Task CheckpointAsync(
        StackSession session,
        IpcConnection connection,
        StackWork work,
        Marks held,
        CancellationToken cancellationToken
    ) {
        if (held.Stamps < StampsPerCheckpoint || work.Standing.Any(static entry => entry.Open)) {
            return;
        }

        KeepBase(session);
        var folder = Path.Combine(CheckpointRoot(session), Guid.NewGuid().ToString("N"));
        await connection.SendAsync(
            new() { Type = IpcTypes.StackCheckpoint, StackSave = new(session.Id, folder) },
            cancellationToken
        );
        held.Stamps = 0;
        held.Checkpoints.Add(
            new(
                folder,
                [..work.Standing.Select(static entry => entry.Id)],
                work.Standing.ToDictionary(static entry => entry.Id, static entry => entry.Stamps.Count)
            )
        );
        if (held.Checkpoints.Count > CheckpointsKept) {
            Directory.Delete(held.Checkpoints[0].Folder, true);
            held.Checkpoints.RemoveAt(0);
        }
    }

    /// <summary>
    ///     A save overwrites what the journal starts from, so what the session opened is copied first. It is in the
    ///     cache: deleting that costs the undo past a save after a lost Worker, and nothing a file holds.
    /// </summary>
    void KeepBase(StackSession session) {
        var held = marks.TryGetValue(session.Id, out var known) ? known : marks[session.Id] = new();
        if (held.Base is not null) {
            return;
        }

        var folder = Path.Combine(CheckpointRoot(session), "base");
        Directory.CreateDirectory(folder);
        foreach (var paint in session.PaintFiles) {
            var file = Path.GetFullPath(paint, Path.GetDirectoryName(session.Path)!);
            if (File.Exists(file)) {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(paint)), true);
            }
        }

        held.Base = folder;
    }

    string CheckpointRoot(StackSession session) => CheckpointRoot(session.Id);

    string CheckpointRoot(string session) =>
        Path.Combine(
            identity.Library is { } library
                ? LibraryLayout.PaintPath(library)
                : Path.Combine(Path.GetTempPath(), $"patina-paint-{Environment.ProcessId}"),
            session
        );

    async Task ForgetClosedAsync(CancellationToken cancellationToken) {
        foreach (var id in sessions.TakeClosed()) {
            if (marks.Remove(id) && Directory.Exists(CheckpointRoot(id))) {
                Directory.Delete(CheckpointRoot(id), true);
            }

            if (told.Remove(id, out var held)) {
                try {
                    await held.Connection.SendAsync(
                        new() { Type = IpcTypes.StackClose, StackClose = id },
                        cancellationToken
                    );
                } catch (Exception e) when (e is IOException or TimeoutException or IpcException) {
                    // A Worker that has gone holds nothing to forget.
                }
            }
        }
    }

    /// <summary>What one connection has been told about one stack.</summary>
    sealed class Told(IpcConnection connection) {
        public IpcConnection Connection { get; } = connection;

        public string? Document { get; set; }

        public Dictionary<long, (int Stamps, bool Open)> Sent { get; } = [];

        /// <summary>The strokes a checkpoint already holds, which the Worker has no record of and cannot undo.</summary>
        public IReadOnlyList<long> Base { get; set; } = [];
    }

    /// <summary>The checkpoints of one session.</summary>
    sealed class Marks {
        public string? Base { get; set; }

        public int Stamps { get; set; }

        public List<Mark> Checkpoints { get; } = [];
    }

    sealed record Mark(string Folder, IReadOnlyList<long> Standing, IReadOnlyDictionary<long, int> Stamps);
}
