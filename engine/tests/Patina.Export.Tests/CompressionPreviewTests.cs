using Patina.Contracts;
using Patina.Export.Presets;
using Vixen.Core.Imaging;
using Vixen.Core.Imaging.BlockCompression;

namespace Patina.Export.Tests;

/// <summary>
///     The compression preview (#217) and the round trip of every format: what a preview shows is what the whole file
///     holds, and every file an export writes reads back as the picture it was.
/// </summary>
public sealed class CompressionPreviewTests : IDisposable {
    const int Side = 64;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-compression-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    /// <summary>Noise over a gradient, which no block format holds exactly.</summary>
    static PackedPicture Noise(int lanes) {
        var random = new Random(7);
        var samples = new float[Side * Side * lanes];
        for (var at = 0; at < samples.Length; at++) {
            var x = at / lanes % Side;
            samples[at] = Math.Clamp(x / (float)Side + (float)(random.NextDouble() - 0.5) * 0.3f, 0f, 1f);
        }

        return new(Side, Side, lanes, samples, []);
    }

    static ExportTexture Texture(string format, string compression, int lanes, bool mips, string space = "linear") =>
        new(
            "x",
            format,
            compression,
            ExportPresetReader.DefaultQuality(format),
            space,
            mips,
            [..Enumerable.Range(0, lanes).Select(static lane => new ChannelSource("roughness", 0, false, 0))]
        );

    /// <summary>
    ///     A region of a level, compressed alone, is exactly that region of the whole file decoded: blocks are made
    ///     alone, and the mips are the whole picture's. So the preview is the file and not a likeness of it.
    /// </summary>
    [Theory]
    [InlineData(ExportCompressions.Bc7, 4, 0)]
    [InlineData(ExportCompressions.Bc4, 1, 1)]
    [InlineData(ExportCompressions.Bc1, 3, 2)]
    public void A_region_is_exactly_that_region_of_the_whole_file(string compression, int lanes, int level) {
        var texture = Texture(ExportFormats.Ktx2, compression, lanes, true);
        var picture = Noise(lanes);

        var region = CompressionPreview.Make(picture, texture, level, (0.7, 0.3), 16, Token);

        var (bytes, levels) = ExportEncoder.Encode(picture, texture, Token);
        var whole = BlockCompressor.Decode(Ktx2.Read(bytes));
        Assert.Equal(levels, region.Levels);
        var (x, y, width, height) = region.Region;
        Assert.Equal((0, 0), (x % 4, y % 4));
        var extent = whole.Levels[level];
        var file = whole.Level(level);
        for (var row = 0; row < height; row++) {
            for (var column = 0; column < width; column++) {
                for (var lane = 0; lane < lanes; lane++) {
                    var expected = file[((y + row) * extent.Width + x + column) * 4 + lane] / 255f;
                    Assert.Equal(expected, region.Written.Samples[(row * width + column) * lanes + lane], 5);
                }
            }
        }

        var errors = CompressionPreview.Errors(region);
        Assert.Equal(lanes, errors.Count);
        Assert.All(errors, static e => Assert.InRange(e.Psnr!.Value, 15, 80));
        Assert.All(errors, static e => Assert.True(e.MaxError > 0));
    }

    [Fact]
    public void A_file_with_no_loss_shows_none() {
        var texture = Texture(ExportFormats.Ktx2, ExportCompressions.None, 4, false);

        var region = CompressionPreview.Make(Noise(4), texture, 0, (0.5, 0.5), 32, Token);

        Assert.All(CompressionPreview.Errors(region), static e => Assert.Null(e.Psnr));
        Assert.Equal((32, 32), (region.Region.Width, region.Region.Height));
    }

    [Fact]
    public void A_jpeg_shows_its_loss_and_the_difference_is_amplified() {
        var texture = Texture(ExportFormats.Jpeg, ExportCompressions.None, 3, false, "srgb") with { Quality = "20" };

        var region = CompressionPreview.Make(Noise(3), texture, 0, (0.5, 0.5), 64, Token);
        var errors = CompressionPreview.Errors(region);

        Assert.All(errors, static e => Assert.InRange(e.Psnr!.Value, 10, 40));
        var faint = CompressionPreview.Difference(region, 1);
        var loud = CompressionPreview.Difference(region, 8);
        Assert.True(loud.Samples.Sum() > faint.Samples.Sum() * 4);
    }

    /// <summary>The GPU memory a preview states is the pre-export check's measure: BC7 one byte a texel, RGBA four.</summary>
    [Fact]
    public void The_memory_is_the_checks_measure() {
        var texture = Texture(ExportFormats.Ktx2, ExportCompressions.Bc7, 4, true);

        Assert.Equal(Side * Side * 4d / 3d, ExportMemory.Bytes(texture, Side, Side));
        Assert.Equal(
            Side * Side * 4 * 4d / 3d,
            ExportMemory.Bytes(texture with { Compression = ExportCompressions.None }, Side, Side)
        );
    }

    /// <summary>
    ///     Every format an export writes, read back by what Patina reads pictures with: PNG, TGA, JPEG, Radiance HDR and
    ///     OpenEXR through MapPictures, KTX2 and DDS through Vixen's readers. TIFF and WebP have no reader here; Vixen's
    ///     ImageWriterTests read them back with libtiff, ImageIO and libwebp.
    /// </summary>
    [Theory]
    [InlineData(ExportFormats.Png8, "srgb", 1 / 255d)]
    [InlineData(ExportFormats.Png16, "linear", 1 / 65535d)]
    [InlineData(ExportFormats.Tga, "srgb", 1 / 255d)]
    [InlineData(ExportFormats.Jpeg, "srgb", 0.2)]
    [InlineData(ExportFormats.Hdr, "linear", 0.01)]
    [InlineData(ExportFormats.Exr16, "linear", 0.001)]
    [InlineData(ExportFormats.Exr32, "linear", 1e-6)]
    [InlineData(ExportFormats.Ktx2, "linear", 1 / 65535d)]
    [InlineData(ExportFormats.Dds, "linear", 1 / 65535d)]
    public void Every_format_reads_back_as_the_picture_it_was(string format, string space, double within) {
        var texture = Texture(format, ExportCompressions.None, 3, false, space);
        var picture = Noise(3);

        var (bytes, _) = ExportEncoder.Encode(picture, texture, Token);

        var read = format switch {
            ExportFormats.Ktx2 => Numbers(Ktx2.Read(bytes)),
            ExportFormats.Dds => Numbers(DdsCodec.Read(bytes)),
            _ => MapPictures.Decode(bytes, ExportFormats.ExtensionOf(format))
        };
        Assert.Equal((Side, Side), (read.Width, read.Height));
        var worst = 0d;
        for (var texel = 0; texel < Side * Side; texel++) {
            for (var lane = 0; lane < 3; lane++) {
                worst = Math.Max(
                    worst,
                    Math.Abs(picture.Samples[texel * 3 + lane] - read.Samples[texel * read.Channels + lane])
                );
            }
        }

        Assert.InRange(worst, 0, within + 1e-6);
    }

    /// <summary>A 16-bit RGBA container's first level as numbers.</summary>
    static FloatImage Numbers(TextureData data) {
        var wide = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ushort>(data.Level(0));
        var samples = new float[wide.Length];
        for (var at = 0; at < samples.Length; at++) {
            samples[at] = wide[at] / 65535f;
        }

        return new(data.Width, data.Height, 4, samples);
    }
}
