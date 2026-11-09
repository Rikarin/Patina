using Patina.Contracts;
using Vixen.Core.Mathematics;

namespace Patina.Export;

/// <summary>
///     Measures a texture set's UV layout before it is exported (#159): its area on the model and in the map, which is
///     its texel density, and the triangles a brush or a bake cannot use as they are. Overlap is found by drawing every
///     triangle into a grid of texels and counting the texels drawn twice; a texel centre on an edge two triangles
///     share belongs to neither, so neighbours never count as an overlap.
/// </summary>
public static class LayoutCheck {
    /// <summary>The side of the grid overlap is counted on, in texels, at most.</summary>
    public const int Grid = 1024;

    /// <summary>Less area than this in the layout, in tiles, is none: about a millionth of a texel at 8K.</summary>
    const double NoArea = 1e-14;

    /// <summary>How close a jump goes, in framings: a tenth of the model either side of the place.</summary>
    const double Close = 0.3;

    /// <summary>What a layout measures, and which texels it covers on a grid of <paramref name="Side" />.</summary>
    /// <param name="Measures">The measures.</param>
    /// <param name="Covered">Each texel of the grid, row by row from the top: whether a triangle covers it.</param>
    /// <param name="Side">The grid's side.</param>
    public sealed record Result(LayoutMeasures Measures, bool[] Covered, int Side);

    /// <summary>Measures one set's triangles.</summary>
    /// <param name="positions">The corners on the model, in the space the viewport draws it.</param>
    /// <param name="coordinates">Their texture coordinates, v down the map.</param>
    /// <param name="indices">Three corners a triangle.</param>
    /// <param name="model">The whole model's bounds, which a camera's pan is measured in.</param>
    /// <param name="side">
    ///     The side of the set's maps, in texels; the grid is no larger than <see cref="Grid" />.
    /// </param>
    public static Result Measure(
        ReadOnlySpan<Vector3> positions,
        ReadOnlySpan<Vector2> coordinates,
        ReadOnlySpan<int> indices,
        BoundingBox model,
        int side
    ) {
        var tally = new Tally(Math.Clamp(side, 16, Grid), indices.Length / 3, model);
        for (var triangle = 0; triangle < tally.Triangles; triangle++) {
            tally.Add(triangle, Corners(positions, indices, triangle), Corners(coordinates, indices, triangle));
        }

        // Turned over is turned against the most of the set: a layout drawn the other way round is still one way.
        foreach (var triangle in tally.TurnedOver()) {
            tally.Spot(
                CheckCodes.UvFlipped,
                triangle,
                Corners(positions, indices, triangle),
                Corners(coordinates, indices, triangle)
            );
        }

        return tally.Result();
    }

    /// <summary>Texels per unit of the model: the side times the square root of map area over surface area.</summary>
    public static double Density(LayoutMeasures measures, int side) {
        ArgumentNullException.ThrowIfNull(measures);
        return measures.SurfaceArea > 0 ? side * Math.Sqrt(measures.MapArea / measures.SurfaceArea) : 0;
    }

    /// <summary>
    ///     A camera that looks straight at a point on the model along the way its triangle faces, closer than the
    ///     framing, with its pan in the whole model's bounding radii as <see cref="ViewportCameraState" /> has it.
    /// </summary>
    public static ViewportCameraState Looking(Vector3 point, Vector3 facing, BoundingBox model) {
        var radius = Math.Max(model.Extent.Length(), 1e-4f);
        var pan = (point - model.Center) / radius;
        var normal = facing.LengthSquared() > 0 ? Vector3.Normalize(facing) : new(0f, 0f, 1f);
        var pitch = Math.Asin(Math.Clamp(normal.Y, -1f, 1f));
        var yaw = Math.Atan2(normal.X, normal.Z);
        return new(yaw, pitch, Close, pan.X, pan.Y, pan.Z);
    }

    /// <summary>
    ///     A place on the map put on the model: the first triangle whose layout holds it, with a camera that looks at
    ///     the point it lands on. A place no triangle holds is given back as it came.
    /// </summary>
    public static CheckSpot Located(
        CheckSpot spot,
        ReadOnlySpan<Vector3> positions,
        ReadOnlySpan<Vector2> coordinates,
        ReadOnlySpan<int> indices,
        BoundingBox model
    ) {
        ArgumentNullException.ThrowIfNull(spot);
        var place = new Vector2(spot.U, spot.V);
        for (var triangle = 0; triangle < indices.Length / 3; triangle++) {
            var (ta, tb, tc) = Corners(coordinates, indices, triangle);
            var whole = Signed(ta, tb, tc);
            if (Math.Abs(whole) < NoArea) {
                continue;
            }

            var u = Signed(place, tb, tc) / whole;
            var v = Signed(ta, place, tc) / whole;
            var w = 1 - u - v;
            if (u < 0 || v < 0 || w < 0) {
                continue;
            }

            var (pa, pb, pc) = Corners(positions, indices, triangle);
            var point = pa * (float)u + pb * (float)v + pc * (float)w;
            return spot with { Triangle = triangle, Camera = Looking(point, Vector3.Cross(pb - pa, pc - pa), model) };
        }

        return spot;
    }

    static (T A, T B, T C) Corners<T>(ReadOnlySpan<T> values, ReadOnlySpan<int> indices, int triangle) =>
        (values[indices[triangle * 3]], values[indices[triangle * 3 + 1]], values[indices[triangle * 3 + 2]]);

    static bool Inside(Vector2 coordinate) => coordinate.X is >= 0f and <= 1f && coordinate.Y is >= 0f and <= 1f;

