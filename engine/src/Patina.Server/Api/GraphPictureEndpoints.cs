using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Graphs;

namespace Patina.Server.Api;

/// <summary>
///     What a node's picture holds, read at the size the graph computes it: its picture written to the preview cache,
///     its histogram, and one texel as the graph stored it (#184), the 2D View's readout at the picture's own
///     precision. Numbers and hashes, never pixels.
/// </summary>
static class GraphPictureEndpoints {
    public static void MapGraphPictures(this RouteGroupBuilder graphs) {
        graphs.MapPost("/{id}/images", WriteImage)
            .Describe(
                "WriteGraphImage",
                "Writes a node's picture at the size the graph computes it. /previews/{hash} serves the PNG.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status503ServiceUnavailable
            );
        graphs.MapPost("/{id}/histogram", ReadHistogram)
            .Describe(
                "ReadGraphHistogram",
                "Counts a node's picture, at the size the graph computes it, into a histogram per channel on the GPU: "
                + "256 shares of its texels for red, green, blue, alpha and luma.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status503ServiceUnavailable
            );
        graphs.MapPost("/{id}/sample", Sample)
            .Describe(
                "SampleGraphPicture",
                "Reads one texel of a node's picture, at the size the graph computes it, as the graph stored it: its "
                + "lanes at the precision of its format and unclamped.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status503ServiceUnavailable
            );
    }

    static Task<Results<Ok<GraphImage>, ProblemHttpResult>> WriteImage(
        string id,
        GraphImageRequest request,
        GraphSessions sessions,
        GraphEvaluator evaluator,
        CancellationToken cancellationToken
    ) =>
        GraphEndpoints.OnEngine(
            id,
            sessions,
            async session => {
                var image = await evaluator.ImageAsync(session, request.Node, request.Port, cancellationToken);
                return new GraphImage(image.Hash, image.Width, image.Height);
            }
        );

    static Task<Results<Ok<GraphHistogram>, ProblemHttpResult>> ReadHistogram(
        string id,
        GraphImageRequest request,
        GraphSessions sessions,
        GraphEvaluator evaluator,
        CancellationToken cancellationToken
    ) =>
        GraphEndpoints.OnEngine(
            id,
            sessions,
            async session => {
                var levels = await evaluator.HistogramAsync(session, request.Node, request.Port, cancellationToken);
                return new GraphHistogram(
                    request.Node,
                    request.Port,
                    levels.Width,
                    levels.Height,
                    levels.Red,
                    levels.Green,
                    levels.Blue,
                    levels.Alpha,
                    levels.Luma
                );
            }
        );

    static Task<Results<Ok<GraphSample>, ProblemHttpResult>> Sample(
        string id,
        GraphSampleRequest request,
        GraphSessions sessions,
        GraphEvaluator evaluator,
        CancellationToken cancellationToken
    ) =>
        GraphEndpoints.OnEngine(
            id,
            sessions,
            async session => {
                var texel = await evaluator.SampleAsync(session, request, cancellationToken);
                return new GraphSample(
                    request.Node,
                    request.Port,
                    texel.X,
                    texel.Y,
                    texel.Width,
                    texel.Height,
                    texel.Format,
                    texel.Values
                );
            }
        );
}
