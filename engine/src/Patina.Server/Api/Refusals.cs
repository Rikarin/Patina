using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;
using System.Globalization;

namespace Patina.Server.Api;

/// <summary>
///     Refusals a client can tell apart. A ProblemDetails carries a stable <c>code</c> beside its neutral
///     <c>detail</c>, and the named <c>values</c> the detail was written from, so that each client says it in its own
///     words and the text can change without breaking one (#198). A refusal with no code is one no client is expected
///     to explain, and shows its detail.
/// </summary>
public static class Refusals {
    /// <summary>A document with unsaved edits, which closing would throw away.</summary>
    public const string Unsaved = "document-unsaved";

    /// <summary>No GPU process is connected, so nothing can be computed until one is.</summary>
    public const string RendererMissing = "renderer-missing";

    /// <summary>The Server was started with no library.</summary>
    public const string NoLibrary = "library-none";

    /// <summary>No graph session has the id. Values: <c>id</c>.</summary>
    public const string GraphNotOpen = "graph-not-open";

    /// <summary>No stack session has the id: it was closed, or the Server restarted. Values: <c>id</c>.</summary>
    public const string StackNotOpen = "stack-not-open";

    /// <summary>A brush on a layer that has no pixels of its own. Values: <c>layer</c>, <c>kind</c>.</summary>
    public const string NotPaintLayer = "stroke-not-paint-layer";

    /// <summary>A stroke on a stack with no mesh to paint on.</summary>
    public const string NoMesh = "stack-no-mesh";

    /// <summary>A batch that paints one channel of a layer twice.</summary>
    public const string RepeatedChannel = "stroke-repeated-channel";

    /// <summary>
    ///     A stroke, preset or project whose brush or symmetry is out of range or names no setting the Server knows.
    ///     Values: <c>reason</c>, which is the Server's own sentence.
    /// </summary>
    public const string BrushRefused = "stroke-brush-refused";

    /// <summary>
    ///     A preset the editor sent that is not one a file could hold. Values: <c>preset</c>, and <c>reason</c>, which
    ///     is the reader's own sentence naming the field.
    /// </summary>
    public const string PresetInvalid = "preset-invalid";

    /// <summary>
    ///     A factory preset, which lives in Patina and cannot be changed or removed. Values: <c>preset</c>.
    /// </summary>
    public const string PresetFactory = "preset-factory";

    /// <summary>No preset of that id is where it was asked for. Values: <c>preset</c>.</summary>
    public const string PresetMissing = "preset-missing";

    /// <summary>
    ///     A compression preview of a project whose maps are not built under their current keys, so there is nothing to
    ///     compress yet. Values: <c>project</c>.
    /// </summary>
    public const string ExportUnbuilt = "export-unbuilt";

    /// <summary>The values of a refusal whose detail is the Server's own sentence, as <c>reason</c>.</summary>
    public static IReadOnlyDictionary<string, string> Reason(string reason) =>
        new Dictionary<string, string> { ["reason"] = reason };

    /// <summary>
    ///     A stroke on a canvas of another shape than its texture set, which painting at the set's shape would make larger
    ///     than the largest side (#19, #241). Values: <c>layer</c>, <c>held</c> (the canvas's size), <c>size</c> (the
    ///     set's) and <c>target</c> (the size of the set's shape it would be painted at).
    /// </summary>
    public const string CanvasLarger = "stroke-canvas-larger";

    /// <summary>
    ///     A stroke at a new size on a canvas a stroke of this session began on at another, whose undo holds the
    ///     pixels it painted over (#19, #241). Values: <c>layer</c>, <c>held</c>, <c>size</c> and <c>target</c> (the size
    ///     it would be painted at).
    /// </summary>
    public const string CanvasPaintedAtSize = "stroke-canvas-painted-at-size";

    // Here and not among the stroke codes above: SK7020 honours no suppression, and reads an unbroken run of codes as a
    // clone of the Worker's message types.
    /// <summary>A channel depth that is none of the vocabulary's formats (#19). Values: <c>format</c>, as sent.</summary>
    public const string ChannelFormatUnknown = "channel-format-unknown";

    /// <summary>A channel taken off a texture set that does not have it. Values: <c>channel</c>, its name.</summary>
    public const string ChannelMissing = "channel-missing";

    /// <summary>
    ///     A texture set's last channel taken off, which would leave it nothing to write. Values: <c>channel</c>.
    /// </summary>
    public const string ChannelLast = "channel-last";

