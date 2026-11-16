using System.Buffers.Binary;

namespace Patina.Export.Materials;

/// <summary>
///     The two ways USD's binary form (<c>.usdc</c>, its "crate") packs its tables (#157): LZ4 blocks behind a byte
///     that counts chunks, as USD's <c>TfFastCompression</c> writes them, and integers as differences from the one
///     before, each in one, two or four bytes or as the most common difference, as its <c>Usd_IntegerCompression</c>
///     writes them. Both are read from bytes the file gives, and anything that runs past them is refused, never read.
/// </summary>
static class CrateCompression {
    /// <summary>The bytes a <c>TfFastCompression</c> buffer holds, which are <paramref name="size" /> long.</summary>
    /// <exception cref="InvalidDataException">The buffer is not one.</exception>
    public static byte[] Decompress(ReadOnlySpan<byte> source, long size) {
        // LZ4 makes at most 255 bytes of one, so a size past that is a damaged file, not a reason to allocate it.
        var output = new byte[Checked(size <= source.Length * 255L + 16 ? size : -1)];
        return Inflate(source, output) == size ? output : throw Broken("a compressed table holds other than it says");
    }

    /// <summary>
    ///     <paramref name="count" /> integers, each the one before plus a difference: the common one, or one held in
    ///     one, two or four bytes, as two bits a number say.
    /// </summary>
    /// <exception cref="InvalidDataException">They run past the bytes.</exception>
    public static int[] Integers(ReadOnlySpan<byte> compressed, int count) {
        var codes = (count * 2 + 7) / 8;
        // The most the encoding of that many integers takes; it is usually far less.
        var encoded = new byte[Checked(4L + codes + 4L * count)];
        var written = Inflate(compressed, encoded);
        return Decode(encoded.AsSpan(0, written), count, codes);
    }

    static int Checked(long size) =>
        size >= 0 && size <= Array.MaxLength ? (int)size : throw Broken("a table is too large");

    /// <summary>A <c>TfFastCompression</c> buffer into <paramref name="output" />, and the bytes it wrote.</summary>
    static int Inflate(ReadOnlySpan<byte> source, Span<byte> output) {
        if (source.IsEmpty) {
            throw Broken("a compressed table is empty");
        }

        var chunks = source[0];
        if (chunks == 0) {
            return Lz4(source[1..], output);
        }

        var at = 1;
        var written = 0;
        for (var chunk = 0; chunk < chunks; chunk++) {
            var length = BinaryPrimitives.ReadInt32LittleEndian(Slice(source, at, 4));
            written += Lz4(Slice(source, at + 4, length), output[written..]);
            at += 4 + length;
        }

        return written;
    }

    static int[] Decode(ReadOnlySpan<byte> encoded, int count, int codes) {
        var common = BinaryPrimitives.ReadInt32LittleEndian(Slice(encoded, 0, 4));
        var values = 4 + codes;
        var numbers = new int[count];
        var previous = 0;
        for (var i = 0; i < count; i++) {
            var code = (Slice(encoded, 4 + i / 4, 1)[0] >> (2 * (i % 4))) & 3;
            previous += code switch {
                0 => common,
                1 => (sbyte)Slice(encoded, values, 1)[0],
                2 => BinaryPrimitives.ReadInt16LittleEndian(Slice(encoded, values, 2)),
                _ => BinaryPrimitives.ReadInt32LittleEndian(Slice(encoded, values, 4))
            };
            values += code == 3 ? 4 : code;
            numbers[i] = previous;
        }

        return numbers;
    }

    /// <summary>One LZ4 block into <paramref name="output" />, and how many bytes it wrote.</summary>
    static int Lz4(ReadOnlySpan<byte> block, Span<byte> output) {
        var at = 0;
        var written = 0;
        while (at < block.Length) {
            var token = block[at++];
            var literals = Length(block, ref at, token >> 4);
            Slice(block, at, literals).CopyTo(Slice(output, written, literals));
            at += literals;
            written += literals;
            if (at >= block.Length) {
                break;
            }

            var offset = BinaryPrimitives.ReadUInt16LittleEndian(Slice(block, at, 2));
            at += 2;
            var match = Length(block, ref at, token & 15) + 4;
            if (offset == 0 || offset > written) {
                throw Broken("a compressed table refers before its start");
            }

            var target = Slice(output, written, match);
            for (var i = 0; i < match; i++) {
                target[i] = output[written - offset + i];
            }

            written += match;
        }

        return written;
    }

    /// <summary>A length of four bits, and the bytes after it that add to it while they are 255.</summary>
    static int Length(ReadOnlySpan<byte> block, ref int at, int length) {
        if (length != 15) {
            return length;
        }

        byte more;
        do {
            more = Slice(block, at++, 1)[0];
            length += more;
        } while (more == 255);

        return length;
    }

    static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> bytes, int at, int length) =>
        at >= 0 && length >= 0 && at <= bytes.Length - length ? bytes.Slice(at, length) : throw Broken("it ends early");

    static Span<byte> Slice(Span<byte> bytes, int at, int length) =>
        at >= 0 && length >= 0 && at <= bytes.Length - length
            ? bytes.Slice(at, length)
            : throw Broken("a table is longer than it says");

    static InvalidDataException Broken(string why) => new($"The USD binary layer cannot be read: {why}.");
}
