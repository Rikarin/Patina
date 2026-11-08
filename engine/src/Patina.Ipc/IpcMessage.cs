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

    /// <summary>
    ///     Server to Worker: lay <see cref="IpcMessage.Maps" /> over the open mesh. The message holds paths and hashes,
    ///     and a Worker that has read a hash does not read it again.
    /// </summary>
    public const string ViewportMaps = "viewport.maps";

    /// <summary>Server to Worker: use <see cref="IpcMessage.Camera" /> from now on.</summary>
    public const string ViewportCamera = "viewport.camera";

    /// <summary>
    ///     Server to Worker: light, show and finish frames as <see cref="IpcMessage.Display" /> says from now on. The
    ///     files in it go with their hashes, and a Worker that has read a hash does not read it again.
    /// </summary>
    public const string ViewportDisplay = "viewport.display";

    /// <summary>
    ///     Server to Worker: render <see cref="IpcMessage.Render" /> into shared memory; answers with
    ///     <see cref="IpcMessage.Frame" />. The pixels are not in the message.
    /// </summary>
    public const string ViewportRender = "viewport.render";

    /// <summary>
    ///     Server to Worker: what is under <see cref="IpcMessage.Pick" />, on the mesh; answers with
    ///     <see cref="IpcMessage.Picked" />. It reads the mesh and no device.
    /// </summary>
    public const string ViewportPick = "viewport.pick";

    /// <summary>
    ///     Server to Worker: from now on the viewport wears the stack <see cref="IpcMessage.Following" />, or nothing
    ///     when that is null. The maps go from the stack's evaluation to the viewport inside the Worker.
    /// </summary>
    public const string ViewportStack = "viewport.stack";

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

    /// <summary>Writes some canvases of a stack, as they stand, to a folder a paste reads them from.</summary>
    public const string StackCopy = "stack.copy";

    public const string StackCheckpoint = "stack.checkpoint";

    public const string StackPixels = "stack.pixels";

    public const string StackClose = "stack.close";

    public const string MeshUnwrap = "mesh.unwrap";

    public const string MeshMovePaint = "mesh.move-paint";

    public const string MeshMaterials = "mesh.materials";

    public const string MeshParts = "mesh.parts";

    public const string MeshPartMask = "mesh.part-mask";

    /// <summary>Renders each fill of a stack alone, small.</summary>
    public const string StackThumbnails = "stack.thumbnails";

    /// <summary>Draws a material small for the shelf, from a copy of its document that nothing keeps.</summary>
    public const string MaterialThumbnail = "material.thumbnail";

    public const string BakeStart = "bake.start";

    /// <summary>
    ///     Server to Worker: start <see cref="IpcMessage.BuildStart" /> and answer at once, as a bake does. It says how
    ///     it ends with <see cref="BuildEnded" />, and a build sends these one at a time.
    /// </summary>
    public const string BuildStart = "build.start";

    /// <summary>Server to Worker: stop the step named by <see cref="IpcMessage.BakeCancel" />.</summary>
    public const string BuildCancel = "build.cancel";

    /// <summary>Worker to Server: <see cref="IpcMessage.BuildEnded" />, however it ended.</summary>
    public const string BuildEnded = "build.ended";

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
    public const int ProtocolVersion = 9;

    public string Type { get; set; } = string.Empty;

    public long Id { get; set; }

    public string? Error { get; set; }

    /// <summary>hello: the Worker.</summary>
    public WorkerHello? Worker { get; set; }

    /// <summary>The reply to health.</summary>
    public WorkerHealth? Health { get; set; }

    /// <summary>bake.start: what to bake and where it goes.</summary>
    public StackLoad? StackLoad { get; set; }

    public StackSync? StackSync { get; set; }

    public StackEvaluate? StackEvaluate { get; set; }

    public StackResult? StackResult { get; set; }

    public StackSave? StackSave { get; set; }

    public StackPixelsAsk? StackPixels { get; set; }

    public StackCopyAsk? StackCopy { get; set; }

    public IReadOnlyList<StackPixels>? StackPicture { get; set; }

    /// <summary>viewport.pick: where to look.</summary>
    public ViewportPickAsk? Pick { get; set; }

    /// <summary>The reply to viewport.pick.</summary>
    public ViewportPick? Picked { get; set; }

    /// <summary>viewport.stack: which stack the viewport wears.</summary>
    public string? Following { get; set; }

    /// <summary>viewport.open: the mesh's path, on a disk the Worker can read.</summary>
    public string? Mesh { get; set; }

    /// <summary>viewport.maps: the pictures.</summary>
    public ViewportMapSet? Maps { get; set; }

    /// <summary>viewport.camera: the camera.</summary>
    public ViewportCamera? Camera { get; set; }

    /// <summary>viewport.display: how frames are lit, shown and finished.</summary>
    public ViewportDisplaySet? Display { get; set; }

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

    public string? StackClose { get; set; }

    public MeshUnwrapAsk? MeshUnwrap { get; set; }

    /// <summary>
    ///     What each set's unwrap came to, in the answer to <see cref="IpcTypes.MeshUnwrap" />. Null wrote nothing.
    /// </summary>
    public IReadOnlyList<UnwrapSet>? MeshUnwrapped { get; set; }

    public MeshMovePaintAsk? MeshMovePaint { get; set; }

    /// <summary>The file for <see cref="IpcTypes.MeshMaterials" /> and <see cref="IpcTypes.MeshParts" />.</summary>
    public string? MeshFile { get; set; }

    /// <summary>The texture set for <see cref="IpcTypes.MeshParts" />.</summary>
    public string? MeshSet { get; set; }

    /// <summary>A model's named meshes, in the answer to <see cref="IpcTypes.MeshParts" />.</summary>
    public IReadOnlyList<string>? Parts { get; set; }

    public MeshPartMaskAsk? MeshPartMask { get; set; }

    /// <summary>A model's materials, in the answer to <see cref="IpcTypes.MeshMaterials" />.</summary>
    public IReadOnlyList<string>? Materials { get; set; }

    /// <summary>The model's other texture sets, worn along with <see cref="Following" />.</summary>
    public IReadOnlyList<string>? FollowingAlso { get; set; }

    public StackThumbnailAsk? StackThumbnails { get; set; }

    /// <summary>The reply to stack.thumbnails.</summary>
    public IReadOnlyList<StackLayerThumbnail>? StackLayerThumbnails { get; set; }

    public MaterialThumbnailAsk? MaterialThumbnail { get; set; }

    /// <summary>The reply to material.thumbnail: the picture's hash, or null when the material draws nothing.</summary>
    public string? ThumbnailHash { get; set; }

    public BakeStart? BakeStart { get; set; }

    /// <summary>bake.cancel: which bake.</summary>
    public BakeTicket? BakeCancel { get; set; }

    /// <summary>bake.progress: how far.</summary>
    public BakeProgressNote? BakeProgress { get; set; }

    /// <summary>bake.ended: how it ended.</summary>
    public BakeEnding? BakeEnded { get; set; }

    /// <summary>build.start: one step of a build.</summary>
    public BuildStart? BuildStart { get; set; }

    /// <summary>build.ended: how a step ended.</summary>
    public BuildEnding? BuildEnded { get; set; }

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

