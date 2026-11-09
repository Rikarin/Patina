using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
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
    ///     A preset the editor sent that is not one a file could hold. Values: <c>preset</c>, and <c>reason</c>, which is
    ///     the reader's own sentence naming the field.
    /// </summary>
    public const string PresetInvalid = "preset-invalid";

    /// <summary>A factory preset, which lives in Patina and cannot be changed or removed. Values: <c>preset</c>.</summary>
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
            : TypedResults.Problem(timeout.Message, statusCode: status);

    public static ProblemHttpResult NoLibraryOpen() =>
        Problem("The Server was started with no library.", StatusCodes.Status404NotFound, NoLibrary);
}
