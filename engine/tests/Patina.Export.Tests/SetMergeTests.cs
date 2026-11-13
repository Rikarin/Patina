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
}
