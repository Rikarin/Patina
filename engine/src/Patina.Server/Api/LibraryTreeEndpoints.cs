using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;
using Patina.Server.Viewport;

namespace Patina.Server.Api;

/// <summary>
///     Folders of projects, and what a project holds. Files are the truth, so these change the disk and ask for a
///     rescan, and a client sees the listings move once the index has caught up.
/// </summary>
static class LibraryTreeEndpoints {
    const string Tag = "Library";
    const int DefaultResolution = 2048;
    const int MinResolution = 16;
    const int MaxResolution = 8192;
    const string BrushFolder = "brushes";
    static readonly TimeSpan BrushPatience = TimeSpan.FromSeconds(60);
    static readonly TimeSpan BrushRetry = TimeSpan.FromMilliseconds(100);

    public static void MapLibraryTreeApi(this IEndpointRouteBuilder api) {
        var library = api.MapGroup("/library").WithTags(Tag);

        library.MapGet("/folders", GetFolders)
            .Described("GetFolders", "The folders directly in a folder, where no parent is the top of the library.");

        library.MapPost("/folders", CreateFolder)
            .Described("CreateFolder", "Makes a folder.");

        library.MapPatch("/folders", UpdateFolder)
            .Described(
                "UpdateFolder",
                "Renames a folder or changes its colour. Nothing in it may be open to rename it."
            );

        library.MapDelete("/folders", DeleteFolder)
            .Described(
                "DeleteFolder",
                "Deletes a folder, never a project: `lift` moves what it holds up a level first."
            );

        library.MapPost("/move", Move)
            .Described(
                "MoveInLibrary",
                "Moves folders and projects into a folder, all of them or none. Nothing moved may be open."
            );

        MapProjectRoutes(library);
        MapMaterialRoutes(library);
    }

    static void MapProjectRoutes(RouteGroupBuilder library) {
        library.MapGet("/project", GetProject)
            .Described(
                "GetProjectContents",
                "What a project holds. Read from its folder, so it works before the index has caught up."
            );

        library.MapPost("/project/meshes", AddMesh)
            .Described("AddProjectMesh", "Brings a mesh into a project, and its detailed mesh if it has one.");

        library.MapPost("/project/documents", AddDocument)
            .Described("AddProjectDocument", "Makes a document of a kind in a project.");

        library.MapPost("/project/pictures", AddPicture)
            .Described("AddProjectPicture", "Brings a picture into a project, for a layer or a mask to read.");

        library.MapPatch("/project/document", SetDocumentMesh)
            .Described("SetDocumentMesh", "Puts a texturing document on another mesh of its project.");

        library.MapPatch("/project/document/role", SetDocumentRole)
            .Described("SetDocumentRole", "Says what a graph is for: a material, a mask, a brush or a filter.");

        library.MapPatch("/project", RenameProject)
            .Described("RenameProject", "Gives a project a new display name. Its folder stays.");
    }

    static void MapMaterialRoutes(RouteGroupBuilder library) {
        library.MapGet("/materials", GetMaterials)
            .Described("GetMaterials", "What a project's layers can be filled with.");

        library.MapPost("/materials/use", UseMaterial)
            .Described("UseMaterial", "Makes a material graph ready for a project's layers, and names it for them.");

        library.MapPost("/materials/brush", MakeBrush)
            .Described("MakeBrush", "Renders a graph to a picture in its project, which a brush can stamp.");

        library.MapPost("/materials/refresh", RefreshMaterials)
            .Described("RefreshMaterials", "Brings a project's layers up to edits saved in the graphs they read.");
    }

    /// <summary>Makes a document and its file.</summary>
    /// <exception cref="ProjectRefusedException">The kind cannot be made, or the mesh is missing.</exception>
    public static void MakeDocument(OpenLibrary library, ProjectDocumentRequest request) {
        var side = request.Resolution ?? DefaultResolution;
        switch (request.Kind) {
            case DocumentKinds.Graph:
                var text = GraphDocuments.Text(GraphDocuments.New(request.Name));
                library.CreateGraph(new(request.Name, request.Project), text);
                break;
            case DocumentKinds.LayeredMaterial:
                MakeStack(library, request, side, null);
                break;
            case DocumentKinds.ImageMaterial:
                ImageMaterial.Create(library, request);
                break;
            case DocumentKinds.Texturing:
                var contents = library.Contents(request.Project);
                var mesh = contents.Meshes.FirstOrDefault(m => m.Id == request.Mesh && m.Id is not null)
                    ?? throw new ProjectRefusedException("A texturing document needs a mesh of its project.");
                MakeStack(library, request, side, mesh);
                break;
            default:
                throw new ProjectRefusedException($"'{request.Kind}' is no kind of document.");
        }
    }

