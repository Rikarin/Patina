using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Stacks;
using System.Globalization;

namespace Patina.Server.Api;

/// <summary>
///     Keeps a big stack quick to set up (#208): edits can wait to be evaluated, and the picture can be composited
///     smaller while it is edited. Neither is in the file, and a save, an export or a check never sees either.
/// </summary>
static class StackPaceEndpoints {
    public static void MapStackPace(this RouteGroupBuilder stacks) =>
        stacks.MapPut("/{id}/pace", Pace)
            .Describe(
                "PaceStack",
                "Pauses or resumes a stack's evaluation, or sets the proxy its picture is composited at while it is "
                + "edited. Every client hears a paced event. Saving, exporting and checking always evaluate it whole.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity
            );

    static Results<Ok<StackPace>, ProblemHttpResult> Pace(string id, StackPaceChange change, StackSessions sessions) {
        if (sessions.Find(id) is not { } session) {
            return StackEndpoints.NoSession(id);
        }

        try {
            return TypedResults.Ok(session.Paced(change.Paused, change.Proxy));
        } catch (ArgumentOutOfRangeException e) {
            return Refusals.Problem(
                e.Message,
                StatusCodes.Status422UnprocessableEntity,
                Refusals.ProxyRefused,
                new Dictionary<string, string> {
                    ["proxy"] = change.Proxy?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
                }
            );
        }
    }
}
