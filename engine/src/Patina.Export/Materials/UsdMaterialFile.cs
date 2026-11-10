using Patina.Export.Presets;
using System.Globalization;
using System.Text;

namespace Patina.Export.Materials;

/// <summary>
///     A USD layer with one material whose surface is a <c>UsdPreviewSurface</c>. Each image is a <c>UsdUVTexture</c>,
///     and a lane holding one minus a map, or a normal whose green runs down, is a texture node of its own with the
///     scale and bias that undo it, since those apply to a whole image.
/// </summary>
sealed class UsdMaterialFile(string name, ICollection<string> warnings) {
    readonly string prim = MaterialFiles.Identifier(name);
    readonly List<string> inputs = [];
    readonly Dictionary<string, string> textures = new(StringComparer.Ordinal);
    readonly StringBuilder nodes = new();

    public string Write(MaterialSlots slots) {
        Colour(slots.BaseColor, "color3f inputs:diffuseColor", "base color");
        Lane(slots.Roughness, "float inputs:roughness", "roughness");
        if (slots.Metalness is null && slots.Specular is not null) {
            inputs.Add("int inputs:useSpecularWorkflow = 1");
            Colour(slots.Specular, "color3f inputs:specularColor", "specular");
        } else {
            Lane(slots.Metalness, "float inputs:metallic", "metalness");
        }

        Lane(slots.Occlusion, "float inputs:occlusion", "occlusion");
        Lane(slots.Opacity, "float inputs:opacity", "opacity");
        Colour(slots.Emissive, "color3f inputs:emissiveColor", "emissive");
        Colour(
            slots.Normal,
            "normal3f inputs:normal",
            "the normal",
            slots.Green == GreenAxis.Up ? "normal-up" : "normal-down"
        );
        Lane(slots.Height, "float inputs:displacement", "height");
        return Text(slots.Height is not null);
    }

    string Text(bool displaced) {
        var surface = string.Concat(inputs.Select(static input => $"\n        {input}"));
        var displacement = displaced
            ? $"\n    token outputs:displacement.connect = </{prim}/Surface.outputs:displacement>"
            : string.Empty;
        return $$"""
                 #usda 1.0
                 (
                     defaultPrim = "{{prim}}"
                     doc = "Written by Patina beside the texture files it names."
                 )

                 def Material "{{prim}}"
                 {
                     token outputs:surface.connect = </{{prim}}/Surface.outputs:surface>{{displacement}}

                     def Shader "Surface"
                     {
                         uniform token info:id = "UsdPreviewSurface"{{surface}}
                         token outputs:surface
                         token outputs:displacement
                     }

                     def Shader "Coordinates"
                     {
                         uniform token info:id = "UsdPrimvarReader_float2"
                         string inputs:varname = "st"
                         float2 outputs:result
                     }
                 {{nodes.ToString().TrimEnd()}}
                 }

                 """.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    void Colour(SlotColour? slot, string input, string label, string kind = "plain") {
        if (slot is not null && Texture(slot.File, label, kind) is { } node) {
            inputs.Add($"{input}.connect = </{prim}/{node}.outputs:rgb>");
        }
    }

    void Lane(SlotLane? slot, string input, string label) {
        if (slot is not null && Texture(slot.File, label, slot.Inverted ? "invert" : "plain") is { } node) {
            inputs.Add($"{input}.connect = </{prim}/{node}.outputs:{"rgba"[slot.Lane]}>");
        }
    }

    /// <summary>The texture node reading a file one way, made the first time it is asked for.</summary>
    string? Texture(SlotFile file, string slot, string kind) {
        if (!MaterialFiles.SceneReads(file.File.Format)) {
            warnings.Add(
                $"USD: {slot} is left out, because USD's own image readers do not open "
                + $"{ExportFormats.LabelOf(file.File.Format)}."
            );
            return null;
        }

        var key = $"{file.Uri}|{kind}";
        if (textures.TryGetValue(key, out var known)) {
            return known;
        }

        var node = $"Texture{textures.Count}";
        textures[key] = node;
        var (scale, bias) = kind switch {
            "invert" => ("(-1, -1, -1, -1)", "(1, 1, 1, 1)"),
            "normal-up" => ("(2, 2, 2, 1)", "(-1, -1, -1, 0)"),
            "normal-down" => ("(2, -2, 2, 1)", "(-1, 1, -1, 0)"),
            _ => ("(1, 1, 1, 1)", "(0, 0, 0, 0)")
        };
        var space = file.Srgb && kind == "plain" ? "sRGB" : "raw";
        nodes.Append(
            CultureInfo.InvariantCulture,
            $$"""

                  def Shader "{{node}}"
                  {
                      uniform token info:id = "UsdUVTexture"
                      asset inputs:file = @{{file.Uri}}@
                      token inputs:sourceColorSpace = "{{space}}"
                      float2 inputs:st.connect = </{{prim}}/Coordinates.outputs:result>
                      token inputs:wrapS = "repeat"
                      token inputs:wrapT = "repeat"
                      float4 inputs:scale = {{scale}}
                      float4 inputs:bias = {{bias}}
                      float3 outputs:rgb
                      float outputs:r
                      float outputs:g
                      float outputs:b
                      float outputs:a
                  }

              """
        );
        return node;
    }
}
