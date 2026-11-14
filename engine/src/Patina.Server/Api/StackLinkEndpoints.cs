using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>A texture set's instances in the model's other sets, and the sources of its own there (#108).</summary>
static class StackLinkEndpoints {
    public static void MapStackLinks(this RouteGroupBuilder stacks) {
        stacks.MapGet("/{id}/instances", GetLinks)
            .Describe(
                "GetStackInstanceLinks",
                "Lists the layers of the model's other texture sets that stand for this set's layers, and the layers "
                + "there that this set's instances stand for, each by set, id and name.",
                StatusCodes.Status404NotFound
            );
        stacks.MapPost("/{id}/channels/apply-to-all", ApplyChannels)
            .Describe(
                "ApplyStackChannelsToAllSets",
                "Gives each of the model's other texture sets the channels this set has and it lacks (#141), each "
                + "starting as what leaves its material alone. A set open in a session takes them as one undo step of "
                + "its own, left unsaved; a set nobody has open has them written to its file. Nothing is taken off a "
                + "set. Says what each set was given, or why it was left alone.",
                StatusCodes.Status404NotFound
            );
    }

    static Results<Ok<StackChannelsApplied>, ProblemHttpResult> ApplyChannels(string id, StackSessions sessions) {
        if (sessions.Find(id) is not { } session) {
            return StackEndpoints.NoSession(id);
        }

        return TypedResults.Ok(StackChannelSpread.Spread(session, sessions));
    }

    static Results<Ok<StackInstanceLinks>, ProblemHttpResult> GetLinks(string id, StackSessions sessions) {
        if (sessions.Find(id) is not { } session) {
            return StackEndpoints.NoSession(id);
        }

        return TypedResults.Ok(
            session.Published is { } set
                ? StackInstanceLinking.Of(set, sessions.Siblings(session))
                : new StackInstanceLinks([], [])
        );
    }
}
