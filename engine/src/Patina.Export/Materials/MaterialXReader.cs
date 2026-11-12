using Patina.Contracts;
using Patina.Export.Presets;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Patina.Export.Materials;

/// <summary>
///     Reads the materials of a MaterialX document as maps (#157): each <c>surfacematerial</c>'s shader, or each shader
///     when there is no material, whose inputs are a value or a chain of nodes ending in an image. It reads what
///     <see cref="MaterialXFile" /> writes and the shaders a studio's tools write beside it: <c>standard_surface</c>,
///     <c>open_pbr_surface</c>, <c>gltf_pbr</c> and <c>UsdPreviewSurface</c>. A chain is followed through
///     <c>convert</c>, <c>extract</c>, <c>invert</c>, <c>normalmap</c> and a <c>multiply</c> by a value; anything else
///     on the way is something a graph would have to compute, so the input is left out and said.
/// </summary>
public sealed class MaterialXReader {
    readonly string folder;
    readonly XElement root;
    readonly List<string> warnings = [];

    MaterialXReader(string path, XElement root) {
        folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        this.root = root;
    }

    /// <summary>The materials of a <c>.mtlx</c> file, in its order.</summary>
    /// <exception cref="InvalidDataException">It is not a MaterialX document, or describes no material.</exception>
    public static IReadOnlyList<ImportedMaterial> Read(string path) {
        XDocument document;
        try {
            using var stream = SharedFile.OpenRead(path);
            document = XDocument.Load(stream);
        } catch (XmlException e) {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a MaterialX document: {e.Message}");
        }

        if (document.Root is not { Name.LocalName: "materialx" } materialx) {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a MaterialX document.");
        }

        return new MaterialXReader(path, materialx).Materials();
    }

    List<ImportedMaterial> Materials() {
        var shaders = root.Elements().Where(static e => SurfaceInputs.Knows(e.Name.LocalName)).ToList();
        List<(string Name, XElement Shader)> found = [];
        foreach (var material in root.Elements().Where(static e => e.Name.LocalName == "surfacematerial")) {
            var named = Attribute(Child(material, "surfaceshader"), "nodename");
            if (Named(shaders, named) is { } shader) {
                found.Add((Attribute(material, "name") ?? Attribute(shader, "name") ?? "Material", shader));
            }
        }

        if (found.Count == 0) {
            found = [..shaders.Select(static s => (Attribute(s, "name") ?? "Material", s))];
        }

        if (found.Count == 0) {
            throw new InvalidDataException(
                "This MaterialX document has no standard_surface, open_pbr_surface, gltf_pbr or UsdPreviewSurface."
            );
        }

        return [..found.Select(pair => Material(pair.Name, pair.Shader))];
    }

    /// <summary>The element of a name among some, or null.</summary>
    static XElement? Named(List<XElement> elements, string? name) {
        foreach (var element in elements) {
            if (Attribute(element, "name") == name) {
                return element;
            }
        }

        return null;
    }

    ImportedMaterial Material(string name, XElement shader) {
        warnings.Clear();
        var inputs = shader.Elements().Where(static e => e.Name.LocalName == "input").ToList();
        List<ImportedSlot> slots = [];
        foreach (var (port, usage) in SurfaceInputs.Of(shader.Name.LocalName)) {
            if (inputs.FirstOrDefault(input => Attribute(input, "name") == port) is { } input
                && Slot(usage, input, root) is { } slot) {
                slots.Add(slot);
            }
        }

        return new(Unprefixed(name), slots, [..warnings]);
    }

    /// <summary>A material's name without the <c>M_</c> the writer puts before it.</summary>
    static string Unprefixed(string name) => name.StartsWith("M_", StringComparison.Ordinal) ? name[2..] : name;

    /// <summary>An input as a map: its value, or what its node chain reads.</summary>
    ImportedSlot? Slot(string usage, XElement input, XElement scope) {
        if (Attribute(input, "value") is { } value && Attribute(input, "nodename") is null) {
            var numbers = Numbers(value);
            return numbers.Count == 0 ? null : new(usage, null, -1, false, Four(numbers));
        }

        var trace = new Trace(usage, -1, false, [1f, 1f, 1f, 1f], true);
        return Follow(usage, input, scope, trace) is { } read
            ? new ImportedSlot(usage, read.File, read.Lane, read.Inverted, read.Factor) { GreenUp = read.GreenUp }
            : null;
    }

    /// <summary>What a node chain has come to on its way back to an image.</summary>
    sealed record Trace(string Usage, int Lane, bool Inverted, float[] Factor, bool GreenUp) {
        public string? File { get; init; }
    }

