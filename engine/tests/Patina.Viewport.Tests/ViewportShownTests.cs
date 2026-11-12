namespace Patina.Viewport.Tests;

/// <summary>
///     What a click reaches under an isolation (#136): the triangles the viewport draws, so a part isolated away in
///     front of another is not what a pick lands on, and the triangle named is the model's own.
/// </summary>
public sealed class ViewportShownTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-shown-");

    public void Dispose() => folder.Delete(true);

    /// <summary>
    ///     Two quads one behind the other, each its own material. With the back one shown alone, the front one's
    ///     triangles are not there for the Worker's pick to meet, and the back one's keep the numbers they have in the
    ///     whole model.
    /// </summary>
    [Fact]
    public void A_part_isolated_away_is_not_there_to_be_picked() {
        var path = Path.Combine(folder.FullName, "stacked.obj");
        File.WriteAllLines(
            path,
            [
                "v 0 0 1", "v 1 0 1", "v 1 1 1", "v 0 1 1",
                "v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0",
                "vt 0 0", "vt 0.4 0", "vt 0.4 1", "vt 0 1",
                "vt 0.6 0", "vt 1 0", "vt 1 1", "vt 0.6 1",
                "usemtl front", "f 1/1 2/2 3/3 4/4",
                "usemtl back", "f 5/5 6/6 7/7 8/8"
            ]
        );
        var (whole, all) = ViewportGeometry.Shown(path, [], []);
        var (back, behind) = ViewportGeometry.Shown(path, [], ["back"]);

        Assert.Equal([0, 1, 2, 3], all);
        Assert.Equal(12, whole.Indices.Length);

        // Only the back quad's corners, at z = 0, are left for a ray to meet; each triangle keeps the model's number.
        Assert.Equal([2, 3], behind);
        Assert.All(back.Indices, corner => Assert.Equal(0f, back.Positions[corner].Z));
        Assert.Equal(6, back.Indices.Length);
    }

    /// <summary>
    ///     A texture set as a stroke reaches it under an isolation: of its own triangles only those of the parts shown,
    ///     none when another set is shown alone, and all of them when nothing is.
    /// </summary>
    [Fact]
    public void A_set_as_a_stroke_reaches_it_keeps_to_what_is_shown() {
        var path = Path.Combine(folder.FullName, "parts.obj");
        File.WriteAllLines(
            path,
            [
                "v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0",
                "v 2 0 0", "v 3 0 0", "v 3 1 0", "v 2 1 0",
                "v 4 0 0", "v 5 0 0", "v 5 1 0",
                "vt 0 0", "vt 0.4 0", "vt 0.4 1", "vt 0 1",
                "o left", "usemtl painted", "f 1/1 2/2 3/3 4/4",
                "o right", "usemtl painted", "f 5/1 6/2 7/3 8/4",
                "o other", "usemtl plain", "f 9/1 10/2 11/3"
            ]
        );

        int[] Reached(string[] parts, string[] materials) =>
            ViewportGeometry.Read(path, "painted", 0, parts, materials).Indices;

        var whole = ViewportGeometry.Read(path, "painted");
        Assert.Equal(12, whole.Indices.Length);
        Assert.Equal(whole.Indices, Reached([], []));
        var left = ViewportGeometry.Read(path, "painted", 0, ["left"], []);
        Assert.Equal(6, left.Indices.Length);
        Assert.All(left.Indices, corner => Assert.InRange(left.Positions[corner].X, 0f, 1f));
        Assert.Empty(Reached([], ["plain"]));
        Assert.Equal(12, Reached([], ["painted"]).Length);
    }

    /// <summary>
    ///     Each triangle knows the named mesh it comes from, in the whole model and in one texture set of it, which is
    ///     what a pick says a layer kept to some meshes takes or leaves (#110).
    /// </summary>
    [Fact]
    public void Each_triangle_names_the_mesh_it_comes_from() {
        var path = Path.Combine(folder.FullName, "named.obj");
        File.WriteAllLines(
            path,
            [
                "v 0 0 1", "v 1 0 1", "v 1 1 1", "v 0 1 1",
                "v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0",
                "vt 0 0", "vt 0.4 0", "vt 0.4 1", "vt 0 1",
                "vt 0.6 0", "vt 1 0", "vt 1 1", "vt 0.6 1",
                "o lid", "usemtl metal", "f 1/1 2/2 3/3 4/4",
                "o box", "usemtl wood", "f 5/5 6/6 7/7 8/8",
                "o hinge", "usemtl metal", "f 1/1 2/2 3/3"
            ]
        );

        var whole = ViewportGeometry.Read(path);
        var metal = ViewportGeometry.Read(path, "metal");

        Assert.Equal(["lid", "lid", "box", "box", "hinge"], Enumerable.Range(0, 5).Select(whole.PartOf));
        Assert.Equal(["lid", "lid", "hinge"], Enumerable.Range(0, 3).Select(metal.PartOf));
        Assert.Null(whole.PartOf(5));
        Assert.Null(whole.PartOf(-1));
    }
}