/// <summary>One step of a build. It is a copy: the Server holds the job, and plans it again after a crash.</summary>
/// <param name="Ticket">Which attempt this is.</param>
/// <param name="Output">An output to build, or null.</param>
/// <param name="Export">A preset to run, or null.</param>
public sealed record BuildStart(BakeTicket Ticket, OutputStep? Output, ExportStep? Export);

/// <summary>How a step ended. A cancelled step has no manifest and no error.</summary>
/// <param name="Ticket">Which attempt.</param>
/// <param name="Output">What an output made.</param>
/// <param name="Export">What a preset made.</param>
/// <param name="Error">Why it could not be done.</param>
public sealed record BuildEnding(BakeTicket Ticket, BuildManifest? Output, ExportManifest? Export, string? Error);

/// <summary>The Worker's hello.</summary>
public sealed record WorkerHello(int Protocol, string Version, int ProcessId);

/// <summary>The Worker's health: enough for a client to tell that it is alive and what it costs.</summary>
public sealed record WorkerHealth(double UptimeSeconds, long WorkingSetBytes, double BusySeconds = 0);

/// <summary>A picture on a disk the Worker can read, and the hash of what is in it.</summary>
public sealed record ViewportMapFile(string Path, string Hash);

/// <summary>The pictures laid over the open mesh's materials. A null slot keeps the material's own.</summary>
public sealed record ViewportMapSet(
    ViewportMapFile? BaseColor = null,
    ViewportMapFile? Normal = null,
    ViewportMapFile? Roughness = null,
    ViewportMapFile? Metallic = null,
    ViewportMapFile? Occlusion = null,
    ViewportMapFile? Emissive = null,
    ViewportMapFile? Opacity = null
) {
    /// <summary>Whether normals here point green down, as a graph's do.</summary>
    public bool GreenDown { get; init; }
}

