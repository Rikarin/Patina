using Patina.Export.Presets;
using System.Buffers.Binary;
using System.Text;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>
///     Every DDS and KTX2 file an export can write, read by a parser written here from the published layouts and not
///     from the writer (#46): Microsoft's DDS_HEADER and DDS_HEADER_DXT10 with DXGI_FORMAT's numbers, and Khronos's
///     KTX 2.0 header and level index with VkFormat's. An engine that reads the format by the book reads these. BC6H,
///     16-bit and float DDS had been read by no outside tool, which is what this stands in for until one does.
/// </summary>
public sealed class ContainerHeaderTests : IDisposable {
    const int Width = 16;
    const int Height = 8;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-containers-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    /// <summary>
    ///     A compression, the lanes and space of the file, and the DXGI and Vulkan formats the specs give it.
    /// </summary>
    public static TheoryData<string, int, string, int[], int[]> Formats() =>
        new() {
            {
                ExportCompressions.Bc1,
                3,
                "linear",
                [71],
                [131, 133]
            },
            {
                ExportCompressions.Bc1,
                3,
                "srgb",
                [72],
                [132, 134]
            },
            {
                ExportCompressions.Bc3,
                4,
                "linear",
                [77],
                [137]
            },
            {
                ExportCompressions.Bc3,
                4,
                "srgb",
                [78],
                [138]
            },
            {
                ExportCompressions.Bc4,
                1,
                "linear",
                [80],
                [139]
            },
            {
                ExportCompressions.Bc5,
                2,
                "linear",
                [83],
                [141]
            },
            {
                ExportCompressions.Bc6H,
                3,
                "linear",
                [95],
                [143]
            },
            {
                ExportCompressions.Bc7,
                4,
                "linear",
                [98],
                [145]
            },
            {
                ExportCompressions.Bc7,
                4,
                "srgb",
                [99],
                [146]
            },
            {
                ExportCompressions.Half,
                4,
                "linear",
                [10],
                [97]
            },
            {
                ExportCompressions.Wide,
                4,
                "linear",
                [2],
                [109]
            },
            {
                ExportCompressions.None,
                4,
                "linear",
                [11],
                [91]
            },
            {
                ExportCompressions.None,
                4,
                "srgb",
                [29, 91],
                [43, 50]
            }
        };

    static string Lanes(int count) =>
        count switch {
            1 => "[roughness]",
            2 => "[roughness, metalness]",
            3 => "[baseColor.r, baseColor.g, baseColor.b]",
            _ => "[baseColor.r, baseColor.g, baseColor.b, roughness]"
        };

    byte[] Write(string format, string compression, int lanes, string space) {
        var preset = ExportPresetReader.Read(
            $"""
             version: 1
             id: c
             name: C
             normal: y-up
             textures:
               - file: x
                 format: {format}
                 compression: {compression}
                 space: {space}
                 mips: true
                 channels: {Lanes(lanes)}
             """,
            "c"
        );
        Dictionary<string, MapPicture> maps = new(StringComparer.Ordinal) {
            [ExportMaps.BaseColor] = new(ExportMaps.BaseColor, Ramp(3), GreenAxis.Up),
            [ExportMaps.Roughness] = new(ExportMaps.Roughness, Ramp(1), GreenAxis.Up),
            [ExportMaps.Metalness] = new(ExportMaps.Metalness, Ramp(1), GreenAxis.Up)
        };
        var made = Path.Combine(folder.FullName, $"{format}-{compression}-{space}");
        var manifest = ExportRun.Run(preset, maps, made, null, Token);
        return File.ReadAllBytes(Path.Combine(made, Assert.Single(manifest.Files).File));
    }

    static FloatImage Ramp(int lanes) {
        var samples = new float[Width * Height * lanes];
        for (var at = 0; at < samples.Length; at++) {
            samples[at] = at % 7 / 7f;
        }

        return new(Width, Height, lanes, samples);
    }

    /// <summary>The bytes of one level: 4 by 4 blocks for BC, rounded up, or texels for a plain format.</summary>
    static long LevelBytes(string compression, string space, int width, int height) {
        var blocks = (long)((width + 3) / 4) * ((height + 3) / 4);
        return compression switch {
            ExportCompressions.Bc1 or ExportCompressions.Bc4 => blocks * 8,
            ExportCompressions.Bc3 or ExportCompressions.Bc5 or ExportCompressions.Bc6H or ExportCompressions.Bc7 =>
                blocks * 16,
            ExportCompressions.Half => (long)width * height * 8,
            ExportCompressions.Wide => (long)width * height * 16,
            _ when space == "linear" => (long)width * height * 8, // a plain linear container is 16 bits a lane
            _ => (long)width * height * 4
        };
    }

