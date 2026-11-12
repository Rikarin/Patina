using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Jobs;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Viewport;
using System.Collections.Frozen;

namespace Patina.Server.Api;

/// <summary>
///     What the viewport lays over a mesh: pictures by path, a finished bake on its low mesh, or a graph's outputs. Any
///     client can do all three (plan §8). A request holds paths and the Server adds each file's hash, so the Worker
///     only reads a picture when it is a new picture, and pixels stay out of the API.
/// </summary>
static class ViewportMapEndpoints {
    const string Tag = "Viewport";
    const int Missing = StatusCodes.Status404NotFound;

    static readonly FrozenSet<string> Rooms =
        new[] { "studio", "overcast", "sunset" }.ToFrozenSet(StringComparer.Ordinal);

    const int Refused = StatusCodes.Status422UnprocessableEntity;
    const int Conflict = StatusCodes.Status409Conflict;
    const int Unavailable = StatusCodes.Status503ServiceUnavailable;

    /// <summary>An Output node's usages the viewport has a slot for.</summary>
    static readonly IReadOnlySet<string> Slots =
        new HashSet<string>(
            ["baseColor", "normal", "roughness", "metalness", "occlusion", "emissive", "opacity"],
            StringComparer.Ordinal
        );

    public static void MapViewportMaps(this IEndpointRouteBuilder api) {
        api.MapPut("/viewport/maps", SetMapsAsync)
            .WithName("SetViewportMaps")
            .WithTags(Tag)
            .WithSummary("Lays pictures over the mesh's own materials. An empty body takes them off.")
            .ProducesProblem(Missing);
        api.MapPut("/viewport/display", SetDisplayAsync)
            .WithName("SetViewportDisplay")
            .WithTags(Tag)
            .WithSummary(
                "Sets how the model is lit, shown and finished: environment, light, exposure, curve, look, view, "
                + "lens and effects. No export sees any of it."
            )
            .ProducesProblem(Missing)
            .ProducesProblem(Refused);
        api.MapPost("/viewport/screenshot", ScreenshotAsync)
            .WithName("TakeViewportScreenshot")
            .WithTags(Tag)
            .WithSummary(
                "Writes the picture the viewport shows now to a PNG on the Server's machine, with its camera, display "
                + "settings and look, at the size asked for or the one clients asked for."
            )
            .ProducesProblem(Refused)
            .ProducesProblem(Unavailable);
        api.MapPut("/viewport/isolation", Isolate)
            .WithName("SetViewportIsolation")
            .WithTags(Tag)
            .WithSummary(
                "Draws only some named meshes and materials (texture sets) of the model, for every client. Empty lists "
                + "draw the whole model."
            );
        api.MapPut("/viewport/bake", ShowBakeAsync)
            .WithName("ShowBakeInViewport")
            .WithTags(Tag)
            .WithSummary("Shows a finished bake's normal and occlusion maps on the mesh it was baked onto.")
            .ProducesProblem(Missing)
            .ProducesProblem(Refused);
        api.MapPut("/viewport/bake-preview", PreviewBakeAsync)
            .WithName("PreviewBakeInViewport")
            .WithTags(Tag)
            .WithSummary(
                "Lays a bake's setup over the model before it runs: where its rays start and how far they reach, the "
                + "high meshes see-through, and which parts match by name. An empty low mesh takes it off."
            )
            .ProducesProblem(Missing)
            .ProducesProblem(Refused)
            .ProducesProblem(Unavailable);
        api.MapGet("/viewport/bake-preview", static (BakePreviewSession preview) => TypedResults.Ok(preview.State))
            .WithName("GetBakePreview")
            .WithTags(Tag)
            .WithSummary("The bake setup the viewport shows, and the key to its colours.");
        api.MapPut("/viewport/graph", ShowGraphAsync)
            .WithName("ShowGraphInViewport")
            .WithTags(Tag)
            .WithSummary(
                "Lays a graph's Output nodes over the mesh, or the display's shape with none, as they stand now, or "
                + "shows one node's picture there. Ask again to see a later edit."
            )
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

    static async Task<Results<Ok<ViewportState>, ProblemHttpResult>> SetDisplayAsync(
        ViewportDisplay request,
        ViewportSession session,
        ServerIdentity identity,
        CancellationToken cancellationToken
    ) {
        if (request.Problem() is { } wrong) {
            return TypedResults.Problem(wrong, statusCode: Refused);
        }

        if (!Rooms.Contains(request.Environment)) {
            if (!PictureFormats.IsEnvironment(request.Environment)) {
                return TypedResults.Problem(
                    $"'{request.Environment}' is no room and no "
                    + $"{string.Join(" or ", PictureFormats.Environments)} file.",
                    statusCode: Refused
                );
            }

            if (!File.Exists(request.Environment)) {
                return TypedResults.Problem($"There is no file '{request.Environment}'.", statusCode: Missing);
            }
        }

        if (request.Look is { } table && !table.EndsWith(".cube", StringComparison.OrdinalIgnoreCase)) {
            return TypedResults.Problem($"'{table}' is not a .cube table.", statusCode: Refused);
        }

        try {
            var (look, picture) = await Task.Run(() => (Hash(request.Look), Hash(request.Picture)), cancellationToken);
            // A stand-in shape gives way to the one chosen; a model's own mesh stays whatever the shape says.
            var mesh = session.State.Mesh;
            if (PreviewShape.Holds(identity.Library, mesh)) {
                var shape = await Task.Run(() => PreviewShape.File(identity.Library, request.Shape), cancellationToken);
                if (shape != mesh) {
                    session.Reshape(shape);
                }
            }

            session.SetDisplay(new(request, look, picture));
            return TypedResults.Ok(session.State);
        } catch (FileNotFoundException e) {
            return TypedResults.Problem(e.Message, statusCode: Missing);
        }
    }

    static async Task<Results<Ok<ViewportScreenshot>, ProblemHttpResult>> ScreenshotAsync(
        ViewportScreenshotRequest request,
        ViewportSession session,
        CancellationToken cancellationToken
    ) {
        if (!request.Path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || !Path.IsPathFullyQualified(request.Path)) {
            return TypedResults.Problem(
                $"'{request.Path}' is not a full path to a .png file.",
                statusCode: Refused
            );
        }

        if (request is { Width: < 2 }
            or { Height: < 2 }
            or { Width: > FrameMemory.MaxWidth }
            or { Height: > FrameMemory.MaxHeight }) {
            return TypedResults.Problem(
                $"A screenshot is 2 to {FrameMemory.MaxWidth} pixels across and 2 to {FrameMemory.MaxHeight} down.",
                statusCode: Refused
            );
        }

        try {
            return TypedResults.Ok(await session.ScreenshotAsync(request, cancellationToken));
        } catch (IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        } catch (TimeoutException e) {
            return Refusals.Of(e, Unavailable);
        }
    }

    /// <summary>
    ///     Shows a bake's setup, refused for options a bake would refuse and for files that are not there, so what is
    ///     shown is a bake that could run.
    /// </summary>
    static async Task<Results<Ok<BakePreviewState>, ProblemHttpResult>> PreviewBakeAsync(
        ViewportBakePreview request,
        BakePreviewSession preview,
        ViewportSession session,
        CancellationToken cancellationToken
    ) {
        if (request.Low.Length > 0) {
            if (request.Problem() is { } wrong) {
                return TypedResults.Problem(wrong, statusCode: Refused);
            }

            string[] files =
                [request.Low, ..request.High, request.Cage ?? string.Empty, request.Options.Rays.DistanceMap];
            if (files.FirstOrDefault(static file => file.Length > 0 && !File.Exists(file)) is { } missing) {
                return TypedResults.Problem($"There is no file at '{missing}'.", statusCode: Missing);
            }

            if (request.High.Count == 0) {
                return TypedResults.Problem(
                    "A bake needs a high mesh, which may be the low mesh itself.",
                    statusCode: Refused
                );
            }
        }

        try {
            var shown = await preview.ShowAsync(request, cancellationToken);
            session.Redraw();
            return TypedResults.Ok(shown);
        } catch (RendererMissingException e) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        } catch (IpcException e) {
            session.Redraw();
            return TypedResults.Problem(e.Message, statusCode: Refused);
        } catch (Exception e) when (e is IOException or TimeoutException) {
            // The Worker went, or took longer than a reply may to read the meshes: the next frame tells it again.
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        }
    }

