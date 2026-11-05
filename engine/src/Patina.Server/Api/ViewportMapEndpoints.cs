using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Jobs;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Viewport;

namespace Patina.Server.Api;

/// <summary>
///     What the viewport lays over a mesh: pictures by path, a finished bake on its low mesh, or a graph's outputs. Any
///     client can do all three (plan §8). A request holds paths and the Server adds each file's hash, so the Worker
///     only reads a picture when it is a new picture, and pixels stay out of the API.
/// </summary>
static class ViewportMapEndpoints {
    const string Tag = "Viewport";
    const int Missing = StatusCodes.Status404NotFound;
    const int Refused = StatusCodes.Status422UnprocessableEntity;
    const int Conflict = StatusCodes.Status409Conflict;
    const int Unavailable = StatusCodes.Status503ServiceUnavailable;

    /// <summary>An Output node's usages the viewport has a slot for.</summary>
    static readonly IReadOnlySet<string> Slots =
        new HashSet<string>(["baseColor", "normal", "roughness", "metalness", "occlusion"], StringComparer.Ordinal);

    public static void MapViewportMaps(this IEndpointRouteBuilder api) {
        api.MapPut("/viewport/maps", SetMapsAsync)
            .WithName("SetViewportMaps")
            .WithTags(Tag)
            .WithSummary("Lays pictures over the mesh's own materials. An empty body takes them off.")
            .ProducesProblem(Missing);
        api.MapPut("/viewport/bake", ShowBakeAsync)
            .WithName("ShowBakeInViewport")
            .WithTags(Tag)
            .WithSummary("Shows a finished bake's normal and occlusion maps on the mesh it was baked onto.")
            .ProducesProblem(Missing)
            .ProducesProblem(Refused);
        api.MapPut("/viewport/graph", ShowGraphAsync)
            .WithName("ShowGraphInViewport")
            .WithTags(Tag)
            .WithSummary("Lays a graph's Output nodes over the mesh, as they stand now. Ask again to see a later edit.")
            .ProducesProblem(Missing)
            .ProducesProblem(Refused)
            .ProducesProblem(Conflict)
            .ProducesProblem(Unavailable);
    }

    static async Task<Results<Ok<ViewportState>, ProblemHttpResult>> SetMapsAsync(
        ViewportMapPaths request,
        ViewportSession session,
        CancellationToken cancellationToken
    ) {
        try {
            session.SetMaps(await Task.Run(() => Hashed(request), cancellationToken));
            return TypedResults.Ok(session.State);
        } catch (FileNotFoundException e) {
            return TypedResults.Problem(e.Message, statusCode: Missing);
        }
    }

    static async Task<Results<Ok<ViewportState>, ProblemHttpResult>> ShowBakeAsync(
        ViewportBakeRequest request,
        JobQueue queue,
        ViewportSession session,
        CancellationToken cancellationToken
    ) {
        if (queue.Find(request.Job) is not { } job) {
            return TypedResults.Problem($"There is no job '{request.Job}'.", statusCode: Missing);
        }

        if (job is not { State: JobStates.Done, Result: { } result }) {
            return TypedResults.Problem($"Job '{request.Job}' has not finished.", statusCode: Refused);
        }

        if (job.Bake.Space != "tangent") {
            return TypedResults.Problem(
                "This bake is in object space, and the viewport reads tangent-space maps.",
                statusCode: Refused
            );
        }

        string? Of(string map) => result.Maps.FirstOrDefault(file => file.Map == map)?.Path;

        try {
            var maps = new ViewportMapPaths(Normal: Of("normal"), Occlusion: Of("ao"));
            var hashed = await Task.Run(() => Hashed(maps), cancellationToken);
            if (!File.Exists(job.Bake.Low)) {
                return TypedResults.Problem($"There is no file at '{job.Bake.Low}'.", statusCode: Missing);
            }

            session.Show(job.Bake.Low, hashed);
            return TypedResults.Ok(session.State);
        } catch (FileNotFoundException e) {
            return TypedResults.Problem(e.Message, statusCode: Missing);
        }
    }

    static async Task<Results<Ok<ViewportState>, ProblemHttpResult>> ShowGraphAsync(
        ViewportGraphRequest request,
        GraphSessions graphs,
        GraphEvaluator evaluator,
        PreviewStore store,
        ViewportSession session,
        CancellationToken cancellationToken
    ) {
        if (graphs.Find(request.Graph) is not { } graph) {
            return TypedResults.Problem($"There is no graph session '{request.Graph}'.", statusCode: Missing);
        }

        if (session.State.Mesh is null) {
            return TypedResults.Problem("Open a mesh first: a graph has no surface of its own.", statusCode: Refused);
        }

        var outputs = graph.Outputs(Slots);
        if (outputs.Count == 0) {
            return TypedResults.Problem("This graph has no Output node the viewport can show.", statusCode: Refused);
        }

        try {
            Dictionary<string, ViewportMapFile> files = new(StringComparer.Ordinal);
            foreach (var (node, usage) in outputs) {
                var image = await evaluator.ImageAsync(graph, node, usage, cancellationToken);
                files[usage] = new(GraphPreviewFiles.PathOf(store.Root, image.Hash), image.Hash);
            }

            session.SetMaps(
                new(
                    files.GetValueOrDefault("baseColor"),
                    files.GetValueOrDefault("normal"),
                    files.GetValueOrDefault("roughness"),
                    files.GetValueOrDefault("metalness"),
                    files.GetValueOrDefault("occlusion")
                )
            );
            return TypedResults.Ok(session.State);
        } catch (IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: Conflict);
        } catch (Exception e) when (e is IOException or TimeoutException) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        }
    }

    static ViewportMapSet Hashed(ViewportMapPaths paths) =>
        new(
            Hash(paths.BaseColor),
            Hash(paths.Normal),
            Hash(paths.Roughness),
            Hash(paths.Metallic),
            Hash(paths.Occlusion)
        );

    /// <exception cref="FileNotFoundException">The file is not there.</exception>
    static ViewportMapFile? Hash(string? path) {
        if (path is null) {
            return null;
        }

        var full = Path.GetFullPath(path);
        return File.Exists(full)
            ? new(full, Revision.OfFile(full).Text)
            : throw new FileNotFoundException($"There is no file at '{path}'.", path);
    }
}
