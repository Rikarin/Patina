using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Graphs;

namespace Patina.Server.Api;

/// <summary>
///     One texel of a node's picture as the graph stored it (#184): the 2D View's readout at the picture's own
///     precision, where its preview holds eight bits a lane. One texel's numbers, never its pixels.
/// </summary>
static class GraphSampleEndpoint {
    public static void MapGraphSample(this RouteGroupBuilder graphs) =>
        graphs.MapPost("/{id}/sample", Sample)
            .Describe(
                "SampleGraphPicture",
                "Reads one texel of a node's picture, at the size the graph computes it, as the graph stored it: its "
                + "lanes at the precision of its format and unclamped.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status503ServiceUnavailable
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
