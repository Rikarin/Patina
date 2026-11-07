using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     What a project's layers can be filled with: the material graphs on its shelf, and a graph rendered to a picture
///     for a brush to stamp.
/// </summary>
static class MaterialEndpoints {
    const string BrushFolder = "brushes";
    static readonly TimeSpan BrushPatience = TimeSpan.FromSeconds(60);
    static readonly TimeSpan BrushRetry = TimeSpan.FromMilliseconds(100);

    public static void MapMaterialRoutes(this RouteGroupBuilder library) {
        library.MapGet("/materials", GetMaterials)
            .Described("GetMaterials", "What a project's layers can be filled with.");

        library.MapPost("/materials/thumbnails", DrawMaterials)
            .Described(
                "GetMaterialThumbnails",
                "The pictures of some materials, by hash under /previews, drawing those the cache lacks first."
            );

        library.MapPost("/materials/use", UseMaterial)
            .Described("UseMaterial", "Makes a material graph ready for a project's layers, and names it for them.");

        library.MapPost("/materials/brush", MakeBrush)
            .Described("MakeBrush", "Renders a graph to a picture in its project, which a brush can stamp.");

        library.MapPost("/materials/refresh", RefreshMaterials)
            .Described("RefreshMaterials", "Brings a project's layers up to edits saved in the graphs they read.");
    }

    static Results<Ok<MaterialList>, ProblemHttpResult> GetMaterials(
        LibraryHost host,
        string? project,
        string? search
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => TypedResults.Ok(new MaterialList(MaterialShelf.List(library, project, search)))
        );

    static async Task<Results<Ok<MaterialThumbnails>, ProblemHttpResult>> DrawMaterials(
        LibraryHost host,
        ShelfThumbnails thumbnails,
        MaterialThumbnailRequest request,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        try {
            return TypedResults.Ok(
                new MaterialThumbnails(await thumbnails.DrawAsync(library, request.Sources, cancellationToken))
            );
        } catch (TimeoutException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    static Results<Ok<MaterialUse>, ProblemHttpResult> UseMaterial(
        LibraryHost host,
        StackSessions stacks,
        MaterialUseRequest request
    ) =>
        LibraryTreeEndpoints.Guarded(
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
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                MaterialShelf.RefreshOpen(library, stacks);
                MaterialShelf.Refresh(library, project);
                return TypedResults.Ok(new MaterialList(MaterialShelf.List(library, project)));
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
            return LibraryTreeEndpoints.NoLibrary();
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
}
