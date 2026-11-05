using Patina.Contracts;
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

    /// <summary>
    ///     Server to Worker: write one node's picture to the cache at full size; answers with
    ///     <see cref="IpcMessage.GraphFullImage" />.
    /// </summary>
    public const string GraphImage = "graph.image";

    /// <summary>Server to Worker: forget the graph named by <see cref="IpcMessage.GraphClose" />.</summary>
    public const string GraphClose = "graph.close";

    /// <summary>
    ///     Server to Worker: start <see cref="IpcMessage.BakeStart" /> and answer at once. The bake runs beside
    ///     whatever
    ///     else is asked, and says how it ends with <see cref="BakeEnded" />, so nothing waits on it and no reply can
    ///     time out on it (ADR 0008).
    /// </summary>
    public const string StackLoad = "stack.load";

    public const string StackSync = "stack.sync";

    public const string StackEvaluate = "stack.evaluate";

    public const string StackSave = "stack.save";

    public const string StackCheckpoint = "stack.checkpoint";

    public const string StackPixels = "stack.pixels";

    public const string StackClose = "stack.close";

    public const string BakeStart = "bake.start";

    /// <summary>Server to Worker: stop the bake named by <see cref="IpcMessage.BakeCancel" />.</summary>
    public const string BakeCancel = "bake.cancel";

    /// <summary>Worker to Server: how far <see cref="IpcMessage.BakeProgress" /> has got.</summary>
    public const string BakeProgress = "bake.progress";

    /// <summary>Worker to Server: <see cref="IpcMessage.BakeEnded" />, however it ended.</summary>
    public const string BakeEnded = "bake.ended";

    public const string Ok = "ok";
    public const string Error = "error";
}

/// <summary>One envelope for every message, so the protocol stays a single source-generated type.</summary>
public sealed class IpcMessage {
    /// <summary>The wire format's version; a hello from another version is refused.</summary>
    public const int ProtocolVersion = 6;

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

    /// <summary>graph.image: which picture, and where it goes.</summary>
    public GraphImageAsk? GraphImage { get; set; }

    /// <summary>The reply to graph.image.</summary>
    public GraphFullImage? GraphFullImage { get; set; }

    /// <summary>The reply to graph.evaluate.</summary>
    public GraphResult? GraphResult { get; set; }

    /// <summary>bake.start: what to bake and where it goes.</summary>
    public StackLoad? StackLoad { get; set; }

    public StackSync? StackSync { get; set; }

    public StackEvaluate? StackEvaluate { get; set; }

    public StackResult? StackResult { get; set; }

    public StackSave? StackSave { get; set; }

    public StackPixelsAsk? StackPixels { get; set; }

    public IReadOnlyList<StackPixels>? StackPicture { get; set; }

    public string? StackClose { get; set; }

    public BakeStart? BakeStart { get; set; }

    /// <summary>bake.cancel: which bake.</summary>
    public BakeTicket? BakeCancel { get; set; }

    /// <summary>bake.progress: how far.</summary>
    public BakeProgressNote? BakeProgress { get; set; }

    /// <summary>bake.ended: how it ended.</summary>
    public BakeEnding? BakeEnded { get; set; }

    public static IpcMessage Ok(long id) => new() { Type = IpcTypes.Ok, Id = id };

    public static IpcMessage Fail(long id, string error) => new() { Type = IpcTypes.Error, Id = id, Error = error };
}

/// <summary>One attempt at a job: a report from an earlier attempt is one that arrived too late.</summary>
/// <param name="Job">The job.</param>
/// <param name="Attempt">Which attempt.</param>
public sealed record BakeTicket(string Job, int Attempt);

/// <summary>
///     A bake for the Worker to run. It is a copy: the Server has the job, and sends it again after a crash.
/// </summary>
/// <param name="Ticket">Which attempt this is.</param>
/// <param name="Request">What to bake.</param>
/// <param name="Folder">The folder to make, which is the cache's for this key.</param>
public sealed record BakeStart(BakeTicket Ticket, BakeRequest Request, string Folder);

/// <summary>How far a bake is.</summary>
public sealed record BakeProgressNote(BakeTicket Ticket, double Fraction);

/// <summary>How a bake ended. A cancelled bake has neither a manifest nor an error.</summary>
/// <param name="Ticket">Which attempt.</param>
/// <param name="Manifest">What is in the folder, when it finished.</param>
/// <param name="Error">Why it could not be done, when it could not.</param>
public sealed record BakeEnding(BakeTicket Ticket, BakeManifest? Manifest, string? Error);

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
/// <param name="ImageRoots">The folders a relative image reference is read from, in order.</param>
/// <param name="MeshMaps">The folder Mesh Map nodes read a bake from, or null when none has been chosen.</param>
public sealed record GraphLoad(
    string Session,
    string Document,
    string? Folder,
    IReadOnlyList<string> ImageRoots,
    string? MeshMaps = null
);

