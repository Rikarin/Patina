using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
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
    const int MinResolution = 16;

    public static void MapLibraryTreeApi(this IEndpointRouteBuilder api) {
        var library = api.MapGroup("/library").WithTags(Tag);

        library.MapGet("/folders", GetFolders)
            .Described("GetFolders", "The folders directly in a folder, where no parent is the top of the library.");

        library.MapPost("/folders", CreateFolder)
            .Described("CreateFolder", "Makes a folder.");

        library.MapPatch("/folders", UpdateFolder)
            .Described(
                "UpdateFolder",
                "Renames a folder or changes its color. Nothing in it may be open to rename it."
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
        library.MapMaterialRoutes();
        library.MapBrushRoutes();
        library.MapPackageRoutes();
        library.MapSnapshotRoutes();
        library.MapSampleRoutes();
        library.MapCostRoutes();
    }

    static void MapProjectRoutes(RouteGroupBuilder library) {
        library.MapGet("/project", GetProject)
            .Described(
                "GetProjectContents",
                "What a project holds. Read from its folder, so it works before the index has caught up."
            );

        library.MapPost("/project/meshes", AddMesh)
            .Described("AddProjectMesh", "Brings a mesh into a project, and its detailed mesh if it has one.");

        library.MapGet("/project/mesh-change", GetMeshChange)
            .Described(
                "GetProjectMeshChange",
                "Whether a mesh file differs from the one the project already has, so that reloading has work to do."
            );

        library.MapGet("/mesh", MeshFactsEndpoint.GetAsync)
            .Described(
                "GetMeshFacts",
                "What a mesh file is: its parts, materials, texture coordinate sets, triangles, box and units. The "
                + "path is below the library or a file on this machine."
            );

        library.MapProjectBlocks();

        library.MapPatch("/project/settings", ProjectSettingsEndpoint.SetAsync)
            .Described(
                "SetProjectSettings",
                "Sets a project's mesh, detailed mesh and auto unwrap. A mesh goes under all its texturing documents."
            );

        library.MapPost("/project/documents", AddDocument)
            .Described("AddProjectDocument", "Makes a document of a kind in a project.");

        library.MapPost("/project/files", ImportFile)
            .Described("ImportProjectFile", "Brings a mesh or a picture into a project. Anything else is refused.");

        library.MapMaterialImport();

        library.MapLibraryFiles();

        library.MapPatch("/project/document", SetDocumentMesh)
            .Described("SetDocumentMesh", "Puts a texturing document on another mesh of its project.");

        library.MapPatch("/project/document/role", SetDocumentRole)
            .Described("SetDocumentRole", "Says what a graph is for: a material, a mask, a brush or a filter.");

        library.MapPost("/project/document/copy", CopyDocument)
            .Described("CopyDocument", "Makes a new document with another's content, as Save As and Duplicate do.");

        library.MapGet("/recovery", GetRecovery)
            .Described("GetRecovery", "Whether a document has work autosaved after its last save.");

        library.MapPost("/recovery", Recover)
            .Described("RecoverDocument", "Restores or forgets autosaved work. The document has to be closed.");

        library.MapPatch("/project", RenameProject)
            .Described("RenameProject", "Gives a project a new display name. Its folder stays.");

        library.MapPost("/project/duplicate", DuplicateProject)
            .Described("DuplicateProject", "Copies a project beside itself, with identities of its own.");

        library.MapDelete("/project", TrashProject)
            .Described("TrashProject", "Moves a project to the library's hidden trash.");
    }

    /// <summary>Makes a document and its file.</summary>
    /// <exception cref="ProjectRefusedException">The kind cannot be made, or the mesh is missing.</exception>
    public static void MakeDocument(OpenLibrary library, ProjectDocumentRequest request) {
        var side = request.Resolution ?? TextureSizes.Default;
        switch (request.Kind) {
            case DocumentKinds.Graph:
                var text = GraphDocuments.Text(GraphDocuments.New(request.Name, side));
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
        StackDocuments.Create(library.ProjectFile(request.Project, file), side, request.Rows ?? side, model);
        var on = mesh?.Id is { } id ? new AssetId(id) : (AssetId?)null;
        library.AddDocument(request.Project, request.Kind, file, request.Name, on);
    }

    internal static string Relative(string project, string below) =>
        below[$"{LibraryLayout.ProjectsFolder}/{project}/".Length..];

    /// <summary>What a request's resolution has to be, or the refusal.</summary>
    public static ProblemHttpResult? BadResolution(int? resolution) =>
        resolution is null or (>= MinResolution and <= TextureSizes.Largest)
            ? null
            : TypedResults.Problem(
                $"A resolution is {MinResolution} to {TextureSizes.Largest}.",
                statusCode: StatusCodes.Status400BadRequest
            );

    /// <summary>Names a route, says what it does, and what it can answer other than success.</summary>
    internal static void Described(this RouteHandlerBuilder route, string name, string summary) =>
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

    static Results<Ok<ProjectMeshChange>, ProblemHttpResult> GetMeshChange(
        LibraryHost host,
        string project,
        string mesh
    ) =>
        Guarded(host, library => TypedResults.Ok(new ProjectMeshChange(library.MeshChanged(project, mesh))));

    static Results<Ok<ProjectContents>, ProblemHttpResult> AddMesh(LibraryHost host, ProjectMeshRequest request) =>
        Guarded(
            host,
            library => {
                library.AddMesh(request.Project, request.Mesh, request.High);
                host.RequestRescan();
                return TypedResults.Ok(library.Contents(request.Project));
            }
        );

    /// <summary>Brings a file into a project once a picture reads whole: a broken one is refused, not copied.</summary>
    static async Task<Results<Ok<ProjectContents>, ProblemHttpResult>> ImportFile(
        LibraryHost host,
        PicturePreviews previews,
        ProjectFileRequest request,
        CancellationToken cancellationToken
    ) =>
        await LibraryPictures.UnreadableAsync(previews, request.File, cancellationToken) is { } refused
            ? refused
            : Guarded(
                host,
                library => {
                    library.Import(request.Project, request.File);
                    host.RequestRescan();
                    return TypedResults.Ok(library.Contents(request.Project));
                }
            );

    static Results<Ok<ProjectContents>, ProblemHttpResult> AddDocument(
        LibraryHost host,
        ProjectDocumentRequest request
    ) {
        if ((BadResolution(request.Resolution) ?? BadResolution(request.Rows)) is { } bad) {
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

    static Results<Ok<ProjectContents>, ProblemHttpResult> CopyDocument(
        LibraryHost host,
        ProjectDocumentCopyRequest request
    ) =>
        Guarded(
            host,
            library => {
                library.CopyDocument(request.Project, new(request.Document), request.Name);
                host.RequestRescan();
                return TypedResults.Ok(library.Contents(request.Project));
            }
        );

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
        Guarded(
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
        Guarded(
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

    static Results<Ok<ProjectContents>, ProblemHttpResult> DuplicateProject(
        LibraryHost host,
        ProjectDuplicateRequest request
    ) =>
        Guarded(
            host,
            library => {
                var copy = library.DuplicateProject(request.Path, request.Name);
                host.RequestRescan();
                return TypedResults.Ok(library.Contents(copy));
            }
        );

    static Results<NoContent, ProblemHttpResult> TrashProject(LibraryHost host, Holders holders, string path) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        try {
            holders.Refuse(library, [path]);
            library.TrashProject(path);
            host.RequestRescan();
            return TypedResults.NoContent();
        } catch (Exception e) when (Refusal(e) is { } problem) {
            return problem;
        }
    }

    internal static Results<Ok<T>, ProblemHttpResult> Guarded<T>(LibraryHost host, Func<OpenLibrary, Ok<T>> act) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        try {
            return act(library);
        } catch (Exception e) when (Refusal(e) is { } problem) {
            return problem;
        }
    }

    internal static ProblemHttpResult? Refusal(Exception e) =>
        e switch {
            FileNotFoundException missing => Refusals.Of(missing),
            ProjectRefusedException refused => Refusals.Of(refused),
            StackEditRefusedException refused => Refusals.Of(refused, StatusCodes.Status400BadRequest),
            LibraryItemOpenException => Refusals.Problem(
                e.Message,
                StatusCodes.Status409Conflict,
                Refusals.LibraryConflict,
                Refusals.Reason(e.Message)
            ),
            _ when Refusals.Busy(e) is { } busy => busy,
            IOException or UnauthorizedAccessException => Refusals.Unwritable(e, StatusCodes.Status409Conflict),
            _ => null
        };

    internal static ProblemHttpResult NoLibrary() => Refusals.NoLibraryOpen();
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