/// <summary>
///     How frames are lit, shown and finished, with the files it names hashed: the viewing table, and the picture the
///     picture view shows.
/// </summary>
/// <param name="Display">The settings as a client gave them.</param>
/// <param name="Look">The <c>.cube</c> table, or null.</param>
/// <param name="Picture">The picture, or null.</param>
public sealed record ViewportDisplaySet(ViewportDisplay Display, ViewportMapFile? Look, ViewportMapFile? Picture);

/// <summary>
///     An orbit around the model and the lens it looks through: radians, a distance in framings, a pan in bounding
///     radii. The lens comes from the display settings, so a pick sees what the picture shows.
/// </summary>
public sealed record ViewportCamera(
    double Yaw,
    double Pitch,
    double Distance,
    double PanX = 0,
    double PanY = 0,
    double PanZ = 0,
    double FieldOfView = 0.7,
    bool Orthographic = false
);

/// <summary>
///     One frame to render, and the slot of the shared-memory file to write it to: RGBA, tightly packed, top row first.
///     The Server owns the file; the Worker maps it, and can map it again.
/// </summary>
public sealed record ViewportRender(int Width, int Height, string SharedMemory, long Offset, long Capacity);

/// <summary>A position on the viewport's picture to find on the mesh.</summary>
/// <param name="X">Across, 0 to 1.</param>
/// <param name="Y">Down, 0 to 1.</param>
/// <param name="Camera">The camera.</param>
/// <param name="Width">The picture's width in the units the reply's view is in.</param>
/// <param name="Height">Its height.</param>
/// <param name="Mesh">The mesh. Only its file is read, so this answers where there is no Vulkan.</param>
public sealed record ViewportPickAsk(double X, double Y, ViewportCamera Camera, int Width, int Height, string Mesh);

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

/// <summary>Asks for a small picture of each fill of a stack.</summary>
/// <param name="Session">The stack.</param>
/// <param name="Folder">Where they go.</param>
public sealed record StackThumbnailAsk(string Session, string Folder);

/// <summary>A layer's own picture.</summary>
/// <param name="Layer">The layer.</param>
/// <param name="Hash">Its name in the preview folder.</param>
public sealed record StackLayerThumbnail(string Layer, string Hash);

/// <summary>Asks for one picture of a loaded graph at the size the graph computes it.</summary>
/// <param name="Session">The graph.</param>
/// <param name="Node">The node.</param>
/// <param name="Port">Its output port, or the usage of an Output node.</param>
/// <param name="Folder">The content-addressed cache the PNG is written to.</param>
public sealed record GraphImageAsk(string Session, int Node, string Port, string Folder);

/// <summary>A full-size picture that is now in the cache. The pixels are not in the message.</summary>
public sealed record GraphFullImage(string Hash, int Width, int Height);

/// <summary>What a <see cref="MaterialThumbnailAsk" /> holds.</summary>
public static class MaterialThumbnailKinds {
    public const string Graph = "graph";
    public const string Stack = "stack";
}

/// <summary>A material to draw small, once, with nothing kept but the picture.</summary>
/// <param name="Kind">What the document is: <c>graph</c> or <c>stack</c>.</param>
/// <param name="Path">Its file, which paint and pictures are found beside. A shipped one names a place for it.</param>
/// <param name="Document">The document, already at the size to draw.</param>
/// <param name="Folder">Where its compounds come from, or null.</param>
/// <param name="ImageRoots">The folders a relative picture is read from, in order.</param>
/// <param name="Node">A graph's Output node to draw. A stack ignores it.</param>
/// <param name="Usage">The map to draw: an Output's usage, or a stack's channel.</param>
/// <param name="PreviewFolder">The content-addressed cache the PNG is written to.</param>
public sealed record MaterialThumbnailAsk(
    string Kind,
    string Path,
    string Document,
    string? Folder,
    IReadOnlyList<string> ImageRoots,
    int Node,
    string Usage,
    string PreviewFolder
);

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

// SK7005: a wire record's constructor is its JSON shape, one parameter per field.
#pragma warning disable SK7005

