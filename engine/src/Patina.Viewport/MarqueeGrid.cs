// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;

namespace Patina.Viewport;

/// <summary>
///     Where a marquee on the picture looks for meshes (#110): a grid of points across the rectangle, a ray through
///     each, so a marquee reaches what shows inside it as a click does. ⚠ A mesh smaller than the spacing can fall
///     between two points, so the spacing is a few pixels, and a wide marquee has at most <see cref="MostAcross" />
///     points a side.
/// </summary>
public static class MarqueeGrid {
    /// <summary>The widest gap between two points, in picture pixels.</summary>
    public const float Spacing = 3f;

    /// <summary>The most points along one side.</summary>
    public const int MostAcross = 160;

    /// <summary>
    ///     The points of the grid, in picture pixels from the top left, for a rectangle given as two opposite
    ///     corners in fractions of the picture, in either order. What lies outside the picture is left out.
    /// </summary>
    /// <param name="corner">One corner, 0 to 1 each way.</param>
    /// <param name="opposite">The other.</param>
    /// <param name="width">The picture's width.</param>
    /// <param name="height">Its height.</param>
    public static IEnumerable<Vector2> Points(Vector2 corner, Vector2 opposite, int width, int height) {
        var left = Math.Clamp(Math.Min(corner.X, opposite.X), 0f, 1f) * width;
        var right = Math.Clamp(Math.Max(corner.X, opposite.X), 0f, 1f) * width;
        var top = Math.Clamp(Math.Min(corner.Y, opposite.Y), 0f, 1f) * height;
        var bottom = Math.Clamp(Math.Max(corner.Y, opposite.Y), 0f, 1f) * height;
        var across = Steps(right - left);
        var down = Steps(bottom - top);
        for (var row = 0; row < down; row++) {
            var y = top + (bottom - top) * (row + 0.5f) / down;
            for (var column = 0; column < across; column++) {
                yield return new(left + (right - left) * (column + 0.5f) / across, y);
            }
        }
    }

    static int Steps(float length) => Math.Clamp((int)MathF.Ceiling(length / Spacing), 1, MostAcross);
}
