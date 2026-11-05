namespace Patina.Server.Viewport;

/// <summary>
///     Picks the share of the asked size to render at (plan §9). While the user's hand is on the view and a frame takes
///     longer than the budget, the scale steps down; when frames come in well under budget it steps back up. Once the
///     hand lifts, the next frame is full size, so what stays on screen is sharp.
/// </summary>
public sealed class RenderScale(double budgetMilliseconds = 1000d / 60d) {
    const double Minimum = 0.5;
    const double Step = 0.85;
    const double Smoothing = 0.2;
    const double Headroom = 0.6;
    const int SettleFrames = 12;

    double smoothed;
    int sinceChange;

    /// <summary>The current scale, in (0, 1].</summary>
    public double Value { get; private set; } = 1d;

    /// <summary>Records one frame's whole cost: rendered, read back and handed to the slowest client.</summary>
    public void Record(double milliseconds, bool interacting) {
        if (!interacting) {
            Reset();
            return;
        }

        smoothed = smoothed == 0d ? milliseconds : smoothed + Smoothing * (milliseconds - smoothed);
        if (++sinceChange < SettleFrames) {
            return;
        }

        if (smoothed > budgetMilliseconds && Value > Minimum) {
            Value = Math.Max(Minimum, Value * Step);
            Changed();
        } else if (smoothed < budgetMilliseconds * Headroom && Value < 1d) {
            Value = Math.Min(1d, Value / Step);
            Changed();
        }
    }

    public void Reset() {
        Value = 1d;
        Changed();
    }

    void Changed() {
        smoothed = 0d;
        sinceChange = 0;
    }
}
