// SPDX-License-Identifier: GPL-3.0-or-later

using Patina.Contracts;
using Vixen.Interchange;

namespace Patina.Viewport;

/// <summary>
///     A model file's materials as maps (#157), read by Vixen's <see cref="ModelReader" /> through Assimp: glTF's
///     metallic-roughness images and factors, and what an FBX or OBJ says of the same. Occlusion is the red of its
///     image, roughness the green and metallic the blue of theirs, as glTF packs them, and a picture inside the file is
///     written to a folder, since no pixel crosses the socket. A glTF's emissive factor and strength scale its
///     emission, its alpha becomes an opacity map unless the material is opaque, and a picture's texture transform
///     goes with its slot, for the graph to draw.
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
        var source = new Source(
            Path.GetDirectoryName(Path.GetFullPath(path))!,
            folder,
            Path.GetExtension(path).ToLowerInvariant() is ".gltf" or ".glb"
        );
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
            materials.Add(Material(names[at], factors, textures, source));
        }

        return materials;
    }

    /// <summary>Where a model's pictures are, where one inside it goes, and whether its factors are glTF's.</summary>
    sealed record Source(string Beside, string Folder, bool Gltf);

    /// <summary>
    ///     One material: base color, roughness and metalness from their images and factors or from the factors alone,
    ///     normal and occlusion when it has an image for them, emission when it has an image or a factor, and opacity
    ///     when its alpha is read.
    /// </summary>
    static ImportedMaterial Material(
        string name,
        ModelMaterialFactors factors,
        ModelMaterialTextures textures,
        Source source
    ) {
        List<string> warnings = [];
        // Each picture once, however many slots read it: glTF packs roughness and metalness in one, and alpha rides
        // in the base color's.
        Dictionary<ModelTexture, string?> files = new(ReferenceEqualityComparer.Instance);
        foreach (var (texture, stem) in (IEnumerable<(ModelTexture?, string)>) [
                     (textures.BaseColor, "baseColor"), (textures.MetallicRoughness, "metallicRoughness"),
                     (textures.Normal, "normal"), (textures.Occlusion, "occlusion"), (textures.Emissive, "emissive")
                 ]) {
            if (texture is not null && !files.ContainsKey(texture)) {
                files[texture] = Picture(texture, source, $"{name}_{stem}", warnings);
            }
        }

        ImportedSlot Slot(string usage, ModelTexture? texture, int lane, float[] factor) =>
            Placed(usage, texture is null ? null : files[texture], texture, lane, factor);

        var colour = factors.BaseColor;
        List<ImportedSlot> slots = [
            Slot(ExportMap.BaseColor, textures.BaseColor, -1, [colour.X, colour.Y, colour.Z, 1f]),
            Slot(ExportMap.Roughness, textures.MetallicRoughness, 1, [factors.Roughness]),
            Slot(ExportMap.Metalness, textures.MetallicRoughness, 2, [factors.Metallic])
        ];
        foreach (var (usage, texture, lane) in (IEnumerable<(string, ModelTexture?, int)>) [
                     (ExportMap.Normal, textures.Normal, -1), (ExportMap.Occlusion, textures.Occlusion, 0)
                 ]) {
            if (texture is not null && Slot(usage, texture, lane, [1f]) is { Texture: not null } slot) {
                slots.Add(slot);
            }
        }

        // An emissive picture that is not there leaves the emission out rather than making it its factor.
        if (Emission(textures, source.Gltf) is { } emission
            && Slot(ExportMap.Emissive, textures.Emissive, -1, emission) is var emissive
            && (emissive.Texture is not null || textures.Emissive is null)) {
            slots.Add(emissive);
        }

        if (textures.AlphaMode != ModelAlphaMode.Opaque) {
            slots.Add(Slot(ExportMap.Opacity, textures.BaseColor, 3, [colour.W]));
        }

        Said(name, textures, warnings);
        return new(name, slots, warnings);
    }

    /// <summary>
    ///     What the emission is multiplied by, or null for none: a glTF's factor times its strength, which is black
    ///     unless the file says otherwise, and one for a picture in a format whose factor is not glTF's.
    /// </summary>
    static float[]? Emission(ModelMaterialTextures textures, bool gltf) {
        if (!gltf) {
            return textures.Emissive is null ? null : [1f, 1f, 1f, 1f];
        }

        var factor = textures.EmissiveFactor * textures.EmissiveStrength;
        return factor is { X: <= 0f, Y: <= 0f, Z: <= 0f } ? null : [factor.X, factor.Y, factor.Z, 1f];
    }

    /// <summary>
    ///     What a material asks for that its graph is not given: a normal's scale, a mask's cutoff, and a picture read
    ///     through a second coordinate set.
    /// </summary>
    static void Said(string name, ModelMaterialTextures textures, List<string> warnings) {
        if (textures.Normal is not null && Math.Abs(textures.NormalScale - 1f) > 1e-4f) {
            warnings.Add($"{name}: its normal's scale of {textures.NormalScale:0.###} is not brought in.");
        }

        if (textures.AlphaMode == ModelAlphaMode.Mask) {
            warnings.Add($"{name}: its alpha is cut at {textures.AlphaCutoff:0.###}, which is the export's to say.");
        }

        ModelTexture?[] pictures = [
            textures.BaseColor, textures.MetallicRoughness, textures.Normal, textures.Occlusion, textures.Emissive
        ];
        foreach (var picture in pictures.Distinct()) {
            if (picture is { UvSet: > 0 }) {
                warnings.Add(
                    $"{name}: '{Path.GetFileName(picture.Path ?? picture.Extension)}' is read through coordinate set "
                    + $"{picture.UvSet + 1}, and a graph's pictures are placed on the first."
                );
            }
        }
    }

    /// <summary>The usages a model's material fills, as an export names its maps.</summary>
    static class ExportMap {
        public const string BaseColor = "baseColor";
        public const string Normal = "normal";
        public const string Roughness = "roughness";
        public const string Metalness = "metalness";
        public const string Occlusion = "occlusion";
        public const string Emissive = "emissive";
        public const string Opacity = "opacity";
    }

    /// <summary>
    ///     A map: a picture's lane times a factor, placed where the picture's texture transform says, or the factor
    ///     alone when there is no picture.
    /// </summary>
    static ImportedSlot Placed(string usage, string? file, ModelTexture? texture, int lane, float[] factor) {
        var four = factor.Length == 1 ? [factor[0], factor[0], factor[0], 1f] : factor;
        var placement = file is not null && texture?.Transform is { } transform
            ? new ImportedPlacement(
                transform.Offset.X,
                transform.Offset.Y,
                transform.Rotation,
                transform.Scale.X,
                transform.Scale.Y
            )
            : null;
        return new(usage, file, file is null ? -1 : lane, false, four) { Placement = placement };
    }

    /// <summary>
    ///     A picture a material names, as a file: one beside the model where it is, or the bytes inside the model
    ///     written to the source's folder. Null, and said, for one that is not there.
    /// </summary>
    static string? Picture(ModelTexture? texture, Source source, string stem, List<string> warnings) {
        if (texture is null) {
            return null;
        }

        if (texture.Embedded is { } bytes) {
            Directory.CreateDirectory(source.Folder);
            var written = Path.Combine(source.Folder, Clean(stem) + texture.Extension);
            File.WriteAllBytes(written, bytes);
            return written;
        }

        if (texture.Path is not { Length: > 0 } relative) {
            return null;
        }

        foreach (var spelled in (string[])[relative, Uri.UnescapeDataString(relative)]) {
            var full = Path.GetFullPath(Path.Combine(source.Beside, spelled.Replace('\\', '/')));
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
