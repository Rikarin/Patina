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
}
