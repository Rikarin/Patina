using Patina.Contracts;

namespace Patina.Server.Stacks;

/// <summary>
///     A stroke carried onto a canvas larger than its texture set, of the set's shape (#19). A stroke in the 2D view
///     has stamps in the set's texels, so it is painted at the canvas's own size by multiplying everything measured in
///     them: the stamps, where it resumes, a brush sized on the map or in texels, a stencil's and a picture's place and
///     scale, and a clone's offset. The canvas keeps its detail, and the stroke lands where the brush went on the set's
///     map, since a texel's middle is at its index and a half on both. A stroke on the model is placed by its picture,
///     and only a brush sized in texels changes.
/// </summary>
public static class StackStrokeScale {
    /// <summary>The canvas's width over the set's, or null when the canvas is the set's size.</summary>
    /// <param name="canvas">The width the stroke paints at.</param>
    /// <param name="set">The set's width.</param>
    public static float? By(int canvas, int set) => canvas == set ? null : (float)canvas / set;

    /// <summary>The stroke as it paints a canvas <paramref name="by" /> times the set's width.</summary>
    /// <param name="stroke">The stroke as sent.</param>
    /// <param name="by">The canvas's width over the set's, or null for the set's own size.</param>
    public static StackStroke At(StackStroke stroke, float? by) {
        if (by is not { } scale) {
            return stroke;
        }

        if (stroke.View is null) {
            return Scaled(stroke, scale);
        }

        return stroke.Brush.SizeSpace == "texture"
            ? stroke with { Brush = stroke.Brush with { Radius = stroke.Brush.Radius * scale } }
            : stroke;
    }

    /// <summary>What the later parts of a drag's stamps are multiplied by, which only the 2D view's need.</summary>
    /// <param name="stroke">The stroke's first part.</param>
    /// <param name="by">The canvas's width over the set's, or null.</param>
    public static float? OfStamps(StackStroke stroke, float? by) => stroke.View is null ? by : null;

    /// <summary>The stroke with what it measures in the set's texels multiplied by <paramref name="by" />.</summary>
    /// <param name="stroke">A stroke with no view.</param>
    /// <param name="by">The canvas's width over the set's.</param>
    public static StackStroke Scaled(StackStroke stroke, float by) =>
        stroke with {
            Brush = stroke.Brush.SizeSpace == "model"
                ? stroke.Brush
                : stroke.Brush with { Radius = stroke.Brush.Radius * by },
            Stamps = Stamps(stroke.Stamps, by),
            Resume = Points(stroke.Resume, by),
            Stencil = Picture(stroke.Stencil, by),
            Source = Source(stroke.Source, by)
        };

    /// <summary>Stamps in the set's texels, in the canvas's.</summary>
    /// <param name="stamps">The stamps.</param>
    /// <param name="by">The canvas's width over the set's.</param>
    public static IReadOnlyList<StackStamp> Stamps(IReadOnlyList<StackStamp> stamps, float by) =>
        [..stamps.Select(stamp => stamp with { X = stamp.X * by, Y = stamp.Y * by })];

    static StackStrokeSource? Source(StackStrokeSource? source, float by) =>
        source is null
            ? null
            : source with {
                Offset = Points(source.Offset, by),
                Picture = Picture(source.Picture, by),
                Frame = Frame(source.Frame, by)
            };

    static StackFrame? Frame(StackFrame? frame, float by) =>
        frame is null ? null : frame with { Centre = Points(frame.Centre, by)!, Scale = frame.Scale * by };

    static IReadOnlyList<float>? Points(IReadOnlyList<float>? point, float by) =>
        point is null ? null : [..point.Select(value => value * by)];

    static StackPicture? Picture(StackPicture? picture, float by) =>
        picture is null ? null : picture with { Centre = Points(picture.Centre, by)!, Scale = picture.Scale * by };
}
