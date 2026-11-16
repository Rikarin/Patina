using System.Buffers.Binary;
using System.Globalization;

namespace Patina.Export.Materials;

/// <summary>
///     A crate file's prims as <see cref="UsdaReader" /> reads a text layer's (#157): each prim's type, and each
///     attribute's value or connection spelled as USD's text form spells it, so one table turns either into maps. Only
///     the values a surface is made of are read, a token, a string, an asset, a number or a vector of them; an array,
///     a dictionary or a time sample is passed over, as the text reader passes over metadata.
/// </summary>
static class UsdCrateValues {
    const ulong Array = 1UL << 63;
    const ulong Inlined = 1UL << 62;
    const ulong Payload = (1UL << 48) - 1;

    const int PrimSpec = 6;
    const int AttributeSpec = 1;

    /// <summary>The prims of a crate file, by path.</summary>
    /// <exception cref="InvalidDataException">The file is not one, or is damaged.</exception>
    public static Dictionary<string, UsdPrim> Prims(byte[] data) {
        var crate = UsdCrate.Read(data);
        Dictionary<string, UsdPrim> prims = new(StringComparer.Ordinal);
        foreach (var spec in crate.Specs.Where(static s => s.Type == PrimSpec)) {
            var path = crate.PathOf(spec.Path);
            var type = crate.FieldsOf(spec.FieldSet).Where(static f => f.Name == "typeName")
                .Select(f => Spell(crate, f.Rep)?.Trim('"'))
                .FirstOrDefault();
            var name = path[(path.LastIndexOf('/') + 1)..];
            prims[path] = new(path, type ?? string.Empty, name, new(StringComparer.Ordinal));
        }

        foreach (var spec in crate.Specs.Where(static s => s.Type == AttributeSpec)) {
            var path = crate.PathOf(spec.Path);
            var dot = path.LastIndexOf('.');
            if (dot <= 0 || !prims.TryGetValue(path[..dot], out var prim)) {
                continue;
            }

            Attribute(crate, prim, path[(dot + 1)..], spec.FieldSet);
        }

        return prims;
    }

    /// <summary>An attribute's value under its name, and its connection under its name and <c>.connect</c>.</summary>
    static void Attribute(UsdCrate crate, UsdPrim prim, string name, int fieldSet) {
        foreach (var (field, rep) in crate.FieldsOf(fieldSet)) {
            var spelled = field switch {
                "default" => Spell(crate, rep),
                "connectionPaths" => Connection(crate, rep),
                _ => null
            };
            if (spelled is not null) {
                prim.Properties[field == "default" ? name : $"{name}.connect"] = spelled;
            }
        }
    }

    /// <summary>A value as the text form spells it, or null for one a surface is not made of.</summary>
    static string? Spell(UsdCrate crate, ulong rep) {
        if ((rep & Array) != 0) {
            return null;
        }

        var type = (int)((rep >> 48) & 0xFF);
        var inlined = (rep & Inlined) != 0;
        var payload = rep & Payload;
        return type switch {
            10 => $"\"{crate.Token(Index(crate.Strings, Scalar(crate, payload, inlined)))}\"",
            11 => $"\"{crate.Token(Scalar(crate, payload, inlined))}\"",
            12 => $"@{crate.Token(Scalar(crate, payload, inlined))}@",
            3 or 4 => Number(Scalar(crate, payload, inlined)),
            7 => Number((float)BitConverter.UInt16BitsToHalf((ushort)Scalar(crate, payload, inlined))),
            8 => Number(BitConverter.Int32BitsToSingle((int)Scalar(crate, payload, inlined))),
            9 => Number(Double(crate, payload, inlined)),
            >= 19 and <= 30 => Vector(crate, type, payload, inlined),
            _ => null
        };
    }

    static long Index(int[] table, long at) =>
        at >= 0 && at < table.Length
            ? table[at]
            : throw new InvalidDataException("The USD binary layer names a string it does not have.");

    /// <summary>Four bytes of a value: in the value's own bits, or at the offset they give.</summary>
    static long Scalar(UsdCrate crate, ulong payload, bool inlined) =>
        inlined ? (long)(uint)payload : BinaryPrimitives.ReadUInt32LittleEndian(At(crate, payload, 4));

    /// <summary>A double, which the file holds inline as a float when a float holds it whole.</summary>
    static double Double(UsdCrate crate, ulong payload, bool inlined) =>
        inlined
            ? BitConverter.Int32BitsToSingle((int)(uint)payload)
            : BinaryPrimitives.ReadDoubleLittleEndian(At(crate, payload, 8));

    /// <summary>
    ///     A vector of two to four doubles, floats, halves or ints. One whose numbers are all small whole ones is held
    ///     inline, a byte each.
    /// </summary>
    static string Vector(UsdCrate crate, int type, ulong payload, bool inlined) {
        var (dimension, kind) = (2 + (type - 19) / 4, (type - 19) % 4);
        if (inlined) {
            return Numbers([..Enumerable.Range(0, dimension).Select(i => (double)(sbyte)(payload >> (8 * i)))]);
        }

        var size = kind switch {
            0 => 8,
            2 => 2,
            _ => 4
        };
        var bytes = At(crate, payload, dimension * size);
        var lanes = new double[dimension];
        for (var i = 0; i < dimension; i++) {
            lanes[i] = Lane(bytes.Slice(i * size, size), kind);
        }

        return Numbers(lanes);
    }

    static double Lane(ReadOnlySpan<byte> bytes, int kind) =>
        kind switch {
            0 => BinaryPrimitives.ReadDoubleLittleEndian(bytes),
            1 => BinaryPrimitives.ReadSingleLittleEndian(bytes),
            2 => (double)BinaryPrimitives.ReadHalfLittleEndian(bytes),
            _ => BinaryPrimitives.ReadInt32LittleEndian(bytes)
        };

    /// <summary>
    ///     The first path a list of paths adds, as <c>&lt;/path&gt;</c>: the lists come explicit, added, prepended,
    ///     appended, deleted and ordered, each there when the header's bit says, and deleted paths connect nothing.
    /// </summary>
    static string? Connection(UsdCrate crate, ulong rep) {
        if ((rep >> 48 & 0xFF) != 34 || (rep & Inlined) != 0) {
            return null;
        }

        var at = crate.CursorAt((long)(rep & Payload));
        var header = at.Bytes(1)[0];
        foreach (var bit in (int[])[2, 4, 32, 64, 8, 16]) {
            if ((header & bit) == 0) {
                continue;
            }

            var count = at.Count(4);
            var paths = Enumerable.Range(0, count).Select(_ => at.Int32()).ToList();
            if (bit != 8 && paths.Count > 0) {
                return $"<{crate.PathOf(paths[0])}>";
            }
        }

        return null;
    }

    static ReadOnlySpan<byte> At(UsdCrate crate, ulong offset, int length) =>
        offset <= (ulong)(crate.Data.Length - length)
            ? crate.Data.Slice((int)offset, length)
            : throw new InvalidDataException("The USD binary layer holds a value past its end.");

    static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    static string Numbers(double[] values) => $"({string.Join(", ", values.Select(Number))})";
}
