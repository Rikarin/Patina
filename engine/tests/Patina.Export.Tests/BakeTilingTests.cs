using Patina.Contracts;
using Patina.Jobs;
using System.Globalization;
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

    /// <summary>A second strip in a file of its own, so every ID source tells it from the first.</summary>
    const string Strip2 = """
                          v 0.6 0 0.02
                          v 0.7 0 0.02
                          v 0.7 1 0.02
                          v 0.6 1 0.02
                          vn 0 0 1
                          f 1//1 2//1 3//1 4//1
                          """;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-tiling-");

    public void Dispose() => folder.Delete(true);

    string Into(string name) => Path.Combine(folder.FullName, name);

    /// <summary>An OBJ's positions turned about <c>z</c> by <paramref name="degrees" />.</summary>
    static string Turned(string obj, float degrees) {
        var (sin, cos) = MathF.SinCos(degrees * MathF.PI / 180f);
        return string.Join('\n', obj.Split('\n').Select(line => TurnedLine(line.Trim(), sin, cos)));
    }

    static string TurnedLine(string line, float sin, float cos) {
        if (line.Split(' ') is not ["v", var xs, var ys, var zs]) {
            return line;
        }

        var x = float.Parse(xs, CultureInfo.InvariantCulture);
        var y = float.Parse(ys, CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"v {x * cos - y * sin} {x * sin + y * cos} {zs}");
    }

    /// <summary>The first map of a bake of the strips onto the patch, all turned about <c>z</c> together.</summary>
    Bitmap Baked(string into, BakeOptions options, float degrees = 0f, bool both = false) {
        File.WriteAllText(Into("patch.obj"), Turned(Patch, degrees));
        File.WriteAllText(Into("strip.obj"), Turned(Strip, degrees));
        File.WriteAllText(Into("strip2.obj"), Turned(Strip2, degrees));
        into = Into(into);
        var manifest = MeshMapBakeRun.Run(
            new() {
                High = both ? [Into("strip.obj"), Into("strip2.obj")] : [Into("strip.obj")],
                Low = Into("patch.obj"),
                Output = into,
                Options = options with { Resolution = Side, Format = BakeInputs.Png8, Gutter = 0 }
            },
            into,
            0,
            null,
            TestContext.Current.CancellationToken
        );
        return PngCodec.Decode(File.ReadAllBytes(Path.Combine(into, manifest.Result.Maps[0].Path)));
    }

    /// <summary>The share of each column's texels a ray found the high mesh from, left to right.</summary>
    float[] Found(bool tileable, float degrees = 0f) {
        var map = Baked(
            $"{tileable}-{degrees}",
            new() { Maps = ["opacity"], Tileable = tileable },
            degrees
        );
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

    /// <summary>
    ///     The repeat follows the patch's texture coordinates, not the axes: a patch turned on its plane tiles along its
    ///     own sides (#287). Along the axes, the copies landed beside the turned patch's box and missed its edge.
    /// </summary>
    [Fact]
    public void A_turned_patch_tiles_along_its_own_sides() {
        var tiled = Found(true, 30f);

        Assert.True(tiled[2] > 0.9f, $"The strip's own side found {tiled[2]}.");
        Assert.True(tiled[Side - 3] > 0.9f, $"Tiled, the right edge found {tiled[Side - 3]}.");
        Assert.True(tiled[Side / 2] < 0.1f, $"Tiled, the middle found {tiled[Side / 2]}.");
    }

    /// <summary>
    ///     A copy of the high mesh laid beside the patch is the same mesh to the ID map, by every source, so the strip
    ///     past the right edge reads the colour it reads past the left (#287). Each copy used to be numbered apart.
    /// </summary>
    [Theory]
    [InlineData("auto")]
    [InlineData("material")]
    [InlineData("mesh")]
    [InlineData("shell")]
    public void A_tileable_id_map_gives_each_copy_its_originals_colour(string source) {
        var map = Baked(source, new() { Maps = ["id"], Tileable = true, Id = new() { Source = source } }, both: true);

        int Colour(int x) {
            var at = (Side / 2 * map.Width + x) * 4;
            return map.Pixels[at] << 16 | map.Pixels[at + 1] << 8 | map.Pixels[at + 2];
        }

        Assert.NotEqual(Colour(2), Colour(Side * 13 / 20));
        Assert.Equal(Colour(2), Colour(Side - 3));
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
