using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;
using Patina.Server.Viewport;

namespace Patina.Server.Api;

/// <summary>
///     Folders of projects, and what a project holds. Files are the truth, so these change the disk and ask for a rescan,
///     and a client sees the listings move once the index has caught up.
/// </summary>
static class LibraryTreeEndpoints {
    const string Tag = "Library";
    const int DefaultResolution = 2048;
    const int MinResolution = 16;
    const int MaxResolution = 8192;

    public static void MapLibraryTreeApi(this IEndpointRouteBuilder api) {
        var library = api.MapGroup("/library").WithTags(Tag);

        library.MapGet("/folders", GetFolders)
            .WithName("GetFolders")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("The folders directly in a folder, where no parent is the top of the library.");

        library.MapPost("/folders", CreateFolder)
            .WithName("CreateFolder")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Makes a folder.");

        library.MapPatch("/folders", UpdateFolder)
            .WithName("UpdateFolder")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Renames a folder or changes its colour. Nothing in it may be open to rename it.");

        library.MapDelete("/folders", DeleteFolder)
            .WithName("DeleteFolder")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Deletes a folder, never a project: `lift` moves what it holds up a level first.");

        library.MapPost("/move", Move)
            .WithName("MoveInLibrary")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Moves folders and projects into a folder, all of them or none. Nothing moved may be open.");

        library.MapGet("/project", GetProject)
            .WithName("GetProjectContents")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("What a project holds. Read from its folder, so it works before the index has caught up.");

        library.MapPut("/project", PutProject)
            .WithName("SetProjectContents")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gives a project a mesh, and a layer stack if it has none.");

        library.MapPatch("/project", RenameProject)
            .WithName("RenameProject")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gives a project a new display name. Its folder stays.");
    }

    /// <summary>Writes the project's stack, so that every new project opens ready to paint.</summary>
    public static void Furnish(
        OpenLibrary library,
        string project,
        string? mesh,
        int? resolution,
        StackSessions stacks
    ) {
        var model = library.UseMesh(project, mesh);
        var contents = library.Contents(project);
        var side = resolution ?? DefaultResolution;
        if (contents.Stack is { } stack && File.Exists(Path.GetFullPath(stack, library.RootPath))) {
            var file = Path.GetFullPath(stack, library.RootPath);
            if (model is not null && stacks.All.All(session => session.Path != file)) {
                StackDocuments.SetModel(file, model);
            }

            return;
        }

        var name = OpenLibrary.StackName(project);
        var full = Path.GetFullPath($"{LibraryLayout.ProjectsFolder}/{project}/{name}", library.RootPath);
        if (!File.Exists(full)) {
            StackDocuments.Create(full, side, side, model);
        }

        library.UseStack(project, name);
    }

    /// <summary>What a request's resolution has to be, or the refusal.</summary>
    public static ProblemHttpResult? BadResolution(int? resolution) =>
        resolution is null or (>= MinResolution and <= MaxResolution)
            ? null
            : TypedResults.Problem(
                $"A resolution is {MinResolution} to {MaxResolution}.",
                statusCode: StatusCodes.Status400BadRequest
            );

    static Results<Ok<FolderList>, ProblemHttpResult> GetFolders(LibraryHost host, string? parent) =>
        Guarded(host, library => TypedResults.Ok(new FolderList(library.Folders(parent ?? string.Empty))));

