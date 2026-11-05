using Patina.Export.Presets;
using Vixen.Core.Imaging;
using Vixen.Core.Imaging.BlockCompression;
using Vixen.Graphics;

namespace Patina.Export;

/// <summary>Turns packed lanes into the bytes of one file. All pixels are encoded by Vixen; nothing here is a codec.</summary>
public static class ExportEncoder {
    /// <summary>The file's bytes, and how many mip levels it has.</summary>
    /// <param name="picture">The lanes.</param>
    /// <param name="texture">What the preset asked for.</param>
    /// <param name="cancellationToken">Stops block compression between blocks.</param>
    public static (byte[] Bytes, int Levels) Encode(
        PackedPicture picture,
        ExportTexture texture,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(picture);
        ArgumentNullException.ThrowIfNull(texture);
        var image = new FloatImage(picture.Width, picture.Height, picture.Lanes, picture.Samples);
        switch (texture.Format) {
            case ExportFormats.Png8:
                return (PngCodec.Encode8(image.Width, image.Height, image.Channels, Unsigned<byte>(picture, 255)), 1);
            case ExportFormats.Png16:
                return (PngCodec.Encode16(image.Width, image.Height, image.Channels, Unsigned<ushort>(picture, 65535)), 1);
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
    ///     Mips first and then blocks: reducing blocks means decoding them. A linear source stays at 16 bits or in float
    ///     through the chain, so a 16-bit height is averaged at 16 bits and only then cut to what the block holds.
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

    /// <summary>Lanes into RGBA. A two-lane normal gets its z back, which is what renormalising a chain reads.</summary>
    static void Store(PackedPicture picture, Span<byte> into, PixelFormat format) {
        var texels = picture.Width * picture.Height;
        var lanes = picture.Lanes;
        var samples = picture.Samples;
        var values = new float[4];
        var unit = format is PixelFormat.Rgba8UNormSrgb or PixelFormat.Rgba16UNorm;
        var stride = format switch {
            PixelFormat.Rgba8UNormSrgb => 1,
            PixelFormat.Rgba32Float => 4,
            _ => 2
        };

        for (var texel = 0; texel < texels; texel++) {
            values[0] = samples[texel * lanes];
            values[1] = lanes > 1 ? samples[texel * lanes + 1] : values[0];
            values[2] = lanes > 2 ? samples[texel * lanes + 2]
                : lanes == 1 ? values[0]
                : ReconstructZ(values[0], values[1]);
            values[3] = lanes > 3 ? samples[texel * lanes + 3] : 1f;
            for (var lane = 0; lane < 4; lane++) {
                var at = (texel * 4 + lane) * stride;
                var value = unit ? Math.Clamp(values[lane], 0f, 1f) : values[lane];
                if (format == PixelFormat.Rgba8UNormSrgb) {
                    into[at] = (byte)MathF.Round(value * 255f);
                } else if (format == PixelFormat.Rgba16UNorm) {
                    BitConverter.TryWriteBytes(into[at..], (ushort)MathF.Round(value * 65535f));
                } else if (format == PixelFormat.Rgba16Float) {
                    BitConverter.TryWriteBytes(into[at..], (Half)value);
                } else {
                    BitConverter.TryWriteBytes(into[at..], value);
                }
            }
        }
    }

    static float ReconstructZ(float red, float green) {
        var x = red * 2f - 1f;
        var y = green * 2f - 1f;
        return MathF.Sqrt(MathF.Max(0f, 1f - x * x - y * y)) * 0.5f + 0.5f;
    }
}
