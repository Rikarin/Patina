using Patina.Contracts;
using Patina.Export.Presets;
using System.Runtime.InteropServices;
using Vixen.Core.Imaging;
using Vixen.Core.Imaging.BlockCompression;
using Vixen.Graphics;

namespace Patina.Export;

/// <summary>A region of one file as the file holds it, beside the same region before it was written.</summary>
/// <param name="Source">The numbers the file was made from, at the level and in the lanes it holds.</param>
/// <param name="Written">The same numbers read back out of what the file holds.</param>
/// <param name="Region">Where in the level the region is: x, y, width and height in texels.</param>
/// <param name="Size">The level's width and height.</param>
/// <param name="Levels">How many levels the file has.</param>
public sealed record CompressionRegion(
    FloatImage Source,
    FloatImage Written,
    (int X, int Y, int Width, int Height) Region,
    (int Width, int Height) Size,
    int Levels
);

/// <summary>
///     What a file's encoding does to its numbers (#217), as an engine reads them: a block-compressed level decoded
///     again, or a JPEG or PNG read back. Blocks are four by four and each is made alone, so a region on block lines
///     comes out exactly as it is in the whole file, and only that region is compressed: a 4K BC7 file can be shown in
///     a moment. Mips are made from the whole picture first, as the file makes them.
/// </summary>
public static class CompressionPreview {
    /// <summary>The largest side of a region, which bounds what one preview costs.</summary>
    public const int MaxSide = 512;

    /// <summary>A region of a file, centred where asked, as it was and as the file holds it.</summary>
    /// <param name="picture">The file's lanes, finished: padded and sized as the export writes them.</param>
    /// <param name="texture">What the file is.</param>
    /// <param name="level">The mip level, which a file without mips has only zero of.</param>
    /// <param name="centre">Where the region is centred, from 0 to 1 across and down.</param>
    /// <param name="side">The region's largest side, up to <see cref="MaxSide" />.</param>
    /// <param name="cancellationToken">Stops block compression.</param>
    /// <exception cref="InvalidDataException">A format there is no reader here for.</exception>
    public static CompressionRegion Make(
        PackedPicture picture,
        ExportTexture texture,
        int level,
        (double X, double Y) centre,
        int side,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(picture);
        ArgumentNullException.ThrowIfNull(texture);
        if (!ExportFormats.IsContainer(texture.Format)) {
            return Plain(picture, texture, centre, side, cancellationToken);
        }

        var levels = ExportEncoder.Levels(picture, texture);
        level = Math.Clamp(level, 0, levels.LevelCount - 1);
        var extent = levels.Levels[level];
        var region = Region((extent.Width, extent.Height), centre, side);
        var crop = new TextureData(levels.Format, region.Width, region.Height, 1);
        Copy(levels.Level(level), extent.Width, region, crop.LevelSpan(0), levels.Format.LevelSize(1, 1));
        var source = Numbers(crop, texture.Channels.Count);
        var held = ExportEncoder.Blocks(crop, texture, cancellationToken);
        var written = held.Format.IsCompressed()
            ? Numbers(BlockCompressor.Decode(held), texture.Channels.Count)
            : source;
        return new(source, written, region, (extent.Width, extent.Height), levels.LevelCount);
    }

    /// <summary>
    ///     A plain file's region, written and read back, which shows a JPEG's loss and an 8-bit file's steps.
    /// </summary>
    static CompressionRegion Plain(
        PackedPicture picture,
        ExportTexture texture,
        (double X, double Y) centre,
        int side,
        CancellationToken cancellationToken
    ) {
        var region = Region((picture.Width, picture.Height), centre, side);
        var lanes = picture.Lanes;
        var samples = new float[region.Width * region.Height * lanes];
        for (var row = 0; row < region.Height; row++) {
            Array.Copy(
                picture.Samples,
                ((region.Y + row) * picture.Width + region.X) * lanes,
                samples,
                row * region.Width * lanes,
                region.Width * lanes
            );
        }

        var crop = new PackedPicture(region.Width, region.Height, lanes, samples, []);
        var (bytes, _) = ExportEncoder.Encode(crop, texture, texture.Dither ?? false, cancellationToken);
        var read = MapPictures.Decode(bytes, ExportFormats.ExtensionOf(texture.Format));
        var source = new FloatImage(region.Width, region.Height, lanes, samples);
        return new(source, Lanes(read, lanes), region, (picture.Width, picture.Height), 1);
    }

    /// <summary>A region on block lines, of the side asked for or the level's, centred as near as it can be.</summary>
    static (int X, int Y, int Width, int Height) Region(
        (int Width, int Height) size,
        (double X, double Y) centre,
        int side
    ) {
        side = Math.Clamp(side, 4, MaxSide);
        var width = Math.Min(size.Width, side);
        var height = Math.Min(size.Height, side);
        var x = (int)Math.Round(centre.X * size.Width - width / 2.0);
        var y = (int)Math.Round(centre.Y * size.Height - height / 2.0);
        x = Math.Clamp(x / 4 * 4, 0, size.Width - width);
        y = Math.Clamp(y / 4 * 4, 0, size.Height - height);
        return (x, y, width, height);
    }

