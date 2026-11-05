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

    /// <summary>Server to Worker: forget any copy of this graph and take <see cref="IpcMessage.GraphLoad" />.</summary>
    public const string GraphLoad = "graph.load";

    /// <summary>
    ///     Server to Worker: write the numbers in <see cref="IpcMessage.GraphEvaluate" />, evaluate what they changed
    ///     and write the previews to the cache; answers with <see cref="IpcMessage.GraphResult" />.
    /// </summary>
    public const string GraphEvaluate = "graph.evaluate";

    /// <summary>Server to Worker: forget the graph named by <see cref="IpcMessage.GraphClose" />.</summary>
    public const string GraphClose = "graph.close";

    public const string Ok = "ok";
    public const string Error = "error";
}

/// <summary>One envelope for every message, so the protocol stays a single source-generated type.</summary>
public sealed class IpcMessage {
    /// <summary>The wire format's version; a hello from another version is refused.</summary>
    public const int ProtocolVersion = 3;

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

    /// <summary>graph.load: a whole document.</summary>
    public GraphLoad? GraphLoad { get; set; }

    /// <summary>graph.evaluate: what changed and where the previews go.</summary>
    public GraphEvaluate? GraphEvaluate { get; set; }

    /// <summary>graph.close: the session.</summary>
    public string? GraphClose { get; set; }

    /// <summary>The reply to graph.evaluate.</summary>
    public GraphResult? GraphResult { get; set; }

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

/// <summary>
///     A graph for the Worker to hold. It is a copy: the Server has the document, and sends it again after a crash.
/// </summary>
/// <param name="Session">The Server's name for it.</param>
/// <param name="Document">The document, in its file format. Text, not pixels.</param>
/// <param name="Folder">The folder compounds are read from, or null for the shipped ones alone.</param>
public sealed record GraphLoad(string Session, string Document, string? Folder);

/// <summary>A number on a node's input, which is all that differs between two value edits.</summary>
/// <param name="Node">The node.</param>
/// <param name="Port">The input.</param>
/// <param name="Lanes">The numbers, or null when the input has gone back to its default.</param>
public sealed record GraphValue(int Node, string Port, IReadOnlyList<float>? Lanes);

/// <summary>Evaluates a loaded graph.</summary>
/// <param name="Session">The Server's name for it.</param>
/// <param name="Values">The numbers that changed since the Worker last heard.</param>
/// <param name="PreviewSide">The longest side of a preview, in pixels.</param>
/// <param name="PreviewFolder">The content-addressed cache that previews are written to.</param>
public sealed record GraphEvaluate(
    string Session,
    IReadOnlyList<GraphValue> Values,
    int PreviewSide,
    string PreviewFolder
);

/// <summary>Where a preview lives in the cache, for the Worker that writes it and the Server that serves it.</summary>
public static class GraphPreviewFiles {
    public const string Extension = ".png";

    public static string PathOf(string folder, string hash) =>
        Path.Combine(folder, hash[..2], string.Concat(hash, Extension));
}

/// <summary>A preview that is now in the cache. The pixels are not in the message.</summary>
public sealed record GraphResultPreview(int Node, string Port, string Hash, int Width, int Height);

/// <summary>A compiler diagnostic.</summary>
public sealed record GraphResultProblem(string Id, string Message, string Severity, int Node, string Port);

/// <summary>What an evaluation did. With an error among the problems nothing ran and there are no previews.</summary>
/// <param name="Compiled">Whether the plan was compiled, rather than refreshed with new numbers.</param>
/// <param name="Evaluated">Whether a plan ran at all.</param>
/// <param name="Ran">How many operations ran.</param>
/// <param name="Reused">How many operations already had a result.</param>
/// <param name="Milliseconds">The time in the Worker.</param>
/// <param name="Previews">Every node's preview.</param>
/// <param name="Problems">Everything the compiler reported.</param>
public sealed record GraphResult(
    bool Compiled,
    bool Evaluated,
    int Ran,
    int Reused,
    double Milliseconds,
    IReadOnlyList<GraphResultPreview> Previews,
    IReadOnlyList<GraphResultProblem> Problems
);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(IpcMessage))]
public sealed partial class IpcJsonContext : JsonSerializerContext;
