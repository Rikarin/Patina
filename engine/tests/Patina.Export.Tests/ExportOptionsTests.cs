using Patina.Contracts;
using Patina.Export.Presets;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>What an export dialog changes: formats, padding, size, dithering, files and sets.</summary>
public sealed class ExportOptionsTests : IDisposable {
    const int Side = 16;

    const string Preset = """
                          version: 1
                          id: test
                          name: Test
                          normal: y-up
                          textures:
                            - file: "{project}_colour"
                              format: png8
                              space: srgb
                              channels: [baseColor.r, baseColor.g, baseColor.b]
                            - file: "{project}_rough"
                              format: png8
                              space: linear
                              channels: [roughness]
                          """;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-options-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    string Path(string name) => System.IO.Path.Combine(folder.FullName, name);

    /// <summary>A grey map whose left half is one value and right half another.</summary>
    static MapPicture Halves(string name, float left, float right) {
        var values = new float[Side * Side];
        for (var at = 0; at < values.Length; at++) {
            values[at] = at % Side < Side / 2 ? left : right;
        }

        return new(name, new(Side, Side, 1, values), GreenAxis.Up);
    }

    /// <summary>Coverage of the left half only, as a stack on a mesh writes it.</summary>
    static MapPicture LeftHalf() => Halves(ExportMaps.Coverage, 1f, 0f);

    static ExportPreset Read(string text = Preset) => ExportPresetReader.Read(text, "test");

    static string WithTexture(string format, string space, string channels, string extra = "") =>
        $"""
         version: 1
         id: one
         name: One
         normal: y-up
         textures:
           - file: x
             format: {format}
             space: {space}
             channels: {channels}
         {extra}
         """;

    [Theory]
    [InlineData(ExportFormats.Tga, ".tga")]
    [InlineData(ExportFormats.Jpeg, ".jpg")]
    [InlineData(ExportFormats.Tiff8, ".tif")]
    [InlineData(ExportFormats.Tiff16, ".tif")]
    [InlineData(ExportFormats.Tiff32, ".tif")]
    [InlineData(ExportFormats.WebP, ".webp")]
    [InlineData(ExportFormats.Hdr, ".hdr")]
    public void Every_new_format_is_one_a_preset_can_name(string format, string extension) {
        var space = ExportFormats.IsFloat(format) || ExportFormats.IsWide(format) ? "linear" : "srgb";

        var preset = Read(WithTexture(format, space, "[baseColor.r, baseColor.g, baseColor.b]"));

        Assert.Equal(format, preset.Textures[0].Format);
        Assert.Equal(extension, ExportFormats.ExtensionOf(format));
    }

