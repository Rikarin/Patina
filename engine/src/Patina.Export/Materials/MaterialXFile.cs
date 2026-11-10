using Patina.Export.Presets;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Patina.Export.Materials;

/// <summary>
///     A MaterialX 1.38 document: a node graph of images feeding a <c>standard_surface</c>, wrapped in a
///     <c>surfacematerial</c>. A lane is an <c>extract</c>, one minus it an <c>invert</c>, and a normal whose green
///     runs down is turned over before <c>normalmap</c>, which reads green up. Standard surface has no occlusion and no
///     specular colour of this kind, so those are said and left out; height is left to a displacement shader this does
///     not write.
/// </summary>
sealed class MaterialXFile {
    const string Float = "float";
    const string Colour3 = "color3";
    const string Vector3 = "vector3";

    readonly string id;
    readonly ICollection<string> warnings;
    readonly XElement graph;
    readonly XElement shader;
    readonly Dictionary<string, string> images = new(StringComparer.Ordinal);

    public MaterialXFile(string name, ICollection<string> warnings) {
        id = MaterialFiles.Identifier(name);
        this.warnings = warnings;
        graph = new("nodegraph", new XAttribute("name", $"NG_{id}"));
        shader = new("standard_surface", new XAttribute("name", $"SR_{id}"), new XAttribute("type", "surfaceshader"));
    }

    public string Write(MaterialSlots slots) {
        Connect("base_color", Colour3, Three(slots.BaseColor, "base color", Colour3));
        Connect("specular_roughness", Float, Grey(slots.Roughness, "roughness"));
        Connect("metalness", Float, Grey(slots.Metalness, "metalness"));
        if (Grey(slots.Opacity, "opacity") is { } opacity) {
            Connect("opacity", Colour3, Node("convert", Colour3, Input("in", Float, opacity)));
        }

        if (Three(slots.Emissive, "emissive", Colour3) is { } emission) {
            shader.Add(Input("emission", Float, value: "1"));
            Connect("emission_color", Colour3, emission);
        }

        if (Three(slots.Normal, "the normal", Vector3) is { } stored) {
            Connect(
                "normal",
                Vector3,
                Node("normalmap", Vector3, Input("in", Vector3, Upright(stored, slots.Green)))
            );
        }

        Unsupported(slots);
        return Text();
    }

    /// <summary>A normal turned so its green runs up: one minus green, as a multiply and an add.</summary>
    string Upright(string stored, GreenAxis green) {
        if (green == GreenAxis.Up) {
            return stored;
        }

        var flipped = Node(
            "multiply",
            Vector3,
            Input("in1", Vector3, stored),
            Input("in2", Vector3, value: "1, -1, 1")
        );
        return Node("add", Vector3, Input("in1", Vector3, flipped), Input("in2", Vector3, value: "0, 1, 0"));
    }

    void Unsupported(MaterialSlots slots) {
        if (slots.Occlusion is not null) {
            warnings.Add("MaterialX: occlusion is left out, because standard_surface has no input for it.");
        }

        if (slots.Specular is not null && slots.Metalness is null) {
            warnings.Add("MaterialX: specular color is left out, because standard_surface is metal-roughness.");
        }

        if (slots.Height is not null) {
            warnings.Add("MaterialX: height is left out, because this writes no displacement shader.");
        }
    }

    string Text() {
        var material = new XElement(
            "surfacematerial",
            new XAttribute("name", $"M_{id}"),
            new XAttribute("type", "material"),
            new XElement(
                "input",
                new XAttribute("name", "surfaceshader"),
                new XAttribute("type", "surfaceshader"),
                new XAttribute("nodename", $"SR_{id}")
            )
        );
        var root = new XElement(
            "materialx",
            new XAttribute("version", "1.38"),
            new XAttribute("colorspace", "lin_rec709"),
            graph,
            shader,
            material
        );
        var document = new XDocument(new("1.0", "utf-8", null), root);
        using var text = new Utf8Writer();
        document.Save(text);
        return text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    static XElement Input(string input, string type, string? node = null, string? value = null) {
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

    /// <summary>An image of all its lanes: colour when its numbers are sRGB, a vector when they are linear.</summary>
    (string Node, string Type)? Image(SlotFile file, string slot) {
        if (!MaterialFiles.SceneReads(file.File.Format) || file.Lanes == 2) {
            warnings.Add(
                $"MaterialX: {slot} is left out, because MaterialX's own image readers do not open "
                + $"{ExportFormats.LabelOf(file.File.Format)}."
            );
            return null;
        }

        var type = (file.Lanes, file.Srgb) switch {
            (1, _) => Float,
            (3, true) => Colour3,
            (3, _) => Vector3,
            (_, true) => "color4",
            _ => "vector4"
        };
        if (images.TryGetValue(file.Uri, out var known)) {
            return (known, type);
        }

        var path = new XElement(
            "input",
            new XAttribute("name", "file"),
            new XAttribute("type", "filename"),
            new XAttribute("value", file.Uri)
        );
        if (file.Srgb) {
            path.Add(new XAttribute("colorspace", "srgb_texture"));
        }

        var node = Node("image", type, path);
        images[file.Uri] = node;
        return (node, type);
    }

    /// <summary>Three lanes, as the type an input wants.</summary>
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

        var lane = from == Float
            ? node
            : Node(
                "extract",
                Float,
                Input("in", from, node),
                Input("index", "integer", value: slot.Lane.ToString(CultureInfo.InvariantCulture))
            );
        return slot.Inverted ? Node("invert", Float, Input("in", Float, lane)) : lane;
    }

    void Connect(string input, string type, string? node) {
        if (node is null) {
            return;
        }

        var output = $"out_{input}";
        graph.Add(
            new XElement(
                "output",
                new XAttribute("name", output),
                new XAttribute("type", type),
                new XAttribute("nodename", node)
            )
        );
        shader.Add(
            new XElement(
                "input",
                new XAttribute("name", input),
                new XAttribute("type", type),
                new XAttribute("nodegraph", $"NG_{id}"),
                new XAttribute("output", output)
            )
        );
    }

    /// <summary>A string writer that says UTF-8 in an XML declaration, which is what the file is.</summary>
    sealed class Utf8Writer : StringWriter {
        public Utf8Writer() : base(CultureInfo.InvariantCulture) { }

        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