    /// <summary>
    ///     Paint carried from the UV set the texture set is laid out in already, which would only resample it (#210).
    ///     Values: <c>uvSet</c>, counted from 1 as a person counts them.
    /// </summary>
    public const string CarrySameUvSet = "carry-paint-same-uv-set";

    /// <summary>A stack command that cannot apply, for a reason no client is expected to explain.</summary>
    public const string StackRefused = "stack-refused";

    /// <summary>A jump to an undo step the history no longer holds. Values: <c>step</c>.</summary>
    public const string HistoryStepGone = "history-step-gone";

    /// <summary>A jump through a history that went to a step it no longer holds.</summary>
    public static ProblemHttpResult Of(HistoryStepGoneException gone, long? step) =>
        Problem(
            gone.Message,
            StatusCodes.Status409Conflict,
            HistoryStepGone,
            new Dictionary<string, string> { ["step"] = step?.ToString(CultureInfo.InvariantCulture) ?? string.Empty }
        );

    /// <summary>Another layer publishes that anchor point name. Values: <c>anchor</c>.</summary>
    public const string AnchorTaken = "anchor-taken";

    /// <summary>An anchor point something reads cannot be taken away. Values: <c>anchor</c>, <c>layer</c>.</summary>
    public const string AnchorInUse = "anchor-in-use";

    /// <summary>
    ///     A group whose picture no canvas could hold, or that is not a group. Values: <c>layer</c>, and
    ///     <c>reason</c>, which is the Server's own sentence.
    /// </summary>
    public const string FlattenRefused = "flatten-refused";

    /// <summary>Unflattening a group that is not flattened. Values: <c>layer</c>.</summary>
    public const string NotFlattened = "not-flattened";

    /// <summary>
    ///     Bake options whose minimum occluder distance is at or past the occlusion distance while occlusion or the
    ///     bent normal is baked, which would be white. Values: <c>minimum</c>, <c>distance</c>, in the rays' units.
    /// </summary>
    public const string BakeOccluderMinimum = BakeOptions.MinimumPastDistance;

    /// <summary>A mask layer that a layer's mask stack does not have. Values: <c>layer</c>, <c>index</c>.</summary>
    public const string MaskLayerMissing = "mask-layer-missing";

    /// <summary>A brush on a mask layer that is not painted. Values: <c>layer</c>, <c>source</c>.</summary>
    public const string MaskLayerNotPaint = "mask-layer-not-paint";

    /// <summary>An instance of a layer that is not there. Values: <c>layer</c>, <c>set</c>.</summary>
    public const string InstanceSourceMissing = "instance-source-missing";

    /// <summary>An instance that would stand, through instances, for itself. Values: <c>layer</c>.</summary>
    public const string InstanceCycle = "instance-cycle";

    /// <summary>
    ///     A node or compound neither the shipped nodes nor the project's compounds have. Values: <c>node</c>.
    /// </summary>
    public const string NodeUnknown = "node-unknown";

    /// <summary>A layer the stack does not have. Values: <c>layer</c>.</summary>
    public const string StackLayerMissing = "stack-layer-missing";

    /// <summary>
    ///     A graph that cannot be published as a package: it reads something a package cannot carry, does not compile,
    ///     or is not a graph (ADR 0027). Values: <c>reason</c>, which is the Server's own sentence.
    /// </summary>
    public const string PackageRefused = "package-refused";

    /// <summary>
    ///     A mesh to reload only if nothing on it moves (#89), on which something would: a material, a texture set's
    ///     layout or the file's name. Nothing changed. Values: <c>reason</c>, which is the Server's own sentence.
    /// </summary>
    public const string MeshLayoutChanged = "mesh-layout-changed";

    /// <summary>A list of the library's files of a kind it does not list. Values: <c>kind</c>.</summary>
    public const string FileKindUnknown = "file-kind-unknown";

    /// <summary>An undo or a redo with nothing to take back or put back. Values: <c>what</c>.</summary>
    public const string HistoryEmpty = "history-empty";

    /// <summary>A new file where one of that name already is. Values: <c>file</c>, its name.</summary>
    public const string FileExists = "file-exists";

    /// <summary>A file a request named that is not there. Values: <c>file</c>, its name.</summary>
    public const string FileMissing = "file-missing";

    /// <summary>An ID map asked of a document whose bake made none.</summary>
    public const string IdMapMissing = "id-map-missing";

    /// <summary>No job has the id. Values: <c>id</c>.</summary>
    public const string JobMissing = "job-missing";

    /// <summary>A job asked for that the queue does not have: 404.</summary>
    public static ProblemHttpResult NoJob(string id) =>
        Problem(
            $"There is no job '{id}'.",
            StatusCodes.Status404NotFound,
            JobMissing,
            new Dictionary<string, string> { ["id"] = id }
        );

