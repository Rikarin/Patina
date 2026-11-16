using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Graphs;

namespace Patina.Server.Api;

/// <summary>
///     Keeps a big graph quick to set up (#306), as a stack's pace does (#208): edits can wait to be evaluated. It is
///     not in the file, and saving never waits on it.
/// </summary>
static class GraphPaceEndpoint {
    public static void MapGraphPace(this RouteGroupBuilder graphs) =>
        graphs.MapPut("/{id}/pace", Pace)
            .Describe(
                "PaceGraph",
                "Pauses or resumes a graph's evaluation. While it is paused, edits wait and the previews stay those of "
                + "its settled revision; resuming evaluates them. Every client hears a paced event.",
                StatusCodes.Status404NotFound
            );

    static Results<Ok<GraphPace>, ProblemHttpResult> Pace(string id, GraphPace pace, GraphSessions sessions) =>
        sessions.Find(id) is { } session
            ? TypedResults.Ok(new GraphPace(session.Pace(pace.Paused)))
            : GraphEndpoints.NoSession(id);
}
