using Patina.Contracts;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Patina.Library;

/// <summary>
///     How large a picture is, read from its header alone, so the index can say it of every picture in a library
///     without decoding one (#306). A picture whose header this does not know, or that is cut short, has no size.
/// </summary>
public static class PictureSizes {
    /// <summary>The most of a file read: a header is near the start, and a JPEG's frame follows its metadata.</summary>
    const int MostRead = 128 * 1024;

    /// <summary>The most of a Radiance picture's text header read for the line that gives its size.</summary>
    const int RadianceHeader = 4096;

    /// <summary>A picture's width and height in texels, or null when its header does not say.</summary>
    /// <param name="file">The picture's whole path.</param>
    /// <exception cref="IOException">It could not be read.</exception>
    public static (int Width, int Height)? Of(string file) {
        using var stream = SharedFile.OpenRead(file);
        var head = new byte[(int)Math.Min(stream.Length, MostRead)];
        stream.ReadExactly(head);
        return Of(head, Path.GetExtension(file));
    }

    /// <summary>A picture's width and height from its first bytes, or null when they do not say.</summary>
    /// <param name="head">The start of the file.</param>
    /// <param name="extension">Its extension, which a TGA, having no signature, is known by.</param>
    public static (int Width, int Height)? Of(ReadOnlySpan<byte> head, string extension) {
        var size = head switch {
            [0x89, (byte)'P', (byte)'N', (byte)'G', ..] when head.Length >= 24 => Big(head, 16, 20),
            [(byte)'G', (byte)'I', (byte)'F', (byte)'8', ..] when head.Length >= 10 => Gif(head),
            [(byte)'B', (byte)'M', ..] when head.Length >= 26 => Bmp(head),
            [(byte)'8', (byte)'B', (byte)'P', (byte)'S', ..] when head.Length >= 22 => Big(head, 18, 14),
            [0xFF, 0xD8, ..] => Jpeg(head),
            [(byte)'R', (byte)'I', (byte)'F', (byte)'F', ..] when head.Length >= 30 => WebP(head),
            [0x76, 0x2F, 0x31, 0x01, ..] => Exr(head),
            [(byte)'#', (byte)'?', ..] => Radiance(head),
            [(byte)'I', (byte)'I', 0x2A, 0x00, ..] => Tiff(head, true),
            [(byte)'M', (byte)'M', 0x00, 0x2A, ..] => Tiff(head, false),
            _ when extension.Equals(".tga", StringComparison.OrdinalIgnoreCase) && head.Length >= 18 => Tga(head),
            _ => null
        };
        return size is var (width, height) && width > 0 && height > 0 ? size : null;
    }

    static (int, int) Big(ReadOnlySpan<byte> head, int width, int height) =>
        ((int)BinaryPrimitives.ReadUInt32BigEndian(head[width..]),
            (int)BinaryPrimitives.ReadUInt32BigEndian(head[height..]));

    static (int, int) Gif(ReadOnlySpan<byte> head) =>
        (BinaryPrimitives.ReadUInt16LittleEndian(head[6..]), BinaryPrimitives.ReadUInt16LittleEndian(head[8..]));

