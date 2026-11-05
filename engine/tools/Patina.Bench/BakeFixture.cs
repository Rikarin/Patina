// SPDX-License-Identifier: GPL-3.0-or-later

using Patina.Contracts;
using Patina.Export;
using Patina.Jobs;
using System.Diagnostics;
using System.Globalization;
using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Interchange;

namespace Patina.Bench;

/// <summary>
///     Writes a high and low pair with hard edges, for S9, for the bake path's tests and for an artist to judge: a small
///     machined housing. The low is a chamfered block with one chart per flat, so every edge between flats is hard. The
///     high adds what a baker has to carry across them: rounded edges, a recessed panel, bolts on the faces beside the
///     edges, and a separate strap lying close over the top. Nothing here is drawn from anything but these numbers.
/// </summary>
public static class BakeFixture {
    const float Half = 1f;
    const float Tall = 0.6f;
    const float Bevel = 0.12f;
    const float Clearance = 1.012f;
    public const string HighFile = "housing_high.glb";
    public const string LowFile = "housing_low.glb";

    /// <summary>The flats of the low: eight sides, a top and a bottom.</summary>
    const int Sides = 8;

    /// <param name="folder">Where the two files go.</param>
    /// <param name="cell">The high's grid spacing: a coarse one is what a test wants.</param>
    public static int Write(string folder, float cell = 0.0055f) {
        Directory.CreateDirectory(folder);
        var (low, strapLow) = Low();
        ModelWriter.Write(Path.Combine(folder, LowFile), [low, strapLow], "housing");

        var (body, strap) = High(cell);
        ModelWriter.Write(Path.Combine(folder, HighFile), [body, strap], "housing");

        Console.WriteLine($"{Path.Combine(folder, HighFile)}: {body.Indices.Length / 3 + strap.Indices.Length / 3} triangles");
        Console.WriteLine($"{Path.Combine(folder, LowFile)}: {(low.Indices.Length + strapLow.Indices.Length) / 3} triangles");
        return 0;
    }

    /// <summary>A point on the outline's flats: the n-th of eight corners of the chamfered square.</summary>
    static Vector2 Corner(int index) {
        var inner = Half - (2f * Bevel);
        Vector2[] corners = [
            new(Half, -inner), new(Half, inner), new(inner, Half), new(-inner, Half),
            new(-Half, inner), new(-Half, -inner), new(-inner, -Half), new(inner, -Half)
        ];
        return corners[((index % Sides) + Sides) % Sides];
    }

    /// <summary>The low: ten flat charts, with the vertices split so that every edge is hard.</summary>
    static (MeshData Body, MeshData Strap) Low() {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var coordinates = new List<Vector2>();
        var indices = new List<int>();

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int chart) {
            // ⚠ Clear of the high, as a low is. A flat lying exactly on the source has every ray rejected at its own
            // origin, which the bake survives through its closest-point fallback and reports as a fifth of it missed.
            (a, b, c, d) = (a * Clearance, b * Clearance, c * Clearance, d * Clearance);
            var (u, v) = (chart % 4, chart / 4);
            var normal = Vector3.Normalize(Vector3.Cross(b - a, d - a));
            var first = positions.Count;
            positions.AddRange([a, b, c, d]);
            normals.AddRange([normal, normal, normal, normal]);

            const float Margin = 0.03f;
            float Across(int cell, float at) => (cell + Margin + (at * (1f - (2f * Margin)))) / 4f;
            coordinates.Add(new(Across(u, 0f), Across(v, 0f)));
            coordinates.Add(new(Across(u, 1f), Across(v, 0f)));
            coordinates.Add(new(Across(u, 1f), Across(v, 1f)));
            coordinates.Add(new(Across(u, 0f), Across(v, 1f)));
            indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
        }

        for (var side = 0; side < Sides; side++) {
            var (from, to) = (Corner(side), Corner(side + 1));
            Quad(
                new(from.X, -Tall, from.Y),
                new(from.X, Tall, from.Y),
                new(to.X, Tall, to.Y),
                new(to.X, -Tall, to.Y),
                side
            );
        }

