using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Patina.Contracts;
using System.Net.WebSockets;
using System.Text.Json;

namespace Patina.Server.Viewport;

/// <summary>
///     One client on <c>/ws/viewport</c>. Input comes in as JSON text. With <c>?frames=false</c> that is all it does
///     and the client takes frames from <see cref="ViewportStream" />; otherwise frames go out as binary messages.
/// </summary>
sealed class ViewportSocket(WebSocket socket, ViewportSession session, bool sendFrames) : IDisposable {
    const int InputBytes = 4096;

    readonly FrameOutbox outbox = new();

    public void Dispose() => outbox.Dispose();

    public async Task RunAsync(CancellationToken cancellationToken) {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (sendFrames) {
            session.Subscribe(outbox);
        }

        var sending = sendFrames ? outbox.PumpAsync(SendAsync, stop.Token) : Task.CompletedTask;
        try {
            await ReceiveLoopAsync(stop.Token);
        } catch (WebSocketException) {
            // The client went away without a goodbye.
        } catch (OperationCanceledException) when (stop.IsCancellationRequested) {
            // The Server is stopping.
        } finally {
            session.Unsubscribe(outbox);
            await stop.CancelAsync();
            try {
                await sending;
            } catch (Exception e) when (e is OperationCanceledException or WebSocketException) {
                // Over either way.
            }
        }
    }

    async ValueTask SendAsync(PublishedFrame frame, CancellationToken cancellationToken) {
        await socket.SendAsync(frame.Header, WebSocketMessageType.Binary, false, cancellationToken);
        await socket.SendAsync(
            frame.Slot.Memory[..frame.PixelBytes],
            WebSocketMessageType.Binary,
            true,
            cancellationToken
        );
    }

    async Task ReceiveLoopAsync(CancellationToken cancellationToken) {
        var buffer = new byte[InputBytes];
        while (socket.State == WebSocketState.Open) {
            var received = await socket.ReceiveAsync(buffer, cancellationToken);
            if (received.MessageType == WebSocketMessageType.Close) {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
                return;
            }

            if (received is { MessageType: WebSocketMessageType.Text, EndOfMessage: true }) {
                Apply(buffer.AsSpan(0, received.Count));
            }
        }
    }

    void Apply(ReadOnlySpan<byte> json) {
        try {
            if (JsonSerializer.Deserialize(json, ContractsJsonContext.Default.ViewportInput) is { } input) {
                session.Apply(input);
            }
        } catch (JsonException) {
            // A client's mistake is not a reason to drop its frames.
        }
    }
}

/// <summary>
///     Frames as one long HTTP response, each a header and its pixels, back to back. A browser reads this several times
///     faster than a WebSocket (ADR 0003), and any HTTP client can read it.
/// </summary>
static class ViewportStream {
    public static async Task RunAsync(HttpContext context, ViewportSession session) {
        // A client that pauses is a dropped frame, not a slow connection to be cut off.
        context.Features.Get<IHttpMinResponseDataRateFeature>()?.MinDataRate = null;
        context.Response.ContentType = "application/octet-stream";
        var body = context.Response.Body;
        await body.FlushAsync(context.RequestAborted);

        using var outbox = new FrameOutbox();
        session.Subscribe(outbox);
        try {
            await outbox.PumpAsync(
                async (frame, cancellationToken) => {
                    await body.WriteAsync(frame.Header, cancellationToken);
                    await body.WriteAsync(frame.Slot.Memory[..frame.PixelBytes], cancellationToken);
                    await body.FlushAsync(cancellationToken);
                },
                context.RequestAborted
            );
        } catch (OperationCanceledException) {
            // The client left.
        } catch (IOException) {
            // The client left mid-frame.
        } finally {
            session.Unsubscribe(outbox);
        }
    }
}
