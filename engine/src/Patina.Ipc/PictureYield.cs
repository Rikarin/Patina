namespace Patina.Ipc;

/// <summary>
///     A picture for the shelf or the Library gives way to anything else queued behind it: it stops at the next of its
///     evaluation's parts and says it was superseded, and the Server asks for it again behind that work.
/// </summary>
/// <remarks>
///     ⚠ <b>A picture is nobody's wait, and on a software GPU it is a long one.</b> On Linux CI's lavapipe a layered
///     material whose masks are scattered grunges took tens of seconds to draw at the shelf's size, the shelf asks for
///     every material at once, and a stroke, a frame or a bake's map waited behind it on the link's one queue: the
///     2D view kept its old picture past a test's fifteen seconds. A picture that has given way
///     <see cref="MostGivenWay" /> times runs to its end, so a document that never rests still lets the shelf fill.
/// </remarks>
public sealed class PictureYield {
    /// <summary>How many times one picture stops for other work before it is drawn whatever is waiting.</summary>
    public const int MostGivenWay = 3;

    readonly Lock gate = new();
    readonly Dictionary<string, int> given = new(StringComparer.Ordinal);
    CancellationTokenSource? drawing;

    /// <summary>Draws a picture that stops when other work arrives, unless it has stopped enough already.</summary>
    /// <param name="key">What the picture is of, the same each time it is asked for.</param>
    /// <param name="draw">Draws it, stopping between parts of its run when the token is cancelled.</param>
    /// <returns>Its answer, or null when it gave way.</returns>
    public T? Draw<T>(string key, Func<CancellationToken, T> draw) where T : class {
        using CancellationTokenSource stop = new();
        lock (gate) {
            drawing = given.GetValueOrDefault(key) < MostGivenWay ? stop : null;
        }

        try {
            var drawn = draw(stop.Token);
            lock (gate) {
                given.Remove(key);
            }

            return drawn;
        } catch (OperationCanceledException) when (stop.IsCancellationRequested) {
            lock (gate) {
                given[key] = given.GetValueOrDefault(key) + 1;
            }

            return null;
        } finally {
            lock (gate) {
                drawing = null;
            }
        }
    }

    /// <summary>Stops the picture being drawn, if there is one that may stop. Called as other work is queued.</summary>
    public void GiveWay() {
        lock (gate) {
            drawing?.Cancel();
        }
    }
}
