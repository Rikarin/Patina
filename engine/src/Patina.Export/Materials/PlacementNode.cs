using Patina.Contracts;

namespace Patina.Export.Materials;

/// <summary>The numbers of a <c>Space/Transform 2D</c> node: its inputs by name.</summary>
/// <param name="Rotation">Rotation, radians, clockwise on screen.</param>
/// <param name="ScaleX">Scale X.</param>
/// <param name="ScaleY">Scale Y.</param>
/// <param name="OffsetX">Offset X, in fractions of the picture.</param>
/// <param name="OffsetY">Offset Y.</param>
/// <param name="ShearX">Shear X: x sheared by y.</param>
/// <param name="ShearY">Shear Y: y sheared by x.</param>
public sealed record Transform2DValues(
    float Rotation,
    float ScaleX,
    float ScaleY,
    float OffsetX,
    float OffsetY,
    float ShearX,
    float ShearY
);

/// <summary>
///     A material's placed picture as a <c>Space/Transform 2D</c> node (#157): the picture the node draws, read at a
///     mesh's coordinate, is the source picture read where glTF's <c>KHR_texture_transform</c> says.
/// </summary>
/// <remarks>
///     <para>
///         glTF reads a picture at <c>M·uv + offset</c>, with <c>M</c> its turn times its scale. The node walks its
///         output and reads its source at <c>A⁻¹(o − ½ − t) + ½</c>, with <c>A</c> its turn times its shear times its
///         scale. So <c>A</c> is <c>M⁻¹</c> and <c>t</c> is <c>A(½ − offset) − ½</c>. <c>M⁻¹</c> with unequal scales
///         is not a turn times a scale, which is what the shear is for: the node's turn is taken as glTF's, and what
///         is left of <c>M⁻¹</c> once that turn is taken out is split into a scale and a shear.
///     </para>
///     <para>
///         ⚠ The node's turn is taken in texels, so this holds on a square picture, which is every graph an import
///         makes.
///     </para>
/// </remarks>
public static class PlacementNode {
    /// <summary>The node's numbers for a placement, or null for one no scale and shear can draw.</summary>
    /// <param name="placement">Where glTF says the picture lands.</param>
    /// <returns>The node's inputs.</returns>
    public static Transform2DValues? Of(ImportedPlacement placement) {
        ArgumentNullException.ThrowIfNull(placement);
        if (Math.Abs(placement.ScaleX) < 1e-6f || Math.Abs(placement.ScaleY) < 1e-6f) {
            return null;
        }

        // A = M⁻¹ = S⁻¹·R(θ), with R(φ) the node's turn [[cos, −sin], [sin, cos]].
        var (c, s) = (MathF.Cos(placement.Rotation), MathF.Sin(placement.Rotation));
        var (inverseX, inverseY) = (1f / placement.ScaleX, 1f / placement.ScaleY);
        var a00 = inverseX * c;
        var a01 = -inverseX * s;
        var a10 = inverseY * s;
        var a11 = inverseY * c;
        Transform2DValues? best = null;
        var margin = 0f;
        foreach (var turn in (float[])[placement.Rotation, 0f, placement.Rotation + MathF.PI / 4f]) {
            // U = R(−φ)·A is the node's shear times its scale: [[a, shearX·b], [shearY·a, b]].
            var (cos, sin) = (MathF.Cos(turn), MathF.Sin(turn));
            var u00 = cos * a00 + sin * a10;
            var u01 = cos * a01 + sin * a11;
            var u10 = -sin * a00 + cos * a10;
            var u11 = -sin * a01 + cos * a11;
            var least = Math.Min(Math.Abs(u00), Math.Abs(u11));
            if (least <= margin) {
                continue;
            }

            margin = least;
            var offsetX = a00 * (0.5f - placement.OffsetX) + a01 * (0.5f - placement.OffsetY) - 0.5f;
            var offsetY = a10 * (0.5f - placement.OffsetX) + a11 * (0.5f - placement.OffsetY) - 0.5f;
            best = new(turn, u00, u11, offsetX, offsetY, u01 / u11, u10 / u00);
        }

        return margin > 1e-4f ? best : null;
    }
}
