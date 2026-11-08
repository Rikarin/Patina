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
    readonly List<IFrameSubscriber> subscribers = [];
    readonly RenderScale scale = new();
    readonly string framesPath = options.Value.Worker.FramesPath;

    string? mesh;
    string? stack;
    IReadOnlyList<string> also = [];
    readonly HashSet<string> held = new(StringComparer.Ordinal);
    long painted;
    string? toldStack;
    ViewportMapSet maps = new();
    ViewportCameraState camera = new(0.6, 0.35, ViewportCameraState.Framed);
    ViewportDisplaySet display = new(new(), null, null);
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
                    stack
                );
            }
        }
    }

    public void SetMesh(string path) => Show(path, new());

    /// <summary>Whether the viewport is wearing this stack.</summary>
    public bool Wears(string session) {
        lock (changing) {
            return stack == session;
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
    public void Wear(string session, string path, IReadOnlyList<string> others, IEnumerable<string> opened) {
        lock (changing) {
            also = others;
            held.UnionWith(opened);
            mesh = path;
            stack = session;
            maps = new();
            painted = 0;
            error = null;
            mapsError = null;
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

    /// <summary>Shows a mesh and pictures together, so that no frame has the one without the other.</summary>
    public void Show(string path, ViewportMapSet pictures) {
        lock (changing) {
            mesh = path;
            stack = null;
            also = [];
            maps = pictures;
            error = null;
            mapsError = null;
            revision++;
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
            error = null;
            mapsError = null;
            revision++;
        }

        Wake();
    }

    /// <summary>Lays pictures over the open mesh. A mesh leaving takes its pictures with it.</summary>
    public void SetMaps(ViewportMapSet pictures) {
        lock (changing) {
            stack = null;
            also = [];
            maps = pictures;
            mapsError = null;
            revision++;
        }

        Wake();
    }

    /// <summary>
    ///     Lights, shows and finishes frames as these settings say, for a new Worker as well as this one. Whatever is
    ///     laid on the mesh stays.
    /// </summary>
    public void SetDisplay(ViewportDisplaySet settings) {
        ArgumentNullException.ThrowIfNull(settings);
        lock (changing) {
            display = settings;
            mapsError = null;
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
        var connection = await worker.ConnectionAsync(cancellationToken);
        if (!ReferenceEquals(connection, told) || work.Mesh != toldMesh) {
            (toldMesh, toldCamera, toldMaps, toldStack, toldDisplay) = (null, null, null, null, null);
            await connection.SendAsync(new() { Type = IpcTypes.ViewportOpen, Mesh = work.Mesh }, cancellationToken);
            (told, toldMesh, toldMaps) = (connection, work.Mesh, new());
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

        await RenderAsync(connection, work, cancellationToken);
    }

    /// <summary>What is under a position on the picture, through the Worker.</summary>
    /// <exception cref="IOException">The Worker went.</exception>
    /// <exception cref="IpcException">No mesh is open, or it cannot be read.</exception>
    public async Task<ViewportPick> PickAsync(ViewportPickRequest request, CancellationToken cancellationToken) {
        ViewportCamera look;
        int across;
        int down;
        string path;
        lock (changing) {
            path = mesh ?? throw new IpcException("Open a mesh first: there is nothing to pick.");
            look = Lens(request.Camera ?? camera, display.Display);
            across = request.Width ?? width;
            down = request.Height ?? height;
        }

        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(PickPatience);
        IpcConnection connection;
        try {
            connection = await worker.ConnectionAsync(patience.Token);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new TimeoutException("No Worker is connected.");
        }

        var reply = await connection.SendAsync(
            new() {
                Type = IpcTypes.ViewportPick,
                Pick = new(request.X, request.Y, look, across, down, path)
            },
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
    async Task TellDisplayAsync(IpcConnection connection, ViewportDisplaySet settings, CancellationToken cancellationToken) {
        toldDisplay = null;
        try {
            await connection.SendAsync(new() { Type = IpcTypes.ViewportDisplay, Display = settings }, cancellationToken);
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

    (Work? Work, long Revision) Snapshot() {
        lock (changing) {
            return subscribers.Count == 0
                ? (null, revision)
                : (new(
                    mesh,
                    new(stack, also, painted),
                    maps,
                    display,
                    Lens(camera, display.Display),
                    (width, height),
                    scale.Value,
                    interacting,
                    lastInput
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
        var factor = fit * work.Scale;
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

        var header = Header(sequence, frame, work.Input, work.Worn.Revision);
        var bytes = frame.Width * frame.Height * 4;
        foreach (var subscriber in audience) {
            slot.Hold();
            subscriber.Offer(new(slot, header, bytes));
        }
    }

    static byte[] Header(uint sequence, ViewportFrame frame, uint input, long stackRevision) {
        var header = new byte[HeaderBytes];
        var span = header.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)frame.Width);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)frame.Height);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], input);
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], (uint)(frame.RenderMilliseconds * 1000d));
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], (uint)stackRevision);
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
    readonly record struct Wearing(string? Stack, IReadOnlyList<string> Also, long Revision);

    readonly record struct Work(
        string? Mesh,
        Wearing Worn,
        ViewportMapSet Maps,
        ViewportDisplaySet Display,
        ViewportCamera Camera,
        (int Width, int Height) Size,
        double Scale,
        bool Interacting,
        uint Input
    );
}