    Trace? Follow(string usage, XElement input, XElement scope, Trace trace) {
        if (Attribute(input, "nodegraph") is { } graphName) {
            var graph = root.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "nodegraph" && Attribute(e, "name") == graphName);
            var output = graph?.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "output"
                    && (Attribute(input, "output") is not { } wanted || Attribute(e, "name") == wanted)
                );
            return graph is null || output is null
                ? Left(usage, $"its node graph '{graphName}' is not there")
                : Follow(usage, output, graph, trace);
        }

        if (Attribute(input, "nodename") is not { } nodeName) {
            return Left(usage, "it names neither a value nor a node");
        }

        var node = scope.Elements()
            .FirstOrDefault(e => e.Name.LocalName != "output" && Attribute(e, "name") == nodeName);
        return node is null ? Left(usage, $"its node '{nodeName}' is not there") : Node(usage, node, scope, trace);
    }

    Trace? Node(string usage, XElement node, XElement scope, Trace trace) {
        switch (node.Name.LocalName) {
            case "image" or "tiledimage" or "gltf_image":
                return Child(node, "file") is { } file && Attribute(file, "value") is { Length: > 0 } uri
                    ? trace with { File = Resolve(node, uri) }
                    : Left(usage, "its image names no file");
            case "convert" or "normalmap":
                return Through(usage, node, "in", scope, trace);
            case "extract":
                var index = int.TryParse(
                    Attribute(Child(node, "index"), "value"),
                    CultureInfo.InvariantCulture,
                    out var lane
                )
                    ? lane
                    : 0;
                return lane is < 0 or > 3
                    ? Left(usage, $"it extracts lane {lane.ToString(CultureInfo.InvariantCulture)}, which no image has")
                    : Through(usage, node, "in", scope, trace with { Lane = index });
            case "invert":
                return Through(usage, node, "in", scope, trace with { Inverted = !trace.Inverted });
            case "multiply" when Constant(node, "in2") is { } scale:
                if (usage == ExportMaps.Normal && scale is [_, < 0, ..]) {
                    // The writer's turn of green: a multiply by (1, -1, 1) and an add of (0, 1, 0) before it.
                    return Through(usage, node, "in1", scope, trace with { GreenUp = !trace.GreenUp });
                }

                return Through(
                    usage,
                    node,
                    "in1",
                    scope,
                    trace with { Factor = [..trace.Factor.Select((f, at) => f * scale[Math.Min(at, scale.Count - 1)])] }
                );
            case "add" when usage == ExportMaps.Normal && Constant(node, "in2") is [_, 1f, ..]:
                return Through(usage, node, "in1", scope, trace);
            default:
                return Left(
                    usage,
                    $"it is made by a '{node.Name.LocalName}' node, which a graph would have to compute"
                );
        }
    }

    Trace? Through(string usage, XElement node, string port, XElement scope, Trace trace) =>
        Child(node, port) is { } input ? Follow(usage, input, scope, trace) : Left(usage, $"its '{port}' is not wired");

    static List<float>? Constant(XElement node, string port) =>
        Child(node, port) is { } input && Attribute(input, "nodename") is null && Attribute(input, "value") is { } text
            ? Numbers(text)
            : null;

    /// <summary>A file an image names, with the prefixes of the elements around it, from the document.</summary>
    string Resolve(XElement node, string uri) {
        var prefix = string.Concat(
            node.AncestorsAndSelf().Reverse().Select(static e => Attribute(e, "fileprefix") ?? string.Empty)
        );
        var relative = Uri.UnescapeDataString(prefix + uri).Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(folder, relative));
    }

    Trace? Left(string usage, string why) {
        warnings.Add($"MaterialX: {usage} is left out, because {why}.");
        return null;
    }

    static XElement? Child(XElement? parent, string name) =>
        parent?.Elements().FirstOrDefault(e => e.Name.LocalName == "input" && Attribute(e, "name") == name);

    static string? Attribute(XElement? element, string name) => element?.Attribute(name)?.Value;

    internal static List<float> Numbers(string text) {
        List<float> numbers = [];
        foreach (var part in text.Split([',', ' ', '(', ')'], StringSplitOptions.RemoveEmptyEntries)) {
            if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    /// <summary>Four lanes from one to four numbers: one is grey, three a color with alpha one.</summary>
    internal static float[] Four(IReadOnlyList<float> numbers) =>
        numbers.Count switch {
            1 => [numbers[0], numbers[0], numbers[0], 1f],
            2 => [numbers[0], numbers[1], 0f, 1f],
            3 => [numbers[0], numbers[1], numbers[2], 1f],
            _ => [numbers[0], numbers[1], numbers[2], numbers[3]]
        };
}
