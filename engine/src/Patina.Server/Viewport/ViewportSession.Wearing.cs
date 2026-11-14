using Patina.Ipc;

namespace Patina.Server.Viewport;

/// <summary>Which stacks the Worker's viewport wears, told without holding a frame for it (#295).</summary>
public sealed partial class ViewportSession {
    /// <summary>Wears sent that the Worker has not answered, while which no frame claims a stack's revision.</summary>
    int wearsUnanswered;

    /// <summary>
    ///     The worn stack's revision a frame shows: none while a wear is on its way, since the Worker answers a frame
    ///     ahead of it and has fed the viewport nothing of the stack yet. Under the lock.
    /// </summary>
    long PaintedShown => Volatile.Read(ref wearsUnanswered) > 0 ? 0 : painted;

    /// <summary>
    ///     Tells the Worker which stacks the viewport wears. ⚠ The wear waits its turn behind the stack's own
    ///     <c>stack.load</c> and <c>stack.sync</c> (#295), which on a software GPU can take minutes, so the frames go
    ///     on without it and the viewport is drawn again once it is worn.
    /// </summary>
    void TellWearing(IpcConnection connection, Wearing worn, CancellationToken cancellationToken) {
        Interlocked.Increment(ref wearsUnanswered);
        _ = WearAsync(connection, worn, cancellationToken);
    }

    async Task WearAsync(IpcConnection connection, Wearing worn, CancellationToken cancellationToken) {
        try {
            await connection.SendAsync(
                new() { Type = IpcTypes.ViewportStack, Following = worn.Stack, FollowingAlso = worn.Also },
                cancellationToken
            );
        } catch (IpcException e) {
            ServerLog.ViewportFailed(logger, e.Message);
        } catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException) {
            // The Worker went, or the Server is stopping: a new Worker is told what the viewport wears again.
        } finally {
            Interlocked.Decrement(ref wearsUnanswered);
            try {
                Redraw();
            } catch (ObjectDisposedException) {
                // Stopped while the wear was on its way.
            }
        }
    }
}
