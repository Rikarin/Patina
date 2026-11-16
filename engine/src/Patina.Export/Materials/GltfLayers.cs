using Patina.Export.Presets;
using Vixen.Interchange;
using Vector3 = Vixen.Core.Mathematics.Vector3;

namespace Patina.Export.Materials;

/// <summary>
///     OpenPBR's maps (#142) as glTF's extensions carry them: a coat through <c>KHR_materials_clearcoat</c>, with its
///     roughness and its own normal, a sheen through <c>KHR_materials_sheen</c>, and transmission through
///     <c>KHR_materials_transmission</c>. Each extension fixes its lanes: the coat in red, its roughness in green, the
///     sheen's roughness in alpha and transmission in red. glTF's sheen has no weight, so its colour is the sheen's
///     colour times its weight, which the preset writes as <c>sheenTint</c>. glTF has no coat colour, and its specular
///     and anisotropy extensions are not written, so those are said and left out.
/// </summary>
static class GltfLayers {
    public static ModelWriterLayers Of(OpenPbrSlots layers, GreenAxis green, ICollection<string> warnings) {
        var coat = Lane(layers.Coat, 0, "the coat", warnings);
        var sheen = Sheen(layers, warnings);
        var transmission = Lane(layers.Transmission, 0, "transmission", warnings);
        Unwritten(layers, warnings);
        return new() {
            ClearcoatFactor = coat is null ? 0f : 1f,
            Clearcoat = coat,
            ClearcoatRoughnessFactor = Factor(layers.CoatRoughness, ExportMaps.CoatRoughness),
            ClearcoatRoughness = coat is null ? null : Lane(layers.CoatRoughness, 1, "coat roughness", warnings),
            ClearcoatNormal = coat is null ? null : Normal(layers.CoatNormal, green, warnings),
            SheenColorFactor = sheen is null ? Vector3.Zero : Vector3.One,
            SheenColor = sheen,
            SheenRoughnessFactor = Factor(layers.SheenRoughness, ExportMaps.SheenRoughness),
            SheenRoughness = sheen is null ? null : Lane(layers.SheenRoughness, 3, "sheen roughness", warnings),
            TransmissionFactor = transmission is null ? 0f : 1f,
            Transmission = transmission
        };
    }

    /// <summary>One with a map, which then multiplies it, or OpenPBR's default for a set without one.</summary>
    static float Factor(SlotLane? slot, string map) => slot is null ? ExportMaps.Default(map, 0) : 1f;

    /// <summary>A grey map in the lane glTF reads it from, or null with the reason said.</summary>
    static string? Lane(SlotLane? slot, int lane, string label, ICollection<string> warnings) {
        if (slot is null) {
            return null;
        }

        // Alpha is linear in a file of either space, which is how glTF shares the sheen's colour and roughness.
        if (slot.Lane == lane && !slot.Inverted) {
            return GltfMaterialFile.Image(slot.File, label, lane == 3 && slot.File.Srgb, warnings);
        }

        warnings.Add($"glTF: {label} is left out, because glTF reads it from {LaneNames[lane]}.");
        return null;
    }

    static readonly string[] LaneNames = ["red", "green", "blue", "alpha"];

    /// <summary>
    ///     The sheen's colour times its weight, sRGB, which is made only for a set with a sheen; null for one without,
    ///     and said when a set has a sheen and the preset writes no file of it.
    /// </summary>
    static string? Sheen(OpenPbrSlots layers, ICollection<string> warnings) {
        if (layers.SheenTint is { } tint) {
            return GltfMaterialFile.Image(tint.File, "the sheen", true, warnings);
        }

        if (layers.Sheen is null) {
            return null;
        }

        warnings.Add(
            "glTF: the sheen is left out, because glTF reads its color times its weight, and this preset writes no "
            + "file of sheenTint."
        );
        return null;
    }

    static string? Normal(SlotColour? slot, GreenAxis green, ICollection<string> warnings) {
        if (slot is null) {
            return null;
        }

        if (green == GreenAxis.Up) {
            return GltfMaterialFile.Image(slot.File, "the coat normal", false, warnings);
        }

        warnings.Add(
            "glTF: the coat normal is left out, because glTF reads green up the image and this preset writes it down."
        );
        return null;
    }

    static void Unwritten(OpenPbrSlots layers, ICollection<string> warnings) {
        if (layers.CoatColor is not null) {
            warnings.Add("glTF: the coat's color is left out, because glTF's clear coat has none.");
        }

        if (layers.SpecularWeight is not null || layers.SpecularColor is not null || layers.Anisotropy is not null) {
            warnings.Add(
                "glTF: specular weight, color and anisotropy are left out, because this writes no glTF extension for "
                + "them."
            );
        }
    }
}
