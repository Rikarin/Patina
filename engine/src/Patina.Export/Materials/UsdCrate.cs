using System.Buffers.Binary;
using System.Text;

namespace Patina.Export.Materials;

/// <summary>
///     A USD layer in its binary form (<c>.usdc</c>, USD's "crate", #157), as far as a material's surface needs: its
///     tables of tokens, strings, fields, field sets, paths and specs, from version 0.4, when USD began compressing
///     them, on. <see cref="UsdCrateValues" /> reads the values a spec's fields hold. Every offset and count the file
///     gives is checked against its length, so a damaged or hostile file is refused and never read past.
/// </summary>
sealed class UsdCrate {
    /// <summary>How every crate file starts.</summary>
    public static ReadOnlySpan<byte> Magic => "PXR-USDC"u8;

    readonly byte[] data;

    UsdCrate(byte[] data) {
        this.data = data;
    }

    public string[] Tokens { get; private set; } = [];
    public int[] Strings { get; private set; } = [];
    public (int Token, ulong Rep)[] Fields { get; private set; } = [];
    public int[] FieldSets { get; private set; } = [];
    public string[] Paths { get; private set; } = [];
    public (int Path, int FieldSet, int Type)[] Specs { get; private set; } = [];

    /// <summary>The file's bytes, which values at an offset are read from.</summary>
    public ReadOnlySpan<byte> Data => data;

    /// <summary>A place in the file to read a value from.</summary>
    public Cursor CursorAt(long offset) => new(data, offset);

    /// <summary>Whether bytes start as a crate file does.</summary>
    public static bool Is(ReadOnlySpan<byte> head) => head.StartsWith(Magic);

    /// <summary>The tables of a crate file.</summary>
    /// <exception cref="InvalidDataException">It is not one, is older than 0.4, or is damaged.</exception>
    public static UsdCrate Read(byte[] data) {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 88 || !Is(data)) {
            throw new InvalidDataException("It is not a USD binary layer.");
        }

        var (major, minor) = (data[8], data[9]);
        if (major != 0 || minor < 4) {
            throw new InvalidDataException(
                $"It is a USD binary layer of version {major}.{minor}, which Patina does not read; "
                + "save it again with a recent USD, or as .usda."
            );
        }

