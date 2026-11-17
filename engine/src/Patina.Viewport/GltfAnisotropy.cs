// SPDX-License-Identifier: GPL-3.0-or-later

using Patina.Contracts;
using System.Text.Json.Nodes;
using Vixen.Interchange;
using Vector2 = Vixen.Core.Mathematics.Vector2;

namespace Patina.Viewport;

/// <summary>
///     A glTF's anisotropy (<c>KHR_materials_anisotropy</c>), read from the file's JSON on every platform, so Import
///     Materials brings in the same maps on each.
/// </summary>
/// <remarks>
///     ⚠ <b>Which Assimp reads the file decides whether it is there at all.</b> Linux and macOS load the Assimp 5 the
///     binding was made for (Vixen's <c>AssimpLoader</c>), whose glTF importer has no anisotropy; Windows' only Assimp
///     is a 6, which reads it. A brushed metal came in brushed on Windows alone, and nothing said so elsewhere.
/// </remarks>
static class GltfAnisotropy {
    const string Extension = "KHR_materials_anisotropy";
    const int ClampToEdge = 33071;
    const int MirroredRepeat = 33648;

    /// <summary>One material's anisotropy as its file says it.</summary>
    /// <param name="Strength">How anisotropic the lobe is; none at 0, glTF's default.</param>
    /// <param name="Rotation">The direction's turn from the tangent, in radians.</param>
    /// <param name="Picture">The direction in red and green and the strength in blue, or null.</param>
    public sealed record Lobe(float Strength, float Rotation, ModelTexture? Picture);

    /// <summary>The anisotropy each material of a glTF names, by its index; none for another format.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="extension">Its extension, with the dot.</param>
    /// <param name="beside">The folder the file is in, where a buffer it names is.</param>
    public static IReadOnlyDictionary<int, Lobe> Of(byte[] bytes, string extension, string beside) {
        Dictionary<int, Lobe> found = [];
        if (GltfDeclarations.Document(bytes, extension) is not var (root, binary)
            || root["materials"] is not JsonArray materials) {
            return found;
        }

        Pictures pictures = new(root, binary ? GltfDeclarations.BinaryOf(bytes) : ReadOnlyMemory<byte>.Empty, beside);
        for (var at = 0; at < materials.Count; at++) {
            if (materials[at] is JsonObject material
                && material["extensions"] is JsonObject extensions
                && extensions[Extension] is JsonObject anisotropy) {
                found[at] = new(
                    Number(anisotropy["anisotropyStrength"], 0f),
                    Number(anisotropy["anisotropyRotation"], 0f),
                    pictures.Of(anisotropy["anisotropyTexture"])
                );
            }
        }

        return found;
    }

    /// <summary>A glTF material's layers with the anisotropy its file gives it: none when it names none.</summary>
    public static ModelMaterialLayers Over(ModelMaterialLayers layers, Lobe? lobe) =>
        layers with {
            AnisotropyStrength = lobe?.Strength ?? 0f,
            AnisotropyRotation = lobe?.Rotation ?? 0f,
            Anisotropy = lobe?.Picture
        };

    static float Number(JsonNode? node, float otherwise) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) ? (float)number : otherwise;

    static int? Index(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<int>(out var index) ? index : null;

    static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    static JsonObject? Item(JsonObject root, string list, int? index) =>
        index is { } at && root[list] is JsonArray items && at >= 0 && at < items.Count
            ? items[at] as JsonObject
            : null;

    static Vector2 Pair(JsonNode? node, float otherwise) =>
        node is JsonArray { Count: 2 } pair
            ? new(Number(pair[0], otherwise), Number(pair[1], otherwise))
            : new(otherwise, otherwise);

    static ModelTextureWrap Wrap(JsonNode? node) =>
        Index(node) switch {
            ClampToEdge => ModelTextureWrap.Clamp,
            MirroredRepeat => ModelTextureWrap.Mirror,
            _ => ModelTextureWrap.Repeat
        };

    /// <summary>The extension a picture of a media type is written with, as Assimp names an embedded one.</summary>
    static string ExtensionOf(string? mime) =>
        mime switch {
            "image/jpeg" => ".jpg",
            ['i', 'm', 'a', 'g', 'e', '/', .. var kind] => $".{kind}",
            _ => ".png"
        };

    /// <summary>The bytes of a <c>data:</c> address, and its media type; null when it is not base64.</summary>
    static (byte[] Bytes, string Mime)? Data(string uri) {
        var comma = uri.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0 || !uri[..comma].EndsWith(";base64", StringComparison.Ordinal)) {
            return null;
        }

        try {
            return (Convert.FromBase64String(uri[(comma + 1)..]), uri["data:".Length..(comma - ";base64".Length)]);
        } catch (FormatException) {
            return null;
        }
    }

    /// <summary>A file's pictures as a texture reference names them: a file beside it, or bytes inside it.</summary>
    sealed class Pictures(JsonObject root, ReadOnlyMemory<byte> binary, string beside) {
        /// <summary>The picture a glTF texture reference names, with its coordinate set, placement and wrap.</summary>
        public ModelTexture? Of(JsonNode? info) {
            if (info is not JsonObject reference
                || Item(root, "textures", Index(reference["index"])) is not { } texture
                || Item(root, "images", Index(texture["source"])) is not { } image
                || Read(image) is not { } read) {
                return null;
            }

            var sampler = Item(root, "samplers", Index(texture["sampler"]));
            var transform = (reference["extensions"] as JsonObject)?["KHR_texture_transform"] as JsonObject;
            return read with {
                UvSet = Index(transform?["texCoord"]) ?? Index(reference["texCoord"]) ?? 0,
                Transform = transform is null
                    ? null
                    : new(
                        Pair(transform["offset"], 0f),
                        Number(transform["rotation"], 0f),
                        Pair(transform["scale"], 1f)
                    ),
                WrapU = Wrap(sampler?["wrapS"]),
                WrapV = Wrap(sampler?["wrapT"])
            };
        }

        ModelTexture? Read(JsonObject image) {
            if (Text(image["uri"]) is not { } uri) {
                return View(Index(image["bufferView"])) is { } bytes
                    ? new(null, bytes, ExtensionOf(Text(image["mimeType"])))
                    : null;
            }

            if (!uri.StartsWith("data:", StringComparison.Ordinal)) {
                return new(uri, null, Path.GetExtension(uri).ToLowerInvariant());
            }

            return Data(uri) is var (data, mime) ? new(null, data, ExtensionOf(mime)) : null;
        }

        /// <summary>A buffer view's bytes, or null for one that is not there.</summary>
        byte[]? View(int? index) {
            if (Item(root, "bufferViews", index) is not { } view
                || Item(root, "buffers", Index(view["buffer"])) is not { } buffer) {
                return null;
            }

            var bytes = Text(buffer["uri"]) is { } uri ? Buffer(uri) : binary;
            var offset = Index(view["byteOffset"]) ?? 0;
            var length = Index(view["byteLength"]) ?? 0;
            return offset >= 0 && length > 0 && (long)offset + length <= bytes.Length
                ? bytes.Slice(offset, length).ToArray()
                : null;
        }

        ReadOnlyMemory<byte> Buffer(string uri) {
            if (uri.StartsWith("data:", StringComparison.Ordinal)) {
                return Data(uri)?.Bytes ?? ReadOnlyMemory<byte>.Empty;
            }

            var file = Path.GetFullPath(Path.Combine(beside, Uri.UnescapeDataString(uri)));
            return File.Exists(file) ? SharedFile.ReadAllBytes(file) : ReadOnlyMemory<byte>.Empty;
        }
    }
}
