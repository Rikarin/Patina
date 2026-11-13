using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Stacks;
using Patina.Server.Viewport;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Patina.Server.Api;

/// <summary>REST API v1. Nothing here may depend on Electron: every client gets the same contract (plan §8).</summary>
static class PatinaApi {
    const string ViewportTag = "Viewport";

    const int ApiVersion = 1;

    /// <summary>How much of Vixen's commit its version keeps, as Git abbreviates one.</summary>
    const int CommitShown = 9;

    /// <summary>The Vixen the Server was built with: its version, then its commit after a plus.</summary>
    static readonly string VixenVersion = Shortened(
        typeof(Vixen.Texturing.Layers.LayerStackAsset).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown"
    );

    static string Shortened(string version) =>
        version.IndexOf('+', StringComparison.Ordinal) is var plus and >= 0 && version.Length > plus + 1 + CommitShown
            ? version[..(plus + 1 + CommitShown)]
            : version;

    public static void MapPatinaApi(this IEndpointRouteBuilder app) {
        var api = app.MapGroup("/api/v1");
        api.MapGet(
                "/system",
                static (ServerIdentity identity, WorkerLink worker) =>
                    TypedResults.Ok(
                        new SystemInfo(
                            ProductInfo.Version,
                            ApiVersion,
                            identity.Library,
                            Host(),
                            worker.Status,
                            TextureSizes.Sides,
                            TextureSizes.Default
                        ) { Vixen = VixenVersion }
                    )
            )
            .WithName("GetSystem")
            .WithTags("System")
            .WithSummary("The Server's version, its library, the machine it runs on and the state of its Worker.");
        api.MapLibraryApi();

        api.MapGet("/viewport", static (ViewportSession session) => TypedResults.Ok(session.State))
            .WithName("GetViewport")
            .WithTags(ViewportTag)
            .WithSummary("The viewport: its mesh, camera and size. Frames come over the /ws/viewport WebSocket.");
        api.MapPut("/viewport/mesh", SetMesh)
            .WithName("SetViewportMesh")
            .WithTags(ViewportTag)
            .WithSummary("Shows a mesh file in the viewport.");
        api.MapDelete(
                "/viewport/mesh",
                static (ViewportSession session, StackSessions stacks) => {
                    session.Clear();
                    foreach (var held in session.Release([]).Select(stacks.Find).OfType<StackSession>()) {
                        stacks.Close(held);
                    }

                    return TypedResults.Ok(session.State);
                }
            )
            .WithName("ClearViewport")
            .WithTags(ViewportTag)
            .WithSummary("Takes the mesh and whatever it wears off the viewport.");
        api.MapPut(
                "/viewport/camera",
                static (ViewportCameraState camera, ViewportSession session) => {
                    session.Apply(new(camera));
                    return TypedResults.Ok(session.State);
                }
            )
            .WithName("SetViewportCamera")
            .WithTags(ViewportTag)
            .WithSummary("Moves the viewport's camera.");

        api.MapPost("/viewport/pick", PickAsync)
            .WithName("PickInViewport")
            .WithTags(ViewportTag)
            .WithSummary("What is under a position in the viewport, and the camera a stroke there carries.")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        api.MapPost("/viewport/cast", CastAsync)
            .WithName("CastInViewport")
            .WithTags(ViewportTag)
            .WithSummary(
                "Where rays along one direction meet the open mesh in its own space, such as a decal's surface measured "
                + "along the decal's facing (#263)."
            )
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        api.MapPut("/viewport/stack", WearStack)
            .WithName("ShowStackInViewport")
            .WithTags(ViewportTag)
            .WithSummary("Puts a stack on its mesh in the viewport, which shows every stroke from then on.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.Map(
                "/ws/viewport",
                static async (HttpContext context, ViewportSession session) => {
                    if (!context.WebSockets.IsWebSocketRequest) {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }

                    using var socket = await context.WebSockets.AcceptWebSocketAsync();
                    var sendFrames = context.Request.Query["frames"] != "false";
                    using var client = new ViewportSocket(socket, session, sendFrames);
                    await client.RunAsync(context.RequestAborted);
                }
            )
            .ExcludeFromDescription();

        // Binary and endless, so outside the OpenAPI document: docs/adr/0003-frame-transport.md describes it.
        api.MapGet("/viewport/frames", ViewportStream.RunAsync)
            .WithMetadata(EndlessRequests.Marker)
            .ExcludeFromDescription();

        api.MapViewportMaps();
        api.MapGraphApi();
        app.MapGraphSocket();
        api.MapStackApi();
        app.MapStackSocket();
        api.MapJobApi();
        api.MapBuildApi();
        api.MapPresetApi();
        app.MapJobSocket();
    }