        UsdCrate crate = new(data);
        crate.ReadSections(new Cursor(data, BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(16, 8))));
        return crate;
    }

    void ReadSections(Cursor toc) {
        var count = toc.Count(32);
        Dictionary<string, long> starts = new(StringComparer.Ordinal);
        for (var i = 0; i < count; i++) {
            var name = Encoding.ASCII.GetString(toc.Bytes(16)).TrimEnd('\0');
            starts[name] = toc.Int64();
            toc.Int64();
        }

        Cursor At(string section) =>
            starts.TryGetValue(section, out var start)
                ? new(data, start)
                : throw new InvalidDataException($"The USD binary layer has no {section} section.");

        Tokens = ReadTokens(At("TOKENS"));
        Strings = ReadStrings(At("STRINGS"));
        Fields = ReadFields(At("FIELDS"));
        FieldSets = ReadFieldSets(At("FIELDSETS"));
        Paths = ReadPaths(At("PATHS"));
        Specs = ReadSpecs(At("SPECS"));
    }

    static string[] ReadTokens(Cursor at) {
        var count = at.Count(0);
        var size = at.Int64();
        var chars = CrateCompression.Decompress(at.Bytes(at.Count(1)), size);
        var tokens = Encoding.UTF8.GetString(chars).Split('\0');
        return tokens.Length >= count
            ? tokens[..count]
            : throw new InvalidDataException("The USD binary layer has fewer tokens than it says.");
    }

    static int[] ReadStrings(Cursor at) {
        var count = at.Count(4);
        return [..Enumerable.Range(0, count).Select(_ => at.Int32())];
    }

    static (int, ulong)[] ReadFields(Cursor at) {
        var count = at.Count(0);
        var tokens = at.Integers(count);
        var reps = CrateCompression.Decompress(at.Bytes(at.Count(1)), count * 8L);
        return [..tokens.Select((token, i) => (token, BinaryPrimitives.ReadUInt64LittleEndian(reps.AsSpan(i * 8, 8))))];
    }

    static int[] ReadFieldSets(Cursor at) => at.Integers(at.Count(0));

    static (int, int, int)[] ReadSpecs(Cursor at) {
        var count = at.Count(0);
        var paths = at.Integers(count);
        var sets = at.Integers(count);
        var types = at.Integers(count);
        return [..Enumerable.Range(0, count).Select(i => (paths[i], sets[i], types[i]))];
    }

    /// <summary>
    ///     The paths, from USD's tree of them: each entry is a path's index, its last element's token (below nought
    ///     for a property) and a jump that says whether a child follows it, a sibling, or both, the sibling then that
    ///     far on. A stack walks it, not recursion, so a scene of many siblings cannot exhaust the thread's.
    /// </summary>
    string[] ReadPaths(Cursor at) {
        var paths = new string[at.Count(0)];
        var count = at.Count(0);
        var indexes = at.Integers(count);
        var elements = at.Integers(count);
        var jumps = at.Integers(count);
        Stack<(int Index, string? Parent)> pending = [];
        pending.Push((0, null));
        var visited = 0;
        while (pending.TryPop(out var next)) {
            var (index, parent) = next;
            while (true) {
                if (index < 0 || index >= count || visited++ >= count) {
                    throw new InvalidDataException("The USD binary layer's paths do not form a tree.");
                }

                var path = parent is null ? "/" : Element(parent, elements[index]);
                Put(paths, indexes[index], path);
                var jump = jumps[index];
                if (jump > 0 || jump == -1) {
                    if (jump > 0) {
                        pending.Push((index + jump, parent));
                    }

                    parent = path;
                } else if (jump != 0) {
                    break;
                }

                index++;
            }
        }

        return [..paths.Select(static p => p ?? string.Empty)];
    }

    string Element(string parent, int element) {
        var name = Token(Math.Abs(element));
        if (element < 0) {
            return $"{parent}.{name}";
        }

        return parent == "/" ? $"/{name}" : $"{parent}/{name}";
    }

    static void Put(string[] paths, int at, string path) {
        if (at < 0 || at >= paths.Length) {
            throw new InvalidDataException("The USD binary layer names a path it does not have.");
        }

        paths[at] = path;
    }

    /// <summary>A token by its index.</summary>
    public string Token(long index) =>
        index >= 0 && index < Tokens.Length
            ? Tokens[index]
            : throw new InvalidDataException("The USD binary layer names a token it does not have.");

    /// <summary>A path by its index.</summary>
    public string PathOf(long index) =>
        index >= 0 && index < Paths.Length
            ? Paths[index]
            : throw new InvalidDataException("The USD binary layer names a path it does not have.");

    /// <summary>A spec's fields: each name and the value it holds.</summary>
    public IEnumerable<(string Name, ulong Rep)> FieldsOf(int fieldSet) {
        for (var at = fieldSet; at >= 0 && at < FieldSets.Length && FieldSets[at] >= 0; at++) {
            var index = FieldSets[at];
            if (index >= Fields.Length) {
                throw new InvalidDataException("The USD binary layer names a field it does not have.");
            }

            yield return (Token(Fields[index].Token), Fields[index].Rep);
        }
    }

    /// <summary>A place in the file that reads forward, refusing anything past the end.</summary>
    internal sealed class Cursor(byte[] data, long at) {
        public ReadOnlySpan<byte> Bytes(long length) {
            if (at < 0 || length < 0 || at > data.Length - length) {
                throw new InvalidDataException("The USD binary layer ends early.");
            }

            var bytes = data.AsSpan((int)at, (int)length);
            at += length;
            return bytes;
        }

        public long Int64() => BinaryPrimitives.ReadInt64LittleEndian(Bytes(8));

        public int Int32() => BinaryPrimitives.ReadInt32LittleEndian(Bytes(4));

        /// <summary>
        ///     A count of things <paramref name="size" /> bytes each that follow, which the rest of the file must hold;
        ///     or, at nought, of things compressed, which LZ4 packs at most 255 to a byte and two bits a number.
        /// </summary>
        public int Count(int size) {
            var count = Int64();
            var most = size > 0 ? (data.Length - at) / size : data.Length * 1024L;
            return count >= 0 && count <= most && count < int.MaxValue / 8
                ? (int)count
                : throw new InvalidDataException("The USD binary layer counts more than it holds.");
        }

        /// <summary>Compressed integers: their size in bytes, then the bytes.</summary>
        public int[] Integers(int count) => CrateCompression.Integers(Bytes(Count(1)), count);
    }
}
