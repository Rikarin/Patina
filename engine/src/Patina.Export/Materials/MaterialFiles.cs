using Patina.Contracts;
using Patina.Export.Presets;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Vixen.Interchange;
using Vector3 = Vixen.Core.Mathematics.Vector3;

namespace Patina.Export.Materials;

/// <summary>
///     The material files a preset writes beside a texture set's files (<see cref="ExportMaterials" />): a glTF
///     material through Vixen's writer, a USD preview surface and a MaterialX standard surface. Each names the files by
///     the slots <see cref="MaterialSlots" /> finds, and says what it had to leave out rather than wiring a lane to the
///     wrong input.
/// </summary>
public static class MaterialFiles {
    /// <summary>What was written and what a person should know.</summary>
    /// <param name="Files">The material files, absolute.</param>
    /// <param name="Warnings">A slot left out, and why.</param>
    public sealed record Written(IReadOnlyList<string> Files, IReadOnlyList<string> Warnings);

    /// <summary>The formats a glTF image can be, the WebP one through <c>EXT_texture_webp</c>.</summary>
    static readonly HashSet<string> GltfFormats =
        new([ExportFormats.Png8, ExportFormats.Png16, ExportFormats.Jpeg, ExportFormats.WebP], StringComparer.Ordinal);

    /// <summary>
    ///     The formats USD's and MaterialX's own image readers open without OpenImageIO: PNG, JPEG, TGA, Radiance HDR
    ///     and OpenEXR.
    /// </summary>
    static readonly HashSet<string> SceneFormats = new(
        [
            ExportFormats.Png8, ExportFormats.Png16, ExportFormats.Jpeg, ExportFormats.Tga, ExportFormats.Hdr,
            ExportFormats.Exr16, ExportFormats.Exr32
        ],
        StringComparer.Ordinal
    );

