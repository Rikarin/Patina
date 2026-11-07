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
            High = pair.High,
            Low = pair.Low,
            Output = folder.FullName,
            Resolution = 128,
            Format = format,
            SearchRadius = 0.05,
            Maps = ["ao"],
            OcclusionRays = 8
        };

    /// <summary>The low was cut to its set and the cage was not, so their layouts could never agree.</summary>
    [Fact]
    public void A_cage_is_cut_to_the_same_set_as_the_low_mesh() {
        var low = Into("low.obj");
        BakeFixture.WriteTwoMaterials(low, 0);
        var cage = Into("cage.obj");
        BakeFixture.WriteTwoMaterials(cage, 0.1f);
        Assert.Equal(12, MeshMapBakeRun.Read(low).Mesh.CornerCount);
        Assert.Equal(6, MeshMapBakeRun.Read(low, "Strap").Mesh.CornerCount);

        var manifest = MeshMapBakeRun.Run(
            Request() with { High = low, Low = low, Cage = cage, Material = "Strap" },
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
            Request() with { Resolution = 256 },
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
                Request() with { Resolution = 512, OcclusionRays = 64 },
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
                Request() with { MatchByName = match, SearchRadius = 0.2, Maps = null },
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
