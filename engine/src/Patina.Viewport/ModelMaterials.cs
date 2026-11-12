// SPDX-License-Identifier: GPL-3.0-or-later

using Patina.Contracts;
using Vixen.Interchange;

namespace Patina.Viewport;

/// <summary>
///     A model file's materials as maps (#157), read by Vixen's <see cref="ModelReader" /> through Assimp: glTF's
///     metallic-roughness images and factors, and what an FBX or OBJ says of the same. Occlusion is the red of its
///     image, roughness the green and metallic the blue of theirs, as glTF packs them, and a picture inside the file is
///     written to a folder, since no pixel crosses the socket. glTF's emissive factor, alpha and texture transforms
///     are not in what the reader gives, so they are not here either.
/// </summary>
public static class ModelMaterials {
    /// <summary>Reads a model's materials.</summary>
    /// <param name="path">The model.</param>
    /// <param name="folder">Where a picture inside it is written.</param>
    /// <exception cref="ModelFormatException">The file is not a readable model.</exception>
    public static IReadOnlyList<ImportedMaterial> Read(string path, string folder) {
        var read = ModelReader.Read(
            SharedFile.ReadAllBytes(path),
            Path.GetExtension(path),
            Path.GetFileNameWithoutExtension(path),
            new ModelReadOptions { GenerateTangents = false, ImportAnimations = false, ImportBlendShapes = false },
            sideFiles: SharedFile.Beside(path)
        );
        var beside = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var names = MaterialNames.Unique(read.Model.Materials);
        List<ImportedMaterial> materials = [];
        // A material no part wears is one Assimp adds of its own, as glTF's importer does for a part that names none.
        var worn = read.Model.Parts.Select(static part => part.Material).ToHashSet();
        for (var at = 0; at < names.Length; at++) {
            if (!worn.Contains(at)) {
                continue;
            }

            var factors = at < read.MaterialFactors.Length ? read.MaterialFactors[at] : ModelMaterialFactors.Default;
            var textures = at < read.MaterialTextures.Length ? read.MaterialTextures[at] : new();
            materials.Add(Material(names[at], factors, textures, beside, folder));
        }

        return materials;
    }

    /// <summary>
    ///     One material: base color, roughness and metalness from their images and factors or from the factors alone,
    ///     and normal, occlusion and emissive when it has an image for them.
    /// </summary>
    static ImportedMaterial Material(
        string name,
        ModelMaterialFactors factors,
        ModelMaterialTextures textures,
        string beside,
        string folder
    ) {
        List<string> warnings = [];

        string? File(ModelTexture? texture, string slot) =>
            Picture(texture, beside, folder, $"{name}_{slot}", warnings);

        var colour = factors.BaseColor;
        var packed = File(textures.MetallicRoughness, "metallicRoughness");
        List<ImportedSlot> slots = [
            Slot(ExportMap.BaseColor, File(textures.BaseColor, "baseColor"), -1, [colour.X, colour.Y, colour.Z, 1f]),
            Slot(ExportMap.Roughness, packed, 1, [factors.Roughness]),
            Slot(ExportMap.Metalness, packed, 2, [factors.Metallic])
        ];
        foreach (var (usage, texture, lane) in (IEnumerable<(string, ModelTexture?, int)>) [
                     (ExportMap.Normal, textures.Normal, -1), (ExportMap.Occlusion, textures.Occlusion, 0),
                     (ExportMap.Emissive, textures.Emissive, -1)
                 ]) {
            if (File(texture, usage) is { } file) {
                slots.Add(Slot(usage, file, lane, [1f]));
            }
        }

        if (textures.Normal is not null && Math.Abs(textures.NormalScale - 1f) > 1e-4f) {
            warnings.Add($"{name}: its normal's scale of {textures.NormalScale:0.###} is not brought in.");
        }

        return new(name, slots, warnings);
    }

    /// <summary>The usages a model's material fills, as an export names its maps.</summary>
    static class ExportMap {
        public const string BaseColor = "baseColor";
        public const string Normal = "normal";
        public const string Roughness = "roughness";
        public const string Metalness = "metalness";
        public const string Occlusion = "occlusion";
        public const string Emissive = "emissive";
    }

    /// <summary>A map: a picture's lane times a factor, or the factor alone when there is no picture.</summary>
    static ImportedSlot Slot(string usage, string? texture, int lane, float[] factor) {
        var four = factor.Length == 1 ? [factor[0], factor[0], factor[0], 1f] : factor;
        return new(usage, texture, texture is null ? -1 : lane, false, four);
    }

    /// <summary>
    ///     A picture a material names, as a file: one beside the model where it is, or the bytes inside the model
    ///     written to <paramref name="folder" />. Null, and said, for one that is not there.
    /// </summary>
    static string? Picture(ModelTexture? texture, string beside, string folder, string stem, List<string> warnings) {
        if (texture is null) {
            return null;
        }

        if (texture.Embedded is { } bytes) {
            Directory.CreateDirectory(folder);
            var written = Path.Combine(folder, Clean(stem) + texture.Extension);
            File.WriteAllBytes(written, bytes);
            return written;
        }

        if (texture.Path is not { Length: > 0 } relative) {
            return null;
        }

        foreach (var spelled in (string[])[relative, Uri.UnescapeDataString(relative)]) {
            var full = Path.GetFullPath(Path.Combine(beside, spelled.Replace('\\', '/')));
            if (File.Exists(full)) {
                return full;
            }
        }

        warnings.Add($"{stem}: its picture '{relative}' is not beside the model.");
        return null;
    }

    static string Clean(string name) {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(letter => invalid.Contains(letter) || letter == ' ' ? '_' : letter));
    }
}
