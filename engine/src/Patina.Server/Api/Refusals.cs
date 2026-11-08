using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Server.Stacks;

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

    /// <summary>A brush on a layer that has no pixels of its own. Values: <c>layer</c>, <c>kind</c>.</summary>
    public const string NotPaintLayer = "stroke-not-paint-layer";

    /// <summary>A stroke on a stack with no mesh to paint on.</summary>
    public const string NoMesh = "stack-no-mesh";

    /// <summary>A batch that paints one channel of a layer twice.</summary>
    public const string RepeatedChannel = "stroke-repeated-channel";

    /// <summary>A stroke whose brush or symmetry is out of range or names no setting the Server knows.</summary>
    public const string BrushRefused = "stroke-brush-refused";

    /// <summary>A stack command that cannot apply, for a reason no client is expected to explain.</summary>
    public const string StackRefused = "stack-refused";

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