    static Results<Created<FolderSummary>, ProblemHttpResult> CreateFolder(
        LibraryHost host,
        FolderCreateRequest request
    ) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        try {
            return TypedResults.Created(
                (string?)null,
                library.CreateFolder(request.Parent ?? string.Empty, request.Name, request.Color)
            );
        } catch (Exception e) when (Refusal(e) is { } problem) {
            return problem;
        }
    }

    static Results<Ok<LibraryMoved>, ProblemHttpResult> UpdateFolder(
        LibraryHost host,
        Holders holders,
        FolderUpdateRequest request
    ) =>
        Guarded(
            host,
            library => {
                var path = request.Path;
                if (request.Color is { } color) {
                    library.SetFolderColor(path, color);
                }

                if (request.Name is not { } name) {
                    return TypedResults.Ok(new LibraryMoved([path]));
                }

                holders.Refuse(library, [path]);
                path = library.RenameFolder(path, name);
                host.RequestRescan();
                return TypedResults.Ok(new LibraryMoved([path]));
            }
        );

    static Results<NoContent, ProblemHttpResult> DeleteFolder(
        LibraryHost host,
        Holders holders,
        string path,
        bool lift = false
    ) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        try {
            holders.Refuse(library, [path]);
            library.DeleteFolder(path, lift);
            host.RequestRescan();
            return TypedResults.NoContent();
        } catch (Exception e) when (Refusal(e) is { } problem) {
            return problem;
        }
    }

    static Results<Ok<LibraryMoved>, ProblemHttpResult> Move(
        LibraryHost host,
        Holders holders,
        LibraryMoveRequest request
    ) =>
        Guarded(
            host,
            library => {
                holders.Refuse(library, request.Paths);
                var moved = library.Move(request.Paths, request.Into ?? string.Empty);
                host.RequestRescan();
                return TypedResults.Ok(new LibraryMoved(moved));
            }
        );

    static Results<Ok<ProjectContents>, ProblemHttpResult> GetProject(LibraryHost host, string path) =>
        Guarded(host, library => TypedResults.Ok(library.Contents(path)));

    static Results<Ok<ProjectContents>, ProblemHttpResult> PutProject(
        LibraryHost host,
        StackSessions stacks,
        ProjectContentsRequest request
    ) {
        if (BadResolution(request.Resolution) is { } bad) {
            return bad;
        }

        return Guarded(
            host,
            library => {
                Furnish(library, request.Path, request.Mesh, request.Resolution, stacks);
                host.RequestRescan();
                return TypedResults.Ok(library.Contents(request.Path));
            }
        );
    }

    static Results<Ok<ProjectContents>, ProblemHttpResult> RenameProject(
        LibraryHost host,
        ProjectRenameRequest request
    ) =>
        Guarded(
            host,
            library => {
                library.RenameProject(request.Path, request.Name);
                host.RequestRescan();
                return TypedResults.Ok(library.Contents(request.Path));
            }
        );

    static Results<Ok<T>, ProblemHttpResult> Guarded<T>(LibraryHost host, Func<OpenLibrary, Ok<T>> act) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        try {
            return act(library);
        } catch (Exception e) when (Refusal(e) is { } problem) {
            return problem;
        }
    }

    static ProblemHttpResult? Refusal(Exception e) =>
        e switch {
            FileNotFoundException => Problem(e, StatusCodes.Status404NotFound),
            ProjectExistsException or LibraryItemOpenException => Problem(e, StatusCodes.Status409Conflict),
            ProjectRefusedException or StackEditRefusedException => Problem(e, StatusCodes.Status400BadRequest),
            IOException or UnauthorizedAccessException => Problem(e, StatusCodes.Status409Conflict),
            _ => null
        };

    static ProblemHttpResult Problem(Exception e, int status) => TypedResults.Problem(e.Message, statusCode: status);

    static ProblemHttpResult NoLibrary() =>
        TypedResults.Problem("This Server has no library.", statusCode: StatusCodes.Status404NotFound);
}

/// <summary>Something cannot change on disk because it is open.</summary>
public sealed class LibraryItemOpenException : Exception {
    public LibraryItemOpenException() { }

    public LibraryItemOpenException(string message) : base(message) { }

    public LibraryItemOpenException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>What has files of the library open, which moving them would pull out from under.</summary>
sealed class Holders(GraphSessions graphs, StackSessions stacks, ViewportSession viewport) {
    /// <exception cref="LibraryItemOpenException">Something below these paths is open.</exception>
    public void Refuse(OpenLibrary library, IReadOnlyList<string> paths) {
        IEnumerable<string> open = [
            ..graphs.All.Select(static s => s.Path),
            ..stacks.All.Select(static s => s.Path),
            ..viewport.State.Mesh is { } mesh ? [mesh] : Array.Empty<string>()
        ];
        var held = open.ToList();
        foreach (var path in paths) {
            var below = Path.GetFullPath($"{LibraryLayout.ProjectsFolder}/{path}", library.RootPath)
                + Path.DirectorySeparatorChar;
            if (held.FirstOrDefault(file => file.StartsWith(below, StringComparison.Ordinal)) is { } file) {
                throw new LibraryItemOpenException($"'{Path.GetFileName(file)}' in '{path}' is open. Close it first.");
            }
        }
    }
}
