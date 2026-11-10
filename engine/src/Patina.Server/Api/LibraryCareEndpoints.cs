using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     Looking after what a library holds: pictures of it, names and tags, pins, and the bin that deleting goes to.
///     Tags and pins are written to <c>project.yaml</c>, which is the truth; the index catches up behind it.
/// </summary>
static class LibraryCareEndpoints {
    const string Tag = "Library";

    /// <summary>Every route here: its method, its path below <c>/library</c>, handler, name and summary.</summary>
    static readonly (string Method, string Pattern, Delegate Handler, string Name, string Summary)[] Routes = [
        (
            HttpMethods.Post, "/thumbnails", DrawAsync, "GetThumbnails",
            "Pictures of documents, projects or shelf materials, by hash under /previews, drawing those the cache "
            + "lacks first. A project is drawn as the document that best stands for it."
        ),
        (HttpMethods.Patch, "/project/tags", SetProjectTags, "SetProjectTags",
            "Sets every tag a project has, in its project.yaml."),
        (HttpMethods.Patch, "/project/document/name", RenameDocument, "RenameDocument",
            "Gives a document a new display name. Its file stays."),
        (HttpMethods.Patch, "/project/document/tags", SetDocumentTags, "SetDocumentTags",
            "Sets every tag a document has, in its project's project.yaml."),
        (HttpMethods.Delete, "/project/document", TrashDocument, "TrashDocument",
            "Moves a document to the library's bin, with the paint it keeps beside it. It may not be open."),
        (HttpMethods.Patch, "/project/reference", SetPin, "SetReferencePin",
            "Pins a project's reference to an asset at the bytes it has now, or lets it follow the asset again."),
        (HttpMethods.Get, "/bin", GetBin, "GetBin",
            "What the library's bin holds: deleted projects and documents, the latest first."),
        (HttpMethods.Post, "/bin/restore", Restore, "RestoreFromBin",
            "Puts something back where it was deleted from. A project whose folder was taken comes back beside it."),
        (HttpMethods.Delete, "/bin/{id}", DeleteFromBin, "DeleteFromBin", "Deletes one thing in the bin for good."),
        (HttpMethods.Delete, "/bin", EmptyBin, "EmptyBin", "Deletes everything in the bin for good."),
        (HttpMethods.Get, "/cache", GetCache, "GetCache",
            "The library's cache folder and how much room it takes. Deleting it costs only time.")
    ];

    public static void MapLibraryCareApi(this IEndpointRouteBuilder api) {
        var library = api.MapGroup("/library").WithTags(Tag);
        foreach (var (method, pattern, handler, name, summary) in Routes) {
            var route = library.MapMethods(pattern, [method], handler);
            if (name == "GetThumbnails") {
                route.ProducesProblem(StatusCodes.Status503ServiceUnavailable);
            }

            route.Described(name, summary);
        }
    }

    static async Task<Results<Ok<ThumbnailList>, ProblemHttpResult>> DrawAsync(
        LibraryHost host,
        Thumbnails thumbnails,
        ThumbnailRequest request,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        try {
            return TypedResults.Ok(
                new ThumbnailList(await thumbnails.DrawAsync(library, request.Sources, cancellationToken))
            );
        } catch (TimeoutException e) {
            return Refusals.Of(e, StatusCodes.Status503ServiceUnavailable);
        }
    }

    static Results<Ok<ProjectContents>, ProblemHttpResult> SetProjectTags(
        LibraryHost host,
        ProjectTagsRequest request
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                library.SetProjectTags(request.Path, request.Tags);
                host.RequestRescan();
                return TypedResults.Ok(library.Contents(request.Path));
            }
        );

    static Results<Ok<ProjectContents>, ProblemHttpResult> RenameDocument(
        LibraryHost host,
        ProjectDocumentRenameRequest request
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                library.RenameDocument(request.Project, new(request.Document), request.Name);
                host.RequestRescan();
                return TypedResults.Ok(library.Contents(request.Project));
            }
        );

    static Results<Ok<ProjectContents>, ProblemHttpResult> SetDocumentTags(
        LibraryHost host,
        ProjectDocumentTagsRequest request
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                library.SetDocumentTags(request.Project, new(request.Document), request.Tags);
                host.RequestRescan();
                return TypedResults.Ok(library.Contents(request.Project));
            }
        );

    static Results<Ok<BinItem>, ProblemHttpResult> TrashDocument(
        LibraryHost host,
        GraphSessions graphs,
        StackSessions stacks,
        AutosaveGate autosave,
        string project,
        Guid document
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                var listed = library.Contents(project).Documents.FirstOrDefault(d => d.Id == document)
                    ?? throw new FileNotFoundException($"This project has no document {document}.");
                var file = Path.GetFullPath(listed.Path, library.RootPath);
                if (graphs.All.Any(open => open.Path == file) || stacks.All.Any(open => open.Path == file)) {
                    throw new LibraryItemOpenException($"'{listed.Name}' is open. Close it first.");
                }

                var binned = library.TrashDocument(project, new(document));
                autosave.Forget(library.RootPath, file);
                host.RequestRescan();
                return TypedResults.Ok(binned);
            }
        );

    static Results<Ok<ProjectReference>, ProblemHttpResult> SetPin(LibraryHost host, ProjectReferenceRequest request) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                var reference = library.SetPin(request.Project, new(request.Asset), request.Pinned);
                host.RequestRescan();
                return TypedResults.Ok(
                    new ProjectReference(
                        reference.Asset.Value,
                        reference.Mode == ReferenceMode.Pinned,
                        reference.Revision?.Text
                    )
                );
            }
        );

    static Results<Ok<BinList>, ProblemHttpResult> GetBin(LibraryHost host) =>
        LibraryTreeEndpoints.Guarded(host, static library => TypedResults.Ok(library.Bin()));

    static Results<Ok<BinRestored>, ProblemHttpResult> Restore(LibraryHost host, BinRestoreRequest request) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                var path = library.Restore(request.Id);
                host.RequestRescan();
                return TypedResults.Ok(new BinRestored(path));
            }
        );

    static Results<NoContent, ProblemHttpResult> DeleteFromBin(LibraryHost host, string id) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        try {
            library.DeleteFromBin(id);
            return TypedResults.NoContent();
        } catch (Exception e) when (LibraryTreeEndpoints.Refusal(e) is { } problem) {
            return problem;
        }
    }

    static Results<Ok<BinEmptied>, ProblemHttpResult> EmptyBin(LibraryHost host) =>
        LibraryTreeEndpoints.Guarded(host, static library => TypedResults.Ok(new BinEmptied(library.EmptyBin())));

    static Results<Ok<CacheInfo>, ProblemHttpResult> GetCache(LibraryHost host) =>
        LibraryTreeEndpoints.Guarded(
            host,
            static library => {
                var folder = Path.Combine(library.RootPath, LibraryLayout.CacheFolder);
                var bytes = Directory.Exists(folder)
                    ? new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories)
                        .Sum(static file => {
                                try {
                                    return file.Length;
                                } catch (FileNotFoundException) {
                                    return 0; // gone while counted: the cache trims itself
                                }
                            }
                        )
                    : 0;
                return TypedResults.Ok(new CacheInfo(folder, bytes));
            }
        );
}