    /// <summary>A bitmap's DIB header: the oldest is twelve bytes of 16-bit sides; any later one has 32-bit ones.</summary>
    static (int, int) Bmp(ReadOnlySpan<byte> head) =>
        BinaryPrimitives.ReadUInt32LittleEndian(head[14..]) == 12
            ? (BinaryPrimitives.ReadUInt16LittleEndian(head[18..]), BinaryPrimitives.ReadUInt16LittleEndian(head[20..]))
            : (BinaryPrimitives.ReadInt32LittleEndian(head[18..]),
                Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(head[22..])));

    static (int, int) Tga(ReadOnlySpan<byte> head) =>
        (BinaryPrimitives.ReadUInt16LittleEndian(head[12..]), BinaryPrimitives.ReadUInt16LittleEndian(head[14..]));

    /// <summary>The first frame header among a JPEG's segments, past whatever metadata comes first.</summary>
    static (int, int)? Jpeg(ReadOnlySpan<byte> head) {
        var at = 2;
        while (at + 9 <= head.Length && head[at] == 0xFF) {
            var marker = head[at + 1];
            if (marker == 0xFF) {
                at++; // fill
                continue;
            }

            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC)) {
                return (BinaryPrimitives.ReadUInt16BigEndian(head[(at + 7)..]),
                    BinaryPrimitives.ReadUInt16BigEndian(head[(at + 5)..]));
            }

            at += 2 + BinaryPrimitives.ReadUInt16BigEndian(head[(at + 2)..]);
        }

        return null;
    }

    /// <summary>A WebP's first chunk: lossy, lossless, or the extended header that holds the canvas.</summary>
    static (int, int)? WebP(ReadOnlySpan<byte> head) {
        if (!head[8..12].SequenceEqual("WEBP"u8)) {
            return null;
        }

        var chunk = head[12..16];
        if (chunk.SequenceEqual("VP8 "u8)) {
            return (BinaryPrimitives.ReadUInt16LittleEndian(head[26..]) & 0x3FFF,
                BinaryPrimitives.ReadUInt16LittleEndian(head[28..]) & 0x3FFF);
        }

        if (chunk.SequenceEqual("VP8L"u8)) {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(head[21..]);
            return ((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }

        return chunk.SequenceEqual("VP8X"u8) ? (Three(head[24..]) + 1, Three(head[27..]) + 1) : null;
    }

    static int Three(ReadOnlySpan<byte> bytes) => bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);

    /// <summary>An OpenEXR's attributes, name, type, size and value each, until its data window.</summary>
    static (int, int)? Exr(ReadOnlySpan<byte> head) {
        var at = 8;
        while (at < head.Length && head[at] != 0) {
            var name = head[at..].IndexOf((byte)0);
            var type = name < 0 ? -1 : head[(at + name + 1)..].IndexOf((byte)0);
            var value = at + name + 1 + type + 1 + 4;
            if (type < 0 || value > head.Length) {
                return null;
            }

            var length = BinaryPrimitives.ReadInt32LittleEndian(head[(value - 4)..]);
            if (head.Slice(at, name).SequenceEqual("dataWindow"u8) && length == 16 && value + 16 <= head.Length) {
                var box = head[value..];
                return (BinaryPrimitives.ReadInt32LittleEndian(box[8..]) - BinaryPrimitives.ReadInt32LittleEndian(box)
                    + 1,
                    BinaryPrimitives.ReadInt32LittleEndian(box[12..])
                    - BinaryPrimitives.ReadInt32LittleEndian(box[4..])
                    + 1);
            }

            at = value + length;
        }

        return null;
    }

    /// <summary>A Radiance picture's text header, then the line that gives its rows and columns.</summary>
    static (int, int)? Radiance(ReadOnlySpan<byte> head) {
        var text = Encoding.ASCII.GetString(head[..Math.Min(head.Length, RadianceHeader)]);
        var end = text.IndexOf("\n\n", StringComparison.Ordinal);
        if (end < 0) {
            return null;
        }

        var line = text[(end + 2)..].Split('\n')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (line.Length != 4
            || !int.TryParse(line[1], NumberStyles.None, CultureInfo.InvariantCulture, out var first)
            || !int.TryParse(line[3], NumberStyles.None, CultureInfo.InvariantCulture, out var second)) {
            return null;
        }

        // "-Y 512 +X 1024" is 512 rows of 1024; a picture turned on its side names X first.
        return line[0].EndsWith('Y') ? (second, first) : (first, second);
    }

    /// <summary>A TIFF's first directory, whose entries 256 and 257 are its width and height.</summary>
    static (int, int)? Tiff(ReadOnlySpan<byte> head, bool little) {
        if (head.Length < 8) {
            return null;
        }

        var directory = (int)Four(head[4..], little);
        if (directory < 8 || directory + 2 > head.Length) {
            return null;
        }

        var entries = Two(head[directory..], little);
        int? width = null;
        int? height = null;
        for (var entry = 0; entry < entries; entry++) {
            var at = directory + 2 + entry * 12;
            if (at + 12 > head.Length) {
                break;
            }

            var tag = Two(head[at..], little);
            // A SHORT lies in the first two bytes of the value, a LONG fills all four.
            var value = Two(head[(at + 2)..], little) == 3
                ? Two(head[(at + 8)..], little)
                : (int)Four(head[(at + 8)..], little);
            width = tag == 256 ? value : width;
            height = tag == 257 ? value : height;
        }

        return width is { } w && height is { } h ? (w, h) : null;
    }

    static int Two(ReadOnlySpan<byte> bytes, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes);

    static uint Four(ReadOnlySpan<byte> bytes, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes);
}