    static void MakeStack(OpenLibrary library, ProjectDocumentRequest request, int side, ProjectMeshInfo? mesh) {
        var file = library.NewDocumentFile(request.Project, request.Name, StackDocuments.Extension);
        var model = mesh is null ? null : Relative(request.Project, mesh.Path);
        StackDocuments.Create(library.ProjectFile(request.Project, file), side, side, model);
        var on = mesh?.Id is { } id ? new AssetId(id) : (AssetId?)null;
        library.AddDocument(request.Project, request.Kind, file, request.Name, on);
    }

    static string Relative(string project, string below) =>
        below[$"{LibraryLayout.ProjectsFolder}/{project}/".Length..];

    /// <summary>What a request's resolution has to be, or the refusal.</summary>
    public static ProblemHttpResult? BadResolution(int? resolution) =>
        resolution is null or (>= MinResolution and <= MaxResolution)
            ? null
            : TypedResults.Problem(
                $"A resolution is {MinResolution} to {MaxResolution}.",
                statusCode: StatusCodes.Status400BadRequest
            );

    /// <summary>Names a route, says what it does, and what it can answer other than success.</summary>
    static void Described(this RouteHandlerBuilder route, string name, string summary) =>
        route.WithName(name)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary(summary);

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

    static Results<Ok<ProjectContents>, ProblemHttpResult> AddMesh(LibraryHost host, ProjectMeshRequest request) =>
        Guarded(
            host,
            library => {
                library.AddMesh(request.Project, request.Mesh, request.High);
                host.RequestRescan();
                return TypedResults.Ok(library.Contents(request.Project));
            }
        );

    static Results<Ok<ProjectPicture>, ProblemHttpResult> AddPicture(LibraryHost host, ProjectPictureRequest request) =>
        Guarded(
            host,
            library => TypedResults.Ok(new ProjectPicture(library.AddPicture(request.Project, request.Picture)))
        );

    static Results<Ok<ProjectContents>, ProblemHttpResult> AddDocument(
        LibraryHost host,
        ProjectDocumentRequest request
    ) {
        if (BadResolution(request.Resolution) is { } bad) {
            return bad;
        }

        return Guarded(
            host,
            library => {
                MakeDocument(library, request);
                host.RequestRescan();
                return TypedResults.Ok(library.Contents(request.Project));
            }
        );
    }

    static Results<Ok<ProjectContents>, ProblemHttpResult> SetDocumentMesh(
        LibraryHost host,
        StackSessions stacks,
        ProjectDocumentMeshRequest request
    ) =>
        Guarded(
            host,
            library => {
                var changed = library.SetDocumentMesh(request.Project, new(request.Document), new(request.Mesh));
                var contents = library.Contents(request.Project);
                var mesh = contents.Meshes.First(m => m.Id == request.Mesh);
                var file = library.ProjectFile(request.Project, changed.File);
                if (stacks.All.All(session => session.Path != file)) {
                    StackDocuments.SetModel(file, Relative(request.Project, mesh.Path));
                }

                host.RequestRescan();
                return TypedResults.Ok(contents);
            }
        );

    static Results<Ok<MaterialList>, ProblemHttpResult> GetMaterials(
        LibraryHost host,
        string? project,
        string? search
    ) =>
        Guarded(host, library => TypedResults.Ok(new MaterialList(MaterialShelf.List(library, project, search))));

    static Results<Ok<MaterialUse>, ProblemHttpResult> UseMaterial(
        LibraryHost host,
        StackSessions stacks,
        MaterialUseRequest request
    ) =>
        Guarded(
            host,
            library => {
                var graph = MaterialShelf.Publish(library, request.Project, request.Source);
                MaterialShelf.Follow(library, request.Project, stacks);
                return TypedResults.Ok(new MaterialUse(graph));
            }
        );

