using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;

namespace Patina.Server.Api;

/// <summary>
///     The library. Every list is a page: the browser is a virtualised grid over 10,000 projects (plan §5), so no
///     endpoint can return a whole library. A write changes the files and returns; the index catches up behind it,
///     and <c>indexing</c> on <c>GET /library</c> is true until it has.
/// </summary>
static class LibraryEndpoints {
    const string Tag = "Library";
    const int DefaultLimit = 50;
    const int MaxLimit = 500;

    public static void MapLibraryApi(this IEndpointRouteBuilder api) {
        var library = api.MapGroup("/library").WithTags(Tag);

        library.MapGet("/", GetLibrary)
            .WithName("GetLibrary")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("The Server's library: its settings, its size and whether a rescan is running.");

        library.MapGet("/projects", GetProjects)
            .WithName("GetProjects")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One page of projects: by name, or by relevance when searching.");

        library.MapPost("/projects", CreateProject)
            .WithName("CreateProject")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Writes a new project's files. It reaches the listings once the index has caught up.");

        library.MapPost("/compounds", CopyCompound)
            .WithName("CopyCompound")
            .WithSummary("Copies a shipped compound into the library as a file, so that it can be edited.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        library.MapPost("/graphs", CreateGraph)
            .WithName("CreateGraph")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Writes a new, empty graph file. It reaches the listings once the index has caught up.");

        library.MapGet("/projects/tags", GetProjectTags)
            .WithName("GetProjectTags")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One page of the tags on projects, by name, with how many projects carry each.");

        library.MapGet("/assets", GetAssets)
            .WithName("GetAssets")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One page of shared assets with their usage counts: by name, or by relevance when searching.");

        library.MapGet("/assets/tags", GetAssetTags)
            .WithName("GetAssetTags")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One page of the tags on shared assets, by name, with how many assets carry each.");

        library.MapGet("/assets/{id:guid}/impact", GetImpact)
            .WithName("GetAssetImpact")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("What saving a shared asset would touch: its dependent projects, and how many are pinned.");
    }

    static Results<Ok<LibraryInfo>, ProblemHttpResult> GetLibrary(LibraryHost host) =>
        host.Library is { } library ? TypedResults.Ok(library.Describe()) : NoLibrary();

    static Results<Ok<ProjectPage>, ProblemHttpResult> GetProjects(
        LibraryHost host,
        string? search,
        string[]? tag,
        int offset = 0,
        int limit = DefaultLimit
    ) =>
        Listing(host, offset, limit, library => library.Projects(new(search, tag, offset, limit)));

    static Results<Created<ProjectSummary>, ProblemHttpResult> CreateProject(
        LibraryHost host,
        ProjectCreateRequest request
    ) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        try {
            var project = library.CreateProject(new(request.Name, request.Folder, request.Tags));
            host.RequestRescan();
            return TypedResults.Created((string?)null, project);
        } catch (ProjectExistsException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status409Conflict);
        } catch (ProjectRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    static Results<Created<GraphCreated>, ProblemHttpResult> CreateGraph(LibraryHost host, GraphCreateRequest request) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        try {
            var text = GraphDocuments.Text(GraphDocuments.New(request.Name));
            var path = library.CreateGraph(new(request.Name, request.Project), text);
            host.RequestRescan();
            return TypedResults.Created((string?)null, new GraphCreated(path));
        } catch (ProjectExistsException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status409Conflict);
        } catch (ProjectRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    static Results<Created<GraphCompound>, ProblemHttpResult> CopyCompound(
        LibraryHost host,
        GraphSessions sessions,
        GraphCompoundCopy request
    ) {
        string? folder;
        if (request.Graph is { } graph) {
            if (sessions.Find(graph) is not { } session) {
                return TypedResults.Problem(
                    $"There is no open graph '{graph}'.",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            folder = session.Folder;
        } else {
            folder = host.Library is { } library ? CompoundFolders.Shared(library.RootPath) : null;
        }

        if (folder is null) {
            return NoLibrary();
        }

        try {
            var copy = sessions.Copy(request, folder);
            host.RequestRescan();
            return TypedResults.Created((string?)null, copy);
        } catch (FileNotFoundException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status404NotFound);
        } catch (ProjectExistsException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status409Conflict);
        } catch (ProjectRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status400BadRequest);
        } catch (GraphEditRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    static Results<Ok<TagPage>, ProblemHttpResult> GetProjectTags(
        LibraryHost host,
        int offset = 0,
        int limit = DefaultLimit
    ) {
        if (BadPage(offset, limit) is { } bad) {
            return bad;
        }

        return host.Library is { } library ? TypedResults.Ok(library.ProjectTags(offset, limit)) : NoLibrary();
    }

    static Results<Ok<TagPage>, ProblemHttpResult> GetAssetTags(
        LibraryHost host,
        int offset = 0,
        int limit = DefaultLimit
    ) {
        if (BadPage(offset, limit) is { } bad) {
            return bad;
        }

        return host.Library is { } library ? TypedResults.Ok(library.AssetTags(offset, limit)) : NoLibrary();
    }

    static Results<Ok<AssetPage>, ProblemHttpResult> GetAssets(
        LibraryHost host,
        string? search,
        string[]? tag,
        int offset = 0,
        int limit = DefaultLimit
    ) =>
        Listing(host, offset, limit, library => library.Assets(new(search, tag, offset, limit)));

    static Results<Ok<AssetImpact>, ProblemHttpResult> GetImpact(
        LibraryHost host,
        Guid id,
        int offset = 0,
        int limit = DefaultLimit
    ) {
        if (BadPage(offset, limit) is { } bad) {
            return bad;
        }

        if (host.Library is not { } library) {
            return NoLibrary();
        }

        return library.Impact(new(id), offset, limit) is { } impact
            ? TypedResults.Ok(impact)
            : TypedResults.Problem($"No shared asset has the id {id}.", statusCode: StatusCodes.Status404NotFound);
    }

    static Results<Ok<T>, ProblemHttpResult> Listing<T>(
        LibraryHost host,
        int offset,
        int limit,
        Func<OpenLibrary, T> page
    ) {
        if (BadPage(offset, limit) is { } bad) {
            return bad;
        }

        return host.Library is { } library ? TypedResults.Ok(page(library)) : NoLibrary();
    }

    static ProblemHttpResult? BadPage(int offset, int limit) =>
        offset < 0 || limit is < 1 or > MaxLimit
            ? TypedResults.Problem(
                $"Offset must not be negative, and limit is 1 to {MaxLimit}.",
                statusCode: StatusCodes.Status400BadRequest
            )
            : null;

    static ProblemHttpResult NoLibrary() =>
        TypedResults.Problem("This Server has no library.", statusCode: StatusCodes.Status404NotFound);
}
