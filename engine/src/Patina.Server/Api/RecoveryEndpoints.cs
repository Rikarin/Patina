using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>Work autosaved after a document's last save: whether there is any, and restoring or forgetting.</summary>
static class RecoveryEndpoints {
    public static void MapRecoveryRoutes(this RouteGroupBuilder library) {
        library.MapGet("/recovery", GetRecovery)
            .Described("GetRecovery", "Whether a document has work autosaved after its last save.");

        library.MapPost("/recovery", Recover)
            .Described("RecoverDocument", "Restores or forgets autosaved work. The document has to be closed.");
    }

    /// <summary>
    ///     Nothing while a session holds unsaved edits: autosave is only a copy of those, and answering an offer of it
    ///     would close the session that has the real thing.
    /// </summary>
    static Results<Ok<DocumentRecovery>, ProblemHttpResult> GetRecovery(
        LibraryHost host,
        StackSessions stacks,
        GraphSessions graphs,
        string path
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                var file = Path.GetFullPath(path, library.RootPath);
                var editing = stacks.All.Any(open => open.Path == file && open.Summary.Dirty)
                    || graphs.All.Any(open => open.Path == file && open.Summary.Dirty);
                return TypedResults.Ok(new DocumentRecovery(editing ? null : Recovery.Newer(library.RootPath, file)));
            }
        );

    static Results<Ok<DocumentRecovery>, ProblemHttpResult> Recover(
        LibraryHost host,
        StackSessions stacks,
        GraphSessions graphs,
        AutosaveGate autosave,
        DocumentRecoveryRequest request
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                var file = Path.GetFullPath(request.Path, library.RootPath);
                if (stacks.All.Any(open => open.Path == file) || graphs.All.Any(open => open.Path == file)) {
                    throw new ProjectRefusedException("Close the document first.");
                }

                if (request.Restore) {
                    autosave.Restore(library.RootPath, file);
                } else {
                    autosave.Forget(library.RootPath, file);
                }

                return TypedResults.Ok(new DocumentRecovery(null));
            }
        );
}
