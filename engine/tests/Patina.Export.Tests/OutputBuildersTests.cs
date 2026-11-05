using Patina.Bench;
using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Graphs;
using Patina.Jobs;
using Vixen.Interchange;

namespace Patina.Export.Tests;

/// <summary>The steps that need no GPU.</summary>
public sealed class OutputBuildersTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-output-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    static OutputBuilders NoGpu() => new(
        static () => throw new InvalidOperationException("This step needs no device."),
        static work => work()
    );

    [Fact]
    public void Levels_of_detail_get_smaller_keep_their_coordinates_and_read_back() {
        var meshes = Path.Combine(folder.FullName, "meshes");
        BakeFixture.Write(meshes, 0.1f);
        var high = Path.Combine(meshes, BakeFixture.HighFile);
        var whole = MeshMapBakeRun.Read(high).Mesh.CornerCount / 3;
        var made = Path.Combine(folder.FullName, "made");

        var manifest = NoGpu().Build(
            new(BuildTools.MeshLod, [high], [], new Dictionary<string, string> { ["ratios"] = "0.5,0.1" }, made, null),
            1,
            Token
        );

        Assert.Equal(["lod1.glb", "lod2.glb"], manifest.Files);
        Assert.Empty(manifest.Maps);
        var half = MeshMapBakeRun.Read(Path.Combine(made, "lod1.glb")).Mesh;
        var tenth = MeshMapBakeRun.Read(Path.Combine(made, "lod2.glb")).Mesh;
        Assert.InRange(half.CornerCount / 3, whole * 0.4, whole * 0.6);
        Assert.InRange(tenth.CornerCount / 3, whole * 0.05, whole * 0.2);
        Assert.Equal(tenth.CornerCount, tenth.TexCoords.Length);
    }

    [Fact]
    public void A_ratio_that_is_no_ratio_is_refused_and_leaves_nothing() {
        var meshes = Path.Combine(folder.FullName, "meshes");
        BakeFixture.Write(meshes, 0.02f);
        var made = Path.Combine(folder.FullName, "made");

        var refused = Assert.Throws<BuildStepFailedException>(() => NoGpu().Build(
                new(
                    BuildTools.MeshLod,
                    [Path.Combine(meshes, BakeFixture.LowFile)],
                    [],
                    new Dictionary<string, string> { ["ratios"] = "1.5" },
                    made,
                    null
                ),
                1,
                Token
            )
        );

        Assert.Contains("1.5", refused.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(made));
        Assert.Empty(Directory.EnumerateDirectories(folder.FullName, "*.part"));
    }

    [Fact]
    public void A_tool_nothing_builds_says_so() {
        var refused = Assert.Throws<BuildStepFailedException>(() => NoGpu().Build(
                new("ambient-occlusion", [], [], new Dictionary<string, string>(), Path.Combine(folder.FullName, "x"), null),
                1,
                Token
            )
        );

        Assert.Contains("ambient-occlusion", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_later_source_wins_a_map_they_share() {
        var low = Source("low", 0.25f);
        var high = Source("high", 0.75f);
        var preset = """
                     version: 1
                     id: p
                     name: P
                     normal: y-up
                     textures:
                       - file: x
                         format: png8
                         space: linear
                         channels: [roughness]
                     """;
        var made = Path.Combine(folder.FullName, "export");

        OutputBuilders.Export(new(preset, [low, high], made), Token);

        Assert.Equal(191, Vixen.Core.Imaging.PngCodec.Decode16(File.ReadAllBytes(Path.Combine(made, "t0.png"))).Samples[0] >> 8);
    }

    ExportSource Source(string name, float value) {
        var path = Directory.CreateDirectory(Path.Combine(folder.FullName, name)).FullName;
        var map = MapPictures.Write(path, "roughness", new(1, 1, 1, [value]), true, null);
        return new(path, new("texture-graph", [map], [], []));
    }
}
