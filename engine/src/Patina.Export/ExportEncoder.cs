using Patina.Export.Presets;
using Vixen.Core.Imaging;
using Vixen.Core.Imaging.BlockCompression;
using Vixen.Graphics;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>
///     Turns packed lanes into the bytes of one file. All pixels are encoded by Vixen; nothing here is a
///     codec.
/// </summary>
public static class ExportEncoder {
    /// <summary>The file's bytes, and how many mip levels it has.</summary>
    /// <param name="picture">The lanes.</param>
    /// <param name="texture">What the preset asked for.</param>
    /// <param name="cancellationToken">Stops block compression between blocks.</param>
    public static (byte[] Bytes, int Levels) Encode(
        PackedPicture picture,
        ExportTexture texture,
        CancellationToken cancellationToken
    ) =>
        Encode(picture, texture, false, cancellationToken);

    /// <summary>The file's bytes, and how many mip levels it has.</summary>
    /// <param name="picture">The lanes.</param>
    /// <param name="texture">What the preset asked for.</param>
    /// <param name="dither">Whether an 8-bit file is dithered rather than rounded.</param>
    /// <param name="cancellationToken">Stops block compression between blocks.</param>
    public static (byte[] Bytes, int Levels) Encode(
        PackedPicture picture,
        ExportTexture texture,
        bool dither,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(picture);
        ArgumentNullException.ThrowIfNull(texture);
        var image = new FloatImage(picture.Width, picture.Height, picture.Lanes, picture.Samples);
        var (width, height, lanes) = (image.Width, image.Height, image.Channels);
        switch (texture.Format) {
            case ExportFormats.Png8:
                return (PngCodec.Encode8(width, height, lanes, Bytes(picture, dither)), 1);
            case ExportFormats.Png16:
                return (PngCodec.Encode16(width, height, lanes, Unsigned<ushort>(picture, 65535)), 1);
            case ExportFormats.Tga:
                return (TgaWriter.Encode(width, height, lanes, Bytes(picture, dither)), 1);
            case ExportFormats.Jpeg:
                var quality = int.Parse(texture.Quality, System.Globalization.CultureInfo.InvariantCulture);
                return (JpegWriter.Encode(width, height, lanes, Bytes(picture, dither), quality), 1);
            case ExportFormats.WebP:
                return (WebPWriter.Encode(width, height, lanes, Bytes(picture, dither)), 1);
            case ExportFormats.Tiff8:
                return (TiffWriter.Encode8(width, height, lanes, Bytes(picture, dither)), 1);
            case ExportFormats.Tiff16:
                return (TiffWriter.Encode16(width, height, lanes, Unsigned<ushort>(picture, 65535)), 1);
            case ExportFormats.Tiff32:
                return (TiffWriter.EncodeFloat(image), 1);
            case ExportFormats.Hdr:
                return (HdrWriter.Encode(image), 1);
            case ExportFormats.Exr16:
                return (ExrCodec.Encode(image, ExrPixels.Half), 1);
            case ExportFormats.Exr32:
                return (ExrCodec.Encode(image), 1);
            case ExportFormats.Ktx2:
                var ktx = Texture(picture, texture, cancellationToken);
                return (Ktx2.Write(ktx), ktx.LevelCount);
            case ExportFormats.Dds:
                var dds = Texture(picture, texture, cancellationToken);
                return (DdsCodec.Write(dds), dds.LevelCount);
            default:
                throw new ArgumentException($"'{texture.Format}' is not a format.", nameof(texture));
        }
    }

    static T[] Unsigned<T>(PackedPicture picture, float top)
        where T : struct, System.Numerics.INumberBase<T> {
        var values = new T[picture.Samples.Length];
        Parallel.For(
            0,
            values.Length,
            at => values[at] = T.CreateTruncating(MathF.Round(Math.Clamp(picture.Samples[at], 0f, 1f) * top))
        );
        return values;
    }

    /// <summary>
    ///     Eight-bit samples, rounded or dithered. Dithering rounds up with a chance equal to how far past the level
    ///     below a value is, so a gradient's mean is kept where rounding steps it, and a value already on a level is
    ///     left there: a mask's zero and one stay exactly zero and one. The chance is a hash of the sample's place, so
    ///     the same maps give the same bytes, which is what lets a file be keyed and reused.
    /// </summary>
    public static byte[] Bytes(PackedPicture picture, bool dither) {
        ArgumentNullException.ThrowIfNull(picture);
        if (!dither) {
            return Unsigned<byte>(picture, 255);
        }

        var values = new byte[picture.Samples.Length];
        Parallel.For(
            0,
            values.Length,
            at => {
                var level = Math.Clamp(picture.Samples[at], 0f, 1f) * 255f;
                var below = MathF.Floor(level);
                var fraction = level - below;
                var up = fraction >= 1e-4f && Chance(at) < fraction;
                values[at] = (byte)(up ? below + 1 : below);
            }
        );
        return values;
    }

