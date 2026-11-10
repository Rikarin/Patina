using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     What a brush panel asks of an open stack besides strokes (#123, #127): a picture of the brush, and what every
///     channel holds at a place, for a material picker. Each is numbers or a hash, never pixels in JSON.
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
                + "and falling, drawn by the Worker under /previews with the alphas this stack's strokes read. A brush "
                + "no stroke could take is refused with its reason.",
                Missing,
                Refused,
                Unavailable
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
        CancellationToken cancellationToken
    ) {
        if (brush.Problem() is { } problem) {
            return Refusals.Problem(problem, Refused, Refusals.BrushRefused, Refusals.Reason(problem));
        }

        return await StackEndpoints.AskWorkerAsync(
            id,
            sessions,
            async session => new StackBrushPreview(await evaluator.PreviewBrushAsync(session, brush, cancellationToken))
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
