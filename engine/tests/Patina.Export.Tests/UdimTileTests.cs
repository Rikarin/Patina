using Patina.Jobs;
using System.Globalization;
using Vixen.Geometry.Uv;

namespace Patina.Export.Tests;

/// <summary>
///     A texture set paints the one UDIM tile its layout sits on (#20): a set on tile 1002 is read moved home onto
///     1001, so a bake fills its own maps, and says which tile it is on so its files can be named by it.
/// </summary>
public sealed class UdimTileTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-udim-");

    public void Dispose() => folder.Delete(true);

    string Into(string name) => Path.Combine(folder.FullName, name);

    /// <summary>Two quads, Body laid out on tile 1001 and Strap on tile 1002, each a material of its own.</summary>
    string TwoSets() {
        var mesh = Into("sets.obj");
        File.WriteAllText(Into("sets.mtl"), "newmtl Body\nKd 1 0 0\nnewmtl Strap\nKd 0 0 1\n");
        File.WriteAllText(
            mesh,
            """
            mtllib sets.mtl
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            v 2 0 0
            v 3 0 0
            v 3 1 0
            v 2 1 0
            vt 0.1 0.1
            vt 0.9 0.1
            vt 0.9 0.9
            vt 0.1 0.9
            vt 1.1 0.1
            vt 1.9 0.1
            vt 1.9 0.9
            vt 1.1 0.9
            vn 0 0 1
            usemtl Body
            f 1/1/1 2/2/1 3/3/1 4/4/1
            usemtl Strap
            f 5/5/1 6/6/1 7/7/1 8/8/1

            """
        );
        return mesh;
    }

    [Fact]
    public void A_set_on_tile_1002_is_read_home_and_names_its_tile() {
        var mesh = TwoSets();

        var (strap, udim) = MeshMapBakeRun.Placed(mesh, "Strap", 0);

        Assert.Equal(1002, udim);
        Assert.Equal(new UvTile(0, 0), UvTiles.Only(OneTile.Triangles(strap.Mesh)));
        Assert.Equal(1001, MeshMapBakeRun.Placed(mesh, "Body", 0).Udim);
    }

    [Fact]
    public void A_mesh_whose_sets_are_on_two_tiles_stays_where_it_is_and_says_the_first() {
        var (whole, udim) = MeshMapBakeRun.Placed(TwoSets(), null, 0);

        Assert.Equal(1001, udim);
        Assert.Equal(2, UvTiles.Reached(OneTile.Triangles(whole.Mesh)).Count);
    }

    /// <summary>A bake of a set on tile 1002 was refused, because it filled 1001 and left 1002 empty.</summary>
    [Fact]
    public void A_set_on_tile_1002_bakes_into_its_own_maps() {
        var mesh = TwoSets();

        var manifest = MeshMapBakeRun.Run(
            new() {
                High = [mesh],
                Low = mesh,
                Material = "Strap",
                Output = folder.FullName,
                Options = new() { Resolution = 64, Format = BakeInputs.Png16, Maps = ["normal"] }
            },
            Into("baked"),
            0,
            null,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(manifest.Result.Maps, static map => map.Map == "normal");
        var covered = Assert.Single(
            manifest.Result.Log,
            static line => line.Contains("held a chart", StringComparison.Ordinal)
        );
        Assert.True(int.Parse(covered.Split(' ')[0], CultureInfo.InvariantCulture) > 64 * 64 / 2, covered);
    }

    /// <summary>
    ///     What the Server keeps of a mesh (#285) names each set's tile as reading it alone does, and a set named for
    ///     no material of the mesh takes the whole mesh's.
    /// </summary>
    [Fact]
    public void One_read_of_every_set_names_each_set_its_tile() {
        var mesh = TwoSets();

        var all = MeshFileReads.All(mesh);
        var kept = new Kept(all);

        Assert.Equal(3, all.Sets.Count);
        Assert.Equal(1002, SetUdim.Of(kept, mesh, "Strap", 0));
        Assert.Equal(1001, SetUdim.Of(kept, mesh, "Body", 0));
        Assert.Equal(1001, SetUdim.Of(kept, mesh, "Renamed", 0));
        foreach (var set in new[] { "Strap", "Body", "Renamed" }) {
            Assert.Equal(SetUdim.Of(MeshFileReads.Instance, mesh, set, 0), SetUdim.Of(kept, mesh, set, 0));
        }
    }

    /// <summary>What a Server keeps of one read, answered as it answers.</summary>
    sealed class Kept(Contracts.MeshSetsRead read) : Library.IMeshSetReads {
        public int? Udim(string file, string? material, int uvSet) => Find(material, uvSet)?.Udim;

        public string? Geometry(string file, string? material, int uvSet) => Find(material, uvSet)?.Geometry;

        public IReadOnlyList<string> Pictures(string file) => read.Pictures;

        Contracts.MeshSetRead? Find(string? material, int uvSet) =>
            read.Sets.FirstOrDefault(set => set.UvSet == uvSet && set.Material == (material ?? string.Empty));
    }

    [Theory]
    [InlineData(1001, "u1_v1")]
    [InlineData(1010, "u10_v1")]
    [InlineData(1021, "u1_v3")]
    public void Mari_names_a_tile_by_its_column_and_row_from_one(int udim, string name) =>
        Assert.Equal(name, OneTile.MariName(udim));
}
