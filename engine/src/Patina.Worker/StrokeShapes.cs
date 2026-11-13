using Patina.Contracts;
using Patina.Ipc;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
using Vixen.Terrain;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>
///     What a stroke's wire settings are to Vixen: the brush, where its stamps land, the copies a symmetry makes and
///     the radius in the stroke's own units. Nothing here holds state, so a replay makes the same stroke.
/// </summary>
static class StrokeShapes {
    public static IPaintPlacement? Placement(StackPaintPart part, PaintMeshSurface? surface, BoundingBox? bounds) {
        var symmetry = bounds is { } box ? Symmetry(part.Symmetry, box) : null;
        if (part.View is not { } view) {
            return surface?.AcrossSeams(symmetry);
        }

        return (surface ?? throw new IpcException("A stroke with a view needs a mesh.")).Through(
            View(view),
            symmetry,
            Occlusion(part.Brush)
        );
    }

    /// <summary>
    ///     Whether a stroke paints only what its camera sees, its tolerance a share of the model's size, and how far a
    ///     face may turn from the camera, which holds either way.
    /// </summary>
    public static PaintOcclusion Occlusion(StackBrush brush) =>
        brush.Occlusion
            ? new(true, brush.OcclusionTolerance / 1000f, brush.Facing)
            : PaintOcclusion.None with { Facing = brush.Facing };

    /// <summary>Where the brush already stands for a free stroke that carries the last one on, or null.</summary>
    public static Vector2? Resume(StackPaintPart part) =>
        part.Curve == "free" && part.Resume is [var x, var y] ? new Vector2(x, y) : null;

    public static PaintView View(StackView view) =>
        new(
            Point(view.Eye),
            Point(view.Right),
            Point(view.Up),
            Point(view.Forward),
            view.VerticalFieldOfView,
            view.Width,
            view.Height
        );

    /// <summary>
    ///     The copies a symmetry makes, through the model's centre or the mesh's origin, with a mirror's plane
    ///     moved along its axis by a share of the bounding radius.
    /// </summary>
    public static PaintSymmetryGroup? Symmetry(StackSymmetry? symmetry, BoundingBox bounds) {
        if (symmetry is null) {
            return null;
        }

        var axis = symmetry.Axis switch {
            "y" => Vector3.UnitY,
            "z" => Vector3.UnitZ,
            _ => Vector3.UnitX
        };
        var centre = symmetry.Centre == "origin" ? Vector3.Zero : bounds.Center;
        var through = centre + axis * (symmetry.Offset * Vector3.Distance(bounds.Minimum, bounds.Maximum) * 0.5f);
        return symmetry.Kind == "radial"
            ? PaintSymmetryGroup.Radial(axis, through, symmetry.Count)
            : PaintSymmetryGroup.Mirror(PaintSymmetry.Across(axis, through));
    }

    /// <summary>
    ///     The radius in the stroke's own units: the picture's for a stroke with a view, texels for one without.
    ///     A size in texels or in the model's units is measured where the stroke starts, and the stroke keeps it.
    /// </summary>
    public static float Radius(StackPaintPart part, PaintMeshSurface? surface, BoundingBox? model) {
        var brush = part.Brush;
        if (brush.SizeSpace == "screen" || surface is null || model is not { } bounds || part.Stamps.Count == 0) {
            return brush.Radius;
        }

        var space = brush.SizeSpace == "model" ? PaintSizeSpace.Model : PaintSizeSpace.Texels;

        // A model size is thousandths of the bounds' diagonal, so it means the same on a model in metres or in
        // centimetres.
        var radius = space == PaintSizeSpace.Model
            ? brush.Radius / 1000f * Vector3.Distance(bounds.Minimum, bounds.Maximum)
            : brush.Radius;
        Vector2 start = new(part.Stamps[0].X, part.Stamps[0].Y);
        var measured = part.View is { } view
            ? surface.PictureRadius(View(view), start, radius, space)
            : surface.AtlasRadius(start, radius, space);
        return float.IsFinite(measured) && measured > 0f ? measured : brush.Radius;
    }

    static Vector3 Point(IReadOnlyList<float> numbers) => new(numbers[0], numbers[1], numbers[2]);

    /// <summary>
    ///     A named alpha, or a picture beside the stack read through its red channel, which <paramref name="held" />
    ///     keeps by its file so a stroke reads it once. Null is round.
    /// </summary>
    /// <param name="name">The alpha a brush names.</param>
    /// <param name="folder">The stack's folder, which a picture is relative to.</param>
    /// <param name="held">The pictures read so far.</param>
    /// <exception cref="IpcException">The picture would not read.</exception>
    public static IBrushMask? Alpha(string? name, string folder, Dictionary<string, IBrushMask> held) {
        if (name is null || name == PaintAlphas.Round) {
            return null;
        }

        if (PaintAlphas.Find(name) is { } shelved) {
            return shelved;
        }

        var file = Path.GetFullPath(name, folder);
        if (held.TryGetValue(file, out var known)) {
            return known;
        }

        try {
            var picture = PngCodec.Decode(SharedFile.ReadAllBytes(file));
            PaintImage image = new(picture.Width, picture.Height);
            for (var at = 0; at < picture.Width * picture.Height; at++) {
                image[at] = PaintImage.Pack(picture.Pixels[at * 4] / 255f, 0f, 0f, 1f);
            }

            return held[file] = new PaintImageMask(image, 0);
        } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) {
            throw new IpcException($"'{name}' would not read as an alpha: {e.Message}");
        }
    }

    /// <summary>How the pen stood at a stamp, as Vixen reads it.</summary>
    public static PaintPen Pen(StackStamp stamp) =>
        new(stamp.Pressure, stamp.Tilt ?? 0f, stamp.Azimuth ?? 0f, stamp.Twist ?? 0f);

    /// <summary>A stroke's brush as Vixen stamps with it, the alpha already found.</summary>
    public static PaintBrush Brush(StackBrush brush, IBrushMask? alpha) =>
        PaintBrush.Default with {
            Radius = brush.Radius,
            Flow = brush.Flow,
            Opacity = brush.Opacity,
            Spacing = brush.Spacing,
            Falloff = brush.Falloff,
            Alpha = alpha,
            Angle = brush.Angle,
            Rotation = brush.Rotation switch {
                "along" => BrushRotation.AlongStroke,
                "random" => BrushRotation.Random,
                // Fixed, and pen and tilt too: the pen turns the stamp from its fixed angle.
                _ => BrushRotation.Fixed
            },
            PositionJitter = brush.PositionJitter,
            AngleJitter = brush.AngleJitter,
            SizeJitter = brush.SizeJitter,
            PressureSize = brush.PressureSize,
            PressureFlow = brush.PressureFlow,
            TiltSize = brush.TiltSize,
            PenAngle = brush.Rotation switch {
                "pen" => PaintPenAngle.Twist,
                "tilt" => PaintPenAngle.Tilt,
                _ => PaintPenAngle.None
            },
            Curve = brush.Curve switch {
                "linear" => BrushFalloffKind.Linear,
                "spherical" => BrushFalloffKind.Spherical,
                "tip" => BrushFalloffKind.Tip,
                _ => BrushFalloffKind.Smooth
            },
            Aspect = brush.Aspect,
            AspectAngle = brush.AspectAngle,
            FlowJitter = brush.FlowJitter,
            ScatterAxis = brush.ScatterAxis == "across" ? PaintScatterAxis.Across : PaintScatterAxis.Both,
            Blend = brush.Accumulate ? PaintStampBlend.Accumulate : PaintStampBlend.Maximum
        };
}
