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

/// <summary>Where frames go. WebSocket is the one transport today (ADR 0003); the session knows nothing about it.</summary>
public interface IFrameSubscriber {
    /// <summary>Takes a frame it now owns a hold on. It must not block, and a slow subscriber drops frames, not time.</summary>
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

    readonly Lock changing = new();
    readonly SemaphoreSlim dirty = new(0, 1);
    readonly List<IFrameSubscriber> subscribers = [];
    readonly RenderScale scale = new();
    readonly string framesPath = options.Value.Worker.FramesPath;

    string? mesh;
    ViewportCameraState camera = new(0.6, 0.35, 2.2);
    int width = DefaultWidth;
    int height = DefaultHeight;
    bool interacting;
    uint lastInput;
    long revision = 1;
    long frames;
    string? error;
    long rendered;
    IpcConnection? told;
    string? toldMesh;
    ViewportCameraState? toldCamera;

    public ViewportState State {
        get {
            lock (changing) {
                return new(mesh, camera, width, height, scale.Value, frames, error);
            }
        }
    }

    public void SetMesh(string path) {
        lock (changing) {
            mesh = path;
            error = null;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            await LoopAsync(stoppingToken);
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // Stopping: nothing to report.
        }
    }

    async Task LoopAsync(CancellationToken cancellationToken) {
        while (true) {
            await dirty.WaitAsync(cancellationToken);
            while (Snapshot() is ({ Mesh: not null } work, var wanted) && wanted != rendered) {
                try {
                    await DrawAsync(work, cancellationToken);
                    rendered = wanted;
                } catch (Exception e) when (e is IOException or TimeoutException) {
                    // The Worker died mid-frame. Nothing is lost: the next one is told everything and this frame runs again.
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
            (toldMesh, toldCamera) = (null, null);
            await connection.SendAsync(new() { Type = IpcTypes.ViewportOpen, Mesh = work.Mesh }, cancellationToken);
            (told, toldMesh) = (connection, work.Mesh);
        }

        if (work.Camera != toldCamera) {
            var camera = new ViewportCamera(work.Camera.Yaw, work.Camera.Pitch, work.Camera.Distance);
            await connection.SendAsync(new() { Type = IpcTypes.ViewportCamera, Camera = camera }, cancellationToken);
            toldCamera = work.Camera;
        }

        await RenderAsync(connection, work, cancellationToken);
    }

    (Work? Work, long Revision) Snapshot() {
        lock (changing) {
            return subscribers.Count == 0
                ? (null, revision)
                : (new(mesh, camera, width, height, scale.Value, interacting, lastInput), revision);
        }
    }

    async Task RenderAsync(IpcConnection connection, Work work, CancellationToken cancellationToken) {
        var (renderWidth, renderHeight) = Fit(work);
        var slot = await memory.RentAsync(cancellationToken);
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
            Math.Min((double)FrameMemory.MaxWidth / work.Width, (double)FrameMemory.MaxHeight / work.Height)
        );
        var factor = fit * work.Scale;
        return (Math.Max(2, (int)(work.Width * factor) & ~1), Math.Max(2, (int)(work.Height * factor) & ~1));
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

        var header = Header(sequence, frame, work.Input);
        var bytes = frame.Width * frame.Height * 4;
        foreach (var subscriber in audience) {
            slot.Hold();
            subscriber.Offer(new(slot, header, bytes));
        }
    }

    static byte[] Header(uint sequence, ViewportFrame frame, uint input) {
        var header = new byte[HeaderBytes];
        var span = header.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)frame.Width);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)frame.Height);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], input);
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], (uint)(frame.RenderMilliseconds * 1000d));
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

    readonly record struct Work(
        string? Mesh,
        ViewportCameraState Camera,
        int Width,
        int Height,
        double Scale,
        bool Interacting,
        uint Input
    );
}
