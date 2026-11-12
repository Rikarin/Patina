using Patina.Export.Presets;
using Vixen.Core.Imaging;
using Vixen.Graphics;
using Vixen.Interchange;

namespace Patina.Export.Tests;

/// <summary>
///     A stack's own export in a format other than its PNGs (#47): one map packed and encoded as an export's file is,
///     keeping its lanes where the format holds them.
/// </summary>
public sealed class MapFileTests {
    const int Side = 8;

    static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A map whose left half is one value and right half another, in every lane but alpha.</summary>
    static FloatImage Halves(int lanes, float left, float right) {
        var samples = new float[Side * Side * lanes];
        for (var at = 0; at < Side * Side; at++) {
            for (var lane = 0; lane < lanes; lane++) {
                samples[at * lanes + lane] = lane == 3 ? 1f : at % Side < Side / 2 ? left : right;
            }
        }

        return new(Side, Side, lanes, samples);
    }

    [Theory]
    [InlineData(ExportFormats.Png8, ".png")]
    [InlineData(ExportFormats.Png16, ".png")]
    [InlineData(ExportFormats.Tga, ".tga")]
    [InlineData(ExportFormats.Jpeg, ".jpg")]
    [InlineData(ExportFormats.Exr32, ".exr")]
    [InlineData(ExportFormats.Hdr, ".hdr")]
    public void A_grey_map_reads_back_as_it_was(string format, string extension) {
        var bytes = MapFile.Encode(
            "roughness",
            Halves(1, 0.2f, 0.8f),
            format,
            ExportCompressions.None,
            MapFile.Quality(format, null),
            false,
            Token
        );

        var read = MapPictures.Decode(bytes, extension);
        Assert.Equal((Side, Side), (read.Width, read.Height));
        Assert.Equal(0.2f, read.Samples[0], 1);
        Assert.Equal(0.8f, read.Samples[(Side - 1) * read.Channels], 1);
    }

    /// <summary>A float map keeps what a byte cannot hold: a height above one survives an EXR.</summary>
    [Fact]
    public void A_float_map_keeps_its_range_in_a_float_file() {
        var bytes = MapFile.Encode(
            "height",
            Halves(1, -0.5f, 3f),
            ExportFormats.Exr32,
            ExportCompressions.None,
            "balanced",
            false,
            Token
        );

        var read = ExrCodec.Decode(bytes);
        Assert.Equal(-0.5f, read.Samples[0], 3);
        Assert.Equal(3f, read.Samples[(Side - 1) * read.Channels], 3);
    }

    /// <summary>
    ///     A color with alpha as a JPEG drops the alpha and keeps its sRGB numbers, and an EXR keeps the alpha and holds
    ///     linear numbers, as an export's base color file does.
    /// </summary>
    [Fact]
    public void A_color_fits_the_format_and_moves_to_linear_only_in_a_float_file() {
        var colour = Halves(4, 0.5f, 0.5f);

        var jpeg = MapPictures.Decode(
            MapFile.Encode("baseColor", colour, ExportFormats.Jpeg, ExportCompressions.None, "100", false, Token),
            ".jpg"
        );
        var exr = ExrCodec.Decode(
            MapFile.Encode("baseColor", colour, ExportFormats.Exr16, ExportCompressions.None, "balanced", false, Token)
        );

        Assert.Equal(0.5f, jpeg.Samples[0], 1);
        Assert.Equal(4, exr.Channels);
        Assert.Equal(MipChain.ToLinear(0.5f), exr.Samples[0], 2);
    }

    /// <summary>A block of one lane takes a color's red, and one of three repeats a grey map's lane.</summary>
    [Fact]
    public void A_block_takes_the_lanes_it_holds() {
        var bc4 = DdsCodec.Read(
            MapFile.Encode("baseColor", Halves(4, 0.25f, 0.75f), ExportFormats.Dds, "bc4", "balanced", false, Token)
        );
        var bc1 = DdsCodec.Read(
            MapFile.Encode("roughness", Halves(1, 0.25f, 0.75f), ExportFormats.Dds, "bc1", "balanced", true, Token)
        );

        Assert.Equal(PixelFormat.Bc4RUNorm, bc4.Format);
        Assert.Equal(PixelFormat.Bc1RgbaUNorm, bc1.Format);
        Assert.True(bc1.LevelCount > 1);
    }

    [Theory]
    [InlineData("gif", null, null, "is not a format")]
    [InlineData(ExportFormats.Png8, "bc7", null, "cannot be compressed")]
    [InlineData(ExportFormats.Ktx2, "bc9", null, "is not a compression")]
    [InlineData(ExportFormats.Jpeg, null, "101", "1 to 100")]
    [InlineData(ExportFormats.Ktx2, "bc7", "slow", "is not a quality")]
    public void Settings_a_file_cannot_take_are_refused_with_the_reason(
        string format,
        string? compression,
        string? quality,
        string reason
    ) =>
        Assert.Contains(reason, MapFile.Problem(format, compression, quality), StringComparison.Ordinal);

    [Fact]
    public void Settings_a_file_can_take_are_not_refused() {
        Assert.Null(MapFile.Problem(ExportFormats.Ktx2, "bc7", "best"));
        Assert.Null(MapFile.Problem(ExportFormats.Jpeg, null, null));
        Assert.Equal("90", MapFile.Quality(ExportFormats.Jpeg, null));
    }
}
