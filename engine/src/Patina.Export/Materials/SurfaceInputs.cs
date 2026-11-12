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
        ExportMaps.Opacity, ExportMaps.Occlusion, ExportMaps.Height
    ];

    /// <summary>Each shader's inputs in <see cref="Usages" /> order, null where it has none for that map.</summary>
    static readonly Dictionary<string, string?[]> Rows = new(StringComparer.Ordinal) {
        ["standard_surface"] = ["base_color", "specular_roughness", "metalness", "normal", "emission_color", "opacity"],
        ["open_pbr_surface"] = [
            "base_color", "specular_roughness", "base_metalness", "geometry_normal", "emission_color",
            "geometry_opacity"
        ],
        ["gltf_pbr"] = ["base_color", "roughness", "metallic", "normal", "emissive", "alpha", "occlusion"],
        [Preview] = [
            "diffuseColor", "roughness", "metallic", "normal", "emissiveColor", "opacity", "occlusion", "displacement"
        ]
    };

    /// <summary>USD's preview surface, which MaterialX can name too.</summary>
    public const string Preview = "UsdPreviewSurface";

    /// <summary>Whether a shader is one whose inputs are known here.</summary>
    public static bool Knows(string shader) => Rows.ContainsKey(shader);

    /// <summary>A shader's inputs, each with the map it holds.</summary>
    public static IEnumerable<(string Input, string Usage)> Of(string shader) =>
        Rows[shader].Select(static (input, at) => (input, Usages[at])).Where(static pair => pair.input is not null)!;
}
