// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Imaging;
using Vixen.Graphics;
using Vixen.Interchange;

namespace Patina.Viewport;

/// <summary>What a texture's numbers are, which decides its colour space and how its mips are averaged.</summary>
enum MapMeaning {
    /// <summary>Colour, stored in sRGB. The GPU decodes it, so that filtering happens in linear light.</summary>
    Colour,

    /// <summary>A tangent-space direction, centred on one half.</summary>
    Normal,

    /// <summary>A linear measurement: roughness, metallic, occlusion.</summary>
    Measurement
}

/// <summary>
///     Reads an encoded image into a texture with a full mip chain. Eight bits stay eight bits; a 16-bit PNG or an
///     EXR, which is what a bake writes, becomes 16-bit float, so that a normal map keeps the precision it was baked
///     to. Everything here is a function of the file's bytes and what they mean.
/// </summary>
static class ViewportImage {
    const float SignedLimit = 0.25f;

    /// <exception cref="InvalidDataException">The bytes are not an image this reads.</exception>
    public static TextureData Decode(byte[] bytes, string extension, MapMeaning meaning) {
        ArgumentNullException.ThrowIfNull(bytes);
        var kind = extension.ToLowerInvariant();
        if (kind == ".exr") {
            var image = ExrCodec.Decode(bytes);
            return Wide(Rgba(image.Width, image.Height, image.Channels, image.Samples), image.Width, image.Height, meaning, true);
        }

        if (kind == ".png" && IsSixteenBit(bytes)) {
            var (width, height, channels, samples) = PngCodec.Decode16(bytes);
            var values = new float[samples.Length];
            Parallel.For(0, values.Length, at => values[at] = samples[at] / (float)ushort.MaxValue);
            return Wide(Rgba(width, height, channels, values), width, height, meaning, false);
        }

        return Narrow(Eight(bytes, kind), meaning);
    }

    /// <summary>One colour, for a slot nothing fills.</summary>
    public static TextureData Solid(byte red, byte green, byte blue, MapMeaning meaning) {
        var format = meaning == MapMeaning.Colour ? PixelFormat.Rgba8UNormSrgb : PixelFormat.Rgba8UNorm;
        var texture = new TextureData(format, 1, 1, 1);
        new byte[] { red, green, blue, byte.MaxValue }.CopyTo(texture.PixelSpan());
        return texture;
    }

    static bool IsSixteenBit(byte[] bytes) => bytes.Length > 24 && bytes[24] == 16;

    static TextureData Eight(byte[] bytes, string extension) {
        var decoder = ImageDecoders.BuiltIn.FirstOrDefault(d => d.Extensions.Contains(extension, StringComparer.Ordinal))
            ?? throw new InvalidDataException($"'{extension}' is not an image format the viewport reads.");
        using var stream = new MemoryStream(bytes);
        return decoder.Decode(stream, extension);
    }

    static TextureData Narrow(TextureData decoded, MapMeaning meaning) {
        if (decoded.Format != PixelFormat.Rgba8UNorm) {
            throw new InvalidDataException($"The viewport does not read {decoded.Format} images.");
        }

        var format = meaning == MapMeaning.Colour ? PixelFormat.Rgba8UNormSrgb : PixelFormat.Rgba8UNorm;
        var texture = new TextureData(format, decoded.Width, decoded.Height);
        decoded.Level(0).CopyTo(texture.LevelSpan(0));
        MipChain.Generate(
            texture,
            meaning switch {
                MapMeaning.Colour => MipOptions.Colour,
                MapMeaning.Normal => MipOptions.NormalMap,
                _ => MipOptions.Linear
            }
        );
        return texture;
    }

    /// <summary>Any number of channels as four, with grey on all three colours and alpha at one.</summary>
    static float[] Rgba(int width, int height, int channels, float[] samples) {
        var pixels = new float[width * height * 4];
        Parallel.For(
            0,
            height,
            y => {
                for (var at = y * width; at < (y + 1) * width; at++) {
                    var from = at * channels;
                    pixels[at * 4] = samples[from];
                    pixels[at * 4 + 1] = samples[from + (channels >= 3 ? 1 : 0)];
                    pixels[at * 4 + 2] = samples[from + (channels >= 3 ? 2 : 0)];
                    pixels[at * 4 + 3] = channels is 4 or 2 ? samples[from + channels - 1] : 1f;
                }
            }
        );
        return pixels;
    }

