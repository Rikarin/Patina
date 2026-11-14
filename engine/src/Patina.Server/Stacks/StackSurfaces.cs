using Patina.Contracts;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     A decal's box and a path's curve between the wire and the stack (#130, #131, #133, #134). Both are numbers in
///     the document, so a change is an edit like any other: it undoes, it saves, and nothing is stamped.
/// </summary>
static class StackSurfaces {
    /// <summary>How many points a path may have: what the Worker's table holds before the curve between them.</summary>
    public const int MaximumPoints = 256;

    /// <summary>A decal as the wire sends it.</summary>
    public static StackDecal? Wire(LayerDecal? decal) =>
        decal is null
            ? null
            : new(
                decal.Centre,
                decal.Facing,
                decal.Up,
                decal.Width,
                decal.Height,
                decal.Depth,
                decal.Angle,
                decal.Fade,
                decal.Tiled,
                decal.Sampling.ToString(),
                decal.Box,
                decal.Warp
            );

    /// <summary>A path as the wire sends it.</summary>
    public static StackSurfacePath? Wire(LayerSurfacePath? path) =>
        path is null
            ? null
            : new(
                [
                    ..path.Points.Select(static point => new StackPathPoint(
                            [point.X, point.Y, point.Z],
                            [point.NormalX, point.NormalY, point.NormalZ],
                            point.Pressure
                        )
                    )
                ],
                path.Radius,
                path.Hardness,
                path.Ribbon,
                path.Repeat,
                path.Angle,
                path.Box
            );

    /// <summary>A decal as the stack keeps it.</summary>
    /// <exception cref="StackEditRefusedException">A number is out of range, or a list is the wrong length.</exception>
    public static LayerDecal Kept(StackDecal decal) {
        if (!(decal.Width > 0f && decal.Height > 0f && decal.Depth > 0f)
            || !float.IsFinite(decal.Width + decal.Height + decal.Depth)) {
            throw new StackEditRefusedException("A decal's width, height and depth are above zero.");
        }

        if (decal.Angle is < 0f or > MathF.PI || decal.Fade is < 0f or > 1f) {
            throw new StackEditRefusedException("A decal's angle is from 0 to π, and its fade from 0 to 1.");
        }

        var facing = Three(decal.Facing, "facing");
        if (!(facing[0] * facing[0] + facing[1] * facing[1] + facing[2] * facing[2] > 0f)) {
            throw new StackEditRefusedException("A decal faces some way: its facing has no length.");
        }

        var warp = decal.Warp ?? [];
        if (!IsWarpGrid(warp.Count) || !warp.All(float.IsFinite)) {
            throw new StackEditRefusedException(
                "A warp is two numbers to a point of a square grid 2 to "
                + $"{StackDecal.MaximumWarpSide} points on a side, such as eighteen for 3 by 3, or none."
            );
        }

        return new() {
            Centre = Three(decal.Centre, "centre"),
            Facing = facing,
            Up = Three(decal.Up, "up"),
            Width = decal.Width,
            Height = decal.Height,
            Depth = decal.Depth,
            Angle = decal.Angle,
            Fade = decal.Fade,
            Tiled = decal.Tiled,
            Sampling = StackEditor.Parsed(decal.Sampling, LayerSampling.Bilinear, "sampling"),
            Box = Box(decal.Box),
            Warp = [..warp]
        };
    }

    /// <summary>Whether so many numbers are none, or a square grid 2 to the finest points on a side (#263).</summary>
    static bool IsWarpGrid(int count) =>
        count == 0 || Enumerable.Range(2, StackDecal.MaximumWarpSide - 1).Any(side => side * side * 2 == count);

    /// <summary>A path as the stack keeps it.</summary>
    /// <exception cref="StackEditRefusedException">A number is out of range, or there are too many points.</exception>
    public static LayerSurfacePath Kept(StackSurfacePath path) {
        if (!(path.Radius > 0f) || !float.IsFinite(path.Radius)) {
            throw new StackEditRefusedException("A path's radius is above zero.");
        }

        if (path.Hardness is < 0f or > 1f || path.Angle is < 0f or > MathF.PI || !(path.Repeat >= 0f)) {
            throw new StackEditRefusedException(
                "A path's hardness is from 0 to 1, its angle from 0 to π, and its repeat zero or more."
            );
        }

        if (path.Points.Count > MaximumPoints) {
            throw new StackEditRefusedException($"A path has at most {MaximumPoints} points.");
        }

        return new() {
            Points = [
                ..path.Points.Select(static point => {
                        var at = Three(point.Position, "point");
                        var normal = Three(point.Normal, "normal");
                        return new LayerSurfacePoint(
                            at[0],
                            at[1],
                            at[2],
                            normal[0],
                            normal[1],
                            normal[2],
                            Math.Clamp(point.Pressure, 0f, 1f)
                        );
                    }
                )
            ],
            Radius = path.Radius,
            Hardness = path.Hardness,
            Ribbon = path.Ribbon,
            Repeat = path.Repeat,
            Angle = path.Angle,
            Box = Box(path.Box)
        };
    }

    static float[] Three(IReadOnlyList<float>? numbers, string what) =>
        numbers is { Count: 3 } && numbers.All(float.IsFinite)
            ? [..numbers]
            : throw new StackEditRefusedException($"A {what} is three numbers.");

    static float[] Box(IReadOnlyList<float>? box) {
        if (box is null || box.Count == 0) {
            return [];
        }

        if (box.Count != 6 || !box.All(float.IsFinite) || !(box[3] >= box[0] && box[4] >= box[1] && box[5] >= box[2])) {
            throw new StackEditRefusedException("A box is six numbers, its least corner then its greatest.");
        }

        return [..box];
    }
}
