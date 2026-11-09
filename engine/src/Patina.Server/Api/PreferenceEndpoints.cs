using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;

namespace Patina.Server.Api;

/// <summary>
///     The person's own settings that the Server acts on, whichever library is open (<see cref="Preferences" />). A
///     client's own settings, such as its theme, stay with the client.
/// </summary>
static class PreferenceEndpoints {
    const string Tag = "Preferences";

    public static void MapPreferenceApi(this IEndpointRouteBuilder api) {
        api.MapGet("/preferences", static (Preferences preferences) => TypedResults.Ok(preferences.Current))
            .WithName("GetPreferences")
            .WithTags(Tag)
            .WithSummary("The person's settings the Server acts on, as their preferences file has them.");

        api.MapPut("/preferences", Set)
            .WithName("SetPreferences")
            .WithTags(Tag)
            .WithSummary("Changes the person's settings, and keeps them in their preferences file.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    static Results<Ok<ServerPreferences>, ProblemHttpResult> Set(Preferences preferences, ServerPreferences request) {
        try {
            return TypedResults.Ok(preferences.Set(request));
        } catch (ArgumentException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status400BadRequest);
        } catch (IOException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
