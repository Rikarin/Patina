using Patina.Contracts;
using Patina.Library;
using System.Globalization;
using Vixen.Texturing.Painting;

namespace Patina.Server.Stacks;

/// <summary>
///     The size each canvas of a session has as a Worker holds it, so that a stroke a Worker would refuse for its
///     canvas's size is refused before it begins, and never stands to fail the stack until it is undone (#241). It
///     follows the Worker's rule (<c>StackHost.Resized</c>): the first stroke of a session on a canvas smaller than its
///     set enlarges it, a canvas is never made smaller, and one a stroke of the session began on is never enlarged,
///     since that stroke's undo holds the pixels it painted over. A copy (a duplicate or a paste) is made at the set's
///     size whatever the size of the canvas it copies, which a Worker resamples (#106), and no stroke began on it, so a
///     set made larger enlarges it. A stroke that carries the last one on is checked as any other. Not thread safe:
///     the session locks.
/// </summary>
/// <param name="folder">The stack's folder, which its canvases are beside.</param>
public sealed class StackCanvasSides(string folder) {
    readonly Dictionary<string, (int Width, int Height)> sides = new(StringComparer.Ordinal);
    readonly HashSet<string> stroked = new(StringComparer.Ordinal);

    /// <summary>How wide each channel of each canvas is as a Worker holds it, where it is known.</summary>
    readonly Dictionary<(string Paint, string Usage), PaintTexelFormat> formats = [];

    /// <summary>
    ///     How wide the texels are that a stroke paints a canvas's channel at (#19): what the set asks, unless the
    ///     channel holds more, which keeps its width so that no paint is rounded, or a stroke of this session began on
    ///     the canvas, whose undo holds the pixels it painted over, which keeps the width it has until it is opened
    ///     again. A Worker widens a channel to it, exactly. Call after <see cref="Check" />, which reads the canvas.
    /// </summary>
    /// <param name="paint">The canvas, beside the stack.</param>
    /// <param name="usage">The channel.</param>
    /// <param name="asked">The width the set asks.</param>
    public PaintTexelFormat Format(string paint, string usage, PaintTexelFormat asked) {
        var held = formats.GetValueOrDefault((paint, usage));
        return PaintTexelFormats.Holds(held, asked) || stroked.Contains(paint) ? held : asked;
    }

    /// <summary>
    ///     The size a stroke paints a canvas at, which is the set's, or refuses a stroke a Worker could not begin. A
    ///     canvas larger than its set is painted at its own size (#19), so the detail it holds stays: a stroke on the
    ///     model is placed by the picture and not by texels, and one in the 2D view is carried onto the canvas's texels
    ///     (<see cref="StackStrokeScale" />). One of another shape, larger on either side, is painted at the smallest
    ///     size of the set's shape that holds it (<see cref="Holding" />), to which the Worker enlarges it, each texel
    ///     repeated, so a stroke is carried onto it by one factor and lands as on a canvas of the set's shape.
    /// </summary>
    /// <param name="layer">The layer's name, for the reason.</param>
    /// <param name="paint">The canvas, beside the stack.</param>
    /// <param name="width">The set's width.</param>
    /// <param name="height">The set's height.</param>
    /// <returns>The size to paint at.</returns>
    /// <exception cref="StackEditRefusedException">
    ///     The canvas cannot be painted at that size in this session.
    /// </exception>
    public (int Width, int Height) Check(string layer, string paint, int width, int height) {
        if (Side(paint) is not { } held || held == (width, height)) {
            return (width, height);
        }

        var target = Holding(held, width, height);
        if (target == held) {
            return held;
        }

        var values = new Dictionary<string, string> {
            ["layer"] = layer,
            ["held"] = Size(held.Width, held.Height),
            ["size"] = Size(width, height),
            ["target"] = Size(target.Width, target.Height)
        };
        if (Math.Max(target.Width, target.Height) > TextureSizes.Largest) {
            throw new StackEditRefusedException(
                $"'{layer}' holds {values["held"]} texels of another shape than the texture set's {values["size"]}, and "
                + $"painting it at the set's shape would make it {values["target"]}, past the largest side. Set the size "
                + "back to that shape to paint it.",
                Api.Refusals.CanvasLarger,
                values
            );
        }

        if (stroked.Contains(paint)) {
            throw new StackEditRefusedException(
                $"'{layer}' was painted at {values["held"]} earlier in this session. Save, close and open the document "
                + $"again to paint it at {values["target"]}.",
                Api.Refusals.CanvasPaintedAtSize,
                values
            );
        }

        return target;
    }

