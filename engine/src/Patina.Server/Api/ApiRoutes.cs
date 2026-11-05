using Patina.Server.Sessions;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Patina.Server.Api;

/// <summary>What the routes of every kind of document have in common.</summary>
static class ApiRoutes {
    public static void Describe(this RouteHandlerBuilder route, string name, string summary, params int[] problems) {
        route.WithName(name).WithSummary(summary);
        foreach (var problem in problems) {
            route.ProducesProblem(problem);
        }
    }

    /// <summary>
    ///     Serves numbered events over a WebSocket, as text messages, until the last one or the client
    ///     goes.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="source">The session, or null when there is none.</param>
    /// <param name="type">How an event is written.</param>
    /// <param name="last">Whether an event is the last the session will send.</param>
    /// <param name="numberOf">An event's number.</param>
    /// <param name="since">The number to start after, or the newest.</param>
    public static async Task FollowAsync<TEvent>(
        HttpContext context,
        IEventSource<TEvent>? source,
        JsonTypeInfo<TEvent> type,
        Func<TEvent, bool> last,
        Func<TEvent, long> numberOf,
        long? since
    ) {
        if (!context.WebSockets.IsWebSocketRequest) {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (source is null) {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var listening = WatchForCloseAsync(socket, stop);
        try {
            var cursor = since ?? source.Sequence;
            var open = true;
            while (open) {
                foreach (var item in source.After(cursor)) {
                    var message = JsonSerializer.SerializeToUtf8Bytes(item, type);
                    await socket.SendAsync(message, WebSocketMessageType.Text, true, stop.Token);
                    cursor = numberOf(item);
                    open = !last(item);
                }

                if (open) {
                    await source.WaitAsync(cursor, stop.Token);
                }
            }

            if (socket.State == WebSocketState.Open) {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, context.RequestAborted);
            }
        } catch (WebSocketException) {
            // The client went away without a goodbye.
        } catch (OperationCanceledException) {
            // The client left, or the Server is stopping.
        } finally {
            await stop.CancelAsync();
            await listening;
        }
    }

    /// <summary>Nothing a client sends here means anything; reading is how a socket learns it has closed.</summary>
    static async Task WatchForCloseAsync(WebSocket socket, CancellationTokenSource stop) {
        var buffer = new byte[256];
        try {
            while (socket.State == WebSocketState.Open) {
                var received = await socket.ReceiveAsync(buffer, stop.Token);
                if (received.MessageType == WebSocketMessageType.Close) {
                    break;
                }
            }
        } catch (Exception e) when (e is WebSocketException or OperationCanceledException) {
            // Over either way.
        }

        await stop.CancelAsync();
    }
}
