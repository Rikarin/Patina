using Patina.Jobs;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>
///     An automatic cage (#148), from files to files: a strap above the patch and a body nearer below it, and the
///     height map says which one the rays took.
/// </summary>
public sealed class BakeAutomaticCageTests : IDisposable {
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

    /// <summary>A body 0.01 under the patch and a strap 0.03 over it, both facing up.</summary>
    const string Layers = """
                          v -0.5 -0.5 -0.01
                          v 1.5 -0.5 -0.01
                          v 1.5 1.5 -0.01
                          v -0.5 1.5 -0.01
                          v -0.5 -0.5 0.03
                          v 1.5 -0.5 0.03
                          v 1.5 1.5 0.03
                          v -0.5 1.5 0.03
                          vn 0 0 1
                          f 1//1 2//1 3//1 4//1
                          f 5//1 6//1 7//1 8//1
                          """;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-auto-cage-");

    public void Dispose() => folder.Delete(true);

    string Into(string name) => Path.Combine(folder.FullName, name);

    /// <summary>The height map's middle texel, as a share of its range: above a half is above the patch.</summary>
    float Middle(bool automatic) {
        File.WriteAllText(Into("patch.obj"), Patch);
        File.WriteAllText(Into("layers.obj"), Layers);
        var into = Into(automatic ? "caged" : "nearer");
        var manifest = MeshMapBakeRun.Run(
            new() {
                High = [Into("layers.obj")],
                Low = Into("patch.obj"),
                Output = into,
                Options = new() {
                    Resolution = 16,
                    Format = BakeInputs.Png8,
                    Maps = ["height"],
                    Height = new() { Range = "rays" },
                    Rays = new() { Units = "absolute", Front = 0.05, Rear = 0.05, AutomaticCage = automatic }
                }
            },
            into,
            0,
            null,
            TestContext.Current.CancellationToken
        );
        var map = PngCodec.Decode(File.ReadAllBytes(Path.Combine(into, manifest.Result.Maps[0].Path)));
        return map.Pixels[(8 * map.Width + 8) * 4] / 255f;
    }

    [Fact]
    public void The_rays_take_the_strap_from_an_automatic_cage_and_the_nearer_body_without_one() {
        var (caged, nearer) = (Middle(true), Middle(false));

        Assert.True(caged > 0.5f, $"From the cage, the height read {caged}.");
        Assert.True(nearer < 0.5f, $"Without one, the height read {nearer}.");
    }
}
