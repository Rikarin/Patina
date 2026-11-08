using Patina.Bench;
using Patina.Contracts;
using Patina.Jobs;
using System.Security.Cryptography;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>A bake from paths to files, on the generated hard-edged pair, with no Server anywhere.</summary>
public sealed class MeshMapBakeRunTests : IClassFixture<MeshMapBakeRunTests.Pair>, IDisposable {
    /// <summary>The pair, written once: a coarse high is all these need.</summary>
    public sealed class Pair : IDisposable {
        readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-pair-");

        public Pair() {
            BakeFixture.Write(folder.FullName, 0.02f);
        }

        public string High => Path.Combine(folder.FullName, BakeFixture.HighFile);

        public string Low => Path.Combine(folder.FullName, BakeFixture.LowFile);

        public void Dispose() => folder.Delete(true);
    }

    readonly Pair pair;
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-maps-");

    public MeshMapBakeRunTests(Pair pair) {
        this.pair = pair;
    }

    public void Dispose() => folder.Delete(true);

    string Into(string name) => Path.Combine(folder.FullName, name);

    BakeRequest Request(string format = BakeInputs.Png16) =>
        new() {
            High = [pair.High],
            Low = pair.Low,
            Output = folder.FullName,
            Options = new() {
                Resolution = 128, Format = format, Maps = ["normal", "height", "ao"], Occlusion = new() { Rays = 8 }
            }
        };

    static BakeRequest With(BakeRequest request, Func<BakeOptions, BakeOptions> change) =>
        request with { Options = change(request.Options) };

