using Patina.Contracts;

namespace Patina.Server.Stacks;

/// <summary>
///     A stroke in the 2D view carried onto a canvas larger than its texture set, of the set's shape (#19). Its stamps
///     are the set's texels, so it is painted at the canvas's own size by multiplying everything measured in them: the
///     stamps, where it resumes, a brush sized on the map or in texels, a stencil's and a picture's place and scale, and
///     a clone's offset. The canvas keeps its detail, and the stroke lands where the brush went on the set's map, since
///     a texel's middle is at its index and a half on both.
/// </summary>
public static class StackStrokeScale {
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
            Source = stroke.Source is { } source
                ? source with {
                    Offset = Points(source.Offset, by),
                    Picture = Picture(source.Picture, by),
                    Frame = source.Frame is { } frame
                        ? frame with { Centre = Points(frame.Centre, by)!, Scale = frame.Scale * by }
                        : null
                }
                : null
        };

    /// <summary>Stamps in the set's texels, in the canvas's.</summary>
    /// <param name="stamps">The stamps.</param>
    /// <param name="by">The canvas's width over the set's.</param>
    public static IReadOnlyList<StackStamp> Stamps(IReadOnlyList<StackStamp> stamps, float by) =>
        [..stamps.Select(stamp => stamp with { X = stamp.X * by, Y = stamp.Y * by })];

    static IReadOnlyList<float>? Points(IReadOnlyList<float>? point, float by) =>
        point is null ? null : [..point.Select(value => value * by)];

    static StackPicture? Picture(StackPicture? picture, float by) =>
        picture is null ? null : picture with { Centre = Points(picture.Centre, by)!, Scale = picture.Scale * by };
}