    static void Copy(
        ReadOnlySpan<byte> level,
        int levelWidth,
        (int X, int Y, int Width, int Height) region,
        Span<byte> into,
        long texel
    ) {
        var size = (int)texel;
        for (var row = 0; row < region.Height; row++) {
            level.Slice(((region.Y + row) * levelWidth + region.X) * size, region.Width * size)
                .CopyTo(into[(row * region.Width * size)..]);
        }
    }

    /// <summary>The first level of RGBA texels as numbers, in a file's lanes.</summary>
    static FloatImage Numbers(TextureData data, int lanes) {
        var texels = data.Width * data.Height;
        var bytes = data.Level(0);
        var rgba = new float[texels * 4];
        switch (data.Format) {
            case PixelFormat.Rgba8UNorm or PixelFormat.Rgba8UNormSrgb:
                for (var at = 0; at < rgba.Length; at++) {
                    rgba[at] = bytes[at] / 255f;
                }

                break;
            case PixelFormat.Rgba16UNorm:
                var wide = MemoryMarshal.Cast<byte, ushort>(bytes);
                for (var at = 0; at < rgba.Length; at++) {
                    rgba[at] = wide[at] / 65535f;
                }

                break;
            case PixelFormat.Rgba16Float:
                var half = MemoryMarshal.Cast<byte, Half>(bytes);
                for (var at = 0; at < rgba.Length; at++) {
                    rgba[at] = (float)half[at];
                }

                break;
            case PixelFormat.Rgba32Float:
                MemoryMarshal.Cast<byte, float>(bytes).CopyTo(rgba);
                break;
            default:
                throw new InvalidDataException($"{data.Format} is not a format this reads back.");
        }

        return Lanes(new(data.Width, data.Height, 4, rgba), lanes);
    }

    /// <summary>A picture cut to a file's first lanes; a grey picture's one lane fills any it lacks.</summary>
    static FloatImage Lanes(FloatImage image, int lanes) {
        if (image.Channels == lanes) {
            return image;
        }

        var texels = image.Width * image.Height;
        var samples = new float[texels * lanes];
        for (var texel = 0; texel < texels; texel++) {
            for (var lane = 0; lane < lanes; lane++) {
                samples[texel * lanes + lane] = image.Samples[texel * image.Channels
                    + Math.Min(lane, image.Channels - 1)];
            }
        }

        return image with { Channels = lanes, Samples = samples };
    }

    /// <summary>
    ///     How far the written numbers are from the source, per lane: the peak signal-to-noise ratio against a peak
    ///     of one (or the brightest source value, for float files), the largest error and the mean one, in the file's
    ///     own numbers.
    /// </summary>
    public static IReadOnlyList<CompressionLaneError> Errors(CompressionRegion region) {
        ArgumentNullException.ThrowIfNull(region);
        var (source, written) = (region.Source, region.Written);
        List<CompressionLaneError> errors = [];
        for (var lane = 0; lane < source.Channels; lane++) {
            double squared = 0, total = 0, largest = 0, peak = 1;
            var texels = source.Width * source.Height;
            for (var texel = 0; texel < texels; texel++) {
                var a = source.Samples[texel * source.Channels + lane];
                var b = written.Samples[texel * written.Channels + lane];
                var error = Math.Abs(a - b);
                squared += error * error;
                total += error;
                largest = Math.Max(largest, error);
                peak = Math.Max(peak, a);
            }

            var mean = squared / Math.Max(1, texels);
            errors.Add(
                new(
                    "rgba"[lane].ToString(),
                    mean <= 0 ? null : 10 * Math.Log10(peak * peak / mean),
                    largest,
                    total / Math.Max(1, texels)
                )
            );
        }

        return errors;
    }

    /// <summary>
    ///     The difference as a grey picture, each texel its largest error over the lanes times
    ///     <paramref name="gain" />, so an error too small to see is seen.
    /// </summary>
    public static FloatImage Difference(CompressionRegion region, double gain) {
        ArgumentNullException.ThrowIfNull(region);
        var (source, written) = (region.Source, region.Written);
        var texels = source.Width * source.Height;
        var samples = new float[texels];
        for (var texel = 0; texel < texels; texel++) {
            float largest = 0;
            for (var lane = 0; lane < source.Channels; lane++) {
                largest = Math.Max(
                    largest,
                    Math.Abs(
                        source.Samples[texel * source.Channels + lane]
                        - written.Samples[texel * written.Channels + lane]
                    )
                );
            }

            samples[texel] = Math.Clamp((float)(largest * gain), 0f, 1f);
        }

        return source with { Channels = 1, Samples = samples };
    }

    /// <summary>
    ///     A picture as an 8-bit PNG for a person: its lanes as they are, a two-lane picture as red and green, and
    ///     alpha left out so that what is under it shows.
    /// </summary>
    public static byte[] Png(FloatImage image) {
        ArgumentNullException.ThrowIfNull(image);
        var texels = image.Width * image.Height;
        var lanes = image.Channels == 1 ? 1 : 3;
        var bytes = new byte[texels * lanes];
        for (var texel = 0; texel < texels; texel++) {
            for (var lane = 0; lane < lanes; lane++) {
                var value = lane < image.Channels ? image.Samples[texel * image.Channels + lane] : 0f;
                bytes[texel * lanes + lane] = (byte)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
            }
        }

        return PngCodec.Encode8(image.Width, image.Height, lanes, bytes);
    }
}