    /// <summary>A graph shown on the mesh that has no Output node to show.</summary>
    public const string GraphNoOutput = "graph-no-output";

    /// <summary>A new graph from a template the library does not have. Values: <c>template</c>.</summary>
    public const string GraphTemplateUnknown = "graph-template-unknown";

    /// <summary>
    ///     A document's export in a format, compression, quality or mips no file can be written with (#47). Values:
    ///     <c>reason</c>, which is the Server's own sentence.
    /// </summary>
    public const string ExportSettingsInvalid = "export-settings-invalid";

    /// <summary>A mesh reload asked about with no mesh named (#89).</summary>
    public const string MeshUnnamed = "mesh-unnamed";

    /// <summary>A UV layout the GPU process was asked to draw and did not (#137).</summary>
    public const string UvLayoutUndrawn = "uv-layout-undrawn";

    /// <summary>
    ///     A color selection of more colors than a mask takes, or of colors that are not three numbers each, or a
    ///     value or background that is not a number (#110). Values: <c>most</c>.
    /// </summary>
    public const string SelectionRefused = "selection-refused";

    /// <summary>
    ///     Several colors selected on a mask whose base is neither a mesh map nor a picture, which the colors after the
    ///     first read (#110).
    /// </summary>
    public const string SelectionNeedsPicture = "selection-needs-picture";

    /// <summary>
    ///     A change to the library that something already there is in the way of: a name taken, a folder not empty, a
    ///     project inside a project. Values: <c>reason</c>, which is the library's own sentence.
    /// </summary>
    public const string LibraryConflict = "library-conflict";

    /// <summary>
    ///     A change the library cannot make as asked: a name with no letter in it, a project that is not there, a
    ///     document open that the change would move. Values: <c>reason</c>, which is the library's own sentence.
    /// </summary>
    public const string LibraryRefused = "library-refused";

    /// <summary>No material in the library has the id. Values: <c>id</c>.</summary>
    public const string MaterialMissing = "material-missing";

    /// <summary>A compound copied with no graph to copy it beside.</summary>
    public const string CompoundNeedsGraph = "compound-needs-graph";

    /// <summary>
    ///     A file named as a picture that does not read as one: broken, cut short, or another kind under a picture's
    ///     extension (#260). Values: <c>file</c>, its name, and <c>reason</c>, the reader's own sentence.
    /// </summary>
    public const string PictureUnreadable = "picture-unreadable";

    /// <summary>A refusal with its code and the values its detail was written from.</summary>
    public static ProblemHttpResult Problem(
        string detail,
        int status,
        string code,
        IReadOnlyDictionary<string, string>? values = null
    ) =>
        TypedResults.Problem(
            detail,
            statusCode: status,
            extensions: new Dictionary<string, object?> {
                ["code"] = code, ["values"] = values ?? new Dictionary<string, string>()
            }
        );

    /// <summary>A stack edit's refusal, with the code it was thrown with.</summary>
    public static ProblemHttpResult Of(StackEditRefusedException refusal, int status) =>
        Problem(refusal.Message, status, refusal.Code, refusal.Values);

    /// <summary>A wait that ran out: for the GPU process when none is connected, or for anything else.</summary>
    public static ProblemHttpResult Of(TimeoutException timeout, int status) =>
        timeout is RendererMissingException
            ? Problem(timeout.Message, status, RendererMissing)
            : Problem(timeout.Message, status, EngineUnavailable);

    /// <summary>
    ///     The library's refusal of a change: a conflict when something is in its way, else a request it cannot make.
    /// </summary>
    public static ProblemHttpResult Of(ProjectRefusedException refusal) =>
        refusal is ProjectExistsException
            ? Problem(refusal.Message, StatusCodes.Status409Conflict, LibraryConflict, Reason(refusal.Message))
            : Problem(refusal.Message, StatusCodes.Status400BadRequest, LibraryRefused, Reason(refusal.Message));

    /// <summary>A file a request needs that is not there, named by its file name alone.</summary>
    public static ProblemHttpResult Of(FileNotFoundException missing) =>
        Problem(
            missing.Message,
            StatusCodes.Status404NotFound,
            FileMissing,
            new Dictionary<string, string> {
                ["file"] = missing.FileName is { Length: > 0 } name ? Path.GetFileName(name) : "That file"
            }
        );

    /// <summary>
    ///     A file or folder another program still had open after the Server waited for it, so the change was not made
    ///     (#266). Trying again usually works; nothing is in the way as with a <see cref="LibraryConflict" />. Values:
    ///     <c>file</c>, its name.
    /// </summary>
    public const string FileBusy = "file-busy";

