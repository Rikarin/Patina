using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     Checks before export (#159): the Worker measures a stack's set, and the Server holds it to its project. Any
///     client
///     lists the problems in its own words from their codes, and goes to each.
/// </summary>
static class StackCheckEndpoints {
    public static void MapStackChecks(this RouteGroupBuilder stacks) =>
        stacks.MapGet("/{id}/checks", GetChecks)
            .Describe(
                "GetStackChecks",
                "Checks the texture set before export, against its project's budget, size limit, texel density and "
                + "export preset, and its layout and values against what a mesh and a physical material can have. Each "
                + "problem has a stable code and where it is.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status503ServiceUnavailable
            );

    static Task<Results<Ok<StackChecks>, ProblemHttpResult>> GetChecks(
        string id,
        StackSessions sessions,
        StackEvaluator evaluator,
        LibraryHost host,
        CancellationToken cancellationToken
    ) =>
        StackEndpoints.AskWorkerAsync(
            id,
            sessions,
            async session => {
                var measured = await evaluator.MeasureAsync(session, cancellationToken);
                var library = host.Library;
                var project = StackChecking.ProjectOf(library, session.Path);
                return StackChecking.Judge(
                    session.State,
                    measured,
                    project,
                    library?.Describe().TexelDensity ?? 0,
                    StackChecking.PresetOf(project, library?.RootPath)
                );
            }
        );
}