    static uint U32(byte[] data, int at) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));

    static ulong U64(byte[] data, int at) => BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(at));

    /// <summary>
    ///     The legacy FourCC a DDS may use in place of a DX10 header for these, from the DDS programming guide.
    /// </summary>
    static string[] LegacyFourCc(string compression) =>
        compression switch {
            ExportCompressions.Bc1 => ["DXT1"],
            ExportCompressions.Bc3 => ["DXT5"],
            ExportCompressions.Bc4 => ["ATI1", "BC4U"],
            ExportCompressions.Bc5 => ["ATI2", "BC5U"],
            _ => []
        };

    [Theory]
    [MemberData(nameof(Formats))]
    public void A_dds_file_is_laid_out_as_microsofts_header_says(
        string compression,
        int lanes,
        string space,
        int[] dxgi,
        int[] vulkan
    ) {
        _ = vulkan;
        var data = Write(ExportFormats.Dds, compression, lanes, space);

        Assert.Equal("DDS "u8.ToArray(), data[..4]);
        Assert.Equal(124u, U32(data, 4));
        var flags = U32(data, 8);
        Assert.Equal(0x1u | 0x2u | 0x4u | 0x1000u, flags & (0x1u | 0x2u | 0x4u | 0x1000u));
        Assert.Equal((uint)Height, U32(data, 12));
        Assert.Equal((uint)Width, U32(data, 16));
        var levels = Math.Max(1, (int)U32(data, 28));
        Assert.Equal(5, levels); // 16 × 8 down to 1 × 1
        Assert.NotEqual(0u, flags & 0x20000u); // DDSD_MIPMAPCOUNT
        Assert.Equal(32u, U32(data, 76));
        Assert.NotEqual(0u, U32(data, 108) & 0x1000u); // DDSCAPS_TEXTURE

        var pixelFlags = U32(data, 80);
        var fourCc = Encoding.ASCII.GetString(data, 84, 4);
        var start = 128;
        if ((pixelFlags & 0x4u) != 0 && fourCc == "DX10") {
            Assert.Contains((int)U32(data, 128), dxgi);
            Assert.Equal(3u, U32(data, 132)); // D3D10_RESOURCE_DIMENSION_TEXTURE2D
            Assert.Equal(1u, U32(data, 140)); // one array element
            start = 148;
        } else if ((pixelFlags & 0x4u) != 0) {
            Assert.Contains(fourCc, LegacyFourCc(compression));
        } else {
            Assert.Equal(ExportCompressions.None, compression);
            Assert.Equal(space == "linear" ? 64u : 32u, U32(data, 88)); // four lanes of 16 or 8 bits
        }

        long expected = 0;
        for (int level = 0, w = Width, h = Height;
             level < levels;
             level++, w = Math.Max(1, w / 2), h = Math.Max(1, h / 2)) {
            expected += LevelBytes(compression, space, w, h);
        }

        Assert.Equal(start + expected, data.LongLength);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void A_ktx2_file_is_laid_out_as_the_khronos_spec_says(
        string compression,
        int lanes,
        string space,
        int[] dxgi,
        int[] vulkan
    ) {
        _ = dxgi;
        var data = Write(ExportFormats.Ktx2, compression, lanes, space);

        Assert.Equal(
            new byte[] { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A },
            data[..12]
        );
        Assert.Contains((int)U32(data, 12), vulkan);
        Assert.Equal((uint)Width, U32(data, 20));
        Assert.Equal((uint)Height, U32(data, 24));
        Assert.Equal(0u, U32(data, 28)); // not a volume
        Assert.True(U32(data, 32) is 0 or 1); // not an array
        Assert.Equal(1u, U32(data, 36)); // one face
        var levels = (int)U32(data, 40);
        Assert.Equal(5, levels);
        Assert.Equal(0u, U32(data, 44)); // no supercompression
        Assert.NotEqual(0u, U32(data, 48)); // a data format descriptor is there
        Assert.NotEqual(0u, U32(data, 52));

        for (int level = 0, w = Width, h = Height;
             level < levels;
             level++, w = Math.Max(1, w / 2), h = Math.Max(1, h / 2)) {
            var offset = U64(data, 80 + level * 24);
            var length = U64(data, 88 + level * 24);
            Assert.Equal((ulong)LevelBytes(compression, space, w, h), length);
            Assert.True(offset + length <= (ulong)data.LongLength, $"level {level} runs past the end.");
        }
    }
}
