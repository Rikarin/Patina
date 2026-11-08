// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Interchange;

namespace Patina.Viewport;

/// <summary>
///     What lights the model: one of the built-in rooms or a radiance file, written as a cube and baked by Vixen's own
///     <see cref="EnvironmentPrefilter" /> and <see cref="SphericalHarmonicsL2" />. The rooms are gradients with soft
///     boxes, because a metal shows what surrounds it, and a uniform environment would make every metal a flat disc.
/// </summary>
sealed class ViewportEnvironment {
    /// <summary>The built-in room the viewport opens in.</summary>
    public const string Default = "studio";

    const int Size = 128;
    const int Levels = 7;
    const int Samples = 64;

    /// <summary>How wide a panorama is read at: four texels for each of a face's, about a face texel's size.</summary>
    const int PanoramaWidth = Size * 4;

    ViewportEnvironment(TextureData prefiltered, SphericalHarmonicsL2 irradiance) {
        Prefiltered = prefiltered;
        Irradiance = irradiance;
    }

    public TextureData Prefiltered { get; }

    public SphericalHarmonicsL2 Irradiance { get; }

    /// <summary>A built-in room by name, or the equirectangular panorama at a path.</summary>
    /// <exception cref="InvalidDataException">The file cannot be read as a picture.</exception>
    public static ViewportEnvironment Load(string source) =>
        source switch {
            Default => Baked(Studio),
            "overcast" => Baked(Overcast),
            "sunset" => Baked(Sunset),
            _ => Baked(Panorama(source))
        };

    static ViewportEnvironment Baked(Func<Vector3, Vector3> radiance) {
        var cube = new TextureData(PixelFormat.Rgba16Float, Size, Size, 1, faceCount: CubeMap.Faces);
        for (var face = 0; face < CubeMap.Faces; face++) {
            for (var y = 0; y < Size; y++) {
                for (var x = 0; x < Size; x++) {
                    CubeMap.WriteTexel(cube, 0, face, x, y, radiance(CubeMap.DirectionOfTexel(face, x, y, Size)));
                }
            }
        }

        return new(EnvironmentPrefilter.Specular(cube, Levels, Samples), SphericalHarmonicsL2.Project(cube));
    }

    /// <summary>
    ///     Reads a panorama: its left edge is behind the model and its middle row the horizon. Nothing is clamped,
    ///     because a sun is most of a sky's light, and Vixen's prefilter reads it as wide as each sample reaches rather
    ///     than as speckle. The picture is first averaged down to about a face texel's size and then read between four
    ///     texels, since a 4K panorama read nearest into a 128 cube hits a small sun one time in twenty.
    /// </summary>
    static Func<Vector3, Vector3> Panorama(string path) {
        var (pixels, width, height) = Shrunk(Radiance(path));
        return direction => {
            var across = MathF.Atan2(direction.X, -direction.Z) / MathF.Tau + 0.5f;
            var down = MathF.Acos(Math.Clamp(direction.Y, -1f, 1f)) / MathF.PI;
            return Bilinear(pixels, width, height, across * width - 0.5f, down * height - 0.5f);
        };
    }

    /// <summary>Four texels around a point, wrapping round the panorama's sides and stopping at its poles.</summary>
    static Vector3 Bilinear(float[] pixels, int width, int height, float x, float y) {
        var row = Math.Clamp(y, 0f, height - 1);
        var top = (int)row;
        var bottom = Math.Min(top + 1, height - 1);
        var left = (int)MathF.Floor(x);
        var across = x - left;
        var down = row - top;
        var upper = Vector3.Lerp(Texel(pixels, width, top, left), Texel(pixels, width, top, left + 1), across);
        var lower = Vector3.Lerp(Texel(pixels, width, bottom, left), Texel(pixels, width, bottom, left + 1), across);
        return Vector3.Lerp(upper, lower, down);
    }

    static Vector3 Texel(float[] pixels, int width, int row, int column) {
        var at = (row * width + (column % width + width) % width) * 4;
        return new(pixels[at], pixels[at + 1], pixels[at + 2]);
    }

