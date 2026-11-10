using Patina.Export.Presets;
using Vixen.Interchange;
using Vector3 = Vixen.Core.Mathematics.Vector3;

namespace Patina.Export.Materials;

/// <summary>
///     A glTF material, written by Vixen's <see cref="ModelWriter" />. glTF fixes its packing: base colour and emissive
///     are sRGB, roughness is green and metallic blue of one file, occlusion is red, and green runs up a normal map. A
///     file packed otherwise is left out, and a material with no metallic-roughness file says what a surface without
///     those maps is, not glTF's default of fully metallic.
/// </summary>
static class GltfMaterialFile {
    /// <summary>The formats a glTF image can be, the WebP one through <c>EXT_texture_webp</c>.</summary>
    static readonly HashSet<string> Formats =
        new([ExportFormats.Png8, ExportFormats.Png16, ExportFormats.Jpeg, ExportFormats.WebP], StringComparer.Ordinal);

    public static string Write(MaterialSlots slots, string name, ICollection<string> warnings) {
        var baseColor = slots.BaseColor is { } colour ? Image(colour.File, "base colour", true, warnings) : null;
        var blended = Blended(slots, baseColor, warnings);
        var metallicRoughness = MetallicRoughness(slots, warnings);
        var emissive = slots.Emissive is { } glow ? Image(glow.File, "emissive", true, warnings) : null;
        var factors = new ModelMaterialFactors(
            new(1f, 1f, 1f, 1f),
            metallicRoughness is null ? ExportMaps.Default(ExportMaps.Metalness, 0) : 1f,
            metallicRoughness is null ? ExportMaps.Default(ExportMaps.Roughness, 0) : 1f
        );
        var material = new ModelWriterMaterial(name, factors) {
            Textures = new() {
                BaseColor = baseColor,
                MetallicRoughness = metallicRoughness,
                Occlusion = Occlusion(slots, warnings),
                Normal = Normal(slots, warnings),
                Emissive = emissive,
                EmissiveFactor = emissive is null ? Vector3.Zero : Vector3.One,
                AlphaMode = blended ? ModelAlphaMode.Blend : ModelAlphaMode.Opaque
            }
        };
        return ModelWriter.GltfMaterials([material], name);
    }

    /// <summary>A file glTF can name in a slot, or null with the reason said.</summary>
    static string? Image(SlotFile file, string slot, bool srgb, ICollection<string> warnings) {
        if (!Formats.Contains(file.File.Format)) {
            warnings.Add(
                $"glTF: {slot} is left out, because glTF does not read {ExportFormats.LabelOf(file.File.Format)}."
            );
            return null;
        }

        if (file.Srgb != srgb) {
            warnings.Add($"glTF: {slot} is left out, because glTF reads it as {(srgb ? "sRGB" : "linear")} numbers.");
            return null;
        }

        return file.Uri;
    }

    /// <summary>Whether opacity is the base colour file's alpha, the only place glTF reads it.</summary>
    static bool Blended(MaterialSlots slots, string? baseColor, ICollection<string> warnings) {
        if (slots.Opacity is null) {
            return false;
        }

        var inAlpha = slots.Opacity is { Lane: 3, Inverted: false } opacity && opacity.File == slots.BaseColor?.File;
        if (!inAlpha) {
            warnings.Add("glTF: opacity is left out, because glTF reads it only from the base colour file's alpha.");
        }

        return inAlpha && baseColor is not null;
    }

    static string? MetallicRoughness(MaterialSlots slots, ICollection<string> warnings) {
        if (slots is { Roughness: { Lane: 1, Inverted: false } rough, Metalness: { Lane: 2, Inverted: false } metal }
            && rough.File == metal.File) {
            return Image(rough.File, "roughness and metalness", false, warnings);
        }

        if (slots.Roughness is not null || slots.Metalness is not null) {
            warnings.Add(
                "glTF: roughness and metalness are left out, because glTF reads them from green and blue of one file."
            );
        }

        return null;
    }

    static string? Normal(MaterialSlots slots, ICollection<string> warnings) {
        if (slots.Normal is not { } normal) {
            return null;
        }

        if (slots.Green == GreenAxis.Up) {
            return Image(normal.File, "the normal", false, warnings);
        }

        warnings.Add(
            "glTF: the normal is left out, because glTF reads green up the image and this preset writes it down."
        );
        return null;
    }

    static string? Occlusion(MaterialSlots slots, ICollection<string> warnings) {
        if (slots.Occlusion is not { } occlusion) {
            return null;
        }

        if (occlusion is { Lane: 0, Inverted: false }) {
            return Image(occlusion.File, "occlusion", false, warnings);
        }

        warnings.Add("glTF: occlusion is left out, because glTF reads it from red.");
        return null;
    }
}
