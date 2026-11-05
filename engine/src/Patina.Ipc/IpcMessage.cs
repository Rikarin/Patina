using System.Text.Json.Serialization;

namespace Patina.Ipc;

/// <summary>
///     Message types. Every message other than <see cref="Ok" /> and <see cref="Error" /> is a request, which gets one
///     of those two as its reply with the same <see cref="IpcMessage.Id" />. Either side may send requests.
/// </summary>
public static class IpcTypes {
    /// <summary>Worker to Server, first on a connection: who it is (<see cref="IpcMessage.Worker" />).</summary>
    public const string Hello = "hello";

    /// <summary>Server to Worker: answers with <see cref="IpcMessage.Health" />.</summary>
    public const string Health = "health";

    /// <summary>Server to Worker: read the mesh at <see cref="IpcMessage.Mesh" />.</summary>
    public const string ViewportOpen = "viewport.open";

    /// <summary>Server to Worker: use <see cref="IpcMessage.Camera" /> from now on.</summary>
    public const string ViewportCamera = "viewport.camera";

    /// <summary>
    ///     Server to Worker: render <see cref="IpcMessage.Render" /> into shared memory; answers with
    ///     <see cref="IpcMessage.Frame" />. The pixels are not in the message.
    /// </summary>
    public const string ViewportRender = "viewport.render";

    public const string Ok = "ok";
    public const string Error = "error";
}

/// <summary>One envelope for every message, so the protocol stays a single source-generated type.</summary>
public sealed class IpcMessage {
    /// <summary>The wire format's version; a hello from another version is refused.</summary>
    public const int ProtocolVersion = 2;

    public string Type { get; set; } = string.Empty;

    public long Id { get; set; }

    public string? Error { get; set; }

    /// <summary>hello: the Worker.</summary>
    public WorkerHello? Worker { get; set; }

    /// <summary>The reply to health.</summary>
    public WorkerHealth? Health { get; set; }

    /// <summary>viewport.open: the mesh's path, on a disk the Worker can read.</summary>
    public string? Mesh { get; set; }

    /// <summary>viewport.camera: the camera.</summary>
    public ViewportCamera? Camera { get; set; }

    /// <summary>viewport.render: what to render and where to put it.</summary>
    public ViewportRender? Render { get; set; }

    /// <summary>The reply to viewport.render.</summary>
    public ViewportFrame? Frame { get; set; }

    public static IpcMessage Ok(long id) => new() { Type = IpcTypes.Ok, Id = id };

    public static IpcMessage Fail(long id, string error) => new() { Type = IpcTypes.Error, Id = id, Error = error };
}

/// <summary>The Worker's hello.</summary>
public sealed record WorkerHello(int Protocol, string Version, int ProcessId);

/// <summary>The Worker's health: enough for a client to tell that it is alive and what it costs.</summary>
public sealed record WorkerHealth(double UptimeSeconds, long WorkingSetBytes);

/// <summary>An orbit around the model: radians, and a distance in bounding radii.</summary>
public sealed record ViewportCamera(double Yaw, double Pitch, double Distance);

/// <summary>
///     One frame to render, and the slot of the shared-memory file to write it to: RGBA, tightly packed, top row first.
///     The Server owns the file; the Worker maps it, and can map it again.
/// </summary>
public sealed record ViewportRender(int Width, int Height, string SharedMemory, long Offset, long Capacity);

/// <summary>A frame that is now in shared memory.</summary>
public sealed record ViewportFrame(int Width, int Height, double RenderMilliseconds);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(IpcMessage))]
public sealed partial class IpcJsonContext : JsonSerializerContext;