    /// <summary>A skew map took a PNG only, and the dialog offered more, so the rest failed after queueing.</summary>
    [Theory]
    [InlineData(".tga")]
    [InlineData(".exr")]
    [InlineData(".png")]
    public void A_skew_map_can_be_any_picture_the_dialog_offers(string extension) {
        var map = Into($"skew{extension}");
        byte[] tga = [0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 2, 0, 8, 0x20, 128, 128, 128, 128];
        File.WriteAllBytes(
            map,
            extension switch {
                ".tga" => tga,
                ".exr" => ExrCodec.Encode(new(2, 2, 1, [0.5f, 0.5f, 0.5f, 0.5f])),
                _ => PngCodec.Encode16(2, 2, 1, new ushort[] { 0x8000, 0x8000, 0x8000, 0x8000 })
            }
        );

        var manifest = MeshMapBakeRun.Run(
            With(Request(), options => options with { Rays = options.Rays with { Skew = 0.5, SkewMap = map } }),
            Into("baked"),
            0,
            null,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(manifest.Result.Maps, static baked => baked.Map == "normal");
    }

    /// <summary>A set whose material has gone baked the whole model into its atlas, overlaps and all.</summary>
    [Fact]
    public void A_texture_set_whose_material_has_gone_is_refused_by_name() {
        var low = Into("low.obj");
        BakeFixture.WriteTwoMaterials(low);

        var refused = Assert.Throws<InvalidDataException>(() => MeshMapBakeRun.Read(low, "Renamed"));

        Assert.Contains("Renamed", refused.Message, StringComparison.Ordinal);
        Assert.Contains("Strap", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>Two quads side by side in the file, the second laid out on UDIM tile 1002.</summary>
    internal const string TwoTiles = """
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
                                     f 1/1 2/2 3/3 4/4
                                     f 5/5 6/6 7/7 8/8
                                     """;

    /// <summary>
    ///     A bake filled tile 1001, left 1002 empty and said nothing, because it only warned when no texel was covered
    ///     at all. A set laid out past the first tile is refused, by name, until Patina bakes UDIM.
    /// </summary>
    [Fact]
    public void A_low_mesh_on_a_second_udim_tile_is_refused_by_name() {
        var low = Into("tiles.obj");
        File.WriteAllText(low, TwoTiles);

        var refused = Assert.Throws<InvalidDataException>(() => MeshMapBakeRun.Run(
                Request() with { High = [low], Low = low },
                Into("baked"),
                0,
                null,
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("1002", refused.Message, StringComparison.Ordinal);
        Assert.Contains("UDIM", refused.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Into("baked")));
    }

    /// <summary>The low was cut to its set and the cage was not, so their layouts could never agree.</summary>
    [Fact]
    public void A_cage_is_cut_to_the_same_set_as_the_low_mesh() {
        var low = Into("low.obj");
        BakeFixture.WriteTwoMaterials(low);
        var cage = Into("cage.obj");
        BakeFixture.WriteTwoMaterials(cage, 0.1f);
        Assert.Equal(12, MeshMapBakeRun.Read(low).Mesh.CornerCount);
        Assert.Equal(6, MeshMapBakeRun.Read(low, "Strap").Mesh.CornerCount);

        var manifest = MeshMapBakeRun.Run(
            Request() with { High = [low], Low = low, Cage = cage, Material = "Strap" },
            Into("baked"),
            0,
            null,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(manifest.Result.Maps, static map => map.Map == "normal");
    }

    [Fact]
    public void A_bake_leaves_a_manifest_and_a_file_for_each_map() {
        var manifest = MeshMapBakeRun.Run(Request(), Into("bake"), 0, null, TestContext.Current.CancellationToken);

        Assert.Equal(["normal", "height", "ao"], manifest.Result.Maps.Select(static map => map.Map));
        foreach (var map in manifest.Result.Maps) {
            Assert.True(File.Exists(Path.Combine(Into("bake"), map.Path)));
            Assert.True(File.Exists(Path.Combine(Into("bake"), $"{map.Map}.preview.png")));
        }

        Assert.Equal(manifest, BakeCache.ReadManifest(Into("bake")), new ByName());
        Assert.True(manifest.Result.Covered > 0);
        Assert.True(
            manifest.Result.Missed * 20 < manifest.Result.Covered,
            $"{manifest.Result.Missed} texels found nothing."
        );
    }

    sealed class ByName : IEqualityComparer<BakeManifest?> {
        public bool Equals(BakeManifest? x, BakeManifest? y) => x?.Name == y?.Name && x?.Format == y?.Format;

        public int GetHashCode(BakeManifest? value) => 0;
    }

    /// <summary>A normal map of a flat side, in each format, is the same picture.</summary>
    [Theory]
    [InlineData(BakeInputs.Png8)]
    [InlineData(BakeInputs.Png16)]
    [InlineData(BakeInputs.Exr16)]
    [InlineData(BakeInputs.Exr32)]
    public void Every_format_reads_back_as_the_same_picture(string format) {
        var manifest = MeshMapBakeRun.Run(
            Request(format),
            Into("bake"),
            0,
            null,
            TestContext.Current.CancellationToken
        );
        var reference = PngCodec.Load(Path.Combine(Into("bake"), "normal.preview.png"));
        var normal = manifest.Result.Maps.Single(static map => map.Map == "normal");
        var read = BakedMapPicture.Read(Path.Combine(Into("bake"), normal.Path), normal, format);

        var worst = 0;
        for (var at = 0; at < reference.Pixels.Length; at++) {
            worst = Math.Max(worst, Math.Abs(reference.Pixels[at] - read.Pixels[at]));
        }

        Assert.True(worst <= 1, $"Off by {worst} levels.");
    }

    /// <summary>A map's depth is real: a 16-bit file holds levels an 8-bit one cannot.</summary>
    [Fact]
    public void A_sixteen_bit_map_has_more_than_256_levels() {
        var manifest = MeshMapBakeRun.Run(
            With(Request(), static options => options with { Resolution = 256 }),
            Into("bake"),
            0,
            null,
            TestContext.Current.CancellationToken
        );
        var file = manifest.Result.Maps.Single(static map => map.Map == "normal").Path;
        var (_, _, _, samples) = PngCodec.Decode16(File.ReadAllBytes(Path.Combine(Into("bake"), file)));

        Assert.True(samples.Distinct().Count() > 256);
        Assert.Contains(samples, static sample => sample % 257 != 0);
    }

    /// <summary>The bytes are the same however many threads made them, which the build key promises.</summary>
    [Fact]
    public void The_same_bake_is_the_same_bytes_at_any_thread_count() {
        var one = MeshMapBakeRun.Run(Request(), Into("one"), 1, null, TestContext.Current.CancellationToken);
        MeshMapBakeRun.Run(Request(), Into("many"), 8, null, TestContext.Current.CancellationToken);

        foreach (var map in one.Result.Maps) {
            Assert.Equal(
                SHA256.HashData(File.ReadAllBytes(Path.Combine(Into("one"), map.Path))),
                SHA256.HashData(File.ReadAllBytes(Path.Combine(Into("many"), map.Path)))
            );
        }
    }

    /// <summary>A stopped bake leaves nothing a reader could mistake for a result.</summary>
    [Fact]
    public void A_cancelled_bake_leaves_nothing_behind() {
        using var stop = new CancellationTokenSource();
        var heard = 0;

        Assert.ThrowsAny<OperationCanceledException>(() =>
            MeshMapBakeRun.Run(
                With(Request(), static options => options with { Resolution = 512, Occlusion = new() { Rays = 64 } }),
                Into("bake"),
                0,
                fraction => {
                    if (fraction > 0.06 && Interlocked.Increment(ref heard) == 1) {
                        stop.Cancel();
                    }
                },
                stop.Token
            )
        );

        Assert.Empty(folder.GetFileSystemInfos());
    }

    /// <summary>The strap lies close over the body. Named parts keep it out of the body's map.</summary>
    [Fact]
    public void Matching_by_name_keeps_the_strap_out_of_the_body_and_it_shows() {
        BakeManifest Bake(bool match, string name) =>
            MeshMapBakeRun.Run(
                With(
                    Request(),
                    options => options with {
                        Rays = options.Rays with { MatchByName = match, Front = 0.2, Rear = 0.2 },
                        Maps = ["normal", "height"]
                    }
                ),
                Into(name),
                0,
                null,
                TestContext.Current.CancellationToken
            );

        Bake(false, "bare");
        Bake(true, "matched");

        // The top's chart runs under the strap, in the atlas's third row (from the bottom), and the two bakes differ
        // there and hardly anywhere else.
        var bare = PngCodec.Load(Path.Combine(Into("bare"), "normal.preview.png"));
        var matched = PngCodec.Load(Path.Combine(Into("matched"), "normal.preview.png"));

        var changed = 0;
        for (var at = 0; at < bare.Pixels.Length; at++) {
            changed += bare.Pixels[at] == matched.Pixels[at] ? 0 : 1;
        }

        Assert.True(changed > 100, $"Matching changed {changed} samples.");
    }

    /// <summary>
    ///     A bake wrote the normal and the height whatever it was asked for, so a bake of an AO map left a normal
    ///     map in the project. Only the maps asked for are written now.
    /// </summary>
    [Fact]
    public void Only_the_maps_asked_for_are_written() {
        var manifest = MeshMapBakeRun.Run(
            With(Request(), static options => options with { Maps = ["ao"] }),
            Into("bake"),
            0,
            null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(["ao"], manifest.Result.Maps.Select(static map => map.Map));
        Assert.False(File.Exists(Path.Combine(Into("bake"), "normal.png")));
    }

    /// <summary>The log names the meshes, each part's match by name, and the time: a wrong match shows.</summary>
    [Fact]
    public void The_log_says_what_was_read_and_how_each_part_matched() {
        var manifest = MeshMapBakeRun.Run(
            With(Request(), static options => options with { Rays = options.Rays with { MatchByName = true } }),
            Into("bake"),
            0,
            null,
            TestContext.Current.CancellationToken
        );
        var log = manifest.Result.Log;

        Assert.Contains(log, static line => line.StartsWith("High: housing_high.glb", StringComparison.Ordinal));
        Assert.Contains(log, static line => line.StartsWith("Low: housing_low.glb", StringComparison.Ordinal));
        Assert.Contains(
            log,
            static line => line.StartsWith("Part strap:", StringComparison.Ordinal)
                && line.Contains("strike only", StringComparison.Ordinal)
        );
        Assert.Contains(log, static line => line.StartsWith("Baked in", StringComparison.Ordinal));
    }

    /// <summary>A studio's own endings: high meshes that end otherwise match nothing, as the log says.</summary>
    [Fact]
    public void Name_endings_are_the_ones_the_bake_is_given() {
        var manifest = MeshMapBakeRun.Run(
            With(
                Request(),
                static options => options with { Rays = options.Rays with { MatchByName = true, HighSuffix = "_hp" } }
            ),
            Into("bake"),
            0,
            null,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(
            manifest.Result.Log,
            static line => line.StartsWith("Part strap: matched no high part", StringComparison.Ordinal)
        );
        Assert.Contains(manifest.Result.Log, static line => line.Contains("strap_high", StringComparison.Ordinal));
    }

    /// <summary>Several high files bake as one, in order.</summary>
    [Fact]
    public void Several_high_meshes_bake_as_one() {
        var second = Into("second.glb");
        File.Copy(pair.High, second);

        var one = MeshMapBakeRun.Run(Request(), Into("one"), 0, null, TestContext.Current.CancellationToken);
        var two = MeshMapBakeRun.Run(
            Request() with { High = [pair.High, second] },
            Into("two"),
            0,
            null,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(
            two.Result.Log,
            static line => line.StartsWith("High: housing_high.glb, second.glb", StringComparison.Ordinal)
        );
        Assert.Equal(one.Result.Covered, two.Result.Covered);
    }

    /// <summary>
    ///     The fixture's high mesh has one material and two parts. Its ID map was one flat colour, every part lost; by
    ///     mesh and by shell it has the parts, and by material it is still the one colour asked for.
    /// </summary>
    [Theory]
    [InlineData("auto", 2)]
    [InlineData("mesh", 2)]
    [InlineData("shell", 2)]
    [InlineData("material", 1)]
    [InlineData("material-colour", 1)]
    public void The_id_source_decides_what_the_id_map_tells_apart(string source, int colours) {
        MeshMapBakeRun.Run(
            With(Request(), options => options with { Maps = ["id"], Id = new() { Source = source } }),
            Into("bake"),
            0,
            null,
            TestContext.Current.CancellationToken
        );
        var picture = PngCodec.Load(Path.Combine(Into("bake"), "id.preview.png"));
        HashSet<int> seen = [];
        for (var at = 0; at + 3 < picture.Pixels.Length; at += 4) {
            var colour = picture.Pixels[at] << 16 | picture.Pixels[at + 1] << 8 | picture.Pixels[at + 2];
            if (colour != 0) {
                seen.Add(colour);
            }
        }

        Assert.True(seen.Count >= colours, $"{source}: {seen.Count} colours.");
        if (colours == 1) {
            Assert.Single(seen);
        }
    }

    [Fact]
    public void A_low_with_no_atlas_is_refused_with_a_reason() {
        var refusal = Assert.Throws<InvalidDataException>(() =>
            MeshMapBakeRun.Run(
                Request() with { Low = pair.High },
                Into("bake"),
                0,
                null,
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("texture coordinates", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture's hard edges survive the trip through a file.</summary>
    [Fact]
    public void A_file_keeps_its_hard_edges_on_the_way_in() {
        var low = MeshMapBakeRun.Read(pair.Low);
        var normals = low.Mesh.Normals.ToArray().Distinct().Count();

        // Ten flats and a strap, each with one normal, on points they share.
        Assert.True(normals >= 8, $"{normals} normals.");
        Assert.True(low.Mesh.PositionCount < low.Mesh.CornerCount, "The two sides of an edge did not weld.");
        Assert.Equal(2, low.Parts.Count);
    }
}
