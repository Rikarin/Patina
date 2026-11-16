using Patina.Export;
using Patina.Server.Graphs;
using Patina.Testing;

namespace Patina.Server.Tests;

/// <summary>The bake the shelf draws its generators from (#301), written into the preview cache as it is needed.</summary>
public sealed class ShelfBakeTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-shelf-bake-");

    public void Dispose() => TempFolders.Delete(folder.FullName);

    /// <summary>
    ///     The sample knot's maps are a bake a Mesh Map node finds, written once: a second ask keeps each file and its
    ///     time, which the generators' pictures are keyed on, and a map the cache lost is written again.
    /// </summary>
    [Fact]
    public void The_shelf_bake_is_written_once_and_again_only_for_what_the_cache_lost() {
        var bake = ShelfBake.In(folder.FullName);

        Assert.NotNull(bake);
        string[] read = ["ao", "curvature", "thickness", "position", "world", "id", "islands", "bounds", "density"];
        Assert.All(read, map => Assert.NotNull(BakedMapPicture.Find(bake, map)));
        var curvature = BakedMapPicture.Find(bake, "curvature")!.Value.Path;
        var written = File.GetLastWriteTimeUtc(curvature);
        File.SetLastWriteTimeUtc(curvature, written.AddDays(-1));
        File.Delete(BakedMapPicture.Find(bake, "ao")!.Value.Path);

        Assert.Equal(bake, ShelfBake.In(folder.FullName));
        Assert.Equal(written.AddDays(-1), File.GetLastWriteTimeUtc(curvature));
        Assert.NotNull(BakedMapPicture.Find(bake, "ao"));
        Assert.True(File.Exists(BakedMapPicture.Find(bake, "ao")!.Value.Path));
    }
}