    /// <summary>Writes a set's material files.</summary>
    /// <param name="preset">The preset, which names the kinds.</param>
    /// <param name="files">The set's files and where each was delivered.</param>
    /// <param name="folder">Where the material files go.</param>
    /// <param name="stem">Their name without an extension, which is also the material's.</param>
    /// <param name="material">The material's name, or nothing for <paramref name="stem" />.</param>
    public static Written Write(
        ExportPreset preset,
        IReadOnlyList<(ExportedFile File, string Path)> files,
        string folder,
        string stem,
        string material
    ) {
        ArgumentNullException.ThrowIfNull(preset);
        var slots = MaterialSlots.Find(preset, files, folder);
        var name = material.Length > 0 ? material : stem;
        List<string> written = [];
        List<string> warnings = [];
        foreach (var kind in preset.Materials) {
            var path = Path.Combine(folder, $"{stem}.{kind}");
            var text = kind switch {
                ExportMaterials.Gltf => Gltf(slots, name, warnings),
                ExportMaterials.Usd => Usd(slots, name, warnings),
                _ => MaterialX(slots, name, warnings)
            };
            File.WriteAllText(path, text, new UTF8Encoding(false));
            written.Add(path);
        }

        return new(written, [..warnings.Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>
    ///     A glTF material. glTF fixes its packing: base colour and emissive are sRGB, roughness is green and metallic
    ///     blue of one file, occlusion is red, and green runs up a normal map. A file packed otherwise is left out, and
    ///     a material with no metallic-roughness file says what a surface without those maps is, not glTF's default of
    ///     fully metallic.
    /// </summary>
    public static string Gltf(MaterialSlots slots, string name, ICollection<string> warnings) {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(warnings);
        string? Image(SlotFile file, string slot, bool srgb) {
            if (!GltfFormats.Contains(file.File.Format)) {
                warnings.Add($"glTF: {slot} is left out, because glTF does not read {ExportFormats.LabelOf(file.File.Format)}.");
                return null;
            }

            if (file.Srgb != srgb) {
                warnings.Add($"glTF: {slot} is left out, because glTF reads it as {(srgb ? "sRGB" : "linear")} numbers.");
                return null;
            }

            return file.Uri;
        }

        var baseColor = slots.BaseColor is { } colour ? Image(colour.File, "base colour", true) : null;
        var opaque = slots.Opacity is not { Lane: 3, Inverted: false } opacity || opacity.File != slots.BaseColor?.File;
        if (slots.Opacity is not null && opaque) {
            warnings.Add("glTF: opacity is left out, because glTF reads it only from the base colour file's alpha.");
        }

        var packed = slots is { Roughness: { Lane: 1, Inverted: false } rough, Metalness: { Lane: 2, Inverted: false } metal }
            && rough.File == metal.File;
        string? metallicRoughness = null;
        if (packed) {
            metallicRoughness = Image(slots.Roughness!.File, "roughness and metalness", false);
        } else if (slots.Roughness is not null || slots.Metalness is not null) {
            warnings.Add(
                "glTF: roughness and metalness are left out, because glTF reads them from the green and blue of one file."
            );
        }

        string? normal = null;
        if (slots.Normal is { } normals) {
            if (slots.Green == GreenAxis.Up) {
                normal = Image(normals.File, "the normal", false);
            } else {
                warnings.Add("glTF: the normal is left out, because glTF reads green up the image and this preset writes it down.");
            }
        }

        string? occlusion = null;
        if (slots.Occlusion is { } ao) {
            if (ao is { Lane: 0, Inverted: false }) {
                occlusion = Image(ao.File, "occlusion", false);
            } else {
                warnings.Add("glTF: occlusion is left out, because glTF reads it from red.");
            }
        }

        var emissive = slots.Emissive is { } glow ? Image(glow.File, "emissive", true) : null;
        var factors = new ModelMaterialFactors(
            new(1f, 1f, 1f, 1f),
            metallicRoughness is null ? ExportMaps.Default(ExportMaps.Metalness, 0) : 1f,
            metallicRoughness is null ? ExportMaps.Default(ExportMaps.Roughness, 0) : 1f
        );
        var material = new ModelWriterMaterial(name, factors) {
            Textures = new() {
                BaseColor = baseColor,
                MetallicRoughness = metallicRoughness,
                Occlusion = occlusion,
                Normal = normal,
                Emissive = emissive,
                EmissiveFactor = emissive is null ? Vector3.Zero : Vector3.One,
                AlphaMode = baseColor is not null && !opaque ? ModelAlphaMode.Blend : ModelAlphaMode.Opaque
            }
        };
        return ModelWriter.GltfMaterials([material], name);
    }

    /// <summary>
    ///     A USD layer with one material whose surface is a <c>UsdPreviewSurface</c>. Each image is a
    ///     <c>UsdUVTexture</c>, and a lane holding one minus a map, or a normal whose green runs down, is a texture
    ///     node of its own with the scale and bias that undo it, since those apply to a whole image.
    /// </summary>
    public static string Usd(MaterialSlots slots, string name, ICollection<string> warnings) {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(warnings);
        var prim = Identifier(name);
        List<string> inputs = [];
        Dictionary<string, string> textures = new(StringComparer.Ordinal);
        StringBuilder nodes = new();

        string? Texture(SlotFile file, string slot, string kind) {
            if (!SceneFormats.Contains(file.File.Format)) {
                warnings.Add($"USD: {slot} is left out, because USD's own image readers do not open {ExportFormats.LabelOf(file.File.Format)}.");
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
            nodes.Append(
                CultureInfo.InvariantCulture,
                $$"""

                      def Shader "{{node}}"
                      {
                          uniform token info:id = "UsdUVTexture"
                          asset inputs:file = @{{file.Uri}}@
                          token inputs:sourceColorSpace = "{{(file.Srgb && kind == "plain" ? "sRGB" : "raw")}}"
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

        Colour(slots.BaseColor, "color3f inputs:diffuseColor", "base colour");
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
        if (slots.Normal is not null) {
            Colour(slots.Normal, "normal3f inputs:normal", "the normal", slots.Green == GreenAxis.Up ? "normal-up" : "normal-down");
        }

        Lane(slots.Height, "float inputs:displacement", "height");
        var surface = string.Concat(inputs.Select(static input => $"\n        {input}"));
        var displacement = slots.Height is null
            ? string.Empty
            : $"\n    token outputs:displacement.connect = </{prim}/Surface.outputs:displacement>";
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

    /// <summary>
    ///     A MaterialX 1.38 document: a node graph of images feeding a <c>standard_surface</c>, wrapped in a
    ///     <c>surfacematerial</c>. A lane is an <c>extract</c>, one minus it an <c>invert</c>, and a normal whose green
    ///     runs down is turned over before <c>normalmap</c>, which reads green up. Standard surface has no occlusion
    ///     and no specular colour of this kind, so those are said and left out; height is left to a displacement
    ///     shader this does not write.
    /// </summary>
    public static string MaterialX(MaterialSlots slots, string name, ICollection<string> warnings) {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(warnings);
        var id = Identifier(name);
        var graph = new XElement("nodegraph", new XAttribute("name", $"NG_{id}"));
        var shader = new XElement("standard_surface", new XAttribute("name", $"SR_{id}"), new XAttribute("type", "surfaceshader"));
        Dictionary<string, string> images = new(StringComparer.Ordinal);

        XElement Input(string input, string type, string? node = null, string? value = null) {
            var element = new XElement("input", new XAttribute("name", input), new XAttribute("type", type));
            if (node is not null) {
                element.Add(new XAttribute("nodename", node));
            }

            if (value is not null) {
                element.Add(new XAttribute("value", value));
            }

            return element;
        }

        string Node(string kind, string type, params XElement[] inputs) {
            var node = $"{kind}{graph.Elements().Count()}";
            graph.Add(new XElement(kind, new XAttribute("name", node), new XAttribute("type", type), inputs));
            return node;
        }

        // An image of all its lanes: colour when its numbers are sRGB, a vector when they are linear.
        (string Node, string Type)? Image(SlotFile file, string slot) {
            if (!SceneFormats.Contains(file.File.Format) || file.Lanes == 2) {
                warnings.Add($"MaterialX: {slot} is left out, because MaterialX's own image readers do not open {ExportFormats.LabelOf(file.File.Format)}.");
                return null;
            }

            var type = (file.Lanes, file.Srgb) switch {
                (1, _) => "float",
                (3, true) => "color3",
                (3, false) => "vector3",
                (_, true) => "color4",
                _ => "vector4"
            };
            if (images.TryGetValue(file.Uri, out var known)) {
                return (known, type);
            }

            var path = new XElement("input", new XAttribute("name", "file"), new XAttribute("type", "filename"), new XAttribute("value", file.Uri));
            if (file.Srgb) {
                path.Add(new XAttribute("colorspace", "srgb_texture"));
            }

            var node = Node("image", type, path);
            images[file.Uri] = node;
            return (node, type);
        }

        // Three lanes, as the type an input wants.
        string? Three(SlotColour? slot, string label, string type) {
            if (slot is null || Image(slot.File, label) is not var (node, from)) {
                return null;
            }

            return from == type ? node : Node("convert", type, Input("in", from, node));
        }

        string? Grey(SlotLane? slot, string label) {
            if (slot is null || Image(slot.File, label) is not var (node, from)) {
                return null;
            }

            var lane = from == "float" ? node : Node("extract", "float", Input("in", from, node), Input("index", "integer", value: slot.Lane.ToString(CultureInfo.InvariantCulture)));
            return slot.Inverted ? Node("invert", "float", Input("in", "float", lane)) : lane;
        }

        void Connect(string input, string type, string? node) {
            if (node is null) {
                return;
            }

            var output = $"out_{input}";
            graph.Add(new XElement("output", new XAttribute("name", output), new XAttribute("type", type), new XAttribute("nodename", node)));
            shader.Add(new XElement("input", new XAttribute("name", input), new XAttribute("type", type), new XAttribute("nodegraph", $"NG_{id}"), new XAttribute("output", output)));
        }

        Connect("base_color", "color3", Three(slots.BaseColor, "base colour", "color3"));
        Connect("specular_roughness", "float", Grey(slots.Roughness, "roughness"));
        Connect("metalness", "float", Grey(slots.Metalness, "metalness"));
        if (Grey(slots.Opacity, "opacity") is { } opacity) {
            Connect("opacity", "color3", Node("convert", "color3", Input("in", "float", opacity)));
        }

        if (Three(slots.Emissive, "emissive", "color3") is { } emission) {
            shader.Add(Input("emission", "float", value: "1"));
            Connect("emission_color", "color3", emission);
        }

        if (Three(slots.Normal, "the normal", "vector3") is { } stored) {
            var upright = slots.Green == GreenAxis.Up
                ? stored
                : Node(
                    "add",
                    "vector3",
                    Input("in1", "vector3", Node("multiply", "vector3", Input("in1", "vector3", stored), Input("in2", "vector3", value: "1, -1, 1"))),
                    Input("in2", "vector3", value: "0, 1, 0")
                );
            Connect("normal", "vector3", Node("normalmap", "vector3", Input("in", "vector3", upright)));
        }

        if (slots.Occlusion is not null) {
            warnings.Add("MaterialX: occlusion is left out, because standard_surface has no input for it.");
        }

        if (slots.Specular is not null && slots.Metalness is null) {
            warnings.Add("MaterialX: specular colour is left out, because standard_surface is metal-roughness.");
        }

        if (slots.Height is not null) {
            warnings.Add("MaterialX: height is left out, because this writes no displacement shader.");
        }

        var material = new XElement(
            "surfacematerial",
            new XAttribute("name", $"M_{id}"),
            new XAttribute("type", "material"),
            new XElement("input", new XAttribute("name", "surfaceshader"), new XAttribute("type", "surfaceshader"), new XAttribute("nodename", $"SR_{id}"))
        );
        var root = new XElement("materialx", new XAttribute("version", "1.38"), new XAttribute("colorspace", "lin_rec709"), graph, shader, material);
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        using var text = new Utf8Writer();
        document.Save(text);
        return text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    /// <summary>A name as a USD prim or MaterialX element may be called: letters, digits and underscores.</summary>
    static string Identifier(string name) {
        var cleaned = NamingTemplate.Clean(name).Replace('-', '_');
        return char.IsAsciiLetter(cleaned[0]) || cleaned[0] == '_' ? cleaned : $"_{cleaned}";
    }

    /// <summary>A string writer that says UTF-8 in an XML declaration, which is what the file is.</summary>
    sealed class Utf8Writer : StringWriter {
        public Utf8Writer() : base(CultureInfo.InvariantCulture) { }

        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
