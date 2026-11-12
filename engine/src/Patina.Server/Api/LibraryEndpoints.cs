using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;

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

        api.MapLibraryTreeApi();
        api.MapLibraryCareApi();
        api.MapPreferenceApi();

        library.MapGet("/projects", GetProjects)
            .WithName("GetProjects")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary(
                "One page of projects: by name, or by relevance when searching. `folder` keeps those directly in it."
            );

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
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary(
                "Writes a new graph file, empty or from a template. It reaches the listings once the index has "
                + "caught up."
            );
        library.MapGet("/graph-templates", static () => TypedResults.Ok(GraphTemplates.All))
            .WithName("GetGraphTemplates")
            .WithSummary("What a new graph can start from: nothing, an Output node per channel, or a starter preset.");

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
        string? folder,
        int offset = 0,
        int limit = DefaultLimit
    ) =>
        Listing(host, offset, limit, library => library.Projects(new(search, tag, offset, limit, folder?.Trim('/'))));

    static Results<Created<ProjectSummary>, ProblemHttpResult> CreateProject(
        LibraryHost host,
        StackSessions stacks,
        ProjectCreateRequest request
    ) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        if (LibraryTreeEndpoints.BadResolution(request.Resolution) is { } bad) {
            return bad;
        }

        try {
            if (request.Mesh is { } mesh) {
                OpenLibrary.RequireMesh(mesh);
            }

            var kind = request.Kind ?? (request.Mesh is null ? null : DocumentKinds.Texturing);
            if (kind is not null && !DocumentKinds.Known(kind)) {
                throw new ProjectRefusedException($"'{kind}' cannot be made yet.");
            }

            if (kind == DocumentKinds.Texturing && request.Mesh is null) {
                throw new ProjectRefusedException("A texturing project needs a mesh.");
            }

            if (kind == DocumentKinds.ImageMaterial && !File.Exists(request.Image)) {
                throw new ProjectRefusedException("An image material needs a picture.");
            }

            var project = library.CreateProject(new(request.Name, request.Folder, request.Tags));
            var added = request.Mesh is { } source ? library.AddMesh(project.Path, source, request.High) : null;
            if (kind is not null) {
                LibraryTreeEndpoints.MakeDocument(
                    library,
                    new(project.Path, kind, request.Name, added?.Id.Value, request.Resolution, request.Image)
                );
            }

            host.RequestRescan();
            return TypedResults.Created((string?)null, project);
        } catch (FileNotFoundException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status404NotFound);
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

        if (LibraryTreeEndpoints.BadResolution(request.Resolution) is { } bad) {
            return bad;
        }

        try {
            var text = GraphDocuments.Text(GraphTemplates.Make(request.Name, request.Resolution, request.Template));
            // A graph belongs to a project, so one made with none gets one of its own.
            var project = request.Project ?? library.CreateProject(new(request.Name)).Path;
            var path = library.CreateGraph(new(request.Name, project), text);
            host.RequestRescan();
            return TypedResults.Created((string?)null, new GraphCreated(path));
        } catch (GraphEditRefusedException e) {
            // Only a template the library does not have refuses a new graph.
            return Refusals.Problem(
                e.Message,
                StatusCodes.Status422UnprocessableEntity,
                Refusals.GraphTemplateUnknown,
                new Dictionary<string, string> { ["template"] = request.Template ?? string.Empty }
            );
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
                return Refusals.Problem(
                    $"There is no open graph '{graph}'.",
                    StatusCodes.Status404NotFound,
                    Refusals.GraphNotOpen,
                    new Dictionary<string, string> { ["id"] = graph }
                );
            }

            folder = session.Folder;
        } else {
            return TypedResults.Problem(
                "A copy of a compound goes beside the graph that uses it, so it needs a graph.",
                statusCode: StatusCodes.Status422UnprocessableEntity
            );
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

    static ProblemHttpResult NoLibrary() => Refusals.NoLibraryOpen();
}
