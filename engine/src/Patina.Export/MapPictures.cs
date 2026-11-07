using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Jobs;
using Vixen.Core.Imaging;

namespace Patina.Export;

/// <summary>A map in memory, in numbers from 0 to 1 except where a float source goes beyond them.</summary>
/// <param name="Name">Which map: one of <see cref="ExportMaps" />.</param>
/// <param name="Image">The samples.</param>
/// <param name="Green">For a normal, which way its green runs.</param>
public sealed record MapPicture(string Name, FloatImage Image, GreenAxis Green);

/// <summary>Reads and writes the lossless maps an output's object holds, at the depth they arrived.</summary>
public static class MapPictures {
    /// <summary>A manifest's name for a way green runs.</summary>
    public static string NameOf(GreenAxis green) => green == GreenAxis.Up ? "up" : "down";

    /// <summary>Reads the maps of an object.</summary>
    /// <param name="folder">The object's folder.</param>
    /// <param name="manifest">What it holds.</param>
    /// <exception cref="InvalidDataException">A file is not what the manifest says.</exception>
    public static IReadOnlyList<MapPicture> Read(string folder, BuildManifest manifest) {
        ArgumentNullException.ThrowIfNull(manifest);
        return [
            ..manifest.Maps.Select(map => new MapPicture(
                    map.Map,
                    ReadFile(Path.Combine(folder, map.File)),
                    map.Green == "down" ? GreenAxis.Down : GreenAxis.Up
                )
            )
        ];
    }

    /// <summary>Writes one map as the smallest file that keeps every bit, and says what was written.</summary>
    /// <param name="folder">The object's folder.</param>
    /// <param name="map">The map's name.</param>
    /// <param name="image">Samples at the depth <paramref name="eightBit" /> says.</param>
    /// <param name="eightBit">Whether the samples came from bytes.</param>
    /// <param name="green">For a normal, which way its green runs.</param>
    public static BuiltMap Write(string folder, string map, FloatImage image, bool eightBit, GreenAxis? green) {
        ArgumentNullException.ThrowIfNull(image);
        var name = map + (eightBit ? ".png" : ".exr");
        File.WriteAllBytes(Path.Combine(folder, name), eightBit ? Png8(image) : ExrCodec.Encode(image));
        return new(map, name, image.Width, image.Height, green is { } axis ? NameOf(axis) : null);
    }

    /// <summary>A file as numbers, 16-bit PNGs and float files included.</summary>
    /// <exception cref="InvalidDataException">It is neither a PNG nor an EXR.</exception>
    public static FloatImage ReadFile(string path) {
        var data = File.ReadAllBytes(path);
        if (Path.GetExtension(path) == ".exr") {
            return ExrCodec.Decode(data);
        }

        var (width, height, channels, samples) = PngCodec.Decode16(data);
        var values = new float[samples.Length];
        for (var at = 0; at < values.Length; at++) {
            values[at] = samples[at] / (float)ushort.MaxValue;
        }

        return new(width, height, channels, values);
    }

    /// <summary>
    ///     A bake's maps under the names a preset reads. A signed map is centred as a picture is, whichever format
    ///     the bake chose, so that a float bake and a 16-bit one export the same file. Green runs up: that is what
    ///     <c>MikkTSpace.Generate</c> makes of v-down coordinates (ADR 0008).
    /// </summary>
    public static IReadOnlyList<MapPicture> OfBake(string folder, BakeManifest manifest) {
        ArgumentNullException.ThrowIfNull(manifest);
        List<MapPicture> pictures = [];
        foreach (var map in manifest.Result.Maps) {
            if (ExportNameOf(map.Map) is not { } name) {
                continue;
            }

            var image = ReadFile(Path.Combine(folder, map.Path));
            if (manifest.Format is BakeInputs.Exr16 or BakeInputs.Exr32) {
                image = Centred(image, map);
            }

            pictures.Add(new(name, image, GreenAxis.Up));
        }

        return pictures;
    }

    /// <summary>What a preset calls a baked map, or null when no preset reads it.</summary>
    public static string? ExportNameOf(string bakedMap) =>
        bakedMap switch {
            "ao" => ExportMaps.Occlusion,
            ExportMaps.Normal or ExportMaps.Height or ExportMaps.Curvature or ExportMaps.Thickness or ExportMaps.Id
                or ExportMaps.Position or ExportMaps.WorldNormal or ExportMaps.BentNormal => bakedMap,
            _ => null
        };

    static FloatImage Centred(FloatImage image, BakedMapFile map) {
        if (!BakedMapPicture.IsSigned(map.Map)) {
            return image;
        }

        var scale = map.Scale > 0 ? (float)map.Scale : 1f;
        var values = new float[image.Samples.Length];
        for (var at = 0; at < values.Length; at++) {
            values[at] = image.Samples[at] / scale * 0.5f + 0.5f;
        }

        return image with { Samples = values };
    }

    static byte[] Png8(FloatImage image) {
        var bytes = new byte[image.Samples.Length];
        for (var at = 0; at < bytes.Length; at++) {
            bytes[at] = (byte)MathF.Round(Math.Clamp(image.Samples[at], 0f, 1f) * byte.MaxValue);
        }

        // Two lanes have no PNG. A third that holds nothing keeps the file a plain picture.
        return image.Channels == 2
            ? PngCodec.Encode8(image.Width, image.Height, 3, Widen(bytes))
            : PngCodec.Encode8(image.Width, image.Height, image.Channels, bytes);
    }

    static byte[] Widen(byte[] pairs) {
        var triples = new byte[pairs.Length / 2 * 3];
        for (var at = 0; at < pairs.Length / 2; at++) {
            (triples[at * 3], triples[at * 3 + 1]) = (pairs[at * 2], pairs[at * 2 + 1]);
        }

        return triples;
    }
}