    /// <summary>
    ///     The smallest size of the set's shape that holds a canvas on both sides, and never smaller than the set: the
    ///     set's size times the larger of the canvas's two sides over the set's. The canvas's own size when it is of
    ///     the set's shape and larger.
    /// </summary>
    /// <param name="held">The canvas's size.</param>
    /// <param name="width">The set's width.</param>
    /// <param name="height">The set's height.</param>
    public static (int Width, int Height) Holding((int Width, int Height) held, int width, int height) {
        // The larger of the two ratios canvas over set, as a fraction of whole numbers so that powers of two are exact.
        var (over, under) = (long)held.Width * height >= (long)held.Height * width
            ? ((long)held.Width, (long)width)
            : (held.Height, height);
        return over <= under
            ? (width, height)
            : ((int)Math.Min(int.MaxValue, Ceiling(width * over, under)),
                (int)Math.Min(int.MaxValue, Ceiling(height * over, under)));
    }

    /// <summary>
    ///     A stroke as it paints a canvas <paramref name="width" /> wide (#19), and what the stamps of the rest of its
    ///     drag are multiplied by, which only a stroke in the 2D view needs (<see cref="StackStrokeScale" />).
    /// </summary>
    /// <param name="stroke">The stroke as sent.</param>
    /// <param name="width">The width it paints at, from <see cref="Check" />.</param>
    /// <param name="set">The set's width.</param>
    public static (StackStroke Stroke, float? Stamps) Carried(StackStroke stroke, int width, int set) {
        var by = StackStrokeScale.By(width, set);
        return (StackStrokeScale.At(stroke, by), StackStrokeScale.OfStamps(stroke, by));
    }

    /// <summary>Takes in what the entries of a step that now stands did to their canvases.</summary>
    /// <param name="entries">The step's strokes, copies and captures.</param>
    /// <param name="width">The set's width, which a copy or a capture is made at.</param>
    /// <param name="height">The set's height.</param>
    public void Stood(IEnumerable<StackJournalEntry> entries, int width, int height) {
        foreach (var entry in entries) {
            if (entry.From is not null || entry.Capture is not null) {
                // Made at the size the journal holds for it, or the set's: a paste or a duplicate of paint at another
                // size is resampled to it (#106).
                sides.TryAdd(entry.Paint, entry.Width > 0 ? (entry.Width, entry.Height) : (width, height));
            } else if (entry.Width > 0) {
                sides[entry.Paint] = (entry.Width, entry.Height);
                stroked.Add(entry.Paint);
                formats[(entry.Paint, entry.Usage)] = Enum.TryParse<PaintTexelFormat>(entry.Format, out var format)
                    ? format
                    : PaintTexelFormat.Rgba8;
            } else {
                sides.TryAdd(entry.Paint, (width, height));
            }
        }
    }

    /// <summary>
    ///     The canvas's size this session, read once from its file. Null for one that is not there yet, which is made
    ///     at the stroke's size, or one that does not read, which the Worker says why of.
    /// </summary>
    (int Width, int Height)? Side(string paint) {
        if (sides.TryGetValue(paint, out var held)) {
            return held;
        }

        var file = LibraryLayout.Beside(folder, paint);
        if (!File.Exists(file)) {
            return null;
        }

        try {
            using var stream = SharedFile.OpenRead(file);
            var canvas = PaintCanvas.Read(stream);
            foreach (var usage in canvas.Channels) {
                formats[(paint, usage)] = canvas.Channel(usage).Format;
            }

            return sides[paint] = (canvas.Width, canvas.Height);
        } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) {
            return null;
        }
    }

    static long Ceiling(long value, long by) => (value + by - 1) / by;

    static string Size(int width, int height) => string.Create(CultureInfo.InvariantCulture, $"{width} × {height}");
}
