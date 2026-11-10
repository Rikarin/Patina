using Vixen.Core.Mathematics;
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

    const uint White = 0xFFFFFFFFu;

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
            var x = margin + (along * (Width - (2f * margin)));
            var y = (Height * 0.5f) + (MathF.Sin(along * MathF.Tau) * Height * 0.14f);

            // A pen pressed harder towards the middle, so a brush that follows pressure tapers at both ends.
            stroke.MoveTo(new Vector2(x, y), null, 0.15f + (0.85f * MathF.Sin(along * MathF.PI)));
        }

        return image;
    }
}
