using Microsoft.Extensions.Options;
using Patina.Contracts;
using Patina.Ipc;
using System.Buffers.Binary;
using System.Diagnostics;

namespace Patina.Server.Viewport;

/// <summary>A published frame, held for one subscriber: release it when finished with.</summary>
/// <param name="Slot">The pixels, in shared memory.</param>
/// <param name="Header">The frame header (<see cref="ViewportSession.HeaderBytes" /> bytes, little-endian).</param>
/// <param name="PixelBytes">How many bytes of the slot are this frame.</param>
public sealed record PublishedFrame(FrameSlot Slot, byte[] Header, int PixelBytes);

/// <summary>
///     Where frames go. WebSocket is the one transport today (ADR 0003); the session knows nothing about it.
/// </summary>
public interface IFrameSubscriber {
    /// <summary>
    ///     Takes a frame it now owns a hold on. It must not block, and a slow subscriber drops frames, not time.
    /// </summary>
    void Offer(PublishedFrame frame);

    /// <summary>How long the subscriber took over its last whole frame, for the render scale.</summary>
    double LastSendMilliseconds { get; }
}

/// <summary>
///     The viewport's state and the loop that turns it into frames. The Server owns the state; a frame is rendered only
///     when it has changed, and only the newest input counts, so a slow link skips camera positions and never queues
///     them. When a Worker dies the state stays, and the loop tells the next Worker the mesh and the camera again.
/// </summary>
public sealed class ViewportSession(
    WorkerLink worker,
    BakePreviewSession bakePreview,
    FrameMemory memory,
    IOptions<ServerOptions> options,
    ILogger<ViewportSession> logger
) : BackgroundService {
    /// <summary>The binary header in front of a frame's pixels on the wire.</summary>
    public const int HeaderBytes = 32;

    const uint Magic = 0x464E5450; // "PTNF"
    const int DefaultWidth = 1280;
    const int DefaultHeight = 720;
    static readonly TimeSpan LostWorkerPause = TimeSpan.FromMilliseconds(20);
    static readonly TimeSpan PickPatience = TimeSpan.FromSeconds(10);
    static readonly TimeSpan SlotPatience = TimeSpan.FromSeconds(5);

    readonly Lock changing = new();
    readonly SemaphoreSlim dirty = new(0, 1);

    /// <summary>
    ///     One draw at a time: the loop's frames and a screenshot both tell the Worker what it has to know.
    /// </summary>
    readonly SemaphoreSlim drawing = new(1, 1);

    readonly List<IFrameSubscriber> subscribers = [];
    readonly RenderScale scale = new();
    readonly string framesPath = options.Value.Worker.FramesPath;

    string? mesh;
    string? stack;
    IReadOnlyList<string> also = [];

    /// <summary>The UV set each worn texture set is laid out in, by its material, when it is not the first.</summary>
    readonly SortedDictionary<string, int> layouts = new(StringComparer.Ordinal);

    readonly HashSet<string> held = new(StringComparer.Ordinal);
    long painted;
    string? toldStack;
    ViewportMapSet maps = new();
    ViewportCameraState camera = new(0.6, 0.35, ViewportCameraState.Framed);
    ViewportDisplaySet display = new(new(), null, null);

    /// <summary>A graph node's picture shown in place of the material, and its file, or null.</summary>
    (ViewportNodeShown Shown, ViewportMapFile File)? node;

    /// <summary>The graph the viewport keeps up with, or null.</summary>
    ViewportFollow? following;

    /// <summary>Goes up as the following starts, stops or changes, so a follower knows its own.</summary>
    long followings;

    TaskCompletionSource followChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Counts changes to what lies on the mesh, for the frame header: a client then reads the state.</summary>
    long laid;

    int width = DefaultWidth;
    int height = DefaultHeight;
    bool interacting;
    uint lastInput;
    long revision = 1;
    long frames;
    string? error;
    string? mapsError;
    long rendered;
    IpcConnection? told;
    string? toldMesh;
    string toldLayouts = string.Empty;
    DateTime toldWritten;
    ViewportMapSet? toldMaps;
    ViewportDisplaySet? toldDisplay;
    ViewportCamera? toldCamera;

    public ViewportState State {
        get {
            lock (changing) {
                return new(
                    mesh,
                    Paths(maps),
                    camera,
                    width,
                    height,
                    scale.Value,
                    frames,
                    error ?? mapsError,
                    display.Display,
                    stack,
                    display.Isolation,
                    node?.Shown,
                    following,
                    display.Highlight
                );
            }
        }
    }

    /// <summary>Whether the viewport is wearing this stack.</summary>
    public bool Wears(string session) {
        lock (changing) {
            return stack == session;
        }
    }

    /// <summary>The stack the viewport wears in front, or null.</summary>
    public string? Worn {
        get {
            lock (changing) {
                return stack;
            }
        }
    }

    /// <summary>The model's other texture sets being worn.</summary>
    public IReadOnlyList<string> Also {
        get {
            lock (changing) {
                return also;
            }
        }
    }

    /// <summary>A client opened a session, so it is the client's to close from now on.</summary>
    public void Claim(string session) {
        lock (changing) {
            held.Remove(session);
        }
    }

    /// <summary>Takes the sessions only the viewport held open, which the caller closes.</summary>
    public IReadOnlyList<string> Release(IEnumerable<string> keeping) {
        lock (changing) {
            string[] dropped = [..held.Except(keeping)];
            held.ExceptWith(dropped);
            return dropped;
        }
    }

    /// <summary>
    ///     Wears a stack on its mesh with the model's other texture sets: the Worker takes the maps from there on, and
    ///     pictures come off.
    /// </summary>
    /// <param name="session">The stack in front.</param>
    /// <param name="path">The mesh.</param>
    /// <param name="others">The other sets.</param>
    /// <param name="opened">Those of them only this opened.</param>
    /// <param name="sets">Each worn set's material and the coordinate set it is laid out in (#210).</param>
    public void Wear(
        string session,
        string path,
        IReadOnlyList<string> others,
        IEnumerable<string> opened,
        IEnumerable<(string Material, int UvSet)> sets
    ) {
        lock (changing) {
            also = others;
            held.UnionWith(opened);
            mesh = path;
            stack = session;
            maps = new();
            node = null;
            painted = 0;
            error = null;
            mapsError = null;
            layouts.Clear();
            foreach (var (material, uvSet) in sets.Where(static set => set.UvSet != 0)) {
                layouts[material] = uvSet;
            }

            Unfollow();
            laid++;
            revision++;
        }

        Wake();
    }

    /// <summary>A worn texture set may have moved to another coordinate set, which reads the mesh again.</summary>
    public void Laid(string session, string material, int uvSet) {
        lock (changing) {
            if (stack != session && !also.Contains(session)) {
                return;
            }

            if (layouts.GetValueOrDefault(material) == uvSet) {
                return;
            }

            if (uvSet == 0) {
                layouts.Remove(material);
            } else {
                layouts[material] = uvSet;
            }

            revision++;
        }

        Wake();
    }

    /// <summary>A stack's maps moved: the next frame shows this revision, if the viewport is wearing it.</summary>
    public void Repainted(string session, long stackRevision) {
        lock (changing) {
            if (stack == session) {
                painted = stackRevision;
            } else if (!also.Contains(session)) {
                return;
            }

            revision++;
        }

        Wake();
    }

    /// <summary>
    ///     Opens a mesh bare. A graph the viewport follows stays followed, and is laid on the new mesh again.
    /// </summary>
    public void SetMesh(string path) {
        lock (changing) {
            Lay(path, new());
            if (following is not null) {
                Retell();
            }
        }

        Wake();
    }

    /// <summary>
    ///     Shows a mesh and pictures together, so that no frame has the one without the other, and with
    ///     <paramref name="alone" /> one picture on it through the picture view, the rest of the display as it was. A
    ///     graph the viewport followed is let go, whether a bake is shown whole or one of its maps alone.
    /// </summary>
    public void Show(string path, ViewportMapSet pictures, ViewportMapFile? alone = null) {
        lock (changing) {
            if (alone is not null) {
                display = display with {
                    Display = display.Display with { View = ViewportViews.Picture, Picture = alone.Path },
                    Picture = alone
                };
            }

            Lay(path, pictures);
            Unfollow();
        }

        Wake();
    }

    /// <summary>Takes everything off the viewport, as when a project closes. The display settings stay.</summary>
    public void Clear() {
        lock (changing) {
            mesh = null;
            stack = null;
            also = [];
            maps = new();
            node = null;
            error = null;
            mapsError = null;
            Unfollow();
            laid++;
            revision++;
        }

        Wake();
    }

    /// <summary>Lays pictures over the open mesh. A mesh leaving takes its pictures with it.</summary>
    public void SetMaps(ViewportMapSet pictures) {
        lock (changing) {
            LayMaps(pictures);
        }

        Wake();
    }

    /// <summary>
    ///     Shows one graph node's picture in place of the material, through the picture view, until something else is
    ///     laid on the mesh. The display settings are not touched, so the view they hold comes back after.
    /// </summary>
    public void ShowNode(ViewportNodeShown shown, ViewportMapFile file) {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(file);
        lock (changing) {
            LayNode(shown, file);
        }

        Wake();
    }

    /// <summary>
    ///     Swaps the mesh for another and keeps whatever lies on it: a stack, pictures or a node. For one stand-in
    ///     shape taking another's place.
    /// </summary>
    public void Reshape(string path) {
        lock (changing) {
            mesh = path;
            error = null;
            laid++;
            revision++;
        }

        Wake();
    }

    /// <summary>The graph the viewport keeps up with, and the number of this following, to check a show by.</summary>
    public (ViewportFollow? Following, long Number) Following {
        get {
            lock (changing) {
                return (following, followings);
            }
        }
    }

    /// <summary>Keeps a graph on the viewport from now on, or stops with null. What lies on the mesh stays.</summary>
    public void Follow(ViewportFollow? graph) {
        lock (changing) {
            if (following is null && graph?.Graph is null) {
                return;
            }

            following = graph is { Graph: not null } ? graph : null;
            Retell();
            laid++;
            revision++;
        }

        Wake();
    }

    /// <summary>Stops following, if the following is still this one: its graph has gone.</summary>
    public void Unfollow(long number) {
        lock (changing) {
            if (number == followings && following is not null) {
                Unfollow();
                laid++;
                revision++;
            }
        }

        Wake();
    }

    /// <summary>Completes when the following is no longer this one.</summary>
    public Task FollowChangedAsync(long number, CancellationToken cancellationToken) {
        lock (changing) {
            return number != followings ? Task.CompletedTask : followChanged.Task.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    ///     Lays a followed graph's outputs on the mesh, or one node's picture with a node, if the following is still
    ///     this one: a client that laid something else meanwhile keeps it. A viewport with no mesh takes the stand-in
    ///     shape.
    /// </summary>
    /// <returns>Whether it was laid.</returns>
    public bool ShowFollowed(long number, string? standIn, GraphOnMesh shown) {
        ArgumentNullException.ThrowIfNull(shown);
        lock (changing) {
            if (number != followings || following is null) {
                return false;
            }

            if (mesh is null && standIn is not null) {
                mesh = standIn;
                error = null;
            }

            if (shown.Node is { } picture) {
                LayNode(picture.Shown, picture.File);
            } else {
                LayMaps(shown.Maps);
            }
        }

        Wake();
        return true;
    }

    /// <summary>Under the lock.</summary>
    void Lay(string path, ViewportMapSet pictures) {
        mesh = path;
        stack = null;
        also = [];
        layouts.Clear();
        maps = pictures;
        node = null;
        error = null;
        mapsError = null;
        laid++;
        revision++;
    }

    /// <summary>Under the lock.</summary>
    void LayMaps(ViewportMapSet pictures) {
        stack = null;
        also = [];
        maps = pictures;
        node = null;
        mapsError = null;
        laid++;
        revision++;
    }

    /// <summary>Under the lock.</summary>
    void LayNode(ViewportNodeShown shown, ViewportMapFile file) {
        stack = null;
        also = [];
        node = (shown, file);
        mapsError = null;
        laid++;
        revision++;
    }

    /// <summary>Lets the followed graph go. Under the lock.</summary>
    void Unfollow() {
        if (following is not null) {
            following = null;
            Retell();
        }
    }

    /// <summary>Tells the follower its following changed, or must be shown again. Under the lock.</summary>
    void Retell() {
        followings++;
        var signal = followChanged;
        followChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.TrySetResult();
    }

    /// <summary>
    ///     Lights, shows and finishes frames as these settings say, for a new Worker as well as this one. Whatever is
    ///     laid on the mesh stays.
    /// </summary>
    public void SetDisplay(ViewportDisplaySet settings) {
        ArgumentNullException.ThrowIfNull(settings);
        lock (changing) {
            display = settings with {
                Isolation = display.Isolation,
                TexelDensity = display.TexelDensity,
                Resolution = display.Resolution,
                Highlight = display.Highlight
            };
            mapsError = null;
            revision++;
        }

        Wake();
    }

    /// <summary>
    ///     What the texel density view measures against: the project's density, and the side of a map whose own size
    ///     cannot be read. A project's, so it is set where a stack is worn and where a project's checks change.
    /// </summary>
    public void SetDensity(double target, int resolution) {
        lock (changing) {
            if (display.TexelDensity == target && display.Resolution == resolution) {
                return;
            }

            display = display with { TexelDensity = target, Resolution = resolution };
            revision++;
        }

        Wake();
    }

    /// <summary>
    ///     Draws some of the model alone, for every client and for a new Worker. Whole takes the isolation off.
    /// </summary>
    public void Isolate(ViewportIsolation isolation) {
        ArgumentNullException.ThrowIfNull(isolation);
        lock (changing) {
            display = display with { Isolation = isolation.Whole ? null : isolation };
            bakePreview.Isolate(display.Isolation);
            revision++;
        }

        Wake();
    }

    /// <summary>Marks some of the model, for every client and for a new Worker. None takes the mark off.</summary>
    public void Highlight(ViewportHighlight highlight) {
        ArgumentNullException.ThrowIfNull(highlight);
        lock (changing) {
            display = display with { Highlight = highlight.None ? null : highlight };
            revision++;
        }

        Wake();
    }

    public void Apply(ViewportInput input) {
        ArgumentNullException.ThrowIfNull(input);
        lock (changing) {
            camera = input.Camera ?? camera;
            if (input is { Width: > 0, Height: > 0 }) {
                width = input.Width.Value;
                height = input.Height.Value;
            }

            interacting = input.Interacting ?? interacting;
            if (!interacting) {
                scale.Reset();
            }

            lastInput = input.Input ?? lastInput;
            revision++;
        }

        Wake();
    }

    public void Subscribe(IFrameSubscriber subscriber) {
        lock (changing) {
            subscribers.Add(subscriber);
            revision++;
        }

        Wake();
    }

    public void Unsubscribe(IFrameSubscriber subscriber) {
        lock (changing) {
            subscribers.Remove(subscriber);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ServiceLoop.RunAsync(logger, nameof(ViewportSession), LoopAsync, stoppingToken);

    async Task LoopAsync(CancellationToken cancellationToken) {
        while (true) {
            await dirty.WaitAsync(cancellationToken);
            while (Snapshot() is ({ Mesh: not null } work, var wanted) && wanted != rendered) {
                try {
                    await DrawAsync(work, cancellationToken);
                    rendered = wanted;
                } catch (Exception e) when (e is IOException or TimeoutException) {
                    // The Worker died mid-frame. Nothing is lost: the next one is told everything and this frame runs
                    // again.
                    ServerLog.ViewportWorkerLost(logger);
                    await Task.Delay(LostWorkerPause, cancellationToken);
                } catch (IpcException e) {
                    Fail(e.Message);
                    rendered = wanted;
                }
            }
        }
    }

    /// <summary>Tells the Worker whatever it does not yet know, then renders. A new Worker knows nothing.</summary>
    async Task DrawAsync(Work work, CancellationToken cancellationToken) {
        await drawing.WaitAsync(cancellationToken);
        try {
            var connection = await TellAsync(work, cancellationToken);
            await RenderAsync(connection, work, cancellationToken);
        } finally {
            drawing.Release();
        }
    }

    /// <summary>
    ///     Writes the picture the viewport shows now to a PNG, at a size of its own: the Worker is told what it does
    ///     not
    ///     know, renders into a frame slot no viewer is offered, and the Server writes the slot's pixels to the file.
    /// </summary>
    /// <exception cref="IpcException">No mesh is open, or the Worker cannot draw it.</exception>
    /// <exception cref="IOException">The Worker went, or the file cannot be written.</exception>
    public async Task<ViewportScreenshot> ScreenshotAsync(
        ViewportScreenshotRequest request,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(request);
        Work work;
        lock (changing) {
            if (mesh is null) {
                throw new IpcException("Open a mesh first: there is nothing to take a picture of.");
            }

            work = new(
                mesh,
                new(stack, also, painted, new Dictionary<string, int>(layouts, StringComparer.Ordinal)),
                maps,
                Shown(),
                Lens(camera, display.Display),
                (request.Width ?? width, request.Height ?? height, 1d),
                false,
                (lastInput, laid)
            );
        }

        var (across, down) = (Math.Clamp(work.Size.Width, 2, FrameMemory.MaxWidth),
            Math.Clamp(work.Size.Height, 2, FrameMemory.MaxHeight));
        using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
            waiting.CancelAfter(PickPatience);
            try {
                await worker.ConnectionAsync(waiting.Token);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                throw new RendererMissingException();
            }
        }

        await drawing.WaitAsync(cancellationToken);
        try {
            var connection = await TellAsync(work, cancellationToken);
            using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            patience.CancelAfter(SlotPatience);
            var slot = await RentAsync(patience.Token, cancellationToken);
            try {
                var reply = await connection.SendAsync(
                    new() {
                        Type = IpcTypes.ViewportRender,
                        Render = new(across, down, framesPath, slot.Offset, FrameMemory.SlotBytes)
                    },
                    cancellationToken
                );
                var frame = reply.Frame!;
                var png = Vixen.Core.Imaging.PngCodec.Encode8(
                    frame.Width,
                    frame.Height,
                    4,
                    slot.GetSpan()[..(frame.Width * frame.Height * 4)]
                );
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.Path))!);
                await File.WriteAllBytesAsync(request.Path, png, cancellationToken);
                return new(request.Path, frame.Width, frame.Height);
            } finally {
                slot.Release();
            }
        } finally {
            drawing.Release();
        }
    }

    /// <summary>Tells the Worker the mesh, maps, settings, stacks and camera it does not yet know.</summary>
    async Task<IpcConnection> TellAsync(Work work, CancellationToken cancellationToken) {
        var connection = await worker.ConnectionAsync(cancellationToken);
        // A texture set moved to another coordinate set is the mesh read again with it (#210).
        var layouts = string.Join(';', work.Worn.UvSets.Select(static pair => $"{pair.Key}={pair.Value}"));
        // A mesh reloaded on the same layout is the same file rewritten, under documents that stay open (#89).
        var written = work.Mesh is { } path && File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;
        if (!ReferenceEquals(connection, told)
            || work.Mesh != toldMesh
            || layouts != toldLayouts
            || written != toldWritten) {
            (toldMesh, toldCamera, toldMaps, toldStack, toldDisplay) = (null, null, null, null, null);
            await connection.SendAsync(
                new() {
                    Type = IpcTypes.ViewportOpen,
                    Mesh = work.Mesh,
                    UvSets = work.Worn.UvSets.Count == 0 ? null : work.Worn.UvSets
                },
                cancellationToken
            );
            (told, toldMesh, toldMaps, toldLayouts, toldWritten) = (connection, work.Mesh, new(), layouts, written);
        }

        if (work.Maps != toldMaps) {
            await TellMapsAsync(connection, work.Maps, cancellationToken);
        }

        if (work.Display != toldDisplay) {
            await TellDisplayAsync(connection, work.Display, cancellationToken);
        }

        var wearing = work.Worn.Stack is null ? null : string.Join('|', [work.Worn.Stack, ..work.Worn.Also]);
        if (wearing != toldStack) {
            await connection.SendAsync(
                new() { Type = IpcTypes.ViewportStack, Following = work.Worn.Stack, FollowingAlso = work.Worn.Also },
                cancellationToken
            );
            toldStack = wearing;
        }

        if (work.Camera != toldCamera) {
            await connection.SendAsync(
                new() { Type = IpcTypes.ViewportCamera, Camera = work.Camera },
                cancellationToken
            );
            toldCamera = work.Camera;
        }

        await bakePreview.TellAsync(connection, cancellationToken);
        return connection;
    }

    /// <summary>Draws again for every viewer, as when something laid over the model changed.</summary>
    public void Redraw() {
        lock (changing) {
            revision++;
        }

        Wake();
    }

    /// <summary>What is under a position on the picture, through the Worker.</summary>
    /// <exception cref="IOException">The Worker went.</exception>
    /// <exception cref="IpcException">No mesh is open, or it cannot be read.</exception>
    public async Task<ViewportPick> PickAsync(ViewportPickRequest request, CancellationToken cancellationToken) {
        ViewportCamera look;
        int across;
        int down;
        string path;
        Dictionary<string, int>? laidOut;
        lock (changing) {
            path = mesh ?? throw new IpcException("Open a mesh first: there is nothing to pick.");
            look = Lens(request.Camera ?? camera, display.Display);
            across = request.Width ?? width;
            down = request.Height ?? height;
            laidOut = layouts.Count == 0 ? null : new(layouts, StringComparer.Ordinal);
        }

        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(PickPatience);
        IpcConnection connection;
        try {
            connection = await worker.ConnectionAsync(patience.Token);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new RendererMissingException();
        }

        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.ViewportPick, Pick = new(request.X, request.Y, look, across, down, path, laidOut) },
            cancellationToken
        );
        return reply.Picked!;
    }

    /// <summary>
    ///     A picture that cannot be read costs the picture and not the view: the error stays on the state, which a
    ///     frame clears for anything else, and the mesh goes on being drawn with what the Worker still has.
    /// </summary>
    async Task TellMapsAsync(IpcConnection connection, ViewportMapSet pictures, CancellationToken cancellationToken) {
        toldMaps = null;
        try {
            await connection.SendAsync(new() { Type = IpcTypes.ViewportMaps, Maps = pictures }, cancellationToken);
        } catch (IpcException e) {
            ServerLog.ViewportFailed(logger, e.Message);
            lock (changing) {
                mapsError = e.Message;
            }
        }

        toldMaps = pictures;
    }

    /// <summary>
    ///     Settings a Worker cannot use cost the settings and not the view, as a picture does: the error stays on the
    ///     state and the frame goes on with what the Worker had.
    /// </summary>
    async Task TellDisplayAsync(
        IpcConnection connection,
        ViewportDisplaySet settings,
        CancellationToken cancellationToken
    ) {
        toldDisplay = null;
        try {
            await connection.SendAsync(
                new() { Type = IpcTypes.ViewportDisplay, Display = settings },
                cancellationToken
            );
            if (logger.IsEnabled(LogLevel.Information)) {
                // What the Worker draws alone, for a frame that shows otherwise to be traced (#271).
                var parts = string.Join(", ", settings.Isolation?.Parts ?? []);
                var materials = string.Join(", ", settings.Isolation?.Materials ?? []);
                ServerLog.ViewportIsolated(logger, parts, materials);
            }
        } catch (IpcException e) {
            ServerLog.ViewportFailed(logger, e.Message);
            lock (changing) {
                mapsError = e.Message;
            }
        }

        toldDisplay = settings;
    }

    /// <summary>A camera with the lens the display settings give it.</summary>
    static ViewportCamera Lens(ViewportCameraState orbit, ViewportDisplay settings) =>
        new(
            orbit.Yaw,
            orbit.Pitch,
            orbit.Distance,
            orbit.PanX,
            orbit.PanY,
            orbit.PanZ,
            settings.FieldOfView,
            settings.Orthographic
        );

    /// <summary>The display the Worker draws with: the picture view of a node shown in place of the material.</summary>
    ViewportDisplaySet Shown() =>
        node is var (_, file)
            ? display with { Display = display.Display with { View = ViewportViews.Picture }, Picture = file }
            : display;

    (Work? Work, long Revision) Snapshot() {
        lock (changing) {
            return subscribers.Count == 0
                ? (null, revision)
                : (new(
                    mesh,
                    new(stack, also, painted, new Dictionary<string, int>(layouts, StringComparer.Ordinal)),
                    maps,
                    Shown(),
                    Lens(camera, display.Display),
                    (width, height, scale.Value),
                    interacting,
                    (lastInput, laid)
                ),
                    revision);
        }
    }

    async Task<FrameSlot> RentAsync(CancellationToken patience, CancellationToken cancellationToken) {
        try {
            return await memory.RentAsync(patience);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new TimeoutException("Every frame slot is held by a viewer that has stopped reading.");
        }
    }

    async Task RenderAsync(IpcConnection connection, Work work, CancellationToken cancellationToken) {
        var (renderWidth, renderHeight) = Fit(work);
        // A reader that stalled holds its slot, and one such reader must not stop every viewer.
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(SlotPatience);
        var slot = await RentAsync(patience.Token, cancellationToken);
        var start = Stopwatch.GetTimestamp();
        try {
            var reply = await connection.SendAsync(
                new() {
                    Type = IpcTypes.ViewportRender,
                    Render = new(renderWidth, renderHeight, framesPath, slot.Offset, FrameMemory.SlotBytes)
                },
                cancellationToken
            );
            Publish(slot, reply.Frame!, work);
        } finally {
            slot.Release();
        }

        var cost = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        lock (changing) {
            var slowest = subscribers.Count == 0 ? 0d : subscribers.Max(static s => s.LastSendMilliseconds);
            scale.Record(Math.Max(cost, slowest), work.Interacting);
        }
    }

    /// <summary>The asked size at the current scale, inside the slot, on even numbers.</summary>
    static (int Width, int Height) Fit(Work work) {
        var fit = Math.Min(
            1d,
            Math.Min((double)FrameMemory.MaxWidth / work.Size.Width, (double)FrameMemory.MaxHeight / work.Size.Height)
        );
        var factor = fit * work.Size.Scale;
        return (Math.Max(2, (int)(work.Size.Width * factor) & ~1), Math.Max(2, (int)(work.Size.Height * factor) & ~1));
    }

    void Publish(FrameSlot slot, ViewportFrame frame, Work work) {
        IFrameSubscriber[] audience;
        uint sequence;
        lock (changing) {
            frames++;
            sequence = (uint)frames;
            error = null;
            audience = [..subscribers];
        }

        var header = Header(sequence, frame, work);
        var bytes = frame.Width * frame.Height * 4;
        foreach (var subscriber in audience) {
            slot.Hold();
            subscriber.Offer(new(slot, header, bytes));
        }
    }

    /// <summary>
    ///     The header: magic, sequence, width, height, the newest input, render microseconds, the worn stack's
    ///     revision, and how many times what lies on the mesh has changed, which tells a client to read the state.
    /// </summary>
    static byte[] Header(uint sequence, ViewportFrame frame, Work work) {
        var header = new byte[HeaderBytes];
        var span = header.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)frame.Width);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)frame.Height);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], work.Marks.Input);
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], (uint)(frame.RenderMilliseconds * 1000d));
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], (uint)work.Worn.Revision);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], (uint)work.Marks.Laid);
        return header;
    }

    void Fail(string reason) {
        ServerLog.ViewportFailed(logger, reason);
        lock (changing) {
            error = reason;
        }
    }

    public override void Dispose() {
        base.Dispose();
        dirty.Dispose();
        drawing.Dispose();
    }

    void Wake() {
        try {
            dirty.Release();
        } catch (SemaphoreFullException) {
            // Already awake: the loop reads the newest state, so one signal stands for any number of changes.
        }
    }

    static ViewportMapPaths Paths(ViewportMapSet set) =>
        new(
            set.BaseColor?.Path,
            set.Normal?.Path,
            set.Roughness?.Path,
            set.Metallic?.Path,
            set.Occlusion?.Path,
            set.Emissive?.Path,
            set.Opacity?.Path
        );

    /// <summary>The stack the viewport wears, if any, and the newest revision of it the Worker has shown.</summary>
    readonly record struct Wearing(
        string? Stack,
        IReadOnlyList<string> Also,
        long Revision,
        IReadOnlyDictionary<string, int> UvSets
    );

    readonly record struct Work(
        string? Mesh,
        Wearing Worn,
        ViewportMapSet Maps,
        ViewportDisplaySet Display,
        ViewportCamera Camera,
        (int Width, int Height, double Scale) Size,
        bool Interacting,
        (uint Input, long Laid) Marks
    );
}