/// <summary>
///     A number on a node's input or a new text on it, which is all that differs between two value edits. A knob on a
///     compound is a text, and only the Worker's compiler can tell a knob from a setting that changes the plan.
/// </summary>
/// <param name="Node">The node.</param>
/// <param name="Port">The input, or the setting's name.</param>
/// <param name="Lanes">The numbers, or null when the input has gone back to its default.</param>
/// <param name="Text">The setting's text, or null when this is a number.</param>
public sealed record GraphValue(int Node, string Port, IReadOnlyList<float>? Lanes, string? Text = null);

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

/// <summary>Asks for one picture of a loaded graph at the size the graph computes it.</summary>
/// <param name="Session">The graph.</param>
/// <param name="Node">The node.</param>
/// <param name="Port">Its output port, or the usage of an Output node.</param>
/// <param name="Folder">The content-addressed cache the PNG is written to.</param>
public sealed record GraphImageAsk(string Session, int Node, string Port, string Folder);

/// <summary>A full-size picture that is now in the cache. The pixels are not in the message.</summary>
public sealed record GraphFullImage(string Hash, int Width, int Height);

// SK7005: a message record's constructor is its wire shape, one parameter per field.
#pragma warning disable SK7005

/// <summary>A stack for a Worker to hold.</summary>
/// <param name="Session">The session.</param>
/// <param name="Path">Its file, which its paint files are relative to.</param>
/// <param name="Document">The document as text.</param>
/// <param name="Set">The texture set it paints.</param>
/// <param name="Folder">The folder its compounds come from.</param>
/// <param name="ImageRoots">The folders a relative image reference is read from.</param>
/// <param name="MeshMaps">The folder of baked maps, or null.</param>
/// <param name="Checkpoint">A folder of canvases newer than the stack's own files, or null.</param>
/// <param name="Created">
///     Paint files this session's strokes made. A journal starts before them, so what a save left in them is not a
///     base.
/// </param>
public sealed record StackLoad(
    string Session,
    string Path,
    string Document,
    string Set,
    string? Folder,
    IReadOnlyList<string> ImageRoots,
    string? MeshMaps,
    string? Checkpoint,
    IReadOnlyList<string> Created
);

#pragma warning restore SK7005

/// <summary>One stroke of the journal, or the part of it a Worker has not been sent.</summary>
/// <param name="Id">Its number in the journal.</param>
/// <param name="Paint">The paint file, relative to the stack.</param>
/// <param name="Usage">Which channel of it.</param>
/// <param name="Brush">The brush.</param>
/// <param name="Colour">The colour, packed 0xAABBGGRR.</param>
/// <param name="Stamps">Where the brush went.</param>
/// <param name="Open">Whether more of it is coming.</param>
public sealed record StackPaintPart(
    long Id,
    string Paint,
    string Usage,
    StackBrush Brush,
    uint Colour,
    IReadOnlyList<StackStamp> Stamps,
    bool Open
);

/// <summary>
///     What the pixels should be: the strokes that stand, in order. The Worker works out how to get
///     there.
/// </summary>
/// <param name="Session">The session.</param>
/// <param name="Document">The document as text, when it changed.</param>
/// <param name="Standing">Every stroke that stands, by number, in order.</param>
/// <param name="Parts">The stamps this Worker has not been sent.</param>
/// <param name="Forget">Strokes that can never stand again.</param>
public sealed record StackSync(
    string Session,
    string? Document,
    IReadOnlyList<long> Standing,
    IReadOnlyList<StackPaintPart> Parts,
    IReadOnlyList<long> Forget
);

/// <summary>Asks for the stack to be composited as it stands.</summary>
/// <param name="Session">The session.</param>
/// <param name="PreviewSide">The longest side of a preview.</param>
/// <param name="PreviewFolder">The content-addressed folder previews go in.</param>
public sealed record StackEvaluate(string Session, int PreviewSide, string PreviewFolder);

/// <summary>How an evaluation went.</summary>
/// <param name="Compiled">Whether the plan compiled.</param>
/// <param name="Evaluated">Whether anything was evaluated at all.</param>
/// <param name="Ran">How many ops ran.</param>
/// <param name="Repaired">How many ran over tiles rather than a whole image.</param>
/// <param name="Reused">How many came from the cache.</param>
/// <param name="Milliseconds">How long it took here.</param>
/// <param name="Previews">The maps, by hash.</param>
/// <param name="Problems">Why it could not evaluate.</param>
public sealed record StackResult(
    bool Compiled,
    bool Evaluated,
    int Ran,
    int Repaired,
    int Reused,
    double Milliseconds,
    IReadOnlyList<StackPreview> Previews,
    IReadOnlyList<string> Problems
);

/// <summary>Writes canvases to a folder: the stack's own for a save, the cache's for a checkpoint.</summary>
/// <param name="Session">The session.</param>
/// <param name="Folder">Where to, or null for beside the stack.</param>
public sealed record StackSave(string Session, string? Folder);

/// <summary>Asks for a digest of each map, and for files when a folder is given.</summary>
/// <param name="Session">The session.</param>
/// <param name="Clean">
///     Whether to evaluate from nothing, as an export does, rather than read what the strokes
///     left.
/// </param>
/// <param name="Folder">Where to write each map, or null to write nothing.</param>
public sealed record StackPixelsAsk(string Session, bool Clean, string? Folder);

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
