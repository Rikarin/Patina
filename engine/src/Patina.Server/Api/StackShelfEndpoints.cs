using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     A stack's layers or one layer's mask onto its project's shelf (#135), as a smart material or a smart mask that
///     any stack can then take: the same document a layered material is, and the same route to it. And the graph a
///     stack stands for, into its project as a new graph document (#205).
/// </summary>
static class StackShelfEndpoints {
    public static void MapStackShelf(this RouteGroupBuilder stacks) {
        stacks.MapPost("/{id}/save-material", SaveMaterial)
            .Describe(
                "SaveStackMaterial",
                "Saves layers, or one layer's mask, into a project as a smart material or smart mask its shelf offers.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity
            );
        stacks.MapPost("/{id}/graph", WriteGraph)
            .Describe(
                "WriteStackGraph",
                "Writes the graph the stack's shown texture set stands for into its project, as a new graph document "
                + "beside it. The stack is left as it is, and nothing keeps the two in step.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity
            );
    }

    static Results<Ok<StackGraphMade>, ProblemHttpResult> WriteGraph(
        string id,
        StackGraphRequest request,
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
            var made = StackGraphs.Write(library, session, request);
            library.Rescan();
            return TypedResults.Ok(made);
        } catch (StackEditRefusedException e) {
            return Refusals.Of(
                e,
                e.Code == Refusals.StackGraphUnsaved
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status422UnprocessableEntity
            );
        } catch (ProjectRefusedException e) {
            return Refusals.Of(e);
        }
    }

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
        } catch (StackEditRefusedException e) {
            return Refusals.Of(e, StatusCodes.Status422UnprocessableEntity);
        } catch (ProjectRefusedException e) {
            return Refusals.Problem(
                e.Message,
                StatusCodes.Status422UnprocessableEntity,
                Refusals.LibraryRefused,
                Refusals.Reason(e.Message)
            );
        } catch (FileNotFoundException e) {
            return Refusals.Of(e);
        }
    }
}
