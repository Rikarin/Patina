using Patina.Contracts;

namespace Patina.Export.Tests;

/// <summary>What a bake's preview draws (#149), from files, against numbers worked out before it ran.</summary>
public sealed class BakePreviewShapesTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-preview-");

    public void Dispose() => folder.Delete(true);

    /// <summary>Named unit quads side by side on the XY plane, facing +Z, each its own object.</summary>
    string Quads(string file, params string[] names) {
        List<string> lines = [];
        for (var at = 0; at < names.Length; at++) {
            var x = at * 2;
            var v = at * 4;
            lines.AddRange(
                [
                    $"o {names[at]}",
                    $"v {x} 0 0", $"v {x + 1} 0 0", $"v {x + 1} 1 0", $"v {x} 1 0",
                    "vt 0 0", "vt 1 0", "vt 1 1", "vt 0 1",
                    "vn 0 0 1",
                    $"f {v + 1}/{v + 1}/{at + 1} {v + 2}/{v + 2}/{at + 1} {v + 3}/{v + 3}/{at + 1}",
                    $"f {v + 1}/{v + 1}/{at + 1} {v + 3}/{v + 3}/{at + 1} {v + 4}/{v + 4}/{at + 1}"
                ]
            );
        }

        var path = Path.Combine(folder.FullName, file);
        File.WriteAllLines(path, lines);
        return path;
    }

    static IEnumerable<float> Coordinate(float[] vertices, int axis) {
        for (var at = axis; at < vertices.Length; at += BakePreviewShapes.FloatsPerVertex) {
            yield return vertices[at];
        }
    }

    /// <summary>
    ///     With no cage the shell is the low mesh pushed out the front distance along its rays, and every point has a
    ///     ray from the rear distance behind to the front distance before it.
    /// </summary>
    [Fact]
    public void The_reach_is_the_low_mesh_pushed_out_the_front_distance_with_a_ray_from_each_point() {
        var quad = Quads("quad.obj", "plate");
        var drawing = BakePreviewShapes.Build(
            new() {
                High = [quad],
                Low = quad,
                HighMesh = false,
                Matches = false,
                Options = new() { Rays = new() { Units = "absolute", Front = 0.2, Rear = 0.1 } }
            }
        );

        Assert.Equal(6 * BakePreviewShapes.FloatsPerVertex, drawing.Triangles.Length);
        Assert.All(Coordinate(drawing.Triangles, 2), static z => Assert.Equal(0.2f, z, 1e-5f));
        var depths = Coordinate(drawing.Lines, 2).ToList();
        // Six shell edges, then four points with a ray out and a ray in.
        Assert.Equal((6 + 4 * 2) * 2, depths.Count);
        Assert.Equal(4, depths.Count(static z => MathF.Abs(z + 0.1f) < 1e-5f));
    }

    /// <summary>
    ///     Parts matched by name share a colour on both sides; a low part that matched nothing is red and has no high
    ///     faces in the key, and a high part nothing matched is named with no low faces.
    /// </summary>
    [Fact]
    public void Matched_parts_share_a_colour_and_the_unmatched_are_named_and_red() {
        var high = Quads("high.obj", "bolt_high", "strap_high");
        var low = Quads("low.obj", "bolt_low", "plate_low");
        var drawing = BakePreviewShapes.Build(
            new() { High = [high], Low = low, Reach = false, Options = new() { Rays = new() { MatchByName = true } } }
        );

        var parts = drawing.Parts.ToDictionary(static part => part.Name);
        Assert.Equal(["bolt", "plate", "strap"], parts.Keys.Order(StringComparer.Ordinal));
        Assert.Equal((2, 2), (parts["bolt"].LowFaces, parts["bolt"].HighFaces));
        Assert.Equal((2, 0), (parts["plate"].LowFaces, parts["plate"].HighFaces));
        Assert.Equal((0, 2), (parts["strap"].LowFaces, parts["strap"].HighFaces));
        Assert.StartsWith("#ff", parts["plate"].Colour, StringComparison.Ordinal);
        Assert.NotEqual(parts["bolt"].Colour, parts["plate"].Colour);

        // Four high triangles, then four low ones: the bolt's are one colour on both sides.
        var colours = Enumerable.Range(0, drawing.Triangles.Length / BakePreviewShapes.FloatsPerVertex / 3)
            .Select(triangle => drawing.Triangles[triangle * 3 * BakePreviewShapes.FloatsPerVertex + 3])
            .ToList();
        Assert.Equal(8, colours.Count);
        Assert.Equal(colours[0], colours[4]);
        Assert.NotEqual(colours[4], colours[6]);
    }

    /// <summary>
    ///     Asked to show matches that are not on, the preview says every ray may strike all of the high
    ///     mesh.
    /// </summary>
    [Fact]
    public void Matches_asked_for_without_matching_by_name_say_so() {
        var quad = Quads("quad.obj", "plate");
        var drawing = BakePreviewShapes.Build(new() { High = [quad], Low = quad });

        Assert.Empty(drawing.Parts);
        Assert.Contains(drawing.Warnings, static warning => warning.Contains("not matched", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A fold of two quads at a right angle along x = 1, each with its own normal, so the fold is a hard edge. With
    ///     <paramref name="split" /> the second quad has coordinates of its own along it, which makes it a UV seam.
    /// </summary>
    string Fold(string file, bool split) {
        List<string> lines = [
            "o fold",
            "v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0", "v 1 0 -1", "v 1 1 -1",
            "vt 0 0", "vt 0.5 0", "vt 0.5 1", "vt 0 1", "vt 1 0", "vt 1 1", "vt 0.6 0", "vt 0.6 1",
            "vn 0 0 1", "vn 1 0 0",
            "f 1/1/1 2/2/1 3/3/1 4/4/1",
            split ? "f 2/7/2 5/5/2 6/6/2 3/8/2" : "f 2/2/2 5/5/2 6/6/2 3/3/2"
        ];
        var path = Path.Combine(folder.FullName, file);
        File.WriteAllLines(path, lines);
        return path;
    }

    static ViewportBakePreview SeamsOnly(string mesh) =>
        new() { High = [mesh], Low = mesh, Reach = false, HighMesh = false, Matches = false };

    /// <summary>
    ///     A hard edge whose coordinates run on across it is the line a tangent-space normal map shows, so it is drawn
    ///     and named; the same edge with a UV seam along it is how it should be, and is not.
    /// </summary>
    [Fact]
    public void A_hard_edge_with_no_uv_seam_is_drawn_and_one_with_a_seam_is_not() {
        var missing = BakePreviewShapes.Build(SeamsOnly(Fold("fold.obj", false)));

        var xs = Coordinate(missing.Lines, 0).ToList();
        Assert.Equal(2, xs.Count);
        Assert.All(xs, static x => Assert.Equal(1f, x, 1e-6f));
        Assert.Equal([0f, 1f], Coordinate(missing.Lines, 1).Order());
        Assert.Contains(missing.Warnings, static warning => warning.StartsWith("1 hard edge", StringComparison.Ordinal));
        // A band either side of it, on each face: two triangles each, reaching into the face and not past x = 1.
        var band = Coordinate(missing.Triangles, 0).ToList();
        Assert.Equal(12, band.Count);
        Assert.All(band, static x => Assert.InRange(x, 0.9f, 1f));
        Assert.Contains(Coordinate(missing.Triangles, 2), static z => z < -0.001f);

        var seamed = BakePreviewShapes.Build(SeamsOnly(Fold("seamed.obj", true)));
        Assert.Empty(seamed.Lines);
        Assert.Empty(seamed.Triangles);
        Assert.Empty(seamed.Warnings);
        Assert.Empty(BakePreviewShapes.Build(SeamsOnly(Fold("off.obj", false)) with { Seams = false }).Lines);
    }

    /// <summary>
    ///     The shell's fill and the high mesh are as opaque as asked, and a shell at none is not drawn at all; the
    ///     shell's edges and rays keep their own.
    /// </summary>
    [Fact]
    public void The_shell_and_the_high_mesh_are_as_opaque_as_asked() {
        var quad = Quads("quad.obj", "plate");
        var preview = new ViewportBakePreview {
            High = [quad],
            Low = quad,
            Matches = false,
            HighOpacity = 0.7,
            ShellOpacity = 0.4,
            Options = new() { Rays = new() { Units = "absolute", Front = 0.2, Rear = 0.1 } }
        };

        var opacities = Coordinate(BakePreviewShapes.Build(preview).Triangles, 6).ToList();
        // Two high triangles, then the shell's two.
        Assert.Equal(12, opacities.Count);
        Assert.All(opacities.Take(6), static a => Assert.Equal(0.7f, a, 1e-6f));
        Assert.All(opacities.Skip(6), static a => Assert.Equal(0.4f, a, 1e-6f));

        var bare = BakePreviewShapes.Build(preview with { ShellOpacity = 0 });
        Assert.Equal(6, Coordinate(bare.Triangles, 6).Count());
        Assert.Equal(BakePreviewShapes.Build(preview).Lines, bare.Lines);
    }

    /// <summary>An opacity outside 0 to 1 is refused, as a bake's own options are.</summary>
    [Fact]
    public void An_opacity_past_one_is_refused() {
        Assert.Null(new ViewportBakePreview().Problem());
        Assert.NotNull(new ViewportBakePreview { ShellOpacity = 1.5 }.Problem());
        Assert.NotNull(new ViewportBakePreview { HighOpacity = -0.1 }.Problem());
        Assert.NotNull(new ViewportBakePreview { HighOpacity = double.NaN }.Problem());
    }
}
