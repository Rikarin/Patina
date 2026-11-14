namespace Patina.Viewport.Tests;

/// <summary>
///     A texture set on one UDIM tile past the first (#20) is read moved home onto 1001, so a stroke, a pick and the
///     2D view land in its own maps; a set on two tiles is left where it is, so its problem is still said.
/// </summary>
public sealed class SetTilesTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-tiles-");

    public void Dispose() => folder.Delete(true);

    /// <summary>Three quads: Body on tile 1001, Strap on 1012, and Belt across 1001 and 1002.</summary>
    string Model() {
        var path = Path.Combine(folder.FullName, "tiles.obj");
        File.WriteAllLines(
            path,
            [
                "v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0",
                "v 2 0 0", "v 3 0 0", "v 3 1 0", "v 2 1 0",
                "v 4 0 0", "v 5 0 0", "v 5 1 0", "v 4 1 0",
                "vt 0.1 0.1", "vt 0.9 0.1", "vt 0.9 0.9", "vt 0.1 0.9",
                "vt 1.1 1.1", "vt 1.9 1.1", "vt 1.9 1.9", "vt 1.1 1.9",
                "vt 0.5 0.1", "vt 1.5 0.1", "vt 1.5 0.9", "vt 0.5 0.9",
                "usemtl Body", "f 1/1 2/2 3/3 4/4",
                "usemtl Strap", "f 5/5 6/6 7/7 8/8",
                "usemtl Belt", "f 9/9 10/10 11/11 12/12"
            ]
        );
        return path;
    }

    static IEnumerable<Vixen.Core.Mathematics.Vector2> Corners(ViewportGeometry geometry) =>
        geometry.Indices.Select(index => geometry.Coordinates[index]);

    [Fact]
    public void A_set_on_one_tile_past_the_first_is_read_home() {
        var strap = ViewportGeometry.Read(Model(), "Strap");

        Assert.All(Corners(strap), static at => Assert.InRange(at.X, 0f, 1f));
        Assert.All(Corners(strap), static at => Assert.InRange(at.Y, 0f, 1f));
        Assert.Contains(Corners(strap), static at => MathF.Abs(at.X - 0.1f) < 1e-5f);
    }

    /// <summary>The viewport draws what a stroke reaches: Body and Strap home, and Belt across its two tiles.</summary>
    [Fact]
    public void The_whole_model_draws_each_set_home_and_leaves_a_set_on_two_tiles_alone() {
        var whole = ViewportGeometry.Read(Model());

        Assert.All(whole.Indices[..12], index => Assert.InRange(whole.Coordinates[index].X, 0f, 1f));
        Assert.Contains(whole.Indices[12..], index => whole.Coordinates[index].X > 1f);
        Assert.Contains(Corners(ViewportGeometry.Read(Model(), "Belt")), static at => at.X > 1f);
    }
}
