using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     What a brush panel asks of an open stack besides strokes (#123, #127): a picture of the brush, what every
///     channel holds at a place, for a material picker, and the quick mask. Each is numbers or a hash, never pixels in
///     JSON.
/// </summary>
static class StackBrushEndpoints {
    const int Missing = StatusCodes.Status404NotFound;
    const int Refused = StatusCodes.Status422UnprocessableEntity;
    const int Unavailable = StatusCodes.Status503ServiceUnavailable;

    public static void MapStackBrushes(this RouteGroupBuilder stacks) {
        stacks.MapPost("/{id}/brush-preview", PreviewBrush)
            .Describe(
                "PreviewStackBrush",
                "A picture of a brush: one stroke of it at a size of its own, white on nothing, its pressure rising "
                + "and falling, drawn by the Worker under /previews with the alphas this stack's strokes read. With "
                + "stamp, one stamp of it instead, as a cursor shows it: square, the stamp's long half-axis reaching "
                + "the middle of each side, turned and stretched as the brush is, with no jitter. A brush no stroke "
                + "could take is refused with its reason.",
                Missing,
                Refused,
                Unavailable
            );
        stacks.MapGet(
                "/{id}/quick-mask",
                Results<Ok<StackQuickMaskState>, ProblemHttpResult>(string id, StackSessions sessions) =>
                    sessions.Find(id) is { } open ? TypedResults.Ok(open.QuickMask) : StackEndpoints.NoSession(id)
            )
            .Describe(
                "GetStackQuickMask",
                "The session's quick mask: whether the viewport lays it over the model, and whether it is painted.",
                Missing
            );
        stacks.MapPut(
                "/{id}/quick-mask",
                Results<Ok<StackQuickMaskState>, ProblemHttpResult>(
                    string id,
                    StackQuickMaskRequest request,
                    StackSessions sessions
                ) => sessions.Find(id) is { } open
                    ? TypedResults.Ok(open.SetQuickMask(request))
                    : StackEndpoints.NoSession(id)
            )
            .Describe(
                "SetStackQuickMask",
                "Shows or hides the session's quick mask in the viewport, red where it is painted, or starts a new, "
                + "empty one. A stroke paints it with quickMask paint, and is kept inside or outside it with inside or "
                + "outside. It is the session's: no save writes it, and neither change is a step of history.",
                Missing
            );
        stacks.MapPost("/{id}/sample", Sample)
            .Describe(
                "SampleStack",
                "What every channel holds at a place on the map, as a material picker takes it up: one texel of each, "
                + "read back from the composite at full size, as numbers.",
                Missing,
                Refused,
                Unavailable
            );
    }

    static async Task<Results<Ok<StackBrushPreview>, ProblemHttpResult>> PreviewBrush(
        string id,
        StackBrush brush,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken,
        bool stamp = false
    ) {
        if (brush.Problem() is { } problem) {
            return Refusals.Problem(problem, Refused, Refusals.BrushRefused, Refusals.Reason(problem));
        }

        return await StackEndpoints.AskWorkerAsync(
            id,
            sessions,
            async session => new StackBrushPreview(
                await evaluator.PreviewBrushAsync(session, brush, stamp, cancellationToken)
            )
        );
    }

    static Task<Results<Ok<StackSample>, ProblemHttpResult>> Sample(
        string id,
        StackFindRequest request,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken
    ) =>
        StackEndpoints.AskWorkerAsync(
            id,
            sessions,
            session => evaluator.SampleAsync(session, request.U, request.V, cancellationToken)
        );
}
