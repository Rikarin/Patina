using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Viewport;
using System.Collections.Concurrent;

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
    ViewportSession viewport,
    GraphSessions graphs,
    AutosaveGate autosave,
    ILogger<StackEvaluator> logger
) : BackgroundService {
    const int PreviewSide = 512;
    const int StampsPerCheckpoint = 2048;
    const int CheckpointsKept = 3;
    const int TrimInterval = 64;
    static readonly TimeSpan LostWorkerPause = TimeSpan.FromMilliseconds(20);
    static readonly TimeSpan WorkerPatience = TimeSpan.FromSeconds(30);

    readonly Dictionary<string, Told> told = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, Marks> marks = new(StringComparer.Ordinal);
    int untilTrim = TrimInterval;

    /// <summary>Writes the pixels beside the stack, then the stack.</summary>
    /// <exception cref="GraphFileChangedException">The file changed behind the session.</exception>
    /// <exception cref="TimeoutException">No Worker came.</exception>
    /// <exception cref="IpcException">The stack would not evaluate, or the Worker would not write it.</exception>
    public async Task<StackEditResult> SaveAsync(
        StackSession session,
        bool force,
        CancellationToken cancellationToken
    ) {
        var (text, revision) = session.PrepareSave(force);
        KeepBase(session);
        await ReachedAsync(session, cancellationToken);
        if (session.EvaluationError is { } failed) {
            throw new IpcException($"The stack did not evaluate, so saving would lose work: {failed}");
        }

        var connection = await ConnectionAsync(cancellationToken);
        await connection.SendAsync(
            new() { Type = IpcTypes.StackSave, StackSave = new(session.Id, null) },
            cancellationToken
        );
        var saved = session.FinishSave(text, revision);
        if (identity.Library is { } library) {
            autosave.Forget(library, session.Path);
        }

        return saved;
    }

    /// <summary>
    ///     A digest of each map, from what the strokes left or from nothing, and files when a folder is
    ///     given.
    /// </summary>
    /// <exception cref="TimeoutException">No Worker came.</exception>
    /// <exception cref="IpcException">It would not evaluate.</exception>
    public async Task<IReadOnlyList<StackPixels>> PixelsAsync(
        StackSession session,
        StackReading reading,
        CancellationToken cancellationToken
    ) {
        await ReachedAsync(session, cancellationToken);
        if (session.EvaluationError is { } failed) {
            // The real reason. The Worker can only say it does not hold a stack that never loaded.
            throw new IpcException(failed);
        }

        var connection = await ConnectionAsync(cancellationToken);
        var ask = new StackPixelsAsk(
            session.Id,
            reading.Clean,
            reading.Folder,
            reading.Sampled,
            reading.Layer,
            reading.File,
            reading.Mask
        );
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.StackPixels, StackPixels = ask },
            cancellationToken
        );
        return reply.StackPicture ?? [];
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ServiceLoop.RunAsync(logger, nameof(StackEvaluator), LoopAsync, stoppingToken);

    /// <summary>A new Worker holds no stack, and a client looking at one has no edit coming to bring it back.</summary>
    void WorkerArrived() {
        foreach (var session in sessions.All) {
            session.Evaluate();
        }
    }

    async Task LoopAsync(CancellationToken stoppingToken) {
        worker.Connected -= WorkerArrived;
        worker.Connected += WorkerArrived;
        while (true) {
            await sessions.WaitAsync(stoppingToken);
            while (await DrainAsync(stoppingToken)) {
                // Another pass: something may have changed while the last stack was evaluating.
            }
        }
    }

    /// <summary>
    ///     The newest checkpoint on the way to these strokes: an undone stroke is in none that can be
    ///     used.
    /// </summary>
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
            throw new RendererMissingException();
        }
    }

    /// <summary>
    ///     Waits until the Worker's pixels are what the journal says, which a save and a read both need. It gives up
    ///     only when no Worker comes, never because the one connected is slow (#275).
    /// </summary>
    async Task ReachedAsync(StackSession session, CancellationToken cancellationToken) {
        // ⚠ A paused stack evaluates for whoever waits here, or a save would wait for a resume that may never come.
        using var reading = session.Reading();
        await WorkerWait.UntilAsync(
            session,
            session.Pending,
            worker.ConnectionAsync,
            WorkerPatience,
            cancellationToken
        );
    }

    /// <summary>
    ///     Copies layers out of a stack with their canvases as the journal leaves them, which only a Worker holds, so
    ///     that what is pasted is what stood at the copy whatever happens to these layers after.
    /// </summary>
    /// <exception cref="StackEditRefusedException">A layer is not in the stack.</exception>
    /// <exception cref="TimeoutException">No Worker came.</exception>
    /// <exception cref="IpcException">The stack would not evaluate, or the Worker would not write it.</exception>
    public async Task<StackClip> CopyAsync(
        StackSession session,
        IReadOnlyList<string> layers,
        StackClips clips,
        CancellationToken cancellationToken
    ) {
        var (text, count, canvases, width, height) = session.Copied(layers);
        var folder = clips.NewFolder();
        if (canvases.Count > 0) {
            await ReachedAsync(session, cancellationToken);
            if (session.EvaluationError is { } failed) {
                throw new IpcException(failed);
            }

            var connection = await ConnectionAsync(cancellationToken);
            await connection.SendAsync(
                new() { Type = IpcTypes.StackCopy, StackCopy = new(session.Id, canvases, folder) },
                cancellationToken
            );
        }

        var id = Guid.NewGuid().ToString("N");
        clips.Add(
            new(id, text, folder, session.Path, width, height, session.Set),
            held => sessions.All.Any(open => open.Reads(held))
        );
        return new(id, count, session.Path);
    }

    /// <summary>Writes a stack's pixels to a folder of autosaved work, leaving its files as they are.</summary>
    public async Task KeepAsync(StackSession session, string folder, CancellationToken cancellationToken) {
        await ReachedAsync(session, cancellationToken);
        var connection = await ConnectionAsync(cancellationToken);
        await connection.SendAsync(
            new() { Type = IpcTypes.StackCheckpoint, StackSave = new(session.Id, folder) },
            cancellationToken
        );
    }

    /// <summary>The layer on top at a place on the map, which the Worker weighs and the Server names (#136).</summary>
    /// <exception cref="TimeoutException">No Worker came.</exception>
    /// <exception cref="IpcException">The stack would not evaluate.</exception>
    public async Task<string?> FindAsync(StackSession session, float u, float v, CancellationToken cancellationToken) {
        await ReachedAsync(session, cancellationToken);
        if (session.EvaluationError is { } failed) {
            throw new IpcException(failed);
        }

        var connection = await ConnectionAsync(cancellationToken);
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.StackFind, StackFind = new(session.Id, u, v) },
            cancellationToken
        );
        return reply.FoundLayer;
    }

    /// <summary>
    ///     A picture of a brush, a stroke of it drawn small by the Worker into the preview cache, or one stamp of it,
    ///     with the alphas this stack's strokes read. Not a stroke: nothing is journalled and the stack is not touched.
    /// </summary>
    /// <remarks>It waits on nothing the stack does and needs no device, so it draws while a stroke evaluates.</remarks>
    /// <exception cref="TimeoutException">No Worker came.</exception>
    /// <exception cref="IpcException">The Worker could not draw it.</exception>
    public async Task<string?> PreviewBrushAsync(
        StackSession session,
        StackBrush brush,
        bool stamp,
        CancellationToken cancellationToken
    ) {
        var connection = await ConnectionAsync(cancellationToken);
        var folder = Path.GetDirectoryName(session.Path) ?? string.Empty;
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.BrushPreview, BrushPreview = new(folder, brush, store.Root, stamp) },
            cancellationToken
        );
        return reply.ThumbnailHash;
    }

    /// <summary>What each channel of a stack holds at a place on the map, read back by the Worker as numbers.</summary>
    /// <exception cref="TimeoutException">No Worker came.</exception>
    /// <exception cref="IpcException">The stack would not evaluate.</exception>
    public async Task<StackSample> SampleAsync(
        StackSession session,
        float u,
        float v,
        CancellationToken cancellationToken
    ) {
        await ReachedAsync(session, cancellationToken);
        if (session.EvaluationError is { } failed) {
            throw new IpcException(failed);
        }

        var connection = await ConnectionAsync(cancellationToken);
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.StackSample, StackSample = new(session.Id, u, v) },
            cancellationToken
        );
        return new(reply.Sampled ?? []);
    }

    /// <summary>
    ///     What the Worker measures of a stack's set before export: its layout and the values in its maps, as numbers.
    /// </summary>
    /// <exception cref="IpcException">The stack does not evaluate, or its mesh does not read.</exception>
    public async Task<StackMeasured> MeasureAsync(StackSession session, CancellationToken cancellationToken) {
        await ReachedAsync(session, cancellationToken);
        if (session.EvaluationError is { } failed) {
            throw new IpcException(failed);
        }

        var connection = await ConnectionAsync(cancellationToken);
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.StackCheck, StackCheck = session.Id },
            cancellationToken
        );
        return reply.StackMeasured ?? throw new IpcException("The Worker measured nothing.");
    }

    /// <summary>Each fill of a stack alone, which the Worker keeps until the layer changes.</summary>
    public async Task<IReadOnlyList<StackLayerThumbnail>> ThumbnailsAsync(
        StackSession session,
        CancellationToken cancellationToken
    ) {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(WorkerPatience);
        try {
            if (session.EvaluationError is { } failed) {
                throw new IpcException(failed);
            }

            var connection = await worker.ConnectionAsync(patience.Token);
            var reply = await connection.SendAsync(
                new() { Type = IpcTypes.StackThumbnails, StackThumbnails = new(session.Id, store.Root) },
                patience.Token
            );
            return reply.StackLayerThumbnails ?? [];
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new RendererMissingException();
        }
    }

    async Task<bool> DrainAsync(CancellationToken cancellationToken) {
        await ForgetClosedAsync(cancellationToken);
        var worked = 0;

        // Whatever a save or a read waits on goes first, not behind other documents (#275).
        foreach (var session in sessions.All.OrderByDescending(static session => session.Awaited)) {
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
        } catch (Exception e) when (e is not OperationCanceledException) {
            // One stack's fault stays that stack's, and the Worker may be holding less than it was told.
            told.Remove(session.Id);
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
        var held = marks.GetOrAdd(session.Id, static _ => new());
        if (!told.TryGetValue(session.Id, out var state)
            || !ReferenceEquals(state.Connection, connection)
            || state.MeshMaps != session.MeshMaps
            || state.BakeEdits != session.BakeEdits
            || state.CompoundEdits != session.CompoundEdits
            || !Prefix(state.Base, work.Standing)) {
            state = new(connection) {
                MeshMaps = session.MeshMaps, BakeEdits = session.BakeEdits, CompoundEdits = session.CompoundEdits
            };
            var load = new StackLoad(
                session.Id,
                session.Path,
                work.Document,
                session.Set,
                session.Folder,
                session.ImageRoots,
                session.MeshMaps,
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
                    work.Forget,
                    work.QuickMask
                )
            },
            cancellationToken
        );
        state.Document = work.Document;
        told[session.Id] = state;

        // ⚠ The viewport is what a drag watches, and a preview costs an encode per map per stamp. The last part of
        // the stroke has no open strokes, so the maps a client lists catch up as it ends.
        var side = viewport.Wears(session.Id)
            && work.Standing.Any(static entry => entry is { Open: true, View: not null })
                ? 0
                : PreviewSide;
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.StackEvaluate, StackEvaluate = new(session.Id, side, store.Root, work.Proxy) },
            cancellationToken
        );
        session.Publish(work, reply.StackResult!);
        viewport.Repainted(session.Id, work.Revision);
        viewport.Laid(session.Id, session.Set, session.UvSet);
        held.Stamps += parts.Sum(static part => part.Stamps.Count);
        await CheckpointAsync(session, connection, work, held, state, cancellationToken);
        TrimNow();
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
        Told told,
        CancellationToken cancellationToken
    ) {
        // ⚠ From what this Worker was told, not the live journal: a stroke can grow after its parts were read, and a
        // checkpoint that claimed those stamps would keep them from a later replay, leaving pixels silently different.
        if (held.Stamps < StampsPerCheckpoint || work.Standing.Any(entry => told.Sent[entry.Id].Open)) {
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
                work.Standing.ToDictionary(static entry => entry.Id, entry => told.Sent[entry.Id].Stamps)
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
    void TrimNow() {
        if (--untilTrim > 0) {
            return;
        }

        untilTrim = TrimInterval;
        store.Trim(
            [
                ..sessions.All.SelectMany(static session => session.PreviewHashes),
                ..graphs.All.SelectMany(static session => session.PreviewHashes)
            ]
        );
    }

    void KeepBase(StackSession session) {
        var held = marks.GetOrAdd(session.Id, static _ => new());
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
            if (marks.TryRemove(id, out _) && Directory.Exists(CheckpointRoot(id))) {
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

        public string? MeshMaps { get; init; }

        public int BakeEdits { get; init; }

        public int CompoundEdits { get; init; }

        public Dictionary<long, (int Stamps, bool Open)> Sent { get; } = [];

        /// <summary>
        ///     The strokes a checkpoint already holds, which the Worker has no record of and cannot
        ///     undo.
        /// </summary>
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
