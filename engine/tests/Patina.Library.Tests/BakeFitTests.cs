using Patina.Contracts;
using System.Text.Json;

namespace Patina.Library.Tests;

/// <summary>
///     A texture set's bake fits it while the set's own faces are the ones baked on (#151): a mesh changed elsewhere
///     leaves it fitting, and a change to its faces, or a bake that did not record them, does not.
/// </summary>
public sealed class BakeFitTests : IDisposable {
    const string Baked = "baked-faces";

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-bake-fit-");
    readonly Func<string, string?, int, string?>? before = BakeFit.Geometry;

    public void Dispose() {
        BakeFit.Geometry = before;
        folder.Delete(true);
    }

    [Fact]
    public void A_mesh_changed_elsewhere_leaves_the_sets_bake_fitting() {
        var (bake, mesh) = Bake(Baked);
        BakeFit.Geometry = static (_, material, uvSet) => material == "Body" && uvSet == 0 ? Baked : "other";

        Assert.Null(BakeFit.Stale(bake, "now", new(0), mesh));
        Assert.Equal(BakeFit.UvSet, BakeFit.Stale(bake, "now", new(1), mesh));
    }

    [Fact]
    public void A_change_to_the_sets_own_faces_makes_its_bake_stale() {
        var (bake, mesh) = Bake(Baked);
        BakeFit.Geometry = static (_, _, _) => "moved";

        Assert.Equal(BakeFit.Mesh, BakeFit.Stale(bake, "now", new(0), mesh));
    }

    [Fact]
    public void A_bake_that_did_not_record_its_faces_is_stale_whenever_the_mesh_changed() {
        var (bake, mesh) = Bake(null);
        BakeFit.Geometry = static (_, _, _) => Baked;

        Assert.Equal(BakeFit.Mesh, BakeFit.Stale(bake, "now", new(0), mesh));
        Assert.Null(BakeFit.Stale(bake, "then", new(0), mesh));
    }

    [Fact]
    public void Without_the_mesh_file_any_change_to_the_mesh_is_stale() {
        var (bake, _) = Bake(Baked);
        BakeFit.Geometry = static (_, _, _) => Baked;

        Assert.Equal(BakeFit.Mesh, BakeFit.Stale(bake, "now", new(0)));
    }

    [Fact]
    public void A_set_whose_shape_changed_reads_its_bake_stale() {
        var wide = Shaped("wide", 2048, 1024);
        Assert.Null(BakeFit.Stale(wide, null, new(0, 2048, 1024)));
        Assert.Null(BakeFit.Stale(wide, null, new(0, 4096, 2048)));
        Assert.Null(BakeFit.Stale(wide, null, new(0)));
        Assert.Equal(BakeFit.Shape, BakeFit.Stale(wide, null, new(0, 1024, 1024)));
        Assert.Equal(BakeFit.UvSet, BakeFit.Stale(wide, null, new(1, 1024, 1024)));

        var square = Shaped("square", 1024, null);
        Assert.Null(BakeFit.Stale(square, null, new(0, 512, 512)));
        Assert.Equal(BakeFit.Shape, BakeFit.Stale(square, null, new(0, 1024, 512)));
    }

    [Theory]
    [InlineData(2048, 1024, 1024, null)]
    [InlineData(2048, 2048, 1024, 1024)]
    [InlineData(2048, 1024, 2048, 4096)]
    [InlineData(1024, 4096, 16, 16)]
    [InlineData(1024, 0, 512, null)]
    public void A_bake_keeps_a_sets_shape_at_its_width(int resolution, int width, int height, int? rows) =>
        Assert.Equal(rows, BakeFit.RowsAt(resolution, width, height));

    string Shaped(string name, int resolution, int? rows) {
        var bake = Path.Combine(folder.FullName, name);
        Directory.CreateDirectory(bake);
        BakeManifest manifest = new(
            "Body",
            "png16",
            new([new("position", "position.png", resolution, 0, rows)], 0, 0, [], false)
        );
        File.WriteAllBytes(
            Path.Combine(bake, $"Body.{LibraryLayout.BakeManifestFile}"),
            JsonSerializer.SerializeToUtf8Bytes(manifest, ContractsJsonContext.Default.BakeManifest)
        );
        return bake;
    }

    (string Bake, string Mesh) Bake(string? geometry) {
        var mesh = Path.Combine(folder.FullName, $"{Guid.NewGuid():N}.obj");
        File.WriteAllText(mesh, "v 0 0 0\n");
        var bake = Path.Combine(folder.FullName, "maps");
        Directory.CreateDirectory(bake);
        BakeManifest manifest = new(
            "Body",
            "png16",
            new([], 0, 0, [], false),
            "then",
            0,
            "tangent",
            "Body",
            geometry
        );
        File.WriteAllBytes(
            Path.Combine(bake, $"Body.{LibraryLayout.BakeManifestFile}"),
            JsonSerializer.SerializeToUtf8Bytes(manifest, ContractsJsonContext.Default.BakeManifest)
        );
        return (bake, mesh);
    }
}
