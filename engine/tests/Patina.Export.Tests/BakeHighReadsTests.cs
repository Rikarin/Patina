using Patina.Contracts;
using Patina.Library;
using System.Globalization;

namespace Patina.Export.Tests;

/// <summary>
///     A change to a high part rebakes only the texture sets whose own parts match it by name, when nothing they bake
///     reaches past their parts, and every set otherwise (#151, ADR 0008).
/// </summary>
public sealed class BakeHighReadsTests : IDisposable {
    /// <summary>The low mesh: a bolt on the atlas's left half and a plate on the right, each its own set.</summary>
    const string Low = """
                       o bolt_low
                       v 0 0 0
                       v 1 0 0
                       v 1 1 0
                       v 0 1 0
                       vt 0.05 0.05
                       vt 0.45 0.05
                       vt 0.45 0.95
                       vt 0.05 0.95
                       vn 0 0 1
                       usemtl Bolt
                       f 1/1/1 2/2/1 3/3/1 4/4/1
                       o plate_low
                       v 2 0 0
                       v 3 0 0
                       v 3 1 0
                       v 2 1 0
                       vt 0.55 0.05
                       vt 0.95 0.05
                       vt 0.95 0.95
                       vt 0.55 0.95
                       usemtl Plate
                       f 5/5/1 6/6/1 7/7/1 8/8/1
                       """;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-high-reads-");

    readonly ToolCatalog tools = new(new Dictionary<string, string>(), "1");

    public void Dispose() => folder.Delete(true);

    string Into(string name) => Path.Combine(folder.FullName, name);

