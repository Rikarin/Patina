using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Jobs;
using System.Runtime.InteropServices;
using Vixen.Core.Imaging;
using Vixen.Graphics;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>A map in memory, in numbers from 0 to 1 except where a float source goes beyond them.</summary>
/// <param name="Name">Which map: one of <see cref="ExportMaps" />.</param>
/// <param name="Image">The samples.</param>
/// <param name="Green">For a normal, which way its green runs.</param>
public sealed record MapPicture(string Name, FloatImage Image, GreenAxis Green);

/// <summary>Reads and writes the lossless maps an output's object holds, at the depth they arrived.</summary>
public static class MapPictures {
    /// <summary>Where a PNG's header says how many bits a sample has.</summary>
    const int PngDepthAt = 24;

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

    /// <summary>A picture as numbers at whatever depth it has: any of <see cref="PictureFormats.Pictures" />.</summary>
    /// <exception cref="InvalidDataException">It is not a picture this reads.</exception>
    public static FloatImage ReadFile(string path) => Decode(SharedFile.ReadAllBytes(path), Path.GetExtension(path));

    /// <summary>
    ///     An encoded picture as numbers at whatever depth it has, as a model file carries one inside
    ///     it.
    /// </summary>
    /// <param name="data">The encoded bytes.</param>
    /// <param name="extension">What they are encoded as, with its leading dot.</param>
    /// <exception cref="InvalidDataException">It is not a picture this reads.</exception>
    public static FloatImage Decode(byte[] data, string extension) {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(extension);
        var kind = extension.ToLowerInvariant();
        if (kind == ".exr") {
            return ExrCodec.Decode(data);
        }

        if (kind is ".tif" or ".tiff") {
            return TiffReader.Decode(data);
        }

        if (kind == ".png") {
            var (width, height, channels, samples) = PngCodec.Decode16(data);
            var values = new float[samples.Length];
            for (var at = 0; at < values.Length; at++) {
                values[at] = samples[at] / (float)ushort.MaxValue;
            }

            return new(width, height, channels, values);
        }

        return Decoded(data, kind);
    }

    /// <summary>Whether a file holds more than a byte can, which a graph then reads at its own depth.</summary>
    public static bool IsDeep(string path) {
        var kind = Path.GetExtension(path).ToLowerInvariant();
        return kind is ".exr" or ".hdr"
            || (kind == ".png" && PngDepth(path) == 16)
            || (kind is ".tif" or ".tiff" && TiffReader.BitsPerSampleOf(SharedFile.ReadAllBytes(path)) > 8);
    }

    static byte PngDepth(string path) {
        Span<byte> head = stackalloc byte[PngDepthAt + 1];
        using var file = SharedFile.OpenRead(path);
        return file.Read(head) > PngDepthAt ? head[PngDepthAt] : (byte)0;
    }

    static FloatImage Decoded(byte[] data, string kind) {
        if (!PictureFormats.Pictures.Contains(kind, StringComparer.Ordinal)) {
            throw new InvalidDataException($"'{kind}' is not a picture this reads.");
        }

        using var stream = new MemoryStream(data);
        var picture = (ImageDecoders.For(ImageDecoders.BuiltIn, kind) ?? new StbImageDecoder()).Decode(stream, kind);
        var bytes = picture.PixelSpan();
        var values = new float[picture.Width * picture.Height * 4];
        if (picture.Format == PixelFormat.Rgba32Float) {
            MemoryMarshal.Cast<byte, float>(bytes).CopyTo(values);
        } else {
            for (var at = 0; at < values.Length; at++) {
                values[at] = bytes[at] / (float)byte.MaxValue;
            }
        }

        return new(picture.Width, picture.Height, 4, values);
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
            ExportMaps.Normal
                or ExportMaps.Height
                or ExportMaps.Curvature
                or ExportMaps.Thickness
                or ExportMaps.Id
                or ExportMaps.Position
                or ExportMaps.WorldNormal
                or ExportMaps.BentNormal => bakedMap,
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
