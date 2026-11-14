using Patina.Contracts;
using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Patina.Library;

/// <summary>
///     Evaluation results kept on disk between sessions, one file per content key, in <c>.ptcache</c> (#208). What a
///     stack's layers computed comes back after a save and a restart without running, and deleting the folder loses
///     nothing but that time. Nothing here knows about a GPU: a result is a key, a shape and bytes.
/// </summary>
/// <remarks>
///     A key says nothing about the device that computed it, so a shelf is one adapter's: <see cref="For" /> names the
///     folder after the adapter, its driver and the build. The oldest results go once the shelf holds more than its
///     budget, oldest by when they were last read or written.
/// </remarks>
public sealed class ResultShelf {
    /// <summary>What a shelf holds before it lets the oldest results go: eight gibibytes.</summary>
    public const long DefaultBudget = 8L << 30;

    const string Extension = ".result";
    const uint Magic = 0x53525450; // "PTRS"
    const int Version = 1;
    const int HeaderBytes = 20;

    readonly string folder;
    readonly Lock gate = new();
    HashSet<UInt128>? known;

    /// <summary>A shelf over a folder, which is made when the first result is written.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="budget">How many bytes it may hold on disk.</param>
    public ResultShelf(string folder, long budget = DefaultBudget) {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentOutOfRangeException.ThrowIfNegative(budget);
        this.folder = folder;
        Budget = budget;
    }

    /// <summary>The folder it keeps results in.</summary>
    public string Folder => folder;

    /// <summary>How many bytes of results it may hold on disk.</summary>
    public long Budget { get; }

    /// <summary>The shelf for one adapter under a library's results folder.</summary>
    /// <param name="results">The library's results folder, <see cref="LibraryLayout.ResultsPath" />.</param>
    /// <param name="adapter">What says the device: its name and its driver's version, and the build.</param>
    public static ResultShelf For(string results, string adapter) {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(adapter));
        return new(Path.Combine(results, Convert.ToHexStringLower(digest.AsSpan(0, 8))));
    }

    /// <summary>Whether a result is kept, from a listing taken once, so an evaluation's misses stay cheap.</summary>
    public bool Has(UInt128 key) {
        lock (gate) {
            return Known().Contains(key);
        }
    }

    /// <summary>The texels kept under a key, or null when there are none of that shape, or they do not read.</summary>
    /// <param name="key">The key.</param>
    /// <param name="format">The format's number, as the evaluator numbers formats.</param>
    /// <param name="width">The width in texels.</param>
    /// <param name="height">The height in texels.</param>
    /// <param name="length">How many bytes the texels are.</param>
    public byte[]? Read(UInt128 key, int format, int width, int height, long length) {
        var path = PathOf(key);
        try {
            using var stream = SharedFile.OpenRead(path);
            Span<byte> header = stackalloc byte[HeaderBytes];
            stream.ReadExactly(header);
            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic
                || BinaryPrimitives.ReadInt32LittleEndian(header[4..]) != Version
                || BinaryPrimitives.ReadInt32LittleEndian(header[8..]) != format
                || BinaryPrimitives.ReadInt32LittleEndian(header[12..]) != width
                || BinaryPrimitives.ReadInt32LittleEndian(header[16..]) != height) {
                return null;
            }

            var texels = new byte[length];
            using (var inflate = new ZLibStream(stream, CompressionMode.Decompress)) {
                inflate.ReadExactly(texels);
                if (inflate.ReadByte() >= 0) {
                    return null;
                }
            }

            Touch(path);
            return texels;
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) {
            lock (gate) {
                known?.Remove(key);
            }

            return null;
        }
    }

    /// <summary>Keeps a result, then lets the oldest go if the shelf is over its budget.</summary>
    /// <param name="key">The key.</param>
    /// <param name="format">The format's number.</param>
    /// <param name="width">The width in texels.</param>
    /// <param name="height">The height in texels.</param>
    /// <param name="texels">The texels.</param>
    public void Write(UInt128 key, int format, int width, int height, ReadOnlySpan<byte> texels) {
        Directory.CreateDirectory(folder);
        var path = PathOf(key);
        var temporary = $"{path}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.tmp";
        using (var stream = File.Create(temporary)) {
            Span<byte> header = stackalloc byte[HeaderBytes];
            BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
            BinaryPrimitives.WriteInt32LittleEndian(header[4..], Version);
            BinaryPrimitives.WriteInt32LittleEndian(header[8..], format);
            BinaryPrimitives.WriteInt32LittleEndian(header[12..], width);
            BinaryPrimitives.WriteInt32LittleEndian(header[16..], height);
            stream.Write(header);
            // Fastest: a fill compresses to almost nothing at any level, and a save waits for this.
            using (var deflate = new ZLibStream(stream, CompressionLevel.Fastest, true)) {
                deflate.Write(texels);
            }

            stream.Flush(true);
        }

        SharedFile.MoveOver(temporary, path);
        lock (gate) {
            Known().Add(key);
        }

        Trim();
    }

    /// <summary>Lets the results least recently used go until the shelf is within its budget.</summary>
    public void Trim() {
        if (!Directory.Exists(folder)) {
            return;
        }

        var files = new DirectoryInfo(folder).EnumerateFiles($"*{Extension}").ToList();
        var total = files.Sum(static file => file.Length);
        foreach (var file in files.OrderBy(static file => file.LastWriteTimeUtc)) {
            if (total <= Budget) {
                break;
            }

            try {
                // ⚠ Measured before: a deleted FileInfo forgets its length, and asking again throws.
                var length = file.Length;
                file.Delete();
                total -= length;
                lock (gate) {
                    if (KeyOf(file.Name) is { } key) {
                        known?.Remove(key);
                    }
                }
            } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                // Another process is reading it; it goes next time.
            }
        }
    }

    string PathOf(UInt128 key) => Path.Combine(folder, key.ToString("x32", CultureInfo.InvariantCulture) + Extension);

    static UInt128? KeyOf(string file) =>
        UInt128.TryParse(Path.GetFileNameWithoutExtension(file), NumberStyles.HexNumber, null, out var key)
            ? key
            : null;

    HashSet<UInt128> Known() {
        if (known is not null) {
            return known;
        }

        known = [];
        if (!Directory.Exists(folder)) {
            return known;
        }

        foreach (var file in Directory.EnumerateFiles(folder, $"*{Extension}")) {
            if (KeyOf(file) is { } key) {
                known.Add(key);
            }
        }

        return known;
    }

    /// <summary>Marks a result as just used, which is what the budget lets go of last.</summary>
    static void Touch(string path) {
        try {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            // Only the order of letting go suffers.
        }
    }
}