    static double Signed(Vector2 a, Vector2 b, Vector2 c) =>
        0.5 * ((double)(b.X - a.X) * (c.Y - a.Y) - (double)(c.X - a.X) * (b.Y - a.Y));

    /// <summary>
    ///     Counts the texel centres strictly inside a triangle. A centre on an edge, within a hair, is left to neither
    ///     side, so two triangles that share an edge never meet in a texel.
    /// </summary>
    /// <returns>Whether it covered a texel another triangle already had.</returns>
    static bool Draw(byte[] counts, int grid, Vector2 a, Vector2 b, Vector2 c) {
        var (ax, ay, bx, by, cx, cy) = (a.X * grid, a.Y * grid, b.X * grid, b.Y * grid, c.X * grid, c.Y * grid);
        var left = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
        var right = Math.Min(grid - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
        var top = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
        var bottom = Math.Min(grid - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
        var area = (double)(bx - ax) * (cy - ay) - (double)(cx - ax) * (by - ay);
        var hair = Math.Abs(area) * 1e-6;
        var overlapped = false;
        for (var y = top; y <= bottom; y++) {
            for (var x = left; x <= right; x++) {
                var (px, py) = (x + 0.5, y + 0.5);
                var w0 = Edge(bx, by, cx, cy, px, py) * Math.Sign(area);
                var w1 = Edge(cx, cy, ax, ay, px, py) * Math.Sign(area);
                var w2 = Edge(ax, ay, bx, by, px, py) * Math.Sign(area);
                if (w0 <= hair || w1 <= hair || w2 <= hair) {
                    continue;
                }

                ref var count = ref counts[y * grid + x];
                overlapped |= count > 0;
                count = (byte)Math.Min(count + 1, 2);
            }
        }

        return overlapped;
    }

    static double Edge(double x0, double y0, double x1, double y1, double px, double py) =>
        (x1 - x0) * (py - y0) - (y1 - y0) * (px - x0);

    /// <summary>What one set's triangles come to, a triangle at a time.</summary>
    sealed class Tally(int grid, int triangles, BoundingBox model) {
        readonly byte[] counts = new byte[grid * grid];
        readonly sbyte[] signs = new sbyte[triangles];
        readonly Dictionary<string, CheckSpot> spots = new(StringComparer.Ordinal);
        double surface;
        double map;
        int outside;
        int degenerate;
        int forward;
        int backward;
        int flipped;

        public int Triangles => triangles;

        /// <summary>
        ///     Counts a triangle's areas, and draws it into the grid where it is in the tile with area there.
        /// </summary>
        public void Add(int triangle, (Vector3 A, Vector3 B, Vector3 C) at, (Vector2 A, Vector2 B, Vector2 C) laid) {
            var area = Vector3.Cross(at.B - at.A, at.C - at.A).Length() * 0.5;
            var signed = Signed(laid.A, laid.B, laid.C);
            surface += area;
            map += Math.Abs(signed);
            if (!Inside(laid.A) || !Inside(laid.B) || !Inside(laid.C)) {
                outside++;
                Spot(CheckCodes.UvOutside, triangle, at, laid);
            } else if (Math.Abs(signed) < NoArea) {
                Flat(triangle, area, at, laid);
            } else {
                signs[triangle] = (sbyte)Math.Sign(signed);
                forward += signed > 0 ? 1 : 0;
                backward += signed > 0 ? 0 : 1;
                if (Draw(counts, grid, laid.A, laid.B, laid.C)) {
                    Spot(CheckCodes.UvOverlap, triangle, at, laid);
                }
            }
        }

        /// <summary>
        ///     A triangle with no area in the layout, which is a problem only if it has some on the model.
        /// </summary>
        void Flat(
            int triangle,
            double area,
            (Vector3 A, Vector3 B, Vector3 C) at,
            (Vector2 A, Vector2 B, Vector2 C) laid
        ) {
            if (area > 0) {
                degenerate++;
                Spot(CheckCodes.UvDegenerate, triangle, at, laid);
            }
        }

        /// <summary>
        ///     The triangles laid out the other way round from most of the set, counted as they are given.
        /// </summary>
        public IEnumerable<int> TurnedOver() {
            var against = forward >= backward ? (sbyte)-1 : (sbyte)1;
            for (var triangle = 0; triangle < triangles; triangle++) {
                if (signs[triangle] == against) {
                    flipped++;
                    yield return triangle;
                }
            }
        }

        /// <summary>Keeps the first example of a kind of problem, with a camera that looks at it.</summary>
        public void Spot(
            string code,
            int triangle,
            (Vector3 A, Vector3 B, Vector3 C) at,
            (Vector2 A, Vector2 B, Vector2 C) laid
        ) {
            if (spots.ContainsKey(code)) {
                return;
            }

            var middle = (laid.A + laid.B + laid.C) / 3f;
            var camera = Looking((at.A + at.B + at.C) / 3f, Vector3.Cross(at.B - at.A, at.C - at.A), model);
            spots[code] = new(code, triangle, middle.X, middle.Y, camera);
        }

        public Result Result() {
            var (covered, twice) = (0, 0);
            var mask = new bool[counts.Length];
            for (var at = 0; at < counts.Length; at++) {
                mask[at] = counts[at] > 0;
                covered += counts[at] > 0 ? 1 : 0;
                twice += counts[at] > 1 ? 1 : 0;
            }

            var measures = new LayoutMeasures(
                triangles,
                surface,
                map,
                outside,
                degenerate,
                flipped,
                covered == 0 ? 0 : (double)twice / covered,
                [..CheckCodes.All.Where(spots.ContainsKey).Select(code => spots[code])]
            );
            return new(measures, mask, grid);
        }
    }
}