    static TextureData Wide(float[] pixels, int width, int height, MapMeaning meaning, bool isFloat) {
        switch (meaning) {
            case MapMeaning.Colour when !isFloat:
                Parallel.For(0, pixels.Length / 4, at => ToLinear(pixels, at * 4));
                break;
            case MapMeaning.Normal when isFloat && IsSigned(pixels):
                Parallel.For(
                    0,
                    pixels.Length / 4,
                    at => {
                        for (var lane = 0; lane < 3; lane++) {
                            pixels[at * 4 + lane] = pixels[at * 4 + lane] * 0.5f + 0.5f;
                        }
                    }
                );
                break;
        }

        var texture = new TextureData(PixelFormat.Rgba16Float, width, height);
        var (current, levelWidth, levelHeight) = (pixels, width, height);
        Write(current, texture.LevelSpan(0));
        for (var level = 1; level < texture.LevelCount; level++) {
            current = Halve(current, levelWidth, levelHeight, out levelWidth, out levelHeight, meaning);
            Write(current, texture.LevelSpan(level));
        }

        return texture;
    }

    /// <summary>
    ///     Whether a float normal holds the direction itself, as a bake does, or the centred numbers an integer format
    ///     would. A map's X and Y average about zero in the first and about a half in the second.
    /// </summary>
    static bool IsSigned(float[] pixels) {
        var sum = 0d;
        for (var at = 0; at < pixels.Length; at += 4) {
            sum += pixels[at] + pixels[at + 1];
        }

        return sum / (pixels.Length / 2d) < SignedLimit;
    }

    static void ToLinear(float[] pixels, int at) {
        for (var lane = at; lane < at + 3; lane++) {
            var value = pixels[lane];
            pixels[lane] = value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
        }
    }

    static float[] Halve(float[] source, int width, int height, out int nextWidth, out int nextHeight, MapMeaning meaning) {
        var (toWidth, toHeight) = (Math.Max(width / 2, 1), Math.Max(height / 2, 1));
        var result = new float[toWidth * toHeight * 4];
        Parallel.For(
            0,
            toHeight,
            y => {
                var rowA = Math.Min(y * 2, height - 1);
                var rowB = Math.Min(y * 2 + 1, height - 1);
                for (var x = 0; x < toWidth; x++) {
                    var columnA = Math.Min(x * 2, width - 1);
                    var columnB = Math.Min(x * 2 + 1, width - 1);
                    var into = (y * toWidth + x) * 4;
                    for (var lane = 0; lane < 4; lane++) {
                        result[into + lane] = 0.25f
                            * (source[(rowA * width + columnA) * 4 + lane]
                               + source[(rowA * width + columnB) * 4 + lane]
                               + source[(rowB * width + columnA) * 4 + lane]
                               + source[(rowB * width + columnB) * 4 + lane]);
                    }

                    if (meaning == MapMeaning.Normal) {
                        Renormalise(result, into);
                    }
                }
            }
        );
        (nextWidth, nextHeight) = (toWidth, toHeight);
        return result;
    }

    static void Renormalise(float[] pixels, int at) {
        var (x, y, z) = (pixels[at] * 2f - 1f, pixels[at + 1] * 2f - 1f, pixels[at + 2] * 2f - 1f);
        var length = MathF.Sqrt(x * x + y * y + z * z);
        if (length > 0f) {
            pixels[at] = x / length * 0.5f + 0.5f;
            pixels[at + 1] = y / length * 0.5f + 0.5f;
            pixels[at + 2] = z / length * 0.5f + 0.5f;
        }
    }

    static void Write(float[] pixels, Span<byte> target) {
        var halves = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Half>(target);
        for (var at = 0; at < pixels.Length; at++) {
            halves[at] = (Half)pixels[at];
        }
    }
}
