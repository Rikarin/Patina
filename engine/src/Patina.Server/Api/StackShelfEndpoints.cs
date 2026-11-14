using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     A stack's layers or one layer's mask onto its project's shelf (#135), as a smart material or a smart mask that
///     any stack can then take: the same document a layered material is, and the same route to it.
/// </summary>
static class StackShelfEndpoints {
    public static void MapStackShelf(this RouteGroupBuilder stacks) =>
        stacks.MapPost("/{id}/save-material", SaveMaterial)
            .Describe(
                "SaveStackMaterial",
                "Saves layers, or one layer's mask, into a project as a smart material or smart mask its shelf offers.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity
            );

    static Results<Ok<StackSavedMaterial>, ProblemHttpResult> SaveMaterial(
        string id,
        StackSaveMaterialRequest request,
        StackSessions sessions,
        LibraryHost host
    ) {
        if (sessions.Find(id) is not { } session) {
            return StackEndpoints.NoSession(id);
        }

        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        try {
            var saved = StackSmartAssets.Save(library, session, request);
            library.Rescan();
            return TypedResults.Ok(saved);
        } catch (Exception e) when (e is StackEditRefusedException or ProjectRefusedException) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        } catch (FileNotFoundException e) {
            return Refusals.Of(e);
        }
    }
}
