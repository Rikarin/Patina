using Patina.Contracts;
using Patina.Jobs;
using Vixen.Core.Imaging;

namespace Patina.Export;

/// <summary>Reads a baked map back as the picture a graph's Mesh Map node takes.</summary>
public static class BakedMapPicture {
    /// <summary>The maps that shade a set from a bake whose normal is in tangent space.</summary>
    static readonly string[] TangentShading = ["normal", "ao"];

    /// <summary>The maps that shade a set from a bake whose normal is in object space, which no set combines.</summary>
    static readonly string[] ObjectShading = ["ao"];

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
                SharedFile.ReadAllBytes(file),
                ContractsJsonContext.Default.BakeManifest
            );
            if (manifest?.Result.Maps.FirstOrDefault(m => m.Map == map) is { } found) {
                return (Path.Combine(folder, found.Path), found, manifest.Format);
            }
        }

        return null;
    }

    /// <summary>
    ///     The maps of a bake that shade a texture set beneath its layers (#86): its normal when it is in tangent
    ///     space, the only one a set's normal can be combined with, and its ambient occlusion. Empty for no bake.
    /// </summary>
    /// <param name="folder">The folder a bake was delivered to, or null.</param>
    public static IReadOnlySet<string> Shading(string? folder) {
        HashSet<string> found = new(StringComparer.Ordinal);
        if (folder is null || !Directory.Exists(folder)) {
            return found;
        }

        foreach (var file in Directory.EnumerateFiles(folder, $"*.{BakeCache.ManifestFile}")) {
            if (BakeCache.ReadManifestFile(file) is not { } manifest) {
                continue;
            }

            var shading = manifest.Space == "tangent" ? TangentShading : ObjectShading;
            found.UnionWith(manifest.Result.Maps.Select(static map => map.Map).Intersect(shading));
        }

        return found;
    }

    /// <summary>
    ///     The box a bake's position map spans in the model's units, six numbers, which decals and paths read the map
    ///     through (#133); null when no bake in the folder has a position map that says it.
    /// </summary>
    /// <param name="folder">The folder a bake was delivered to, or null.</param>
    public static IReadOnlyList<float>? PositionBox(string? folder) {
        if (folder is null || !Directory.Exists(folder)) {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(folder, $"*.{BakeCache.ManifestFile}")
                     .Order(StringComparer.Ordinal)) {
            if (BakeCache.ReadManifestFile(file) is { PositionBox: { Count: 6 } box } manifest
                && box.All(float.IsFinite)
                && manifest.Result.Maps.Any(static map => map.Map == "position")) {
                return box;
            }
        }

        return null;
    }

    /// <summary>Whether a float file holds this map's measurement from below zero, which a picture centres.</summary>
    public static bool IsSigned(string map) => map is "normal" or "height" or "bent" or "curvature" or "world";

    /// <summary>
    ///     The picture. A float file holds the measurement, so a signed map is centred on one half here, exactly as an
    ///     integer file was when it was written: a graph sees the same picture whichever way a bake was stored.
    /// </summary>
    /// <exception cref="InvalidDataException">The file is not one this reads.</exception>
    public static Bitmap Read(string path, BakedMapFile map, string format) {
        ArgumentNullException.ThrowIfNull(map);
        var data = SharedFile.ReadAllBytes(path);
        return format is BakeInputs.Exr16 or BakeInputs.Exr32 ? FromFloat(ExrCodec.Decode(data), map) : FromPng(data);
    }

    /// <summary>
    ///     The picture at the depth it was stored, centred as <see cref="Read" /> centres it. A 16-bit position map
    ///     read at eight bits holds 256 places across the model, which a volume noise finer than that draws as steps.
    /// </summary>
    /// <exception cref="InvalidDataException">The file is not one this reads.</exception>
    public static FloatImage ReadDeep(string path, BakedMapFile map, string format) {
        ArgumentNullException.ThrowIfNull(map);
        var data = SharedFile.ReadAllBytes(path);
        if (format is BakeInputs.Exr16 or BakeInputs.Exr32) {
            var image = ExrCodec.Decode(data);
            var signed = IsSigned(map.Map);
            var scale = map.Scale > 0 ? (float)map.Scale : 1f;
            var samples = new float[image.Samples.Length];
            for (var at = 0; at < samples.Length; at++) {
                var value = image.Samples[at];
                samples[at] = Math.Clamp(signed ? value / scale * 0.5f + 0.5f : value, 0f, 1f);
            }

            return image with { Samples = samples };
        }

        var (width, height, channels, words) = PngCodec.Decode16(data);
        var values = new float[words.Length];
        for (var at = 0; at < values.Length; at++) {
            values[at] = words[at] / (float)ushort.MaxValue;
        }

        return new(width, height, channels, values);
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
        var signed = IsSigned(map.Map);
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
