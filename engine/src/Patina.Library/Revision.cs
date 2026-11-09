using Patina.Contracts;
using System.Buffers;
using System.Security.Cryptography;

namespace Patina.Library;

/// <summary>
///     The revision of a file: the hash of its bytes, algorithm included, so that a stronger hash can arrive without
///     changing what the old one meant. SHA-256 because a key match reuses a file on another machine (plan §5): a
///     collision would ship the wrong texture, and ARM and x64 both compute it in hardware.
/// </summary>
public readonly record struct Revision {
    const string Prefix = "sha256:";
    const int HexLength = 64;

    static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdef");

    Revision(string text) {
        Text = text;
    }

    /// <summary>The form files and the index hold: <c>sha256:</c> and 64 lower-case hex digits.</summary>
    public string Text { get; }

    /// <summary>The digest alone, which names the file in a content-addressed folder.</summary>
    public string Hex => Text[Prefix.Length..];

    public static Revision Of(ReadOnlySpan<byte> content) => FromDigest(SHA256.HashData(content));

    /// <summary>The revision of a file's bytes, read while anybody else may still write or replace it.</summary>
    public static Revision OfFile(string path) {
        using var stream = SharedFile.OpenRead(path);
        return FromDigest(SHA256.HashData(stream));
    }

    public static bool TryParse(string? text, out Revision revision) {
        var valid = text?.Length == Prefix.Length + HexLength
            && text.StartsWith(Prefix, StringComparison.Ordinal)
            && text.AsSpan(Prefix.Length).IndexOfAnyExcept(HexDigits) < 0;
        revision = valid ? new(text!) : default;
        return valid;
    }

    /// <summary>Reads text the index wrote, which is known to be a revision.</summary>
    internal static Revision Stored(string text) => new(text);

    public override string ToString() => Text;

    static Revision FromDigest(byte[] digest) => new(Prefix + Convert.ToHexStringLower(digest));
}
