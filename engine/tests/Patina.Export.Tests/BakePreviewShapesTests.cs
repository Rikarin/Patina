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
}
