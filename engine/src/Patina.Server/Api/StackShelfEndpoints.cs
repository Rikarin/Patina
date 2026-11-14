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
    ) =>
        IntoProject(
            id,
            sessions,
            host,
            (library, session) => StackGraphs.Write(library, session, request),
            static e => e switch {
                StackEditRefusedException { Code: Refusals.StackGraphUnsaved } unsaved =>
                    Refusals.Of(unsaved, StatusCodes.Status409Conflict),
                StackEditRefusedException refused => Refusals.Of(refused, StatusCodes.Status422UnprocessableEntity),
                ProjectRefusedException library => Refusals.Of(library),
                _ => Refusals.Of((FileNotFoundException)e)
            }
        );

    static Results<Ok<StackSavedMaterial>, ProblemHttpResult> SaveMaterial(
        string id,
        StackSaveMaterialRequest request,
        StackSessions sessions,
        LibraryHost host
    ) =>
        IntoProject(
            id,
            sessions,
            host,
            (library, session) => StackSmartAssets.Save(library, session, request),
            static e => e switch {
                StackEditRefusedException refused => Refusals.Of(refused, StatusCodes.Status422UnprocessableEntity),
                ProjectRefusedException library => Refusals.Problem(
                    library.Message,
                    StatusCodes.Status422UnprocessableEntity,
                    Refusals.LibraryRefused,
                    Refusals.Reason(library.Message)
                ),
                _ => Refusals.Of((FileNotFoundException)e)
            }
        );

    /// <summary>
    ///     Writes something of an open stack into its library, which then looks again, or says why not: a refusal of
    ///     the stack, of the library or of a missing file, each in the words <paramref name="refused" /> gives it.
    /// </summary>
    static Results<Ok<T>, ProblemHttpResult> IntoProject<T>(
        string id,
        StackSessions sessions,
        LibraryHost host,
        Func<OpenLibrary, StackSession, T> write,
        Func<Exception, ProblemHttpResult> refused
    ) {
        if (sessions.Find(id) is not { } session) {
            return StackEndpoints.NoSession(id);
        }

        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        try {
            var written = write(library, session);
            library.Rescan();
            return TypedResults.Ok(written);
        } catch (Exception e) when (e is StackEditRefusedException
                                        or ProjectRefusedException
                                        or FileNotFoundException) {
            return refused(e);
        }
    }
}
