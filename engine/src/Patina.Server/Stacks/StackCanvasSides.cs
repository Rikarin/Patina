using Patina.Contracts;
using Patina.Library;
using System.Globalization;
using Vixen.Texturing.Painting;

namespace Patina.Server.Stacks;

/// <summary>
///     The size each canvas of a session has as a Worker holds it, so that a stroke a Worker would refuse for its canvas's
///     size is refused before it begins, and never stands to fail the stack until it is undone (#241). It follows the
///     Worker's rule (<c>StackHost.Resized</c>): the first stroke of a session on a canvas smaller than its set enlarges
///     it, a canvas is never made smaller, and one a stroke of the session began on is never enlarged, since that
///     stroke's undo holds the pixels it painted over. Not thread safe: the session locks.
/// </summary>
/// <param name="folder">The stack's folder, which its canvases are beside.</param>
public sealed class StackCanvasSides(string folder) {
    readonly Dictionary<string, (int Width, int Height)> sides = new(StringComparer.Ordinal);
    readonly HashSet<string> stroked = new(StringComparer.Ordinal);

    /// <summary>Refuses a stroke on a canvas, at the set's size, that a Worker could not begin.</summary>
    /// <param name="layer">The layer's name, for the reason.</param>
    /// <param name="paint">The canvas, beside the stack.</param>
    /// <param name="width">The set's width, which the stroke paints at.</param>
    /// <param name="height">The set's height.</param>
    /// <exception cref="StackEditRefusedException">The canvas cannot be painted at that size in this session.</exception>
    public void Check(string layer, string paint, int width, int height) {
        if (Side(paint) is not { } held || held == (width, height)) {
            return;
        }

        var values = new Dictionary<string, string> {
            ["layer"] = layer,
            ["held"] = Size(held.Width, held.Height),
            ["size"] = Size(width, height)
        };
        if (held.Width > width || held.Height > height) {
            throw new StackEditRefusedException(
                $"'{layer}' holds {values["held"]} texels and the texture set is {values["size"]}. Painting it smaller "
                + "would throw its detail away, so set the size back to paint this layer.",
                Api.Refusals.CanvasLarger,
                values
            );
        }

        if (stroked.Contains(paint)) {
            throw new StackEditRefusedException(
                $"'{layer}' was painted at {values["held"]} earlier in this session. Save, close and open the document "
                + $"again to paint it at {values["size"]}.",
                Api.Refusals.CanvasPaintedAtSize,
                values
            );
        }
    }

    /// <summary>Takes in what the entries of a step that now stands did to their canvases.</summary>
    /// <param name="entries">The step's strokes, copies and captures.</param>
    /// <param name="width">The set's width, which a copy or a capture is made at.</param>
    /// <param name="height">The set's height.</param>
    public void Stood(IEnumerable<StackJournalEntry> entries, int width, int height) {
        foreach (var entry in entries) {
            if (entry.Width > 0) {
                sides[entry.Paint] = (entry.Width, entry.Height);
                stroked.Add(entry.Paint);
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
            return sides[paint] = (canvas.Width, canvas.Height);
        } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) {
            return null;
        }
    }

    static string Size(int width, int height) =>
        string.Create(CultureInfo.InvariantCulture, $"{width} × {height}");
}
