// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Patina.Viewport;

/// <summary>
///     The extensions a glTF uses without listing them in <c>extensionsUsed</c> (#310). Assimp reads a material's
///     extension only when the file lists it there, so a hand-written file loses its clear coat, sheen and transmission
///     without a word. The file's JSON is read here, every extension it names is listed, and the reader is given that;
///     what was missing is said.
/// </summary>
public static class GltfDeclarations {
    const uint Magic = 0x46546C67; // "glTF"
    const uint JsonChunk = 0x4E4F534A; // "JSON"
    const int Header = 12;
    const int ChunkHeader = 8;

    /// <summary>A file's bytes with every extension it uses listed, and the ones that were not.</summary>
    /// <param name="Bytes">The bytes to read: the file's own when nothing was missing.</param>
    /// <param name="Missing">The extensions used and not listed, by the index of each material naming one.</param>
    public sealed record Declared(byte[] Bytes, IReadOnlyDictionary<int, IReadOnlyList<string>> Missing);

    /// <summary>Lists what a glTF uses. Anything else, or JSON that will not parse, is given back as it is.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="extension">Its extension, with the dot.</param>
    public static Declared Of(byte[] bytes, string extension) {
        ArgumentNullException.ThrowIfNull(bytes);
        var none = new Declared(bytes, new Dictionary<int, IReadOnlyList<string>>());
        var binary = extension.Equals(".glb", StringComparison.OrdinalIgnoreCase);
        if (!binary && !extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase)) {
            return none;
        }

        var json = binary ? JsonOf(bytes) : bytes.AsMemory();
        if (json.IsEmpty || Parse(json) is not JsonObject root) {
            return none;
        }

        HashSet<string> listed = [..Names(root["extensionsUsed"])];
        var named = new SortedSet<string>(StringComparer.Ordinal);
        Collect(root, named);
        named.ExceptWith(listed);
        if (named.Count == 0) {
            return none;
        }

        var used = root["extensionsUsed"] as JsonArray ?? [];
        foreach (var name in named) {
            used.Add(name);
        }

        root["extensionsUsed"] = used;
        var text = Encoding.UTF8.GetBytes(root.ToJsonString());
        return new(binary ? Rebuilt(bytes, text) : text, ByMaterial(root, named));
    }

    static JsonNode? Parse(ReadOnlyMemory<byte> json) {
        try {
            return JsonNode.Parse(json.Span, documentOptions: new() { AllowTrailingCommas = true });
        } catch (JsonException) {
            return null;
        }
    }

    static IEnumerable<string> Names(JsonNode? list) =>
        list is JsonArray array
            ? array.Select(static item => item?.GetValueKind() == JsonValueKind.String ? item.GetValue<string>() : null)
                .OfType<string>()
            : [];

    /// <summary>Every name under an <c>extensions</c> object anywhere in the file.</summary>
    static void Collect(JsonNode? node, ISet<string> named) {
        switch (node) {
            case JsonObject item:
                foreach (var (key, value) in item) {
                    if (key == "extensions" && value is JsonObject extensions) {
                        foreach (var (name, _) in extensions) {
                            named.Add(name);
                        }
                    }

                    Collect(value, named);
                }

                break;
            case JsonArray list:
                foreach (var value in list) {
                    Collect(value, named);
                }

                break;
        }
    }

    /// <summary>For each material, the missing extensions it names itself or in one of its pictures.</summary>
    static Dictionary<int, IReadOnlyList<string>> ByMaterial(JsonObject root, ISet<string> missing) {
        Dictionary<int, IReadOnlyList<string>> found = [];
        if (root["materials"] is not JsonArray materials) {
            return found;
        }

        for (var at = 0; at < materials.Count; at++) {
            var named = new SortedSet<string>(StringComparer.Ordinal);
            Collect(materials[at], named);
            named.IntersectWith(missing);
            if (named.Count > 0) {
                found[at] = [..named];
            }
        }

        return found;
    }

    /// <summary>The JSON chunk of a binary glTF, or nothing for one Patina cannot read.</summary>
    static ReadOnlyMemory<byte> JsonOf(byte[] bytes) {
        if (bytes.Length < Header + ChunkHeader
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Magic
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(Header + 4)) != JsonChunk) {
            return ReadOnlyMemory<byte>.Empty;
        }

        var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(Header));
        return length > (uint)(bytes.Length - Header - ChunkHeader)
            ? ReadOnlyMemory<byte>.Empty
            : bytes.AsMemory(Header + ChunkHeader, (int)length);
    }

    /// <summary>A binary glTF with a new JSON chunk, padded with spaces, and its other chunks as they were.</summary>
    static byte[] Rebuilt(byte[] bytes, byte[] json) {
        var oldLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(Header));
        var rest = bytes.AsSpan(Header + ChunkHeader + oldLength);
        var padded = (json.Length + 3) / 4 * 4;
        var result = new byte[Header + ChunkHeader + padded + rest.Length];
        bytes.AsSpan(0, Header).CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), (uint)result.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(Header), (uint)padded);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(Header + 4), JsonChunk);
        json.CopyTo(result.AsSpan(Header + ChunkHeader));
        result.AsSpan(Header + ChunkHeader + json.Length, padded - json.Length).Fill((byte)' ');
        rest.CopyTo(result.AsSpan(Header + ChunkHeader + padded));
        return result;
    }
}
