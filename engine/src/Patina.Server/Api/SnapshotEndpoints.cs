using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     Named checkpoints of a document (#209). A snapshot is of the file as saved, so the Server refuses one of a
///     document with unsaved edits rather than keep less than the person sees, and puts one back only into a closed
///     document, which an open session would otherwise write over.
/// </summary>
static class SnapshotEndpoints {
    public static void MapSnapshotRoutes(this RouteGroupBuilder library) {
        library.MapGet("/snapshots", List)
            .Described("GetDocumentSnapshots", "A document's snapshots, newest first.");

        library.MapPost("/snapshots", Take)
            .Described(
                "TakeDocumentSnapshot",
                "Keeps the document's file and its paint as they are saved, under a name. Unsaved edits are refused."
            );

        library.MapPost("/snapshots/restore", Restore)
            .Described(
                "RestoreDocumentSnapshot",
                "Puts a closed document back as a snapshot holds it, after keeping a snapshot of how it stood."
            );

        library.MapDelete("/snapshots", Forget)
            .Described("DeleteDocumentSnapshot", "Forgets a snapshot. The document is not changed.");
    }

    static Results<Ok<DocumentSnapshotList>, ProblemHttpResult> List(LibraryHost host, string path) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => TypedResults.Ok(Snapshots.List(library.RootPath, DocumentFile(library, path)))
        );

    static Results<Ok<DocumentSnapshot>, ProblemHttpResult> Take(
        LibraryHost host,
        StackSessions stacks,
        GraphSessions graphs,
        SnapshotTakeRequest request
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                var file = DocumentFile(library, request.Path);
                if (stacks.All.Any(open => open.Path == file && open.Summary.Dirty)
                    || graphs.All.Any(open => open.Path == file && open.Summary.Dirty)) {
                    throw new StackEditRefusedException(
                        "The document has unsaved edits, which a snapshot of its file would leave out.",
                        Refusals.SnapshotUnsaved
                    );
                }

                return TypedResults.Ok(Snapshots.Take(library.RootPath, file, request.Name));
            }
        );

    static Results<Ok<DocumentSnapshot>, ProblemHttpResult> Restore(
        LibraryHost host,
        StackSessions stacks,
        GraphSessions graphs,
        SnapshotRequest request
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                var file = DocumentFile(library, request.Path);
                if (stacks.All.Any(open => open.Path == file) || graphs.All.Any(open => open.Path == file)) {
                    throw new StackEditRefusedException(
                        "The document is open, and its session would write over what the snapshot puts back.",
                        Refusals.SnapshotDocumentOpen
                    );
                }

                var before = Snapshots.Restore(library.RootPath, file, request.Id);
                host.RequestRescan();
                return TypedResults.Ok(before);
            }
        );

    static Results<Ok<DocumentSnapshotList>, ProblemHttpResult> Forget(LibraryHost host, string path, Guid id) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                var file = DocumentFile(library, path);
                Snapshots.Forget(file, id);
                return TypedResults.Ok(Snapshots.List(library.RootPath, file));
            }
        );

    /// <summary>A document's full path, which has to be a file inside the library.</summary>
    /// <exception cref="ProjectRefusedException">The path leaves the library.</exception>
    static string DocumentFile(OpenLibrary library, string path) {
        var root = Path.GetFullPath(library.RootPath);
        var file = Path.GetFullPath(path, root);
        return file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? file
            : throw new ProjectRefusedException("A snapshot is of a document inside the library.");
    }
}