        // The caps as fans of quads would need more charts; two quads and two trims keep the atlas simple.
        var inner = Half - (2f * Bevel);
        Quad(new(-Half, Tall, -inner), new(-Half, Tall, inner), new(Half, Tall, inner), new(Half, Tall, -inner), 8);
        Quad(new(-Half, -Tall, -inner), new(Half, -Tall, -inner), new(Half, -Tall, inner), new(-Half, -Tall, inner), 9);
        Quad(new(-inner, Tall, inner), new(-inner, Tall, Half), new(inner, Tall, Half), new(inner, Tall, inner), 10);
        Quad(new(-inner, Tall, -Half), new(-inner, Tall, -inner), new(inner, Tall, -inner), new(inner, Tall, -Half), 11);
        Quad(new(-inner, -Tall, inner), new(inner, -Tall, inner), new(inner, -Tall, Half), new(-inner, -Tall, Half), 12);
        Quad(new(-inner, -Tall, -Half), new(inner, -Tall, -Half), new(inner, -Tall, -inner), new(-inner, -Tall, -inner), 13);

        var body = Mesh("housing_low", positions, normals, coordinates, indices);

        positions = [];
        normals = [];
        coordinates = [];
        indices = [];

        // A strap across the top, close enough that its neighbour's rays would strike it: what match-by-name is for.
        const float StrapHalf = 0.16f;
        const float StrapLift = 0.03f;
        Quad(new(-StrapHalf, Tall + StrapLift, -1.02f), new(-StrapHalf, Tall + StrapLift, 1.02f), new(StrapHalf, Tall + StrapLift, 1.02f), new(StrapHalf, Tall + StrapLift, -1.02f), 14);

