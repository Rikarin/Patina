using Patina.Export.Presets;

namespace Patina.Export.Tests;

/// <summary>
///     A file whose format follows its maps (#141): <c>png</c>, <c>tiff</c> and <c>exr</c> are written as deep as the
///     deepest map they read, so a channel painted at 16 bits or float reaches the file whole.
/// </summary>
public sealed class ChannelDepthExportTests : IDisposable {
    const int Side = 8;

    /// <summary>A value between two eight-bit levels: 76.5 of 255.</summary>
    const float Between = 0.3f;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-depth-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    static MapPicture Flat(string name, int depth) =>
        new(name, new(Side, Side, 1, [..Enumerable.Repeat(Between, Side * Side)]), GreenAxis.Up) { Depth = depth };

    static ExportPreset Preset(string format, string space, string channels) =>
        ExportPresetReader.Read(
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
             """,
            "test"
        );

    [Theory]
    [InlineData("png", 8, "linear", "png8")]
    [InlineData("png", 16, "linear", "png16")]
    [InlineData("png", 32, "linear", "png16")]
    [InlineData("png", 16, "srgb", "png8")]
    [InlineData("tiff", 8, "linear", "tiff8")]
    [InlineData("tiff", 16, "linear", "tiff16")]
    [InlineData("tiff", 32, "linear", "tiff32")]
    [InlineData("tiff", 32, "srgb", "tiff8")]
    [InlineData("exr", 8, "linear", "exr16")]
    [InlineData("exr", 16, "linear", "exr16")]
    [InlineData("exr", 32, "linear", "exr32")]
    [InlineData("png8", 32, "linear", "png8")]
    public void A_following_format_settles_on_the_depth_of_its_maps(
        string format,
        int depth,
        string space,
        string settled
    ) =>
        Assert.Equal(settled, ExportFormats.Settled(format, depth, space));

    /// <summary>
    ///     A file reading a 16-bit height and an 8-bit roughness is 16-bit, and keeps the value eight bits round away;
    ///     one reading only the roughness is 8-bit. The manifest says the format each file was written in.
    /// </summary>
    [Fact]
    public void A_png_is_as_deep_as_the_deepest_map_it_reads() {
        var maps = new Dictionary<string, MapPicture> {
            ["height"] = Flat("height", 16), ["roughness"] = Flat("roughness", 8)
        };

        var deep = Assert.Single(
            ExportRun.Run(Preset("png", "linear", "[height.r, roughness.r, 0, 1]"), maps, Where("deep"), null, Token)
                .Files
        );
        var narrow = Assert.Single(
            ExportRun.Run(Preset("png", "linear", "[roughness.r]"), maps, Where("narrow"), null, Token).Files
        );

        Assert.Equal(ExportFormats.Png16, deep.Format);
        Assert.Equal(ExportFormats.Png8, narrow.Format);
        Assert.EndsWith(".png", deep.File, StringComparison.Ordinal);
        var read = MapPictures.ReadFile(Path.Combine(Where("deep"), deep.File));
        Assert.Equal(Between, read.Samples[0], 4);
        Assert.NotEqual(Between, MapPictures.ReadFile(Path.Combine(Where("narrow"), narrow.File)).Samples[0], 4);
    }

    /// <summary>An EXR reading a map computed at 32 bits is a 32-bit float file, and a 16-bit map's a half.</summary>
    [Fact]
    public void An_exr_is_float_only_where_a_map_is() {
        var maps = new Dictionary<string, MapPicture> {
            ["height"] = Flat("height", 32), ["roughness"] = Flat("roughness", 16)
        };

        var wide = ExportRun.Run(Preset("exr", "linear", "[height.r]"), maps, Where("wide"), null, Token);
        var half = ExportRun.Run(Preset("exr", "linear", "[roughness.r]"), maps, Where("half"), null, Token);

        Assert.Equal(ExportFormats.Exr32, Assert.Single(wide.Files).Format);
        Assert.Equal(ExportFormats.Exr16, Assert.Single(half.Files).Format);
    }

    /// <summary>A following format holds what its family holds: an EXR is linear, as every float file is.</summary>
    [Fact]
    public void A_following_exr_refuses_srgb_numbers_and_a_png_takes_them() {
        Assert.Throws<ExportPresetException>(static () => Preset(
                "exr",
                "srgb",
                "[baseColor.r, baseColor.g, baseColor.b]"
            )
        );
        Assert.Equal("png", Preset("png", "srgb", "[baseColor.r, baseColor.g, baseColor.b]").Textures[0].Format);
    }

    /// <summary>A derived map is as deep as the deepest map it is worked out from.</summary>
    [Fact]
    public void Glossiness_is_as_deep_as_the_roughness_it_comes_from() {
        var maps = new Dictionary<string, MapPicture> { ["roughness"] = Flat("roughness", 16) };

        var file = Assert.Single(
            ExportRun.Run(Preset("png", "linear", "[glossiness.r]"), maps, Where("gloss"), null, Token).Files
        );

        Assert.Equal(ExportFormats.Png16, file.Format);
    }

    /// <summary>A stack's own export of one map follows the map's depth as an export's file does.</summary>
    [Fact]
    public void One_map_written_alone_settles_on_its_own_depth() {
        Assert.Equal(ExportFormats.Png16, MapFile.Settled("height", ExportFormats.Png, 16));
        Assert.Equal(ExportFormats.Png8, MapFile.Settled("baseColor", ExportFormats.Png, 16));
        Assert.Equal(ExportFormats.Tiff32, MapFile.Settled("roughness", ExportFormats.Tiff, 32));
        Assert.Null(MapFile.Problem(ExportFormats.Exr, null, null));
    }

    string Where(string name) => Path.Combine(folder.FullName, name);
}
