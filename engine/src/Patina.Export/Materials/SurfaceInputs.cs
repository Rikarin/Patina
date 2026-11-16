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
        ExportMaps.Sheen, ExportMaps.SheenColor, ExportMaps.SheenRoughness, ExportMaps.Transmission,
        ExportMaps.SpecularWeight, ExportMaps.SpecularColor, ExportMaps.Anisotropy, ExportMaps.CoatColor,
        ExportMaps.CoatNormal, ExportMaps.AnisotropyRotation, ExportMaps.Subsurface, ExportMaps.SubsurfaceColor,
        ExportMaps.SubsurfaceRadius, ExportMaps.ThinFilm, ExportMaps.ThinFilmThickness
    ];

    /// <summary>Each shader's inputs in <see cref="Usages" /> order, null where it has none for that map.</summary>
    static readonly Dictionary<string, string?[]> Rows = new(StringComparer.Ordinal) {
        ["standard_surface"] = [
            "base_color", "specular_roughness", "metalness", "normal", "emission_color", "opacity", null, null, "coat",
            "coat_roughness", "sheen", "sheen_color", "sheen_roughness", "transmission", "specular", "specular_color",
            "specular_anisotropy", "coat_color", "coat_normal", "specular_rotation", "subsurface", "subsurface_color",
            "subsurface_scale", null, null
        ],
        ["open_pbr_surface"] = [
            "base_color", "specular_roughness", "base_metalness", "geometry_normal", "emission_color",
            "geometry_opacity", null, null, "coat_weight", "coat_roughness", "fuzz_weight", "fuzz_color",
            "fuzz_roughness", "transmission_weight", "specular_weight", "specular_color",
            "specular_roughness_anisotropy", "coat_color", "geometry_coat_normal", null, "subsurface_weight",
            "subsurface_color", "subsurface_radius", "thin_film_weight", "thin_film_thickness"
        ],
        ["gltf_pbr"] = [
            "base_color", "roughness", "metallic", "normal", "emissive", "alpha", "occlusion", null, "clearcoat",
            "clearcoat_roughness", null, "sheen_color", "sheen_roughness", "transmission", "specular", "specular_color",
            null, null, "clearcoat_normal"
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
    ///     A material's slots less OpenPBR's maps (#142) that do nothing, since a tool writes every input of a surface
    ///     whether it uses that layer or not: a map set to the number that leaves the base alone, and a layer's
    ///     roughness, colour, normal, radius, thickness or turn when the layer has no weight. A material without a coat
    ///     comes in without a coat channel. A shader whose sheen has no weight of its own, glTF's, gets a whole one
    ///     while its sheen colour is not black, which is what makes OpenPBR's sheen glTF's.
    /// </summary>
    /// <param name="slots">What the shader's inputs said.</param>
    /// <param name="shader">The shader, whose row says whether its sheen has a weight.</param>
    public static List<ImportedSlot> Kept(List<ImportedSlot> slots, string shader) {
        var kept = slots.FindAll(static slot => !Idle(slot));
        if (Weightless(shader)
            && kept.Find(static slot => slot.Usage == ExportMaps.SheenColor) is { } colour
            && (colour.Texture is not null || colour.Value.Take(3).Any(static lane => lane > 0f))) {
            kept.Add(new(ExportMaps.Sheen, null, -1, false, [1f, 1f, 1f, 1f]));
        }

        var weighed = kept.Select(static slot => slot.Usage).ToHashSet(StringComparer.Ordinal);
        return kept.FindAll(slot => slot.Usage switch {
                ExportMaps.CoatRoughness or ExportMaps.CoatColor or ExportMaps.CoatNormal =>
                    weighed.Contains(ExportMaps.Coat),
                ExportMaps.SheenColor or ExportMaps.SheenRoughness => weighed.Contains(ExportMaps.Sheen),
                ExportMaps.SubsurfaceColor or ExportMaps.SubsurfaceRadius => weighed.Contains(ExportMaps.Subsurface),
                ExportMaps.ThinFilmThickness => weighed.Contains(ExportMaps.ThinFilm),
                ExportMaps.AnisotropyRotation => weighed.Contains(ExportMaps.Anisotropy),
                _ => true
            }
        );
    }

    /// <summary>Whether a shader has a sheen colour and no sheen weight, as glTF's has.</summary>
    static bool Weightless(string shader) =>
        Rows.TryGetValue(shader, out var row)
        && Array.IndexOf(Usages, ExportMaps.Sheen) is var weight
        && Array.IndexOf(Usages, ExportMaps.SheenColor) is var colour
        && row.Length > colour
        && row[weight] is null
        && row[colour] is not null;

    /// <summary>
    ///     Whether a slot is a constant at OpenPBR's default, lane by lane, or a coat normal with no picture, which is
    ///     flat whatever its numbers say.
    /// </summary>
    static bool Idle(ImportedSlot slot) =>
        slot.Texture is null
        && ExportMaps.OpenPbr.Contains(slot.Usage, StringComparer.Ordinal)
        && (slot.Usage == ExportMaps.CoatNormal
            || Enumerable.Range(0, Math.Min(3, slot.Value.Count))
                .All(lane => slot.Value[lane].Equals(ExportMaps.Default(slot.Usage, lane))));

    /// <summary>A shader's inputs, each with the map it holds.</summary>
    public static IEnumerable<(string Input, string Usage)> Of(string shader) =>
        Rows[shader].Select(static (input, at) => (input, Usages[at])).Where(static pair => pair.input is not null)!;
}
