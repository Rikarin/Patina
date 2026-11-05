using Patina.Contracts;
using Patina.Jobs;
using Vixen.Core.Imaging;

namespace Patina.Export;

/// <summary>Reads a baked map back as the 8-bit picture a graph's Mesh Map node takes.</summary>
public static class BakedMapPicture {
    /// <summary>Finds a map in a folder a bake was delivered to.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="map">The map's name.</param>
    /// <returns>The file, and what it takes to read it, or null when no bake there has it.</returns>
    public static (string Path, BakedMapFile Map, string Format)? Find(string folder, string map) {
        if (!Directory.Exists(folder)) {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(folder, $"*.{BakeCache.ManifestFile}")
                     .Order(StringComparer.Ordinal)) {
            var manifest = System.Text.Json.JsonSerializer.Deserialize(
                File.ReadAllBytes(file),
                ContractsJsonContext.Default.BakeManifest
            );
            if (manifest?.Result.Maps.FirstOrDefault(m => m.Map == map) is { } found) {
                return (Path.Combine(folder, found.Path), found, manifest.Format);
            }
        }

        return null;
    }

    /// <summary>
    ///     The picture. A float file holds the measurement, so a signed map is centred on one half here, exactly as an
    ///     integer file was when it was written: a graph sees the same picture whichever way a bake was stored.
    /// </summary>
    /// <exception cref="InvalidDataException">The file is not one this reads.</exception>
    public static Bitmap Read(string path, BakedMapFile map, string format) {
        ArgumentNullException.ThrowIfNull(map);
        var data = File.ReadAllBytes(path);
        return format is BakeInputs.Exr16 or BakeInputs.Exr32 ? FromFloat(ExrCodec.Decode(data), map) : FromPng(data);
    }

    static Bitmap FromPng(byte[] data) {
        var (width, height, channels, samples) = PngCodec.Decode16(data);
        var pixels = new byte[width * height * 4];

        for (var at = 0; at < width * height; at++) {
            for (var lane = 0; lane < 3; lane++) {
                pixels[at * 4 + lane] = (byte)(samples[at * channels + (channels == 1 ? 0 : lane)] >> 8);
            }

            pixels[at * 4 + 3] = byte.MaxValue;
        }

        return new(width, height, pixels);
    }

    static Bitmap FromFloat(FloatImage image, BakedMapFile map) {
        var pixels = new byte[image.Width * image.Height * 4];
        var signed = map.Map is "normal" or "height" or "bent" or "curvature" or "world";
        var scale = map.Scale > 0 ? (float)map.Scale : 1f;

        for (var at = 0; at < image.Width * image.Height; at++) {
            for (var lane = 0; lane < 3; lane++) {
                var value = image.Samples[at * image.Channels + (image.Channels == 1 ? 0 : lane)];
                value = signed ? value / scale * 0.5f + 0.5f : value;
                pixels[at * 4 + lane] = (byte)MathF.Round(Math.Clamp(value, 0f, 1f) * byte.MaxValue);
            }

            pixels[at * 4 + 3] = byte.MaxValue;
        }

        return new(image.Width, image.Height, pixels);
    }
}
