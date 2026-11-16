namespace Patina.Ipc;

/// <summary>
///     A picture for the shelf or the Library gives way to anything else queued behind it: it stops at the next of its
///     evaluation's parts and says it was superseded, and the Server asks for it again behind that work.
/// </summary>
/// <remarks>
///     ⚠ <b>A picture is nobody's wait, and on a software GPU it is a long one.</b> On Linux CI's lavapipe a layered
///     material whose masks are scattered grunges took tens of seconds to draw at the shelf's size, the shelf asks for
///     every material at once, and a stroke, a frame or a bake's map waited behind it on the link's one queue: the
///     2D view kept its old picture past a test's fifteen seconds.
///     ⚠ <b>It gives way every time, with no count after which it holds on.</b> A first version drew a picture whole
///     once it had stopped three times, and on SwiftShader that one picture then held up the evaluation after a
///     bake for longer than a test waits. Nothing the Server sends the Worker comes by itself: frames, strokes and
///     saves come from a person's work, so the pictures finish once that rests.
/// </remarks>
public sealed class PictureYield {
    readonly Lock gate = new();
    CancellationTokenSource? drawing;

    /// <summary>Draws a picture that stops when other work arrives.</summary>
    /// <param name="draw">Draws it, stopping between parts of its run when the token is cancelled.</param>
    /// <returns>Its answer, or null when it gave way.</returns>
    public T? Draw<T>(Func<CancellationToken, T> draw) where T : class {
        ArgumentNullException.ThrowIfNull(draw);
        using CancellationTokenSource stop = new();
        lock (gate) {
            drawing = stop;
        }

        try {
            return draw(stop.Token);
        } catch (OperationCanceledException) when (stop.IsCancellationRequested) {
            return null;
        } finally {
            lock (gate) {
                drawing = null;
            }
        }
    }

    /// <summary>Stops the picture being drawn, if there is one. Called as other work is queued.</summary>
    public void GiveWay() {
        lock (gate) {
            drawing?.Cancel();
        }
    }
}
