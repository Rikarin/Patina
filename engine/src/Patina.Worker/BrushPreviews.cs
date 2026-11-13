using Patina.Ipc;
using System.Security.Cryptography;
using Vixen.Core.Imaging;
using Vixen.Terrain;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>
///     A picture of a brush (#123): one stroke of it, white on nothing, along a gentle wave whose pressure rises and
///     falls, so its shape, spacing, jitter, build-up and what pressure does to it all show at a glance.
/// </summary>
/// <remarks>
///     ⚠ <b>At a size of its own and not the brush's.</b> Everything else a brush has is a share of its radius or none
///     of it, so drawn at one size the stroke shows what the brush is at any; a 400-texel brush drawn as it is would be
///     a white bar.
/// </remarks>
static class BrushPreviews {
    /// <summary>The picture's width.</summary>
    public const int Width = 256;

    /// <summary>Its height.</summary>
    public const int Height = 64;

    /// <summary>The radius a stroke is drawn at, which leaves room for scatter and a stretched stamp.</summary>
    const float Radius = Height * 0.22f;

    /// <summary>How many places the pointer reports along the stroke.</summary>
    const int Steps = 96;

    /// <summary>The side of a stamp's picture, square.</summary>
    public const int StampSide = 96;

    const uint White = 0xFFFFFFFFu;

    /// <summary>brush.preview: the stroke drawn into the preview folder, answered with its picture's hash.</summary>
    /// <exception cref="IpcException">It names no brush, or an alpha that would not read.</exception>
    public static IpcMessage Answer(IpcMessage request) {
        var ask = request.BrushPreview ?? throw new IpcException("brush.preview names no brush.");
        var alpha = StrokeShapes.Alpha(ask.Brush.Alpha, ask.Folder, new(StringComparer.Ordinal));
        var brush = StrokeShapes.Brush(ask.Brush, alpha);
        var image = ask.Stamp ? DrawStamp(brush) : Draw(brush);
        var png = PngCodec.Encode(new(image.Width, image.Height, image.ToTexels()));
        var hash = Convert.ToHexStringLower(SHA256.HashData(png));
        GraphPreviewFiles.Store(ask.PreviewFolder, hash, png);
        return GraphHost.Drawn(hash);
    }

    /// <summary>The stroke, drawn the same every time for the same brush.</summary>
    /// <param name="brush">The brush, its alpha found.</param>
    public static PaintImage Draw(PaintBrush brush) {
        PaintImage image = new(Width, Height);
        PaintStroke stroke = new(
            image,
            PaintCoverage.Everywhere(Width, Height),
            brush with { Radius = Radius },
            White,
            0
        );
        var margin = Radius * 1.5f;
        for (var step = 0; step <= Steps; step++) {
            var along = step / (float)Steps;
            var x = margin + along * (Width - 2f * margin);
            var y = Height * 0.5f + MathF.Sin(along * MathF.Tau) * Height * 0.14f;

            // A pen pressed harder towards the middle, so a brush that follows pressure tapers at both ends.
            stroke.MoveTo(new(x, y), null, 0.15f + 0.85f * MathF.Sin(along * MathF.PI));
        }

        return image;
    }

    /// <summary>
    ///     One stamp of the brush, as a cursor shows it: at full pressure and flow with nothing random, so the same
    ///     brush draws the same stamp, and as large as the square holds, its long half-axis half the side.
    /// </summary>
    /// <param name="brush">The brush, its alpha found.</param>
    /// <remarks>
    ///     A stretched stamp keeps its area (Vixen's <see cref="PaintBrush.Aspect" />), so its long half-axis is the
    ///     radius times the square root of the stretch: the radius is cut to match, and a cursor is drawn at the same
    ///     share of the brush's radius.
    /// </remarks>
    public static PaintImage DrawStamp(PaintBrush brush) {
        var stretch = brush.Aspect > 1f && float.IsFinite(brush.Aspect)
            ? MathF.Min(brush.Aspect, PaintBrush.MaximumAspect)
            : 1f;
        PaintImage image = new(StampSide, StampSide);
        PaintStroke stroke = new(
            image,
            PaintCoverage.Everywhere(StampSide, StampSide),
            brush with {
                Radius = StampSide * 0.5f / MathF.Sqrt(stretch),
                Flow = 1f,
                Opacity = 1f,
                PositionJitter = 0f,
                AngleJitter = 0f,
                SizeJitter = 0f,
                FlowJitter = 0f,
                PressureSize = 0f,
                PressureFlow = 0f,
                TiltSize = 0f,
                Rotation = BrushRotation.Fixed,
                PenAngle = PaintPenAngle.None
            },
            White,
            0
        );
        stroke.MoveTo(new(StampSide * 0.5f, StampSide * 0.5f), null, 1f);
        return image;
    }
}