    static async Task<Results<Ok<ViewportPick>, ProblemHttpResult>> PickAsync(
        ViewportPickRequest request,
        ViewportSession session,
        CancellationToken cancellationToken
    ) {
        try {
            return TypedResults.Ok(await session.PickAsync(request, cancellationToken));
        } catch (Patina.Ipc.IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status409Conflict);
        } catch (Exception e) when (e is IOException or TimeoutException) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    static async Task<Results<Ok<ViewportCast>, ProblemHttpResult>> CastAsync(
        ViewportCastRequest request,
        ViewportSession session,
        CancellationToken cancellationToken
    ) {
        var rays = request.Origins.Count / 3;
        var direction = request.Direction;
        if (request.Origins.Count % 3 != 0
            || rays > ViewportCastRequest.MaximumRays
            || direction.Count != 3
            || !(direction[0] * direction[0] + direction[1] * direction[1] + direction[2] * direction[2] > 0f)
            || !request.Origins.Concat(direction).All(float.IsFinite)) {
            return Refusals.Problem(
                $"A cast is three numbers to a ray, at most {ViewportCastRequest.MaximumRays} rays, and a direction of "
                + "three numbers that has a length.",
                StatusCodes.Status400BadRequest,
                Refusals.CastRefused
            );
        }

        try {
            return TypedResults.Ok(await session.CastAsync(request, cancellationToken));
        } catch (Patina.Ipc.IpcException e) {
            return Refusals.Problem(
                e.Message,
                StatusCodes.Status409Conflict,
                Refusals.ViewportMeshUnavailable,
                Refusals.Reason(e.Message)
            );
        } catch (TimeoutException e) {
            return Refusals.Of(e, StatusCodes.Status503ServiceUnavailable);
        } catch (IOException e) {
            return Refusals.Problem(e.Message, StatusCodes.Status503ServiceUnavailable, Refusals.RendererMissing);
        }
    }

    static Results<Ok<ViewportState>, ProblemHttpResult> WearStack(
        ViewportStackRequest request,
        StackSessions stacks,
        ViewportSession session,
        ServerIdentity identity,
        LibraryHost host
    ) {
        if (stacks.Find(request.Stack) is not { } stack) {
            return Refusals.Problem(
                $"There is no open stack '{request.Stack}'.",
                StatusCodes.Status404NotFound,
                Refusals.StackNotOpen,
                new Dictionary<string, string> { ["id"] = request.Stack }
            );
        }

        // A material has no mesh of its own, and is seen on a shape instead.
        var file = stack.ModelFile ?? PreviewShape.File(identity.Library, session.State.Display.Shape);
        if (!File.Exists(file)) {
            return TypedResults.Problem(
                $"There is no file at '{stack.ModelFile}'.",
                statusCode: StatusCodes.Status422UnprocessableEntity
            );
        }

        // A model's other texture sets are worn with it, or the rest of it would show nothing while one is painted.
        HashSet<string> before = [..stacks.All.Select(static open => open.Id)];
        string[] others = [..OtherSets(host.Library, stack).Select(path => stacks.Open(path).Id)];
        var worn = (IEnumerable<StackSession>)[stack, ..others.Select(stacks.Find).OfType<StackSession>()];
        session.Wear(stack.Id, file, others, others.Except(before), [..worn.Select(static s => (s.Set, s.UvSet))]);
        var (target, resolution) = StackChecking.Density(host.Library, stack);
        session.SetDensity(target, resolution);
        foreach (var closing in session.Release([..others]).Select(stacks.Find).OfType<StackSession>()) {
            stacks.Close(closing);
        }

        foreach (var other in others.Select(stacks.Find).OfType<StackSession>()) {
            other.Evaluate();
        }

        stack.Evaluate();
        return TypedResults.Ok(session.State);
    }

    /// <summary>The documents on the other materials of a stack's model, as the project lists them.</summary>
    static string[] OtherSets(OpenLibrary? library, StackSession stack) {
        var root = library is null ? null : Path.Combine(library.RootPath, LibraryLayout.ProjectsFolder);
        var folder = Path.GetDirectoryName(stack.Path);
        if (library is null || root is null || folder is null || !folder.StartsWith(root, StringComparison.Ordinal)) {
            return [];
        }

        try {
            var contents = library.Contents(Path.GetRelativePath(root, folder).Replace('\\', '/'));
            return [
                ..contents.Documents.Where(static document => document.Material is not null)
                    .Select(document => Path.GetFullPath(document.Path, library.RootPath))
                    .Where(path => path != stack.Path)
            ];
        } catch (FileNotFoundException) {
            return []; // a stack beside no project
        }
    }

    static Results<Ok<ViewportState>, ProblemHttpResult> SetMesh(ViewportMeshRequest request, ViewportSession session) {
        if (!File.Exists(request.Path)) {
            return TypedResults.Problem(
                $"There is no file at '{request.Path}'.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        session.SetMesh(request.Path);
        return TypedResults.Ok(session.State);
    }

    static HostInfo Host() =>
        new(
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription
        );
}