/// <summary>One stroke of the journal, or the part of it a Worker has not been sent.</summary>
/// <param name="Id">Its number in the journal.</param>
/// <param name="Paint">The paint file, relative to the stack.</param>
/// <param name="Usage">Which channel of it.</param>
/// <param name="Brush">The brush.</param>
/// <param name="Colour">The colour, packed 0xAABBGGRR.</param>
/// <param name="Stamps">Where the brush went.</param>
/// <param name="Open">Whether more of it is coming.</param>
/// <param name="Model">The mesh that stood when it was painted, as the document names it, or empty.</param>
/// <param name="View">The camera of a stroke on the mesh, or null for one in the atlas.</param>
/// <param name="Curve">free, line or path.</param>
/// <param name="Mirror">x, y, z or null.</param>
/// <param name="Start">What a canvas this stroke is the first on starts as, packed like the colour, or null.</param>
/// <param name="From">
///     For no stroke at all: the canvas, relative to the stack or absolute, that <paramref name="Paint" /> becomes a
///     copy of, every channel of it. Undone, the copy goes.
/// </param>
public sealed record StackPaintPart(
    long Id,
    string Paint,
    string Usage,
    StackBrush Brush,
    uint Colour,
    IReadOnlyList<StackStamp> Stamps,
    bool Open,
    string Model,
    StackView? View,
    string Curve,
    string? Mirror,
    uint? Start = null,
    string? From = null
);

#pragma warning restore SK7005

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
/// <param name="PreviewSide">The longest side of a preview, or 0 for none: a drag the viewport is showing.</param>
/// <param name="PreviewFolder">The content-addressed folder previews go in.</param>
public sealed record StackEvaluate(string Session, int PreviewSide, string PreviewFolder);

// SK7005: a message record's constructor is its wire shape, one parameter per field.
#pragma warning disable SK7005
/// <summary>How an evaluation went.</summary>
/// <param name="Compiled">Whether the plan compiled.</param>
/// <param name="Evaluated">Whether anything was evaluated at all.</param>
/// <param name="Ran">How many ops ran.</param>
/// <param name="Repaired">How many ran over tiles rather than a whole image.</param>
/// <param name="Reused">How many came from the cache.</param>
/// <param name="Milliseconds">How long it took here.</param>
/// <param name="Previews">The maps, by hash.</param>
/// <param name="Problems">Why it could not evaluate, or what a client should be told.</param>
/// <param name="Found">What the compile said, by layer.</param>
public sealed record StackResult(
    bool Compiled,
    bool Evaluated,
    int Ran,
    int Repaired,
    int Reused,
    double Milliseconds,
    IReadOnlyList<StackPreview> Previews,
    IReadOnlyList<string> Problems,
    IReadOnlyList<StackProblem> Found
);
#pragma warning restore SK7005

/// <summary>Gives a mesh that has no UV layout one, in a new file.</summary>
/// <param name="Source">The mesh file.</param>
/// <param name="Output">Where the unwrapped mesh goes.</param>
/// <param name="Options">How to lay it out.</param>
public sealed record MeshUnwrapAsk(string Source, string Output, UnwrapOptions Options);

/// <summary>Moves paint files from one mesh's layout to another's, rewriting them.</summary>
/// <param name="From">The mesh they are on.</param>
/// <param name="To">The mesh they go to.</param>
/// <param name="Paints">The paint files.</param>
/// <param name="Set">The texture set they belong to, whose triangles are all that are read.</param>
/// <param name="Backup">
///     Where copies of what they were go. Paint on a layout that did not change stays as it is.
/// </param>
public sealed record MeshMovePaintAsk(string From, string To, IReadOnlyList<string> Paints, string Set, string Backup);

/// <summary>Draws where some of a model's named meshes lie in its layout, white on black.</summary>
/// <param name="Mesh">The mesh file.</param>
/// <param name="Parts">The named meshes.</param>
/// <param name="Side">The picture's width and height.</param>
/// <param name="Output">The picture.</param>
public sealed record MeshPartMaskAsk(string Mesh, IReadOnlyList<string> Parts, int Side, string Output);

/// <summary>Writes some canvases of a stack as they stand, under their own names, to a folder.</summary>
/// <param name="Session">The session.</param>
/// <param name="Paints">The paint files, relative to the stack. One that holds nothing is not written.</param>
/// <param name="Folder">Where to.</param>
public sealed record StackCopyAsk(string Session, IReadOnlyList<string> Paints, string Folder);

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
/// <param name="Viewport">Whether to read what the viewport samples rather than evaluate anything.</param>
public sealed record StackPixelsAsk(string Session, bool Clean, string? Folder, bool Viewport = false);

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