    [Theory]
    [InlineData(ExportFormats.Jpeg, "srgb", "[baseColor.r, baseColor.g, baseColor.b, opacity.r]", "lanes")]
    [InlineData(ExportFormats.Hdr, "srgb", "[emissive.r, emissive.g, emissive.b]", "linear")]
    [InlineData(ExportFormats.Tiff32, "srgb", "[height.r]", "linear")]
    [InlineData(ExportFormats.Tiff16, "srgb", "[height.r]", "linear")]
    [InlineData(ExportFormats.Tga, "linear", "[roughness.r, metalness.r]", "lanes")]
    public void A_file_a_format_cannot_hold_is_refused(string format, string space, string channels, string why) {
        var refused = Assert.Throws<ExportPresetException>(() => Read(WithTexture(format, space, channels)));

        Assert.Contains(why, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_jpeg_takes_a_quality_from_one_to_a_hundred() {
        var preset = Read(
            WithTexture(ExportFormats.Jpeg, "srgb", "[roughness]")
                .Replace("channels", "quality: 75\n    channels", StringComparison.Ordinal)
        );

        Assert.Equal("75", preset.Textures[0].Quality);
        Assert.Equal("90", Read(WithTexture(ExportFormats.Jpeg, "srgb", "[roughness]")).Textures[0].Quality);
        Assert.Throws<ExportPresetException>(static () => Read(
                WithTexture(ExportFormats.Jpeg, "srgb", "[roughness]")
                    .Replace("channels", "quality: best\n    channels", StringComparison.Ordinal)
            )
        );
    }

    [Fact]
    public void A_preset_names_its_padding_size_filter_and_dither() {
        var preset = Read(
            WithTexture(
                ExportFormats.Png8,
                "linear",
                "[roughness]",
                "padding:\n  mode: colour\n  distance: 4\n  colour: [0.5, 0.25]\n"
                + "size: 1024\nfilter: bilinear\ndither: true"
            )
        );

        Assert.Equal((PaddingMode.Colour, 4), (preset.Padding.Mode, preset.Padding.Distance));
        Assert.Equal([0.5f, 0.25f], preset.Padding.Colour);
        Assert.Equal(1024, preset.Size);
        Assert.Equal(ExportFilters.Bilinear, preset.Filter);
        Assert.True(preset.Dither);
        Assert.Equal(
            PaddingMode.Infinite,
            Read(WithTexture(ExportFormats.Png8, "linear", "[roughness]", "padding: infinite")).Padding.Mode
        );
        Assert.Throws<ExportPresetException>(static () =>
            Read(WithTexture(ExportFormats.Png8, "linear", "[roughness]", "padding: lots"))
        );
    }

    /// <summary>
    ///     Infinite padding gives the uncovered half the covered half's value; without padding it keeps what the stack
    ///     left there.
    /// </summary>
    [Theory]
    [InlineData("infinite", 0.25f)]
    [InlineData("none", 0.75f)]
    public void Padding_fills_what_the_layout_does_not_cover_from_what_it_does(string mode, float right) {
        var maps = new Dictionary<string, MapPicture> {
            ["roughness"] = Halves("roughness", 0.25f, 0.75f), [ExportMaps.Coverage] = LeftHalf()
        };
        var preset = Read(WithTexture(ExportFormats.Png16, "linear", "[roughness]", $"padding: {mode}"));

        ExportRun.Run(preset, maps, Path("object"), null, Token);

        var written = MapPictures.ReadFile(Directory.GetFiles(Path("object"), "*.png").Single());
        Assert.Equal(0.25f, written.Samples[0], 3);
        Assert.Equal(right, written.Samples[Side - 1], 3);
    }

    [Fact]
    public void A_file_is_written_at_the_size_asked_for_with_the_filter_asked_for() {
        var maps = new Dictionary<string, MapPicture> { ["roughness"] = Halves("roughness", 0f, 1f) };
        var preset = Read(WithTexture(ExportFormats.Png16, "linear", "[roughness]", "size: 4"));

        var manifest = ExportRun.Run(preset, maps, Path("object"), null, Token);

        var file = Assert.Single(manifest.Files);
        Assert.Equal((4, 4), (file.Width, file.Height));
        var written = MapPictures.ReadFile(System.IO.Path.Combine(Path("object"), file.File));
        Assert.Equal([0f, 0f, 1f, 1f], written.Samples[..4]);
    }

    /// <summary>
    ///     A value a third of the way between two 8-bit levels rounds to the lower one every time, and dithers to the
    ///     upper one about a third of the time, so the mean is kept. A value on a level stays on it.
    /// </summary>
    [Fact]
    public void Dithering_keeps_the_mean_and_leaves_exact_levels_alone() {
        const int count = 64 * 64;
        var between = (100f + 1f / 3f) / 255f;
        var picture = new PackedPicture(64, 64, 1, [..Enumerable.Repeat(between, count)], []);
        var exact = new PackedPicture(
            64,
            64,
            1,
            [..Enumerable.Repeat(1f, count / 2), ..Enumerable.Repeat(0f, count / 2)],
            []
        );

        var rounded = ExportEncoder.Bytes(picture, false);
        var dithered = ExportEncoder.Bytes(picture, true);

        Assert.All(rounded, static value => Assert.Equal(100, value));
        Assert.All(dithered, static value => Assert.InRange(value, (byte)100, (byte)101));
        Assert.InRange(dithered.Average(static b => (double)b), 100.30, 100.37);
        Assert.Equal(ExportEncoder.Bytes(exact, false), ExportEncoder.Bytes(exact, true));
        Assert.Equal(dithered, ExportEncoder.Bytes(picture, true));
    }

    [Theory]
    [InlineData(ExportFormats.Tga)]
    [InlineData(ExportFormats.Jpeg)]
    [InlineData(ExportFormats.Hdr)]
    public void A_new_format_reads_back_as_the_picture_it_was(string format) {
        var maps = new Dictionary<string, MapPicture> { ["roughness"] = Halves("roughness", 0.2f, 0.8f) };
        var preset = Read(WithTexture(format, "linear", "[roughness]"));

        var manifest = ExportRun.Run(preset, maps, Path("object"), null, Token);

        var read = MapPictures.ReadFile(System.IO.Path.Combine(Path("object"), Assert.Single(manifest.Files).File));
        Assert.Equal(0.2f, read.Samples[0], 1);
        Assert.Equal(0.8f, read.Samples[(Side - 1) * read.Channels], 1);
    }

    [Fact]
    public void Options_turn_a_file_off_change_a_format_and_move_the_revision() {
        var preset = Read();
        var options = new ExportOptions {
            Files = [new("{project}_rough", false), new("{project}_colour", true, ExportFormats.Tiff16)]
        };

        var changed = ExportOverrides.Apply(preset, options);

        var file = Assert.Single(changed.Textures);
        Assert.Equal(ExportFormats.Tiff16, file.Format);
        Assert.Equal("linear", file.Space);
        Assert.NotEqual(preset.Revision, changed.Revision);
        Assert.Equal(changed.Revision, ExportOverrides.Apply(preset, options).Revision);
        Assert.Same(preset, ExportOverrides.Apply(preset, null));
    }

    [Fact]
    public void Options_a_preset_cannot_take_are_refused_with_the_reason() {
        var preset = Read();
        var withAlpha = Read(
            WithTexture(ExportFormats.Png8, "srgb", "[baseColor.r, baseColor.g, baseColor.b, opacity.r]")
        );

        var refused = Assert.Throws<ExportPresetException>(() => ExportOverrides.Apply(
                withAlpha,
                new() { Files = [new("x", true, ExportFormats.Jpeg)] }
            )
        );

        Assert.Contains("JPEG 8-bit holds 1, 3 lanes, not 4", refused.Message, StringComparison.Ordinal);
        Assert.Throws<ExportPresetException>(() => ExportOverrides.Apply(preset, new() { Files = [new("nothing")] }));
        Assert.Throws<ExportPresetException>(() => ExportOverrides.Apply(preset, new() { Size = 0 }));
        Assert.Throws<ExportPresetException>(() => ExportOverrides.Apply(
                preset,
                new() { Files = [new("{project}_colour", false), new("{project}_rough", false)] }
            )
        );
    }

    /// <summary>
    ///     Exporting one set of two writes only that set, under the name it has when both are exported: a file's name
    ///     does not depend on what else was chosen.
    /// </summary>
    [Fact]
    public void Choosing_one_set_writes_it_under_its_own_name() {
        var preset = ExportOverrides.Apply(Read(), new() { Sets = ["Body"] });
        ExportSet[] sets = [
            new("Body", new Dictionary<string, MapPicture> { ["roughness"] = Halves("roughness", 0f, 1f) }),
            new("Head", new Dictionary<string, MapPicture> { ["roughness"] = Halves("roughness", 1f, 0f) })
        ];

        var manifest = ExportRun.Run(preset, sets, Path("object"), null, Token);
        var delivered = ExportRun.Deliver("key", Path("object"), manifest, Path("out"), new("Crate", "test"));

        Assert.All(manifest.Files, static file => Assert.Equal("Body", file.Set));
        Assert.Contains("Body", System.IO.Path.GetFileName(Assert.Single(delivered)), StringComparison.Ordinal);
    }
}
