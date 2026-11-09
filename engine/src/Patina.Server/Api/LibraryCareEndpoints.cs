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

    public static void MapLibraryCareApi(this IEndpointRouteBuilder api) {
        var library = api.MapGroup("/library").WithTags(Tag);

        library.MapPost("/thumbnails", DrawAsync)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .Described(
                "GetThumbnails",
                "Pictures of documents, projects or shelf materials, by hash under /previews, drawing those the "
                + "cache lacks first. A project is drawn as the document that best stands for it."
            );

        library.MapPatch("/project/tags", SetProjectTags)
            .Described("SetProjectTags", "Sets every tag a project has, in its project.yaml.");

        library.MapPatch("/project/document/name", RenameDocument)
            .Described("RenameDocument", "Gives a document a new display name. Its file stays.");

        library.MapPatch("/project/document/tags", SetDocumentTags)
            .Described("SetDocumentTags", "Sets every tag a document has, in its project's project.yaml.");

        library.MapDelete("/project/document", TrashDocument)
            .Described(
                "TrashDocument",
                "Moves a document to the library's bin, with the paint it keeps beside it. It may not be open."
            );

        library.MapPatch("/project/reference", SetPin)
            .Described(
                "SetReferencePin",
                "Pins a project's reference to an asset at the bytes it has now, or lets it follow the asset again."
            );

        library.MapGet("/bin", GetBin)
            .Described("GetBin", "What the library's bin holds: deleted projects and documents, the latest first.");

        library.MapPost("/bin/restore", Restore)
            .Described(
                "RestoreFromBin",
                "Puts something back where it was deleted from. A project whose folder was taken comes back beside it."
            );

        library.MapDelete("/bin/{id}", DeleteFromBin)
            .Described("DeleteFromBin", "Deletes one thing in the bin for good.");

        library.MapDelete("/bin", EmptyBin)
            .Described("EmptyBin", "Deletes everything in the bin for good.");

        library.MapGet("/cache", GetCache)
            .Described("GetCache", "The library's cache folder and how much room it takes. Deleting it costs only time.");
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

    static Results<Ok<ProjectContents>, ProblemHttpResult> SetProjectTags(LibraryHost host, ProjectTagsRequest request) =>
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
                Recovery.Clear(library.RootPath, file);
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