    static Ok<ViewportState> Isolate(ViewportIsolation request, ViewportSession session) {
        session.Isolate(request);
        return TypedResults.Ok(session.State);
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

        string? Of(string map) => result.Maps.FirstOrDefault(file => file.Map == map)?.Path;

        var tangent = job.Bake!.Options.Space == "tangent";
        if (request.Map is { } wanted && Of(wanted) is null) {
            return TypedResults.Problem($"Job '{request.Job}' made no '{wanted}' map.", statusCode: Missing);
        }

        // ⚠ One map alone is shown as stored, which any map can be; on the material only a tangent space normal reads.
        if (!tangent && request.Map is null) {
            return TypedResults.Problem(
                "This bake is in object space, and the viewport reads tangent-space maps.",
                statusCode: Refused
            );
        }

        try {
            var maps = tangent ? new ViewportMapPaths(Normal: Of("normal"), Occlusion: Of("ao")) : new();
            var (hashed, alone) = await Task.Run(
                () => (Hashed(maps), request.Map is { } map ? Hash(Of(map)) : null),
                cancellationToken
            );
            if (!File.Exists(job.Bake.Low)) {
                return TypedResults.Problem($"There is no file at '{job.Bake.Low}'.", statusCode: Missing);
            }

            session.Show(job.Bake.Low, hashed, alone);
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
        ServerIdentity identity,
        CancellationToken cancellationToken
    ) {
        if (graphs.Find(request.Graph) is not { } graph) {
            return TypedResults.Problem($"There is no graph session '{request.Graph}'.", statusCode: Missing);
        }

        if (request.Node is { } shownNode) {
            return await ShowNodeAsync(
                graph,
                shownNode,
                request.Port,
                evaluator,
                store,
                session,
                identity,
                cancellationToken
            );
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

            StandIn(session, identity);

            session.SetMaps(
                new(
                    files.GetValueOrDefault("baseColor"),
                    files.GetValueOrDefault("normal"),
                    files.GetValueOrDefault("roughness"),
                    files.GetValueOrDefault("metalness"),
                    files.GetValueOrDefault("occlusion"),
                    files.GetValueOrDefault("emissive"),
                    files.GetValueOrDefault("opacity")
                ) {
                    // A graph's Height to Normal points green down, and the shader reads green up (ADR 0011).
                    GreenDown = true
                }
            );
            return TypedResults.Ok(session.State);
        } catch (IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: Conflict);
        } catch (Exception e) when (e is IOException or TimeoutException) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        }
    }

    /// <summary>One node's picture on the mesh, as stored, in place of the material.</summary>
    static async Task<Results<Ok<ViewportState>, ProblemHttpResult>> ShowNodeAsync(
        GraphSession graph,
        int node,
        string? port,
        GraphEvaluator evaluator,
        PreviewStore store,
        ViewportSession session,
        ServerIdentity identity,
        CancellationToken cancellationToken
    ) {
        // The output a node's preview shows is the one that has a picture, which a client need not know.
        var output = port ?? graph.State.Previews.FirstOrDefault(preview => preview.Node == node)?.Port;
        if (output is null) {
            return TypedResults.Problem(
                $"Node {node} has no picture yet. Ask again once the graph has evaluated it.",
                statusCode: Conflict
            );
        }

        try {
            var image = await evaluator.ImageAsync(graph, node, output, cancellationToken);
            StandIn(session, identity);
            session.ShowNode(
                new(graph.Id, node, output),
                new(GraphPreviewFiles.PathOf(store.Root, image.Hash), image.Hash)
            );
            return TypedResults.Ok(session.State);
        } catch (IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: Conflict);
        } catch (Exception e) when (e is IOException or TimeoutException) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        }
    }

    /// <summary>A graph has no surface of its own, so with no mesh open it is seen on the display's shape.</summary>
    static void StandIn(ViewportSession session, ServerIdentity identity) {
        if (session.State is { Mesh: null, Display.Shape: var shape }) {
            session.SetMesh(PreviewShape.File(identity.Library, shape));
        }
    }

    static ViewportMapSet Hashed(ViewportMapPaths paths) =>
        new(
            Hash(paths.BaseColor),
            Hash(paths.Normal),
            Hash(paths.Roughness),
            Hash(paths.Metallic),
            Hash(paths.Occlusion),
            Hash(paths.Emissive),
            Hash(paths.Opacity)
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
