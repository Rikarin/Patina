using Patina.Contracts;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

namespace Patina.Server.Viewport;

/// <summary>
///     One client on <c>/ws/viewport</c>: frames go out as binary messages, a header and then the pixels, and input comes
///     in as JSON text. It holds at most one frame beyond the one being sent, so a slow client sees fewer frames, not old
///     ones (ADR 0003).
/// </summary>
sealed class ViewportSocket(WebSocket socket, ViewportSession session) : IFrameSubscriber, IDisposable {
    const int InputBytes = 4096;

    readonly Lock waiting = new();
    readonly SemaphoreSlim ready = new(0, 1);
    PublishedFrame? next;
    long lastSendTicks;

    public double LastSendMilliseconds =>
        TimeSpan.FromTicks(Interlocked.Read(ref lastSendTicks)).TotalMilliseconds;

    public void Dispose() => ready.Dispose();

    public void Offer(PublishedFrame frame) {
        PublishedFrame? dropped;
        lock (waiting) {
            dropped = next;
            next = frame;
        }

        dropped?.Slot.Release();
        if (dropped is null) {
            ready.Release();
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken) {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        session.Subscribe(this);
        var sending = SendLoopAsync(stop.Token);
        try {
            await ReceiveLoopAsync(stop.Token);
        } catch (WebSocketException) {
            // The client went away without a goodbye.
        } catch (OperationCanceledException) when (stop.IsCancellationRequested) {
            // The Server is stopping.
        } finally {
            session.Unsubscribe(this);
            await stop.CancelAsync();
            try {
                await sending;
            } catch (Exception e) when (e is OperationCanceledException or WebSocketException) {
                // Over either way.
            }

            lock (waiting) {
                next?.Slot.Release();
                next = null;
            }
        }
    }

    async Task SendLoopAsync(CancellationToken cancellationToken) {
        while (true) {
            await ready.WaitAsync(cancellationToken);
            PublishedFrame? frame;
            lock (waiting) {
                frame = next;
                next = null;
            }

            if (frame is null) {
                continue;
            }

            try {
                var start = Stopwatch.GetTimestamp();
                await socket.SendAsync(frame.Header, WebSocketMessageType.Binary, false, cancellationToken);
                await socket.SendAsync(
                    frame.Slot.Memory[..frame.PixelBytes],
                    WebSocketMessageType.Binary,
                    true,
                    cancellationToken
                );
                Interlocked.Exchange(ref lastSendTicks, Stopwatch.GetElapsedTime(start).Ticks);
            } finally {
                frame.Slot.Release();
            }
        }
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
