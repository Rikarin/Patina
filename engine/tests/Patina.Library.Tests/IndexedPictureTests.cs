using System.Buffers.Binary;
using System.Text;

namespace Patina.Library.Tests;

/// <summary>
///     Every project's pictures are in the index at any depth, with their sizes from their headers (#306), so a list
///     of them is a query and not a walk of every project's folder.
/// </summary>
public sealed class IndexedPictureTests : IDisposable {
    readonly TestLibrary library = new();

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => library.Dispose();

    void Picture(string relative, byte[] bytes) {
        var file = LibraryLayout.Full(library.Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, bytes);
        library.Age(file);
    }

    [Fact]
    public void A_rescan_lists_each_projects_pictures_at_any_depth_with_their_sizes() {
        library.Write("props/crate", TestLibrary.Project("Crate"));
        library.Write("hero", TestLibrary.Project("Hero"));
        Picture("projects/props/crate/pictures/worn/edges/rust.png", Png(2048, 1024));
        Picture("projects/props/crate/scratch.gif", Gif(16, 8));
        Picture("projects/hero/skin.tga", [1, 2, 3]); // cut short: listed, with no size
        Picture("projects/hero/bakes/maps/normal.png", Png(8, 8)); // a bake is not a picture of the user's
        Picture("projects/hero/mesh-mask-a.png", Png(8, 8)); // nor is a mask the Server drew
        Picture("projects/loose.png", Png(4, 4)); // in no project
        using var open = library.Open();

        open.Rescan(Token);

        LibraryPicture[] expected = [
            new("projects/hero/skin.tga", "hero", "Hero", null, null),
            new("projects/props/crate/pictures/worn/edges/rust.png", "props/crate", "Crate", 2048, 1024),
            new("projects/props/crate/scratch.gif", "props/crate", "Crate", 16, 8)
        ];
        Assert.Equal(expected, open.Pictures());
        Assert.Equal(0, open.Costs(string.Empty).Documents); // a picture is not a document that costs
    }

    [Fact]
    public void A_picture_saved_over_or_deleted_is_known_after_the_next_scan() {
        library.Write("crate", TestLibrary.Project("Crate"));
        Picture("projects/crate/a.png", Png(64, 64));
        Picture("projects/crate/b.png", Png(32, 32));
        using var open = library.Open();
        open.Rescan(Token);

        Picture("projects/crate/a.png", Png(128, 32));
        File.Delete(LibraryLayout.Full(library.Root, "projects/crate/b.png"));
        open.Rescan(Token);

        var picture = Assert.Single(open.Pictures());
        Assert.Equal(("projects/crate/a.png", 128, 32), (picture.Path, picture.Width, picture.Height));
    }

    public static TheoryData<string, byte[], int, int> Headers() =>
        new() {
            { ".png", Png(300, 200), 300, 200 },
            { ".gif", Gif(300, 200), 300, 200 },
            { ".bmp", Bmp(300, -200), 300, 200 },
            { ".psd", Psd(300, 200), 300, 200 },
            { ".tga", Tga(300, 200), 300, 200 },
            { ".jpg", Jpeg(300, 200), 300, 200 },
            { ".webp", WebP(300, 200), 300, 200 },
            { ".hdr", Encoding.ASCII.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 200 +X 300\n"), 300, 200 },
            { ".exr", Exr(300, 200), 300, 200 },
            { ".tif", Tiff(300, 200), 300, 200 }
        };

    [Theory]
    [MemberData(nameof(Headers))]
    public void A_pictures_size_is_read_from_its_header(string extension, byte[] head, int width, int height) =>
        Assert.Equal((width, height), PictureSizes.Of(head, extension));

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".exr")]
    public void A_header_cut_short_or_unknown_has_no_size(string extension) {
        Assert.Null(PictureSizes.Of([0x89, (byte)'P', (byte)'N', (byte)'G'], extension));
        Assert.Null(PictureSizes.Of([0xFF, 0xD8, 0xFF, 0xE0, 0xFF, 0xFF], extension));
        Assert.Null(PictureSizes.Of([0x76, 0x2F, 0x31, 0x01, 2, 0, 0, 0, (byte)'a'], extension));
        Assert.Null(PictureSizes.Of("not a picture at all"u8, extension));
    }

    static byte[] Png(int width, int height) {
        byte[] head = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, .."IHDR"u8, ..new byte[8]];
        BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(20), height);
        return head;
    }

    static byte[] Gif(int width, int height) {
        byte[] head = [.."GIF89a"u8, 0, 0, 0, 0];
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(6), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(8), (ushort)height);
        return head;
    }

    static byte[] Bmp(int width, int height) {
        var head = new byte[54];
        "BM"u8.CopyTo(head);
        BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(22), height); // negative: drawn top down
        return head;
    }

    static byte[] Psd(int width, int height) {
        var head = new byte[26];
        "8BPS"u8.CopyTo(head);
        BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(14), height);
        BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(18), width);
        return head;
    }

    static byte[] Tga(int width, int height) {
        var head = new byte[18];
        head[2] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(12), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(14), (ushort)height);
        return head;
    }

    /// <summary>A JPEG with a metadata segment before its frame, as a camera writes one.</summary>
    static byte[] Jpeg(int width, int height) {
        byte[] head = [0xFF, 0xD8, 0xFF, 0xE1, 0, 6, 1, 2, 3, 4, 0xFF, 0xC0, 0, 17, 8, 0, 0, 0, 0, 3];
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(15), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(17), (ushort)width);
        return head;
    }

    static byte[] WebP(int width, int height) {
        var head = new byte[30];
        "RIFF"u8.CopyTo(head);
        "WEBPVP8X"u8.CopyTo(head.AsSpan(8));
        head[24] = (byte)(width - 1);
        head[25] = (byte)((width - 1) >> 8);
        head[27] = (byte)(height - 1);
        head[28] = (byte)((height - 1) >> 8);
        return head;
    }

    /// <summary>An OpenEXR header with an attribute before its data window, which starts away from naught.</summary>
    static byte[] Exr(int width, int height) {
        List<byte> head = [0x76, 0x2F, 0x31, 0x01, 2, 0, 0, 0];
        head.AddRange([.."compression\0compression\0"u8, 1, 0, 0, 0, 0]);
        head.AddRange([.."dataWindow\0box2i\0"u8, 16, 0, 0, 0]);
        foreach (var value in (int[])[10, 20, 10 + width - 1, 20 + height - 1]) {
            head.AddRange(BitConverter.GetBytes(value));
        }

        head.Add(0);
        return [..head];
    }

    /// <summary>A big-endian TIFF whose width is a SHORT and whose height is a LONG.</summary>
    static byte[] Tiff(int width, int height) {
        var head = new byte[8 + 2 + 2 * 12];
        "MM"u8.CopyTo(head);
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(2), 42);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(10), 256);
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(12), 3);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(18), (ushort)width);
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(22), 257);
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(24), 4);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(30), (uint)height);
        return head;
    }
}
