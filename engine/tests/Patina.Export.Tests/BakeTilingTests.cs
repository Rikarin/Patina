using Patina.Contracts;
using Patina.Jobs;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>
///     A tileable bake (#212): a high strip that spills past the left edge of a flat patch is met past the right edge
///     too, where the repeat lays it, so the maps repeat without a seam.
/// </summary>
public sealed class BakeTilingTests : IDisposable {
    const int Side = 64;

    /// <summary>The patch: a unit square facing up, laid out over the whole square.</summary>
    const string Patch = """
                         v 0 0 0
                         v 1 0 0
                         v 1 1 0
                         v 0 1 0
                         vt 0 0
                         vt 1 0
                         vt 1 1
                         vt 0 1
                         vn 0 0 1
                         f 1/1/1 2/2/1 3/3/1 4/4/1
                         """;

    /// <summary>A strip a little above the patch, from a tenth past its left edge to a sixth into it.</summary>
    const string Strip = """
                         v -0.1 0 0.02
                         v 0.15 0 0.02
                         v 0.15 1 0.02
                         v -0.1 1 0.02
                         vn 0 0 1
                         f 1//1 2//1 3//1 4//1
                         """;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-tiling-");

    public void Dispose() => folder.Delete(true);

    string Into(string name) => Path.Combine(folder.FullName, name);

    /// <summary>The share of each column's texels a ray found the high mesh from, left to right.</summary>
    float[] Found(bool tileable) {
        File.WriteAllText(Into("patch.obj"), Patch);
        File.WriteAllText(Into("strip.obj"), Strip);
        var into = Into(tileable ? "tiled" : "once");
        var manifest = MeshMapBakeRun.Run(
            new() {
                High = [Into("strip.obj")],
                Low = Into("patch.obj"),
                Output = into,
                Options = new() {
                    Resolution = Side,
                    Format = BakeInputs.Png8,
                    Gutter = 0,
                    Maps = ["opacity"],
                    Tileable = tileable
                }
            },
            into,
            0,
            null,
            TestContext.Current.CancellationToken
        );
        var map = PngCodec.Decode(File.ReadAllBytes(Path.Combine(into, manifest.Result.Maps.Single().Path)));
        var columns = new float[map.Width];
        for (var y = 0; y < map.Height; y++) {
            for (var x = 0; x < map.Width; x++) {
                columns[x] += map.Pixels[(y * map.Width + x) * 4] / 255f / map.Height;
            }
        }

        return columns;
    }

    [Fact]
    public void A_tileable_bake_meets_past_the_right_edge_what_spills_past_the_left() {
        var once = Found(false);
        var tiled = Found(true);

        Assert.True(once[2] > 0.9f, $"The strip's own side found {once[2]}.");
        Assert.True(once[Side - 3] < 0.1f, $"Baked once, the right edge found {once[Side - 3]}.");
        Assert.True(tiled[Side - 3] > 0.9f, $"Tiled, the right edge found {tiled[Side - 3]}.");
        Assert.True(tiled[Side / 2] < 0.1f, $"Tiled, the middle found {tiled[Side / 2]}.");
    }

    /// <summary>A bake that does not tile keeps the key it had before tiling could be chosen.</summary>
    [Fact]
    public void A_bake_that_does_not_tile_keeps_its_key() {
        File.WriteAllText(Into("patch.obj"), Patch);
        BakeRequest request = new() { High = [Into("patch.obj")], Low = Into("patch.obj"), Output = Into("out") };
        var tools = new Library.ToolCatalog(new Dictionary<string, string>(), "1");

        var unset = BakeInputs.KeyOf(request, tools);
        var off = BakeInputs.KeyOf(request with { Options = request.Options with { Tileable = false } }, tools);
        var on = BakeInputs.KeyOf(request with { Options = request.Options with { Tileable = true } }, tools);

        Assert.Equal(unset, off);
        Assert.NotEqual(unset, on);
    }
}
