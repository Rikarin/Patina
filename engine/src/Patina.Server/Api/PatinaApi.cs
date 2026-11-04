using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Viewport;
using System.Runtime.InteropServices;

namespace Patina.Server.Api;

/// <summary>REST API v1. Nothing here may depend on Electron: every client gets the same contract (plan §8).</summary>
static class PatinaApi {
    const int ApiVersion = 1;

    public static void MapPatinaApi(this IEndpointRouteBuilder app) {
        var api = app.MapGroup("/api/v1");
        api.MapGet(
                "/system",
                static (ServerIdentity identity, WorkerLink worker) =>
                    TypedResults.Ok(
                        new SystemInfo(ProductInfo.Version, ApiVersion, identity.Library, Host(), worker.Status)
                    )
            )
            .WithName("GetSystem")
            .WithTags("System")
            .WithSummary("The Server's version, its library, the machine it runs on and the state of its Worker.");

        api.MapGet("/viewport", static (ViewportSession session) => TypedResults.Ok(session.State))
            .WithName("GetViewport")
            .WithTags("Viewport")
            .WithSummary("The viewport: its mesh, camera and size. Frames come over the /ws/viewport WebSocket.");
        api.MapPut("/viewport/mesh", SetMesh)
            .WithName("SetViewportMesh")
            .WithTags("Viewport")
            .WithSummary("Shows a mesh file in the viewport.");
        api.MapPut(
                "/viewport/camera",
                static (ViewportCameraState camera, ViewportSession session) => {
                    session.Apply(new(camera));
                    return TypedResults.Ok(session.State);
                }
            )
            .WithName("SetViewportCamera")
            .WithTags("Viewport")
            .WithSummary("Moves the viewport's camera.");

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
        api.MapGet("/viewport/frames", ViewportStream.RunAsync).ExcludeFromDescription();
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
