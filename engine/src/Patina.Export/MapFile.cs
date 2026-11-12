using Patina.Export.Presets;
using Vixen.Core.Imaging;

namespace Patina.Export;

/// <summary>
///     One map written alone as a file of a chosen format: what a stack's own export writes when it is asked for more
///     than its PNGs (#47). It is the file a preset would write that reads only this map's lanes, so it is packed and
///     encoded by the same code, and its numbers move between sRGB and linear just as an export's do.
/// </summary>
public static class MapFile {
    /// <summary>The lane counts tried after the map's own, in order: drop alpha, then add grey, then one, then two.</summary>
    static readonly int[] Fallbacks = [3, 4, 1, 2];

    /// <summary>Why a file's settings cannot be written, or null.</summary>
    /// <param name="format">One of <see cref="ExportFormats.All" />.</param>
    /// <param name="compression">For KTX2 and DDS, one of <see cref="ExportCompressions.All" />, or null for none.</param>
    /// <param name="quality">JPEG's 1 to 100, or a block search's fast, balanced or best, or null for the default.</param>
    public static string? Problem(string format, string? compression, string? quality) {
        ArgumentNullException.ThrowIfNull(format);
        if (!ExportFormats.All.Contains(format, StringComparer.Ordinal)) {
            return $"'{format}' is not a format. It is one of {string.Join(", ", ExportFormats.All)}.";
        }

        var squeeze = compression ?? ExportCompressions.None;
        if (!ExportCompressions.All.Contains(squeeze, StringComparer.Ordinal)) {
            return $"'{squeeze}' is not a compression.";
        }

        if (squeeze != ExportCompressions.None && !ExportFormats.IsContainer(format)) {
            return $"{ExportFormats.LabelOf(format)} cannot be compressed; only ktx2 and dds can.";
        }

        return ExportPresetReader.QualityProblem(format, Quality(format, quality));
    }

    /// <summary>A file's quality, or the format's default when it names none.</summary>
    public static string Quality(string format, string? quality) =>
        quality ?? ExportPresetReader.DefaultQuality(format);

    /// <summary>
    ///     A map's file. It keeps the map's own lanes where the format holds them, and otherwise the nearest it does:
    ///     a JPEG of a colour with alpha drops the alpha, and a block that needs three lanes repeats a grey map's one.
    /// </summary>
    /// <param name="usage">The map's channel, which says whether its numbers are sRGB.</param>
    /// <param name="image">Its samples.</param>
    /// <param name="format">One of <see cref="ExportFormats.All" />.</param>
    /// <param name="compression">One of <see cref="ExportCompressions.All" />.</param>
    /// <param name="quality">What <see cref="Quality" /> settled on.</param>
    /// <param name="mips">Whether a KTX2 or DDS file carries a mip chain.</param>
    /// <param name="cancellationToken">Stops block compression between blocks.</param>
    /// <exception cref="ArgumentException">No lane count fits the format and compression.</exception>
    public static byte[] Encode(
        string usage,
        FloatImage image,
        string format,
        string compression,
        string quality,
        bool mips,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(image);
        var texture = Texture(usage, image.Channels, format, compression, quality, mips);
        var maps = new Dictionary<string, MapPicture>(StringComparer.Ordinal) {
            // Green is written as the map has it: the stack's own export has no target engine to turn it for.
            [usage] = new(usage, image, GreenAxis.Down)
        };
        var picture = ChannelPacker.Pack(texture, maps, GreenAxis.Down);
        return ExportEncoder.Encode(picture, texture, false, cancellationToken).Bytes;
    }

    /// <summary>The one texture a preset would hold for this map in this format.</summary>
    static ExportTexture Texture(
        string usage,
        int lanes,
        string format,
        string compression,
        string quality,
        bool mips
    ) {
        foreach (var count in (int[])[lanes, ..Fallbacks]) {
            foreach (var space in (string[])[ExportMaps.IsColour(usage) ? "srgb" : "linear", "linear"]) {
                if (ExportPresetReader.ShapeProblem(format, compression, space, count) is null) {
                    return new(
                        usage,
                        format,
                        compression,
                        quality,
                        space,
                        mips && ExportFormats.IsContainer(format),
                        [..Enumerable.Range(0, count).Select(lane => new ChannelSource(usage, lane, false, 0f))]
                    );
                }
            }
        }

        throw new ArgumentException(
            $"{usage} cannot be written as {ExportFormats.LabelOf(format)} with {compression}.",
            nameof(format)
        );
    }
}