        return (body, Mesh("strap_low", positions, normals, coordinates, indices));
    }

    static MeshData Mesh(string name, List<Vector3> positions, List<Vector3> normals, List<Vector2> coordinates, List<int> indices) =>
        new() {
            Name = name,
            Positions = [..positions],
            Normals = [..normals],
            TexCoords = [..coordinates],
            Indices = [..indices],
            Bounds = new(
                positions.Aggregate(Vector3.Min),
                positions.Aggregate(Vector3.Max)
            )
        };

    /// <summary>
    ///     The high: one closed surface, swept around the outline, so a ray can never find its way inside. The rim runs
    ///     around a quarter circle from each side into each cap, which is what the low's hard edges have to bake.
    /// </summary>
    /// <param name="cell">The grid's spacing.</param>
    static (MeshData Body, MeshData Strap) High(float cell) {
        var columns = (int)(Half * 8f / cell);
        var rows = (int)((Half + Tall) * 4f / cell);

        var positions = new Vector3[columns * (rows + 1)];
        var normals = new Vector3[positions.Length];

        Parallel.For(0, rows + 1, row => {
            var along = (float)row / rows;
            for (var column = 0; column < columns; column++) {
                var (angle, step) = (MathF.Tau * column / columns, MathF.Tau / columns);
                var at = (row * columns) + column;
                positions[at] = Surface(angle, along);

                var turning = Surface(angle + step, along) - Surface(angle - step, along);
                var rising = Surface(angle, MathF.Min(along + (1f / rows), 1f)) - Surface(angle, MathF.Max(along - (1f / rows), 0f));
                var normal = Vector3.Cross(rising, turning);
                normals[at] = normal.LengthSquared() > 0f ? Vector3.Normalize(normal) : new(0f, row < rows / 2 ? -1f : 1f, 0f);
            }
        });

        var indices = new int[columns * rows * 6];
        for (var row = 0; row < rows; row++) {
            for (var column = 0; column < columns; column++) {
                var next = (column + 1) % columns;
                var (a, b, c, d) = (
                    (row * columns) + column,
                    (row * columns) + next,
                    ((row + 1) * columns) + column,
                    ((row + 1) * columns) + next
                );
                var at = ((row * columns) + column) * 6;
                (indices[at], indices[at + 1], indices[at + 2]) = (a, c, b);
                (indices[at + 3], indices[at + 4], indices[at + 5]) = (b, c, d);
            }
        }

        var body = new MeshData {
            Name = "housing_high",
            Positions = positions,
            Normals = normals,
            Indices = indices,
            Bounds = new(new(-Half, -Tall, -Half), new(Half, Tall, Half))
        };

        List<Vector3> strapPositions = [];
        List<Vector3> strapNormals = [];
        List<int> strapIndices = [];
        Strap(strapPositions, strapNormals, strapIndices, cell);

        return (
            body,
            new() {
                Name = "strap_high",
                Positions = [..strapPositions],
                Normals = [..strapNormals],
                Indices = [..strapIndices],
                Bounds = new(new(-0.2f, Tall, -1.1f), new(0.2f, Tall + 0.1f, 1.1f))
            }
        );
    }

    /// <summary>The outline's distance from the axis at an angle: the low's own outline, a little rounded.</summary>
    static float Reach(float angle) {
        var (x, z) = (MathF.Cos(angle), MathF.Sin(angle));
        var sum = 0f;

        // The largest distance that stays inside all eight flats, softened near each corner. A plain minimum is the
        // low's outline exactly; the power mean rounds it by a few percent, like a machined corner.
        for (var flat = 0; flat < Sides; flat++) {
            var (ox, oz) = Facing(flat);
            var along = (x * ox) + (z * oz);
            if (along > 0f) {
                sum += MathF.Pow(along / Offset(flat), Roundness);
            }
        }

        return MathF.Pow(sum, -1f / Roundness);
    }

    const float Roundness = 64f;

    static (float X, float Z) Facing(int flat) {
        var (from, to) = (Corner(flat), Corner(flat + 1));
        var (dx, dz) = (to.X - from.X, to.Y - from.Y);
        var length = MathF.Sqrt((dx * dx) + (dz * dz));
        return flat % 2 == 0 ? (dz / length, -dx / length) : (dz / length, -dx / length);
    }

    static float Offset(int flat) {
        var (x, z) = Facing(flat);
        var corner = Corner(flat);
        return (corner.X * x) + (corner.Y * z);
    }

    /// <summary>A point of the closed surface: around the outline, and up from the bottom centre to the top centre.</summary>
    static Vector3 Surface(float angle, float along) {
        var reach = Reach(angle);
        const float Rim = 0.05f;

        // The profile, by length: in along the bottom, up the rim, up the side, over the rim, and back in along the top.
        var flat = (reach - Rim) / reach;
        var arc = Rim * MathF.PI / 2f;
        var side = 2f * (Tall - Rim);
        var length = (2f * (reach - Rim)) + (2f * arc) + side;
        var walked = along * length;

        float radial;
        float height;
        if (walked < reach - Rim) {
            (radial, height) = (walked / (reach - Rim) * flat, -Tall);
        } else if (walked < reach - Rim + arc) {
            var turn = (walked - (reach - Rim)) / arc * (MathF.PI / 2f);
            (radial, height) = (flat + (MathF.Sin(turn) * Rim / reach), -Tall + Rim - (MathF.Cos(turn) * Rim));
        } else if (walked < reach - Rim + arc + side) {
            (radial, height) = (1f, -Tall + Rim + (walked - (reach - Rim) - arc));
        } else if (walked < reach - Rim + (2f * arc) + side) {
            var turn = (walked - (reach - Rim) - arc - side) / arc * (MathF.PI / 2f);
            (radial, height) = (flat + (MathF.Cos(turn) * Rim / reach), Tall - Rim + (MathF.Sin(turn) * Rim));
        } else {
            (radial, height) = ((length - walked) / (reach - Rim) * flat, Tall);
        }

        var (x, z) = (MathF.Cos(angle) * reach * radial, MathF.Sin(angle) * reach * radial);

        if (radial >= 1f) {
            return Relieved(new(x, height, z), angle);
        }

        return height >= Tall ? new(x, height + TopRelief(new((x + Half) / (2f * Half), (z + Half) / (2f * Half))), z) : new(x, height, z);
    }

    /// <summary>Moves a point of a side along the normal of the flat it is on.</summary>
    static Vector3 Relieved(Vector3 point, float angle) {
        var (best, nearest) = (0, float.NegativeInfinity);
        for (var flat = 0; flat < Sides; flat++) {
            var (x, z) = Facing(flat);
            var along = ((MathF.Cos(angle) * x) + (MathF.Sin(angle) * z)) / Offset(flat);
            if (along > nearest) {
                (best, nearest) = (flat, along);
            }
        }

        if (best % 2 != 0) {
            return point;
        }

        var (from, to) = (Corner(best), Corner(best + 1));
        var (dx, dz) = (to.X - from.X, to.Y - from.Y);
        var wide = (dx * dx) + (dz * dz);
        var u = (((point.X - from.X) * dx) + ((point.Z - from.Y) * dz)) / wide;
        var v = (point.Y + Tall) / (2f * Tall);
        var height = SideRelief(new(u, v));
        var (ox, oz) = Facing(best);
        return new(point.X + (ox * height), point.Y, point.Z + (oz * height));
    }

    static float Bolt(float across, float along, float centreAcross, float centreAlong) {
        const float Radius = 0.075f;
        var distance = MathF.Sqrt(((across - centreAcross) * (across - centreAcross)) + ((along - centreAlong) * (along - centreAlong)));
        if (distance > Radius) {
            return 0f;
        }

        // A dome with a slot in it.
        var dome = 0.03f * MathF.Sqrt(1f - ((distance * distance) / (Radius * Radius)));
        var slot = MathF.Abs(across - centreAcross - (along - centreAlong)) < 0.012f ? 0.012f : 0f;
        return dome - slot;
    }

    /// <summary>The relief of a broad side: a recessed panel, and bolts close to the edges.</summary>
    static float SideRelief(Vector2 point) {
        var (x, y) = (point.X, point.Y);

        // A recessed panel with a sloped wall. Inward, so the low's flat still encloses it.
        var panel = MathF.Min(MathF.Min(x - 0.22f, 0.78f - x), MathF.Min(y - 0.18f, 0.82f - y));
        var height = -Math.Clamp(panel * 2.5f, 0f, 0.025f);

        foreach (var bolt in (ReadOnlySpan<(float, float)>)[(0.1f, 0.14f), (0.9f, 0.14f), (0.1f, 0.86f), (0.9f, 0.86f)]) {
            height += Bolt(x, y, bolt.Item1, bolt.Item2) * 0.8f;
        }

        return height;
    }

    /// <summary>The relief of the top: ridges, for fine detail that aliases at a low sample count.</summary>
    static float TopRelief(Vector2 point) => (MathF.Abs(MathF.Sin(point.X * 90f)) - 1f) * 0.006f;

    /// <summary>A grid over a parallelogram, moved along its normal by a height, with normals from its own differences.</summary>
    static void Patch(
        List<Vector3> positions,
        List<Vector3> normals,
        List<int> indices,
        float cell,
        Vector3 origin,
        Vector3 across,
        Vector3 along,
        Func<Vector2, float> height
    ) {
        var columns = Math.Max(2, (int)(across.Length() / cell));
        var rows = Math.Max(2, (int)(along.Length() / cell));
        var facing = Vector3.Normalize(Vector3.Cross(across, along));
        var first = positions.Count;
        var (points, shades) = (new Vector3[(columns + 1) * (rows + 1)], new Vector3[(columns + 1) * (rows + 1)]);

        Vector3 At(float u, float v) => origin + (across * u) + (along * v) + (facing * height(new(u, v)));

        var (du, dv) = (1f / columns, 1f / rows);
        Parallel.For(0, rows + 1, row => {
            for (var column = 0; column <= columns; column++) {
                var (u, v) = (column * du, row * dv);
                points[(row * (columns + 1)) + column] = At(u, v);
                var normal = Vector3.Cross(At(u + du, v) - At(u - du, v), At(u, v + dv) - At(u, v - dv));
                shades[(row * (columns + 1)) + column] = normal.LengthSquared() > 0f ? Vector3.Normalize(normal) : facing;
            }
        });

        positions.AddRange(points);
        normals.AddRange(shades);

        for (var row = 0; row < rows; row++) {
            for (var column = 0; column < columns; column++) {
                var at = first + (row * (columns + 1)) + column;
                indices.AddRange([at, at + columns + 1, at + 1, at + 1, at + columns + 1, at + columns + 2]);
            }
        }
    }

    static void Strap(List<Vector3> positions, List<Vector3> normals, List<int> indices, float cell) {
        const float Lift = 0.03f;
        Patch(
            positions,
            normals,
            indices,
            cell,
            new(-0.16f, Tall + Lift, -1.02f),
            new(0f, 0f, 2.04f),
            new(0.32f, 0f, 0f),
            static point => (0.02f * MathF.Sin(point.Y * MathF.PI)) + (MathF.Abs(MathF.Sin(point.X * 60f)) < 0.2f ? -0.004f : 0f)
        );
    }

    /// <summary>
    ///     S9: threaded occlusion at 4K with 256 rays over two million triangles, on an atlas with no empty space, which
    ///     is the worst an atlas can be. Prints what ADR 0008 quotes.
    /// </summary>
    public static int Spike(string folder, int resolution, int rays, double reach) {
        var (high, low) = (Path.Combine(folder, "s9_high.glb"), Path.Combine(folder, "s9_low.glb"));
        Directory.CreateDirectory(folder);
        if (!File.Exists(high)) {
            var (dense, thin) = Knot(1000);
            ModelWriter.Write(high, [dense], "s9");
            ModelWriter.Write(low, [thin], "s9");
        }

        var request = new BakeRequest(
            high,
            low,
            folder,
            Resolution: resolution,
            Maps: ["ao"],
            Format: BakeInputs.Png8,
            OcclusionRays: rays,
            SearchRadius: 0.2,
            OcclusionRadius: reach
        );

        var clock = Stopwatch.StartNew();
        var result = MeshMapBakeRun.Run(request, Path.Combine(folder, $"out-{Guid.NewGuid():N}"), 0, null, default);
        var seconds = clock.Elapsed.TotalSeconds;
        var cpu = Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
        var total = result.Result.Covered * (double)rays;

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{resolution} texels, {rays} rays, reach {reach}: {seconds:F1} s wall, {cpu:F0} s CPU, {result.Result.Covered} texels covered, "
                + $"{total / seconds / 1e6:F1} M rays/s, {cpu * 1e9 / total:F0} ns CPU per ray"
            )
        );
        return 0;
    }

    /// <summary>The knot of <see cref="MeshFixture" /> with a bumpy skin, as a grid, and its smooth low with a full atlas.</summary>
    static (MeshData High, MeshData Low) Knot(int side) {
        static Vector3 Centre(float t) {
            var radius = 2f + MathF.Cos(3f * t);
            return new Vector3(radius * MathF.Cos(2f * t), MathF.Sin(3f * t), radius * MathF.Sin(2f * t)) * 0.4f;
        }

        static float Bump(float u, float v) {
            var (a, b, sum, amplitude, fx, fy) = (u * MathF.Tau, v * MathF.Tau, 0f, 1f, 20f, 3f);
            for (var octave = 0; octave < 6; octave++) {
                sum += amplitude * MathF.Abs(MathF.Sin((a * fx) + (1.3f * octave) + (2f * MathF.Sin(b * fy)))) * MathF.Abs(MathF.Sin((b * fy) + (a * 3f)));
                amplitude *= 0.55f;
                fx *= 2.1f;
                fy = MathF.Round(fy * 2.1f);
            }

            return sum;
        }

        static Vector3 Surface(float u, float v, float bump) {
            var t = u * MathF.Tau;
            var centre = Centre(t);
            var tangent = Vector3.Normalize(Centre(t + 0.001f) - centre);
            var turn = Vector3.Normalize(Vector3.Cross(tangent, Vector3.Normalize(centre)));
            var lift = Vector3.Cross(turn, tangent);
            var angle = -v * MathF.Tau;
            return centre + (((turn * MathF.Cos(angle)) + (lift * MathF.Sin(angle))) * (0.11f + (bump * 0.05f * Bump(u, v))));
        }

        static MeshData Grid(string name, int columns, int rows, float bump) {
            var positions = new Vector3[(columns + 1) * (rows + 1)];
            var coordinates = new Vector2[positions.Length];
            var normals = new Vector3[positions.Length];
            var indices = new int[columns * rows * 6];

            Parallel.For(0, rows + 1, row => {
                for (var column = 0; column <= columns; column++) {
                    var (u, v) = ((float)column / columns, (float)row / rows);
                    var at = (row * (columns + 1)) + column;
                    positions[at] = Surface(u, v, bump);
                    coordinates[at] = new(u, v);
                    var normal = Vector3.Cross(Surface(u, v + 1e-3f, bump) - Surface(u, v - 1e-3f, bump), Surface(u + 1e-3f, v, bump) - Surface(u - 1e-3f, v, bump));
                    normals[at] = normal.LengthSquared() > 0f ? Vector3.Normalize(normal) : Vector3.UnitY;
                }
            });

            var slot = 0;
            for (var row = 0; row < rows; row++) {
                for (var column = 0; column < columns; column++) {
                    var at = (row * (columns + 1)) + column;
                    foreach (var corner in (ReadOnlySpan<int>)[at, at + columns + 1, at + 1, at + 1, at + columns + 1, at + columns + 2]) {
                        indices[slot++] = corner;
                    }
                }
            }

            return new() {
                Name = name,
                Positions = positions,
                Normals = normals,
                TexCoords = coordinates,
                Indices = indices,
                Bounds = new(new(-2f), new(2f))
            };
        }

        return (Grid("knot_high", side, side, 1f), Grid("knot_low", 180, 24, 0f));
    }
}
