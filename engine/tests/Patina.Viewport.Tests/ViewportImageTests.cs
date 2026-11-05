using Vixen.Core.Imaging;
using Vixen.Graphics;

namespace Patina.Viewport.Tests;

/// <summary>What the numbers in an image become, with no device: formats, colour spaces and mips.</summary>
public sealed class ViewportImageTests {
    static byte[] Checker(byte dark, byte light) {
        var pixels = new byte[2 * 2 * 4];
        for (var at = 0; at < 4; at++) {
            var value = at is 0 or 3 ? dark : light;
            (pixels[at * 4], pixels[at * 4 + 1], pixels[at * 4 + 2], pixels[at * 4 + 3]) = (value, value, value, 255);
        }

        return PngCodec.Encode(new(2, 2, pixels));
    }

    [Fact]
    public void Colour_is_sRGB_and_a_measurement_is_not() {
        Assert.Equal(
            PixelFormat.Rgba8UNormSrgb,
            ViewportImage.Decode(Checker(0, 255), ".png", MapMeaning.Colour).Format
        );
        Assert.Equal(PixelFormat.Rgba8UNorm, ViewportImage.Decode(Checker(0, 255), ".png", MapMeaning.Normal).Format);
        Assert.Equal(
            PixelFormat.Rgba8UNorm,
            ViewportImage.Decode(Checker(0, 255), ".png", MapMeaning.Measurement).Format
        );
    }

    [Fact]
    public void A_mip_of_colour_is_averaged_in_linear_light() {
        // Half black and half white is 188 in linear light, and 128 if the encoded bytes are averaged.
        var colour = ViewportImage.Decode(Checker(0, 255), ".png", MapMeaning.Colour);
        var measurement = ViewportImage.Decode(Checker(0, 255), ".png", MapMeaning.Measurement);

        Assert.Equal(2, colour.LevelCount);
        Assert.InRange((int)colour.Level(1)[0], 186, 190);
        Assert.InRange((int)measurement.Level(1)[0], 126, 129);
    }

    [Fact]
    public void A_sixteen_bit_map_keeps_more_than_eight_bits() {
        var samples = new ushort[] {
            32768, 32768, 65535, 32900, 32768, 65535, 32768, 32900, 65535, 32900, 32900, 65535
        };
        var picture = PngCodec.Encode16(2, 2, 3, samples);

        var texture = ViewportImage.Decode(picture, ".png", MapMeaning.Normal);

        Assert.Equal(PixelFormat.Rgba16Float, texture.Format);
        var first = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Half>(texture.Level(0));
        Assert.Equal(32900f / 65535f, (float)first[4], 3);
        Assert.NotEqual((float)first[0], (float)first[4]);
    }

    [Fact]
    public void A_bake_in_EXR_holds_the_direction_and_comes_out_centred() {
        var direction = new[] { 0f, 0f, 1f, 0.6f, 0f, 0.8f, -0.6f, 0f, 0.8f, 0f, 0f, 1f };
        var picture = ExrCodec.Encode(new(2, 2, 3, direction));

        var texture = ViewportImage.Decode(picture, ".exr", MapMeaning.Normal);

        var first = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Half>(texture.Level(0));
        Assert.Equal(0.5f, (float)first[0], 3);
        Assert.Equal(0.8f, (float)first[4], 3);
        Assert.Equal(0.2f, (float)first[8], 3);
    }

    [Fact]
    public void An_EXR_already_centred_is_left_alone() {
        var centred = new[] { 0.5f, 0.5f, 1f, 0.8f, 0.5f, 0.9f, 0.2f, 0.5f, 0.9f, 0.5f, 0.5f, 1f };
        var picture = ExrCodec.Encode(new(2, 2, 3, centred));

        var texture = ViewportImage.Decode(picture, ".exr", MapMeaning.Normal);

        var first = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Half>(texture.Level(0));
        Assert.Equal(0.8f, (float)first[4], 3);
    }

    [Fact]
    public void Bytes_that_are_no_image_are_refused() =>
        Assert.ThrowsAny<Exception>(static () => ViewportImage.Decode([1, 2, 3], ".png", MapMeaning.Colour));
}