    /// <summary>A number in [0, 1) from a place: a 32-bit integer hash (Wang's), so it has no pattern to see.</summary>
    static float Chance(int at) {
        var x = (uint)at;
        x = (x ^ 61) ^ (x >> 16);
        x *= 9;
        x ^= x >> 4;
        x *= 0x27d4eb2d;
        x ^= x >> 15;
        return (x >> 8) / 16777216f;
    }

    /// <summary>
    ///     Mips first and then blocks: reducing blocks means decoding them. A linear source stays at 16 bits or in
    ///     float through the chain, so a 16-bit height is averaged at 16 bits and only then cut to what the block
    ///     holds.
    /// </summary>
    static TextureData Texture(PackedPicture picture, ExportTexture texture, CancellationToken cancellationToken) {
        var srgb = texture.Space == "srgb";
        var source = texture.Compression switch {
            ExportCompressions.Half => PixelFormat.Rgba16Float,
            ExportCompressions.Wide or ExportCompressions.Bc6H => PixelFormat.Rgba32Float,
            _ => srgb ? PixelFormat.Rgba8UNormSrgb : PixelFormat.Rgba16UNorm
        };
        var data = new TextureData(source, picture.Width, picture.Height, texture.Mips ? 0 : 1);
        Store(picture, data.LevelSpan(0), source);

        if (texture.Mips) {
            MipChain.Generate(data, Options(texture));
        }

        var target = texture.Compression switch {
            ExportCompressions.Bc1 => PixelFormat.Bc1RgbaUNorm,
            ExportCompressions.Bc3 => PixelFormat.Bc3RgbaUNorm,
            ExportCompressions.Bc4 => PixelFormat.Bc4RUNorm,
            ExportCompressions.Bc5 => PixelFormat.Bc5RgUNorm,
            ExportCompressions.Bc6H => PixelFormat.Bc6HRgbUFloat,
            ExportCompressions.Bc7 => PixelFormat.Bc7RgbaUNorm,
            _ => (PixelFormat?)null
        };
        if (target is not { } block) {
            return data;
        }

        return BlockCompressor.Encode(
            data,
            srgb ? block.ToSrgb() : block,
            new() {
                Quality = texture.Quality switch {
                    "fast" => BlockQuality.Fast,
                    "best" => BlockQuality.Best,
                    _ => BlockQuality.Balanced
                },
                CancellationToken = cancellationToken
            }
        );
    }

    static MipOptions Options(ExportTexture texture) {
        if (texture.Maps.Contains(ExportMaps.Normal, StringComparer.Ordinal)) {
            return MipOptions.NormalMap;
        }

        var cutout = texture.Channels is [_, _, _, { Map: ExportMaps.Opacity }];
        return texture.Space != "srgb" ? MipOptions.Linear : cutout ? MipOptions.CutoutColour : MipOptions.Colour;
    }

    /// <summary>
    ///     Lanes into RGBA. A two-lane normal gets its z back, which is what renormalising a chain
    ///     reads.
    /// </summary>
    static void Store(PackedPicture picture, Span<byte> into, PixelFormat format) {
        var texels = picture.Width * picture.Height;
        Span<float> values = stackalloc float[4];
        var stride = format switch {
            PixelFormat.Rgba8UNormSrgb => 1,
            PixelFormat.Rgba32Float => 4,
            _ => 2
        };

        for (var texel = 0; texel < texels; texel++) {
            Widen(picture, texel, values);
            for (var lane = 0; lane < 4; lane++) {
                Write(into[((texel * 4 + lane) * stride)..], format, values[lane]);
            }
        }
    }

    /// <summary>One texel's lanes as RGBA. A grey lane is every colour, and alpha is opaque unless given.</summary>
    static void Widen(PackedPicture picture, int texel, Span<float> values) {
        var lanes = picture.Lanes;
        var at = texel * lanes;
        var samples = picture.Samples;
        values[0] = samples[at];
        values[1] = lanes > 1 ? samples[at + 1] : samples[at];
        if (lanes > 2) {
            values[2] = samples[at + 2];
        } else {
            values[2] = lanes == 1 ? samples[at] : ReconstructZ(values[0], values[1]);
        }

        values[3] = lanes > 3 ? samples[at + 3] : 1f;
    }

    static void Write(Span<byte> into, PixelFormat format, float value) {
        switch (format) {
            case PixelFormat.Rgba8UNormSrgb:
                into[0] = (byte)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
                break;
            case PixelFormat.Rgba16UNorm:
                BitConverter.TryWriteBytes(into, (ushort)MathF.Round(Math.Clamp(value, 0f, 1f) * 65535f));
                break;
            case PixelFormat.Rgba16Float:
                BitConverter.TryWriteBytes(into, (Half)value);
                break;
            default:
                BitConverter.TryWriteBytes(into, value);
                break;
        }
    }

    static float ReconstructZ(float red, float green) {
        var x = red * 2f - 1f;
        var y = green * 2f - 1f;
        return MathF.Sqrt(MathF.Max(0f, 1f - x * x - y * y)) * 0.5f + 0.5f;
    }
}