    /// <summary>A panorama averaged down by a whole factor to about <see cref="PanoramaWidth" /> across.</summary>
    static (float[] Pixels, int Width, int Height) Shrunk((float[] Pixels, int Width, int Height) picture) {
        var factor = (picture.Width + PanoramaWidth - 1) / PanoramaWidth;
        if (factor <= 1) {
            return picture;
        }

        var width = (picture.Width + factor - 1) / factor;
        var height = Math.Max(1, (picture.Height + factor - 1) / factor);
        var shrunk = new float[width * height * 4];
        Parallel.For(
            0,
            height,
            row => {
                for (var column = 0; column < width; column++) {
                    var sum = Vector3.Zero;
                    var count = 0;
                    for (var y = row * factor; y < Math.Min((row + 1) * factor, picture.Height); y++) {
                        for (var x = column * factor; x < Math.Min((column + 1) * factor, picture.Width); x++) {
                            sum += Texel(picture.Pixels, picture.Width, y, x);
                            count++;
                        }
                    }

                    var mean = sum / Math.Max(count, 1);
                    var at = (row * width + column) * 4;
                    (shrunk[at], shrunk[at + 1], shrunk[at + 2]) = (mean.X, mean.Y, mean.Z);
                }
            }
        );
        return (shrunk, width, height);
    }

    /// <summary>A panorama's numbers, four to a texel.</summary>
    static (float[] Pixels, int Width, int Height) Radiance(string path) {
        var kind = Path.GetExtension(path).ToLowerInvariant();
        if (kind == ".exr") {
            var image = ExrCodec.Decode(File.ReadAllBytes(path));
            var lanes = image.Channels;
            var pixels = new float[image.Width * image.Height * 4];
            for (var at = 0; at < image.Width * image.Height; at++) {
                for (var lane = 0; lane < 3; lane++) {
                    pixels[at * 4 + lane] = image.Samples[at * lanes + (lanes >= 3 ? lane : 0)];
                }
            }

            return (pixels, image.Width, image.Height);
        }

        using var file = File.OpenRead(path);
        var picture = new StbImageDecoder().Decode(file, StbImageDecoder.HighDynamicRangeExtension);
        return (MemoryMarshal.Cast<byte, float>(picture.PixelSpan()).ToArray(), picture.Width, picture.Height);
    }

    static Vector3 Studio(Vector3 direction) {
        var height = direction.Y * 0.5f + 0.5f;
        var room = Vector3.Lerp(new(0.09f, 0.085f, 0.08f), new(0.42f, 0.44f, 0.48f), height * height);
        var key = SoftBox(direction, Vector3.Normalize(new(0.6f, 0.7f, 0.5f)), 0.86f) * new Vector3(9f, 8.6f, 8f);
        var fill = SoftBox(direction, Vector3.Normalize(new(-0.8f, 0.2f, 0.3f)), 0.92f) * new Vector3(1.6f, 1.9f, 2.4f);
        return room + key + fill;
    }

    /// <summary>Soft light from everywhere above, which shows a material's colour with no bright reflections.</summary>
    static Vector3 Overcast(Vector3 direction) {
        var height = direction.Y * 0.5f + 0.5f;
        return Vector3.Lerp(new(0.16f, 0.16f, 0.15f), new(0.95f, 0.98f, 1.02f), height);
    }

    /// <summary>A low warm sun under a cooling sky, which shows a surface's relief.</summary>
    static Vector3 Sunset(Vector3 direction) {
        var height = Math.Clamp(direction.Y, 0f, 1f);
        var sky = Vector3.Lerp(new(1f, 0.46f, 0.2f), new(0.14f, 0.24f, 0.46f), MathF.Sqrt(height));
        var ground = new Vector3(0.1f, 0.07f, 0.06f);
        var sun = SoftBox(direction, Vector3.Normalize(new(0.8f, 0.12f, 0.45f)), 0.985f) * new Vector3(40f, 22f, 10f);
        return (direction.Y > 0f ? sky : ground) + sun;
    }

    static float SoftBox(Vector3 direction, Vector3 axis, float edge) =>
        Math.Clamp((Vector3.Dot(direction, axis) - edge) / ((1f - edge) * 0.25f), 0f, 1f);
}
