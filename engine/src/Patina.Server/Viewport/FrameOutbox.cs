using System.Diagnostics;

namespace Patina.Server.Viewport;

/// <summary>
///     One client's place in line: at most one frame beyond the one being sent, so a slow client sees fewer frames and
///     never old ones. Every transport sends what this hands it.
/// </summary>
sealed class FrameOutbox : IFrameSubscriber, IDisposable {
    readonly Lock waiting = new();
    readonly SemaphoreSlim ready = new(0, 1);
    PublishedFrame? next;
    long lastSendTicks;

    public double LastSendMilliseconds => TimeSpan.FromTicks(Interlocked.Read(ref lastSendTicks)).TotalMilliseconds;

    public void Dispose() {
        lock (waiting) {
            next?.Slot.Release();
            next = null;
        }

        ready.Dispose();
    }

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

    /// <summary>Hands each frame to <paramref name="send" />, one at a time, until cancelled.</summary>
    public async Task PumpAsync(
        Func<PublishedFrame, CancellationToken, ValueTask> send,
        CancellationToken cancellationToken
    ) {
        while (true) {
            await ready.WaitAsync(cancellationToken).ConfigureAwait(false);
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
                await send(frame, cancellationToken).ConfigureAwait(false);
                Interlocked.Exchange(ref lastSendTicks, Stopwatch.GetElapsedTime(start).Ticks);
            } finally {
                frame.Slot.Release();
            }
        }
    }
}