    /// <summary>
    ///     A library write that failed because somebody else had the file open: one <see cref="SharedFile" /> waited
    ///     for and gave up on, or a sharing or lock violation (Win32 errors 32 and 33) from a write that does not
    ///     wait. Null for any other failure.
    /// </summary>
    public static ProblemHttpResult? Busy(Exception e) {
        if (e is not FileBusyException && !(e is IOException && (e.HResult & 0xFFFF) is 32 or 33)) {
            return null;
        }

        var path = (e as FileBusyException)?.Path;
        return Problem(
            e.Message,
            StatusCodes.Status409Conflict,
            FileBusy,
            new Dictionary<string, string> { ["file"] = path is { Length: > 0 } ? Path.GetFileName(path) : "That file" }
        );
    }

    public static ProblemHttpResult NoLibraryOpen() =>
        Problem("The Server was started with no library.", StatusCodes.Status404NotFound, NoLibrary);

    /// <summary>The GPU engine went, or did not answer in time. Asking again once it is back may work.</summary>
    public const string EngineUnavailable = "engine-unavailable";

    /// <summary>
    ///     A request the GPU engine took and could not carry out. Values: <c>reason</c>, which is the engine's own
    ///     sentence.
    /// </summary>
    public const string EngineRefused = "engine-refused";

    /// <summary>
    ///     A graph edit the graph cannot take, such as a port it does not have. Values: <c>reason</c>, which is the
    ///     Server's own sentence.
    /// </summary>
    public const string GraphRefused = "graph-refused";

    /// <summary>A save over a file that changed on disk after it was opened, which saving would write over.</summary>
    public const string FileChanged = "file-changed";

    /// <summary>
    ///     A file that could not be written or read where it was asked for. Values: <c>reason</c>, the system's own
    ///     sentence.
    /// </summary>
    public const string FileUnwritable = "file-unwritable";

    /// <summary>
    ///     A request to the GPU engine that failed: refused by it, or with no engine to answer. Anything else the
    ///     request threw is the engine gone or slow.
    /// </summary>
    public static ProblemHttpResult OfEngine(Exception failure, int status) =>
        failure switch {
            RendererMissingException => Problem(failure.Message, status, RendererMissing),
            IpcException => Problem(failure.Message, status, EngineRefused, Reason(failure.Message)),
            _ => Problem(failure.Message, status, EngineUnavailable)
        };

    /// <summary>A graph edit's refusal: 422, with the Server's sentence as its reason.</summary>
    public static ProblemHttpResult Of(GraphEditRefusedException refusal) =>
        Problem(refusal.Message, StatusCodes.Status422UnprocessableEntity, GraphRefused, Reason(refusal.Message));

    /// <summary>A save that would write over another program's change: 409.</summary>
    public static ProblemHttpResult Of(GraphFileChangedException changed) =>
        Problem(changed.Message, StatusCodes.Status409Conflict, FileChanged);

    /// <summary>A file the system would not let the Server write or read, with its sentence as the reason.</summary>
    public static ProblemHttpResult Unwritable(Exception failure, int status) =>
        Problem(failure.Message, status, FileUnwritable, Reason(failure.Message));

    // Apart from the codes above: SK7020 honours no suppression, and reads a longer unbroken run of codes as a clone.
    /// <summary>A cast whose rays or direction are not numbers it can cast (#263).</summary>
    public const string CastRefused = "viewport-cast-refused";

    /// <summary>
    ///     A question about the viewport's mesh when none is open, or it cannot be read. Values: <c>reason</c>, the
    ///     Worker's own sentence.
    /// </summary>
    public const string ViewportMeshUnavailable = "viewport-mesh-unavailable";

    /// <summary>
    ///     A document written by a newer build of Patina, at a format this build does not read whole (#162). Values:
    ///     <c>written</c>, the file's format, and <c>reads</c>, the newest this build reads.
    /// </summary>
    public const string DocumentNewer = "document-newer";

    /// <summary>A stack proxy that is not 1, 2, 4 or 8 (#208). Values: <c>proxy</c>.</summary>
    public const string ProxyRefused = "stack-proxy-refused";

    /// <summary>A snapshot of a document with unsaved edits, which its file does not hold yet (#209).</summary>
    public const string SnapshotUnsaved = "snapshot-unsaved";

    /// <summary>A snapshot put back into a document that is open, whose session would write over it (#209).</summary>
    public const string SnapshotDocumentOpen = "snapshot-document-open";
}
