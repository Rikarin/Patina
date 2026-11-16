using Patina.Contracts;
using Patina.Export.Presets;

namespace Patina.Export.Materials;

/// <summary>
///     Which input of each surface shader a material file names holds which map (#157): MaterialX's
///     <c>standard_surface</c>, <c>open_pbr_surface</c> and <c>gltf_pbr</c>, and <c>UsdPreviewSurface</c>, which
///     USD and MaterialX share.
/// </summary>
static class SurfaceInputs {
    /// <summary>The maps, in the order each shader's row names its inputs.</summary>
    static readonly string[] Usages = [
        ExportMaps.BaseColor, ExportMaps.Roughness, ExportMaps.Metalness, ExportMaps.Normal, ExportMaps.Emissive,
        ExportMaps.Opacity, ExportMaps.Occlusion, ExportMaps.Height, ExportMaps.Coat, ExportMaps.CoatRoughness,
        ExportMaps.Sheen, ExportMaps.SheenColor, ExportMaps.SheenRoughness, ExportMaps.Transmission
    ];

    /// <summary>Each shader's inputs in <see cref="Usages" /> order, null where it has none for that map.</summary>
    static readonly Dictionary<string, string?[]> Rows = new(StringComparer.Ordinal) {
        ["standard_surface"] = [
            "base_color", "specular_roughness", "metalness", "normal", "emission_color", "opacity", null, null, "coat",
            "coat_roughness", "sheen", "sheen_color", "sheen_roughness", "transmission"
        ],
        ["open_pbr_surface"] = [
            "base_color", "specular_roughness", "base_metalness", "geometry_normal", "emission_color",
            "geometry_opacity", null, null, "coat_weight", "coat_roughness", "fuzz_weight", "fuzz_color",
            "fuzz_roughness", "transmission_weight"
        ],
        ["gltf_pbr"] = [
            "base_color", "roughness", "metallic", "normal", "emissive", "alpha", "occlusion", null, "clearcoat",
            "clearcoat_roughness", null, "sheen_color", "sheen_roughness", "transmission"
        ],
        [Preview] = [
            "diffuseColor", "roughness", "metallic", "normal", "emissiveColor", "opacity", "occlusion", "displacement",
            "clearcoat", "clearcoatRoughness"
        ]
    };

    /// <summary>USD's preview surface, which MaterialX can name too.</summary>
    public const string Preview = "UsdPreviewSurface";

    /// <summary>Whether a shader is one whose inputs are known here.</summary>
    public static bool Knows(string shader) => Rows.ContainsKey(shader);

    /// <summary>
    ///     A material's slots less OpenPBR's layer maps (#142) that do nothing, since a tool writes every input of a
    ///     surface whether it uses that layer or not: a layer map set to the number that leaves the base alone, and a
    ///     layer's roughness or colour when the layer has no weight. A material without a coat comes in without a coat
    ///     channel. glTF's sheen has no weight of its own, so it does not come in.
    /// </summary>
    public static List<ImportedSlot> Kept(List<ImportedSlot> slots) {
        var kept = slots.FindAll(static slot => !Idle(slot));
        var weighed = kept.Select(static slot => slot.Usage).ToHashSet(StringComparer.Ordinal);
        return kept.FindAll(slot => slot.Usage switch {
                ExportMaps.CoatRoughness => weighed.Contains(ExportMaps.Coat),
                ExportMaps.SheenColor or ExportMaps.SheenRoughness => weighed.Contains(ExportMaps.Sheen),
                _ => true
            }
        );
    }

    static bool Idle(ImportedSlot slot) =>
        slot.Texture is null
        && ExportMaps.OpenPbr.Contains(slot.Usage, StringComparer.Ordinal)
        && slot.Value[0].Equals(ExportMaps.Default(slot.Usage, 0));

    /// <summary>A shader's inputs, each with the map it holds.</summary>
    public static IEnumerable<(string Input, string Usage)> Of(string shader) =>
        Rows[shader].Select(static (input, at) => (input, Usages[at])).Where(static pair => pair.input is not null)!;
}
