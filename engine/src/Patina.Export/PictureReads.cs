using Patina.Library;
using Vixen.Core.Imaging;

namespace Patina.Export;

/// <summary>
///     A picture read whole, and the PNG a client is shown of it (#260, #269), where pictures are decoded: in the
///     Worker (#293). A decoder is a parser of a file anybody made, and one that crashes must take down the Worker,
///     never the Server.
/// </summary>
public static class PictureReads {
    /// <summary>Reads a picture whole and, when asked, writes the PNG a client is shown of it.</summary>
    /// <param name="file">The picture.</param>
    /// <param name="preview">Where the PNG goes, or null for none.</param>
    /// <param name="side">The PNG's longer side at most, or null for the picture's own size.</param>
    /// <returns>Null when it read, or why it does not read as a picture.</returns>
    /// <exception cref="IOException">The file could not be read or the PNG written.</exception>
    public static string? Read(string file, string? preview, int? side) {
        FloatImage image;
        try {
            image = MapPictures.ReadFile(file);
        } catch (Exception e) when (IsUnreadable(e)) {
            return e.Message;
        }

        if (preview is not null) {
            LibraryFiles.Replace(preview, MapPictures.Preview(Fitted(image, side)));
        }

        return null;
    }

    /// <summary>
    ///     Writes a picture as a map brought into a bake (#300): a PNG of 16 bits a lane at its own size, a value
    ///     past 0 to 1 clamped and two lanes given an empty third, and with its green turned over when asked.
    /// </summary>
    /// <param name="file">The picture.</param>
    /// <param name="map">Where the PNG goes.</param>
    /// <param name="turnGreen">Whether the second lane becomes one less itself, for a green that ran down.</param>
    /// <returns>Null when it read, or why it does not read as a picture.</returns>
    /// <exception cref="IOException">The file could not be read or the PNG written.</exception>
    public static string? Map(string file, string map, bool turnGreen) {
        FloatImage image;
        try {
            image = MapPictures.ReadFile(file);
        } catch (Exception e) when (IsUnreadable(e)) {
            return e.Message;
        }

        var lanes = image.Channels == 2 ? 3 : image.Channels;
        var samples = new ushort[image.Width * image.Height * lanes];
        for (var texel = 0; texel < image.Width * image.Height; texel++) {
            for (var lane = 0; lane < image.Channels; lane++) {
                var value = Math.Clamp(image.Samples[texel * image.Channels + lane], 0f, 1f);
                value = turnGreen && lane == 1 ? 1f - value : value;
                samples[texel * lanes + lane] = (ushort)MathF.Round(value * ushort.MaxValue);
            }
        }

        LibraryFiles.Replace(map, PngCodec.Encode16(image.Width, image.Height, lanes, samples));
        return null;
    }

    /// <summary>
    ///     What a decoder throws for bytes that are not the picture they claim to be. A header that promises more than
    ///     the file holds runs a reader off its end as often as it says so.
    /// </summary>
    static bool IsUnreadable(Exception e) =>
        e is InvalidDataException
            or EndOfStreamException
            or ArgumentException
            or IndexOutOfRangeException
            or NotSupportedException
            or OverflowException;

    static FloatImage Fitted(FloatImage image, int? side) {
        if (side is not { } most || Math.Max(image.Width, image.Height) <= most) {
            return image;
        }

        var scale = (double)most / Math.Max(image.Width, image.Height);
        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));
        return ImageResize.Resize(image, width, height);
    }
}
