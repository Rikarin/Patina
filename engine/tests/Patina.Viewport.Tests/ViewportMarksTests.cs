namespace Patina.Viewport.Tests;

/// <summary>
///     What the viewport marks on the model (#136): the triangles of the parts and materials named, washed, with an
///     outline only where the marked faces end, and nothing a part isolated away would leave floating.
/// </summary>
public sealed class ViewportMarksTests : IDisposable {
    const int Floats = ViewportOverlay.FloatsPerVertex;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-marks-");

    public void Dispose() => folder.Delete(true);

    /// <summary>
    ///     Two quads side by side in one material and a triangle in another. The left quad is split along a UV seam,
    ///     so its corners are separate vertices; its outline is still its four sides and not its diagonal.
    /// </summary>
    string Model() {
        var path = Path.Combine(folder.FullName, "parts.obj");
        File.WriteAllLines(
            path,
            [
                "v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0",
                "v 2 0 0", "v 3 0 0", "v 3 1 0", "v 2 1 0",
                "v 4 0 0", "v 5 0 0", "v 5 1 0",
                "vt 0 0", "vt 0.4 0", "vt 0.4 1", "vt 0 1", "vt 0.5 0.5",
                "o left", "usemtl painted", "f 1/1 2/2 3/3", "f 1/5 3/5 4/4",
                "o right", "usemtl painted", "f 5/1 6/2 7/3 8/4",
                "o other", "usemtl plain", "f 9/1 10/2 11/3"
            ]
        );
        return path;
    }

    [Fact]
    public void A_part_is_washed_and_outlined_along_its_own_edges() {
        var mesh = ViewportMesh.Read(Model());

        var shapes = ViewportMarks.Shapes(mesh, (["left"], []), ([], []));

        Assert.Equal(2 * 3 * Floats, shapes.Triangles.Length);
        // Four sides, two vertices a side: the diagonal is shared by both triangles and is no outline.
        Assert.Equal(4 * 2 * Floats, shapes.Lines.Length);
    }

    [Fact]
    public void A_material_marks_every_part_of_it() {
        var mesh = ViewportMesh.Read(Model());

        var shapes = ViewportMarks.Shapes(mesh, ([], ["painted"]), ([], []));

        Assert.Equal(4 * 3 * Floats, shapes.Triangles.Length);
        Assert.Equal(8 * 2 * Floats, shapes.Lines.Length);
    }

    /// <summary>A part denser than the most washed is washed evenly thinner, its outline whole (#271).</summary>
    [Fact]
    public void A_dense_mark_is_washed_thinner_and_outlined_whole() {
        var mesh = ViewportMesh.Read(Model());

        var shapes = ViewportMarks.Shapes(mesh, ([], ["painted"]), ([], []), 2);

        Assert.Equal(2 * 3 * Floats, shapes.Triangles.Length);
        Assert.Equal(8 * 2 * Floats, shapes.Lines.Length);
    }

    [Fact]
    public void Nothing_named_or_isolated_away_marks_nothing() {
        var mesh = ViewportMesh.Read(Model());

        Assert.True(ViewportMarks.Shapes(mesh, ([], []), ([], [])).IsEmpty);
        Assert.True(ViewportMarks.Shapes(mesh, (["no such part"], []), ([], [])).IsEmpty);
        Assert.True(ViewportMarks.Shapes(mesh, ([], ["painted"]), ([], ["plain"])).IsEmpty);
        var kept = ViewportMarks.Shapes(mesh, ([], ["painted"]), (["right"], []));
        Assert.Equal(2 * 3 * Floats, kept.Triangles.Length);
    }
}