    /// <summary>
    ///     The high mesh: each part a fan a little above its low part, its centre at its own height, and a frame that
    ///     matches nothing and holds the box still.
    /// </summary>
    string High(float bolt, float plate, string plateName = "plate_high") {
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"""
             o bolt_high
             v 0 0 0.02
             v 1 0 0.02
             v 1 1 0.02
             v 0 1 0.02
             v 0.5 0.5 {bolt}
             vn 0 0 1
             f 1//1 2//1 5//1
             f 2//1 3//1 5//1
             f 3//1 4//1 5//1
             f 4//1 1//1 5//1
             o {plateName}
             v 2 0 0.02
             v 3 0 0.02
             v 3 1 0.02
             v 2 1 0.02
             v 2.5 0.5 {plate}
             f 6//1 7//1 10//1
             f 7//1 8//1 10//1
             f 8//1 9//1 10//1
             f 9//1 6//1 10//1
             o frame_high
             v -1 -1 0
             v 4 -1 1
             v 4 2 0
             f 11//1 12//1 13//1

             """
        );
        File.WriteAllText(Into("low.obj"), Low);
        File.WriteAllText(Into("high.obj"), text);
        return Into("high.obj");
    }

    BakeRequest Request(string set, Func<BakeOptions, BakeOptions>? change = null) {
        BakeOptions options = new() {
            Resolution = 32,
            Maps = ["normal", "height", "ao", "opacity"],
            Rays = new() { MatchByName = true, Front = 0.05, Rear = 0.05 },
            Occlusion = new() { WithinPart = true, Rays = 8 }
        };
        return new() {
            High = [Into("high.obj")],
            Low = Into("low.obj"),
            Material = set,
            Output = Into("out"),
            Options = change?.Invoke(options) ?? options
        };
    }

    /// <summary>
    ///     The key as the Server works it out, from what the Worker read of each file (#285), and checked against the
    ///     key the command line works out reading the files itself.
    /// </summary>
    string Key(BakeRequest request) {
        var key = BakeKeyReads.KeyOf(request, tools, new WorkerRead()).Hex;
        Assert.Equal(key, BakeKeyReads.KeyOf(request, tools, MeshFileReads.Instance).Hex);
        return key;
    }

    /// <summary>What a Server keeps of the Worker's read of each file, answered as it answers, read afresh.</summary>
    sealed class WorkerRead : IMeshSetReads {
        public int? Udim(string file, string? material, int uvSet) => Find(file, material, uvSet)?.Udim;

        public string? Geometry(string file, string? material, int uvSet) => Find(file, material, uvSet)?.Geometry;

        public IReadOnlyList<string> Pictures(string file) => MeshFileReads.All(file).Pictures;

        public IReadOnlyList<string>? PartNames(string file, string? material, int uvSet) =>
            Find(file, material, uvSet)?.Parts;

        public MeshHighRead? High(string file) => MeshFileReads.All(file).High;

        static MeshSetRead? Find(string file, string? material, int uvSet) =>
            MeshFileReads.All(file)
                .Sets.FirstOrDefault(set => set.UvSet == uvSet && set.Material == (material ?? string.Empty));
    }

    [Fact]
    public void A_change_to_another_part_keeps_a_sets_key_and_a_change_to_its_own_does_not() {
        High(0.02f, 0.02f);
        var (bolt, plate) = (Key(Request("Bolt")), Key(Request("Plate")));
        High(0.02f, 0.04f);
        var (boltAfter, plateAfter) = (Key(Request("Bolt")), Key(Request("Plate")));
        High(0.04f, 0.04f);

        Assert.Equal(bolt, boltAfter);
        Assert.NotEqual(plate, plateAfter);
        Assert.NotEqual(boltAfter, Key(Request("Bolt")));
    }

    /// <summary>
    ///     The key is only kept where it is true: a set baked again from the changed file is the same, byte for byte,
    ///     though the plate now stands high enough beside the bolt to darken it when occlusion reaches across parts.
    /// </summary>
    [Fact]
    public void A_set_whose_key_is_kept_bakes_the_same_after_the_change() {
        byte[][] Maps(string into, bool within) {
            var request = Request(
                "Bolt",
                options => options with { Occlusion = options.Occlusion with { WithinPart = within } }
            );
            var manifest = MeshMapBakeRun.Run(request, Into(into), 0, null, TestContext.Current.CancellationToken);
            return [..manifest.Result.Maps.Select(map => File.ReadAllBytes(Path.Combine(Into(into), map.Path)))];
        }

        High(0.02f, 0.02f);
        var before = Maps("before", true);
        var acrossBefore = Maps("across-before", false);
        High(0.02f, 0.8f);
        var after = Maps("after", true);
        var acrossAfter = Maps("across-after", false);

        Assert.Equal(4, before.Length);
        Assert.Equal(before, after);
        Assert.NotEqual(acrossBefore, acrossAfter);
    }

    /// <summary>Anything reaching past the set's parts reads the high mesh whole: any change rebakes it.</summary>
    [Theory]
    [InlineData("unmatched")]
    [InlineData("occlusion")]
    [InlineData("id")]
    [InlineData("colour")]
    [InlineData("curvature")]
    [InlineData("rounding")]
    [InlineData("edges")]
    [InlineData("tileable")]
    [InlineData("renamed")]
    public void Anything_that_reaches_past_the_parts_rebakes_the_set(string what) {
        Func<BakeOptions, BakeOptions> change = what switch {
            "unmatched" => static options => options with { Rays = options.Rays with { MatchByName = false } },
            "occlusion" => static options => options with { Occlusion = options.Occlusion with { WithinPart = false } },
            "rounding" => static options => options with { Rounding = options.Rounding with { Enabled = true } },
            "edges" => static options => options with {
                Maps = ["edges"], Edges = options.Edges with { Mesh = "high" }
            },
            "tileable" => static options => options with { Tileable = true },
            "renamed" => static options => options,
            _ => options => options with { Maps = [what] }
        };
        High(0.02f, 0.02f);
        var before = Key(Request("Bolt", change));
        High(0.02f, 0.04f, what == "renamed" ? "bolt_high" : "plate_high");

        Assert.NotEqual(before, Key(Request("Bolt", change)));
    }

    /// <summary>A map of the low mesh's own edges stays within the parts, as the maps it is read beside do.</summary>
    [Fact]
    public void The_low_meshs_edges_stay_within_the_parts() {
        Assert.True(
            BakeHighReads.WithinParts(
                Request(
                        "Bolt",
                        static options => options with { Maps = ["edges", "normal"], Edges = new() { Mesh = "low" } }
                    )
                    .Options
            )
        );
        Assert.False(BakeHighReads.WithinParts(new()));
    }
}