    static Results<Ok<MaterialList>, ProblemHttpResult> RefreshMaterials(
        LibraryHost host,
        StackSessions stacks,
        string project
    ) =>
        Guarded(
            host,
            library => {
                MaterialShelf.RefreshOpen(library, stacks);
                MaterialShelf.Refresh(library, project);
                return TypedResults.Ok(new MaterialList(MaterialShelf.List(library, project)));
            }
        );

    static Results<Ok<ProjectContents>, ProblemHttpResult> SetDocumentRole(
        LibraryHost host,
        ProjectDocumentRoleRequest request
    ) =>
        Guarded(
            host,
            library => {
                var document = library.Contents(request.Project).Documents.FirstOrDefault(d => d.Id == request.Document)
                    ?? throw new FileNotFoundException($"This project has no document {request.Document}.");
                MaterialShelf.CheckRole(library, document.Path, request.Role);
                library.SetDocumentRole(request.Project, new(request.Document), request.Role);
                host.RequestRescan();
                return TypedResults.Ok(library.Contents(request.Project));
            }
        );

    /// <summary>
    ///     A graph's Output as a picture in <c>brushes/</c>, through a session of its own unless one is open. The
    ///     Worker only has a picture once it has evaluated, so this asks until it does.
    /// </summary>
    static async Task<Results<Ok<MaterialBrush>, ProblemHttpResult>> MakeBrush(
        LibraryHost host,
        GraphSessions graphs,
        GraphEvaluator evaluator,
        PreviewStore previews,
        MaterialUseRequest request,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        var full = Path.GetFullPath(request.Source, library.RootPath);
        if (!File.Exists(full)) {
            return TypedResults.Problem(
                $"There is no graph '{request.Source}'.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        var held = graphs.All.Any(open => open.Path == full);
        var session = graphs.Open(request.Source);
        try {
            var (node, usage) = MaterialShelf.BrushOutput(await File.ReadAllTextAsync(full, cancellationToken));
            session.Evaluate();
            using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            patience.CancelAfter(BrushPatience);
            var picture = await RenderedAsync(session, evaluator, node, usage, patience.Token);
            var name = Path.GetFileNameWithoutExtension(request.Source);
            var alpha = $"{BrushFolder}/{name}.png";
            var file = library.ProjectFile(request.Project, alpha);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.Copy(previews.Find(picture.Hash)!, file, true);
            return TypedResults.Ok(new MaterialBrush(alpha));
        } catch (ProjectRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return TypedResults.Problem(
                "The graph did not render in time.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        } finally {
            if (!held) {
                graphs.Close(session);
            }
        }
    }

    static async Task<GraphFullImage> RenderedAsync(
        GraphSession session,
        GraphEvaluator evaluator,
        int node,
        string usage,
        CancellationToken cancellationToken
    ) {
        while (true) {
            try {
                return await evaluator.ImageAsync(session, node, usage, cancellationToken);
            } catch (IpcException) when (!cancellationToken.IsCancellationRequested) {
                if (session.EvaluationError is { } failed) {
                    throw new ProjectRefusedException(failed);
                }

                await Task.Delay(BrushRetry, cancellationToken);
            }
        }
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
        List<string> held = [
            ..graphs.All.Select(static s => s.Path),
            ..stacks.All.Select(static s => s.Path),
            ..viewport.State.Mesh is { } mesh ? [mesh] : Array.Empty<string>()
        ];
        foreach (var path in paths) {
            if (Holder(
                    held,
                    Path.GetFullPath($"{LibraryLayout.ProjectsFolder}/{path}", library.RootPath)
                ) is { } file) {
                throw new LibraryItemOpenException($"'{Path.GetFileName(file)}' in '{path}' is open. Close it first.");
            }
        }
    }

    static string? Holder(List<string> held, string folder) {
        var below = folder + Path.DirectorySeparatorChar;
        return held.Find(file => file.StartsWith(below, StringComparison.Ordinal));
    }
}
