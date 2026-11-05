using Patina.Bench;
using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Graphs;
using Patina.Jobs;
using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Interchange;

namespace Patina.Export.Tests;

/// <summary>The steps that need no GPU.</summary>
public sealed class OutputBuildersTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-output-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    static OutputBuilders NoGpu() =>
        new(
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

        var manifest = NoGpu()
            .Build(
                new(
                    BuildTools.MeshLod,
                    [high],
                    [],
                    new Dictionary<string, string> { ["ratios"] = "0.5,0.1" },
                    made,
                    null
                ),
                1,
                Token
            );

        Assert.Equal(["lod1.glb", "lod2.glb"], manifest.Files);
        Assert.Empty(manifest.Maps);
        var half = MeshMapBakeRun.Read(Path.Combine(made, "lod1.glb")).Mesh;
        var tenth = MeshMapBakeRun.Read(Path.Combine(made, "lod2.glb")).Mesh;
        Assert.InRange(half.CornerCount / 3.0, whole * 0.4, whole * 0.6);
        Assert.InRange(tenth.CornerCount / 3.0, whole * 0.05, whole * 0.2);
    }

    [Fact]
    public void A_level_keeps_the_source_s_coordinates_so_its_maps_still_fit() {
        const int Side = 32;
        List<Vector3> positions = [];
        List<Vector3> normals = [];
        List<Vector2> coordinates = [];
        List<int> indices = [];
        for (var y = 0; y <= Side; y++) {
            for (var x = 0; x <= Side; x++) {
                positions.Add(new(x, y, MathF.Sin(x * 0.2f) * MathF.Sin(y * 0.2f)));
                normals.Add(Vector3.UnitZ);
                coordinates.Add(new(x / (float)Side, y / (float)Side));
            }
        }

        for (var y = 0; y < Side; y++) {
            for (var x = 0; x < Side; x++) {
                var at = y * (Side + 1) + x;
                indices.AddRange([at, at + 1, at + Side + 2, at, at + Side + 2, at + Side + 1]);
            }
        }

        var source = Path.Combine(folder.FullName, "grid.glb");
        ModelWriter.Write(
            source,
            [
                new MeshData {
                    Name = "grid",
                    Positions = [..positions],
                    Normals = [..normals],
                    TexCoords = [..coordinates],
                    Indices = [..indices],
                    Bounds = new(new(0f, 0f, -1f), new(Side, Side, 1f))
                }
            ]
        );
        var made = Path.Combine(folder.FullName, "made");

        NoGpu()
            .Build(
                new(
                    BuildTools.MeshLod,
                    [source],
                    [],
                    new Dictionary<string, string> { ["ratios"] = "0.25" },
                    made,
                    null
                ),
                1,
                Token
            );

        var level = MeshMapBakeRun.Read(Path.Combine(made, "lod1.glb")).Mesh;
        Assert.InRange(level.CornerCount / 3, 1, Side * Side);
        Assert.Equal(level.CornerCount, level.TexCoords.Length);
        foreach (var corner in level.TexCoords) {
            var u = corner.X * Side;
            Assert.Equal(MathF.Round(u), u, 3);
        }
    }

    [Fact]
    public void A_ratio_that_is_no_ratio_is_refused_and_leaves_nothing() {
        var meshes = Path.Combine(folder.FullName, "meshes");
        BakeFixture.Write(meshes, 0.02f);
        var made = Path.Combine(folder.FullName, "made");

        var refused = Assert.Throws<BuildStepFailedException>(() => NoGpu()
            .Build(
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
        var refused = Assert.Throws<BuildStepFailedException>(() => NoGpu()
            .Build(
                new(
                    "ambient-occlusion",
                    [],
                    [],
                    new Dictionary<string, string>(),
                    Path.Combine(folder.FullName, "x"),
                    null
                ),
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

        Assert.Equal(
            191,
            Vixen.Core.Imaging.PngCodec.Decode16(File.ReadAllBytes(Path.Combine(made, "t0.png"))).Samples[0] >> 8
        );
    }

    ExportSource Source(string name, float value) {
        var path = Directory.CreateDirectory(Path.Combine(folder.FullName, name)).FullName;
        var map = MapPictures.Write(path, "roughness", new(1, 1, 1, [value]), true, null);
        return new(path, new("texture-graph", [map], [], []));
    }
}
