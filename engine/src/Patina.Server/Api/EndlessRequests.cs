namespace Patina.Server.Api;

/// <summary>
///     The requests that never end on their own: every WebSocket (events, viewport input) and the frame stream. They
///     end when the Server begins to stop (#236).
/// </summary>
/// <remarks>
///     ⚠ <b>A graceful stop waits for every request in flight</b>, up to the host's shutdown timeout of 30 s. A client
///     following events kept the Server stopping for all of it, so the desktop app, which waits 3 s, killed it on every
///     quit: the quit took 3 s longer than it had to, and the Server never left its discovery entry or removed its
///     socket. Ending these when the Server stops lets an ordinary request in flight, such as a save, finish.
/// </remarks>
public static class EndlessRequests {
    /// <summary>Marks an endpoint whose response is endless, as the frame stream's is.</summary>
    public static readonly object Marker = new EndlessResponse();

    /// <summary>Ends the endless requests when the Server begins to stop.</summary>
    public static void UseEndlessRequestsEndOnStop(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);
        var stopping = app.Lifetime.ApplicationStopping;
        app.Use(async (context, next) => {
                var endless = context.WebSockets.IsWebSocketRequest
                    || context.GetEndpoint()?.Metadata.GetMetadata<EndlessResponse>() is not null;
                if (!endless) {
                    await next(context);
                    return;
                }

                await using var ending = stopping.Register(static held => ((HttpContext)held!).Abort(), context);
                await next(context);
            }
        );
    }

    sealed class EndlessResponse;
}
