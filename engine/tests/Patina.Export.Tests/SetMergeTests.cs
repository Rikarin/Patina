using Patina.Export.Presets;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>Texture sets that share one UV square, written as one atlas (#211).</summary>
public sealed class SetMergeTests : IDisposable {
    const int Side = 8;

    const string Preset = """
                          version: 1
                          id: p
                          name: P
                          normal: y-up
                          textures:
                            - file: T_{project}_R
                              format: png16
                              space: linear
                              channels: [roughness]
                            - file: T_{project}_N
                              format: png16
                              space: linear
                              channels: [normal.r, normal.g, normal.b]
                          """;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-merge-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    /// <summary>
    ///     A one-lane map whose texels left of <paramref name="split" /> are one value and the rest another.
    /// </summary>
    static FloatImage Split(int side, int split, float left, float right) {
        var values = new float[side * side];
        for (var at = 0; at < values.Length; at++) {
            values[at] = at % side < split ? left : right;
        }

        return new(side, side, 1, values);
    }

    /// <summary>A set laid out on the left half of the square, or the right.</summary>
    static ExportSet Set(string name, bool left, float roughness, int side = Side, GreenAxis green = GreenAxis.Up) {
        var half = side / 2;
        var normal = new float[side * side * 3];
        for (var at = 0; at < side * side; at++) {
            normal[at * 3] = 0.5f;
            normal[at * 3 + 1] = 0.25f;
            normal[at * 3 + 2] = 1f;
        }

        return new(
            name,
            new Dictionary<string, MapPicture> {
                [ExportMaps.Roughness] = new(ExportMaps.Roughness, Split(side, half, roughness, roughness), green),
                [ExportMaps.Normal] = new(ExportMaps.Normal, new(side, side, 3, normal), green),
                [ExportMaps.Coverage] = new(
                    ExportMaps.Coverage,
                    left ? Split(side, half, 1f, 0f) : Split(side, half, 0f, 1f),
                    GreenAxis.Up
                )
            }
        );
    }

    /// <summary>
    ///     Each set is read moved home onto tile 1001 (#20), so sets on two UDIM tiles would have been laid on one
    ///     another as though they shared a square.
    /// </summary>
    [Fact]
    public void Sets_on_two_udim_tiles_are_not_merged_and_say_why() {
        List<string> warnings = [];

        var merged = SetMerge.Merge(
            [Set("Body", true, 0.25f), Set("Strap", false, 0.75f) with { Udim = 1002 }],
            warnings
        );

        Assert.Null(merged);
        Assert.Contains("1001, 1002", Assert.Single(warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void Sets_on_one_tile_past_the_first_merge_and_keep_its_number() {
        List<string> warnings = [];

        var merged = SetMerge.Merge(
            [Set("Body", true, 0.25f) with { Udim = 1011 }, Set("Strap", false, 0.75f) with { Udim = 1011 }],
            warnings
        )!;

        Assert.Empty(warnings);
        Assert.Equal(1011, merged.Udim);
    }

    [Fact]
    public void Each_set_is_written_where_its_layout_is_and_nothing_overlaps() {
        List<string> warnings = [];

        var merged = SetMerge.Merge([Set("Body", true, 0.25f), Set("Strap", false, 0.75f)], warnings)!;

        Assert.Empty(warnings);
        Assert.Equal(string.Empty, merged.Name);
        var rough = merged.Maps[ExportMaps.Roughness].Image.Samples;
        Assert.Equal(0.25f, rough[0]);
        Assert.Equal(0.75f, rough[Side - 1]);
        Assert.All(merged.Maps[ExportMaps.Coverage].Image.Samples, static covered => Assert.Equal(1f, covered));
    }

    /// <summary>A smaller set is enlarged to the largest, and its layout with it.</summary>
    [Fact]
    public void A_smaller_set_is_enlarged_to_the_largest() {
        List<string> warnings = [];

        var merged = SetMerge.Merge([Set("Body", true, 0.25f, 4), Set("Strap", false, 0.75f)], warnings)!;

        var rough = merged.Maps[ExportMaps.Roughness].Image;
        Assert.Equal((Side, Side), (rough.Width, rough.Height));
        Assert.Equal(0.25f, rough.Samples[0], 0.001f);
        Assert.Equal(0.75f, rough.Samples[Side - 1], 0.001f);
    }

    [Fact]
    public void Overlapping_layouts_are_written_from_the_first_set_and_said() {
        List<string> warnings = [];

        var merged = SetMerge.Merge([Set("Body", true, 0.25f), Set("Strap", true, 0.75f)], warnings)!;

        Assert.Equal(0.25f, merged.Maps[ExportMaps.Roughness].Image.Samples[0]);
        Assert.Contains("32 texels", Assert.Single(warnings), StringComparison.Ordinal);
    }

    /// <summary>A normal whose green runs the other way is turned over to the first set's.</summary>
    [Fact]
    public void A_normal_whose_green_runs_the_other_way_is_turned_to_the_first_sets() {
        List<string> warnings = [];

        var merged = SetMerge.Merge(
            [Set("Body", true, 0.25f), Set("Strap", false, 0.75f, green: GreenAxis.Down)],
            warnings
        )!;

        var normal = merged.Maps[ExportMaps.Normal];
        Assert.Equal(GreenAxis.Up, normal.Green);
        Assert.Equal(0.25f, normal.Image.Samples[1]);
        Assert.Equal(0.75f, normal.Image.Samples[(Side - 1) * 3 + 1]);
    }

    [Fact]
    public void A_set_on_no_mesh_cannot_be_placed_so_the_sets_stay_apart() {
        List<string> warnings = [];
        var loose = Set("Strap", false, 0.75f);
        loose = loose with {
            Maps = loose.Maps.Where(static m => m.Key != ExportMaps.Coverage).ToDictionary(StringComparer.Ordinal)
        };

        Assert.Null(SetMerge.Merge([Set("Body", true, 0.25f), loose], warnings));
        Assert.Contains("Strap", Assert.Single(warnings), StringComparison.Ordinal);
    }

    /// <summary>Through a whole export: one file of each, under the project's name, and no set's name in it.</summary>
    [Fact]
    public void A_merged_export_writes_one_file_of_each_named_as_one_set_is() {
        var preset = ExportOverrides.Apply(ExportPresetReader.Read(Preset, "p"), new() { Merge = true });
        var made = Path.Combine(folder.FullName, "made");

        var manifest = ExportRun.Run(
            preset,
            [Set("Body", true, 0.25f), Set("Strap", false, 0.75f)],
            made,
            null,
            Token
        );
        var delivered = ExportRun.Deliver(
                "key",
                made,
                manifest,
                Path.Combine(folder.FullName, "out"),
                new("Crate", "p")
            )
            .Files.Select(Path.GetFileName);

        Assert.False(manifest.PerSet);
        Assert.Equal(["T_Crate_R.png", "T_Crate_N.png"], delivered);
    }

    /// <summary>
    ///     Sets on two tiles merge into one set of files per tile when every file names its tile (#281): the two on
    ///     1001 become one atlas and the one on 1002 its own files, each named by its tile.
    /// </summary>
    [Fact]
    public void Sets_on_two_tiles_merge_per_tile_when_the_files_name_the_tile() {
        var preset = ExportOverrides.Apply(
            ExportPresetReader.Read(Preset.Replace("T_{project}_R", "T_{project}_R.{udim}"), "p"),
            new() { Merge = true }
        );
        preset = preset with {
            Textures = [preset.Textures[0], preset.Textures[1] with { File = "T_{project}_N_{tile}" }]
        };
        var made = Path.Combine(folder.FullName, "made");

        var manifest = ExportRun.Run(
            preset,
            [
                Set("Body", true, 0.25f), Set("Strap", false, 0.75f),
                Set("Head", true, 0.5f) with { Udim = 1002 }
            ],
            made,
            null,
            Token
        );
        var delivered = ExportRun.Deliver(
                "key",
                made,
                manifest,
                Path.Combine(folder.FullName, "out"),
                new("Crate", "p")
            )
            .Files.Select(Path.GetFileName)
            .Order(StringComparer.Ordinal);

        Assert.DoesNotContain(manifest.Warnings, static w => w.Contains("not merged", StringComparison.Ordinal));
        Assert.Equal(
            ["T_Crate_N_u1_v1.png", "T_Crate_N_u2_v1.png", "T_Crate_R.1001.png", "T_Crate_R.1002.png"],
            delivered
        );
    }

    /// <summary>
    ///     A merged export on two tiles names every tile's files (#289): USD and MaterialX through the
    ///     <c>&lt;UDIM&gt;</c> token when the names differ only by the tile's number, and glTF, which has no tiles, a
    ///     material per tile.
    ///     One material file for both tiles named only the first tile's files.
    /// </summary>
    [Theory]
    [InlineData("T_{project}_N.{udim}", true)]
    [InlineData("T_{project}_N_{tile}", false)]
    public void A_merged_export_on_two_tiles_names_every_tiles_files_in_its_materials(string normal, bool token) {
        var preset = ExportOverrides.Apply(
            ExportPresetReader.Read(Preset.Replace("T_{project}_R", "T_{project}_R.{udim}"), "p"),
            new() { Merge = true }
        );
        preset = preset with {
            Textures = [preset.Textures[0], preset.Textures[1] with { File = normal }],
            Materials = ["gltf", "usda", "mtlx"]
        };
        var made = Path.Combine(folder.FullName, "made");
        var target = Path.Combine(folder.FullName, "out");
        var manifest = ExportRun.Run(
            preset,
            [Set("Body", true, 0.25f), Set("Head", true, 0.5f) with { Udim = 1002 }],
            made,
            null,
            Token
        );

        var delivered = ExportRun.Deliver("key", made, manifest, target, new("Crate", "p"), preset: preset)
            .Files.Select(Path.GetFileName)
            .Where(static f => !f!.EndsWith(".png", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        string[] apart = ["Crate_1001.mtlx", "Crate_1001.usda", "Crate_1002.mtlx", "Crate_1002.usda"];
        string[] wanted = [..(token ? ["Crate.mtlx", "Crate.usda"] : apart), "Crate_1001.gltf", "Crate_1002.gltf"];
        Assert.Equal(wanted.Order(StringComparer.Ordinal), delivered);
        // glTF reads the normal and not a roughness alone in red, so the normal is what names the tile.
        var second = File.ReadAllText(Path.Combine(target, "Crate_1002.gltf"));
        Assert.Contains(token ? "T_Crate_N.1002.png" : "T_Crate_N_u2_v1.png", second, StringComparison.Ordinal);
        Assert.DoesNotContain(token ? "1001" : "u1_v1", second, StringComparison.Ordinal);
        if (token) {
            Assert.Contains("@T_Crate_R.<UDIM>.png@", File.ReadAllText(Path.Combine(target, "Crate.usda")));
            Assert.Contains("T_Crate_N.&lt;UDIM&gt;.png", File.ReadAllText(Path.Combine(target, "Crate.mtlx")));
        } else {
            Assert.Contains("T_Crate_N_u2_v1.png", File.ReadAllText(Path.Combine(target, "Crate_1002.usda")));
        }
    }

    /// <summary>A name without the tile is one file for two tiles, so the sets stay apart and it says how.</summary>
    [Fact]
    public void Sets_on_two_tiles_stay_apart_when_a_file_does_not_name_the_tile() {
        List<string> warnings = [];

        var merged = SetMerge.PerTile(
            [Set("Body", true, 0.25f), Set("Strap", false, 0.75f) with { Udim = 1002 }],
            false,
            warnings
        );

        Assert.Null(merged);
        Assert.Contains("{udim} or {tile}", Assert.Single(warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void A_tile_of_one_set_keeps_that_sets_maps_under_no_name() {
        List<string> warnings = [];
        var head = Set("Head", true, 0.5f) with { Udim = 1002 };

        var merged = SetMerge.PerTile([Set("Body", true, 0.25f), Set("Strap", false, 0.75f), head], true, warnings)!;

        Assert.Empty(warnings);
        Assert.Equal([1001, 1002], merged.Select(static set => set.Udim));
        Assert.All(merged, static set => Assert.Equal(string.Empty, set.Name));
        Assert.Same(head.Maps, merged[1].Maps);
    }

    /// <summary>
    ///     One set of files has one size, the export's: a size chosen for one set is not used, and the report says so
    ///     (#252).
    /// </summary>
    [Fact]
    public void A_merged_export_writes_at_the_exports_size_and_names_a_sets_own_size_it_did_not_use() {
        var preset = ExportOverrides.Apply(
            ExportPresetReader.Read(Preset, "p"),
            new() { Merge = true, Size = 4, PerSet = [new("Strap") { Size = 2 }] }
        );

        var manifest = ExportRun.Run(
            preset,
            [Set("Body", true, 0.25f), Set("Strap", false, 0.75f)],
            Path.Combine(folder.FullName, "made"),
            null,
            Token
        );

        Assert.All(manifest.Files, static file => Assert.Equal((4, 4), (file.Width, file.Height)));
        Assert.Contains(manifest.Warnings, static w => w.Contains("Strap (2 px)", StringComparison.Ordinal));
    }
}
