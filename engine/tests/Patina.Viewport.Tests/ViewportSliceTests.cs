namespace Patina.Viewport.Tests;

/// <summary>
///     Where a symmetry's mirror meets the model (#132): one segment across each triangle the plane crosses, lying on
///     the plane, none for a triangle it misses, and none for a part an isolation hides.
/// </summary>
public sealed class ViewportSliceTests : IDisposable {
    const int Floats = ViewportOverlay.FloatsPerVertex;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-slice-");

    public void Dispose() => folder.Delete(true);

    /// <summary>
    ///     A strip of two quads from x = 0 to x = 2, its box centred on x = 1, and a triangle apart from x = 4 to 5.
    ///     The model's box runs from 0 to 5, so its centre is at x = 2.5.
    /// </summary>
    string Model() {
        var path = Path.Combine(folder.FullName, "strip.obj");
        File.WriteAllLines(
            path,
            [
                "v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0", "v 2 0 0", "v 2 1 0",
                "v 4 0 0", "v 5 0 0", "v 5 1 0",
                "o strip", "f 1 2 3", "f 1 3 4", "f 2 5 6", "f 2 6 3",
                "o apart", "f 7 8 9"
            ]
        );
        return path;
    }

    static float[] Xs(float[] lines) => [..Enumerable.Range(0, lines.Length / Floats).Select(at => lines[at * Floats])];

    [Fact]
    public void A_mirror_through_the_middle_cuts_only_the_triangles_it_crosses_along_the_plane() {
        var mesh = ViewportMesh.Read(Model());

        // Through the origin and moved a fifth of the half-diagonal along x: the diagonal of the box from (0, 0, 0) to
        // (5, 1, 0) is √26, so the plane stands at x = 0.2 · √26 / 2 ≈ 0.51, inside the strip's first quad.
        var mirror = new ViewportMirror(0, true, 0.2f);
        var at = ViewportSlice.Plane(mirror, mesh.Bounds).At;
        var shapes = ViewportSlice.Shapes(mesh, mirror, mesh.Parts);

        Assert.InRange(at, 0.5f, 0.52f);
        Assert.Empty(shapes.Triangles);
        // Both triangles of the first quad, one segment each, every end on the plane.
        Assert.Equal(2 * 2 * Floats, shapes.Lines.Length);
        Assert.All(Xs(shapes.Lines), x => Assert.Equal(at, x, 4));
    }

    [Fact]
    public void A_mirror_through_the_models_centre_misses_a_gap_and_a_part_hidden_draws_nothing() {
        var mesh = ViewportMesh.Read(Model());

        // The box's centre, x = 2.5, falls between the strip and the triangle apart.
        Assert.True(ViewportSlice.Shapes(mesh, new(0, false, 0f), mesh.Parts).IsEmpty);
        Assert.True(ViewportSlice.Shapes(mesh, null, mesh.Parts).IsEmpty);

        // Along y at the middle, every triangle is crossed; with the triangle apart hidden, only the strip's four are.
        var across = new ViewportMirror(1, false, 0f);
        Assert.Equal(5 * 2 * Floats, ViewportSlice.Shapes(mesh, across, mesh.Parts).Lines.Length);
        var strip = ViewportSlice.Shapes(mesh, across, mesh.Shown(["strip"], []));
        Assert.Equal(4 * 2 * Floats, strip.Lines.Length);
    }
}
