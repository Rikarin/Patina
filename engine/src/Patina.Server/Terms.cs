using Patina.Contracts;
using System.Text;

namespace Patina.Server;

/// <summary>
///     What a person calls the words the engine stores: a blend mode, a mask source, a channel, a mesh map. The
///     values stay what files and commands carry; a label goes beside each one on the wire, so that no client shows a
///     raw value and none holds a list of its own (#198).
/// </summary>
public static class Terms {
    /// <summary>The words whose label is not what splitting them into words gives.</summary>
    static readonly Dictionary<string, string> Named = new(StringComparer.Ordinal) {
        ["ColourDodge"] = "Color Dodge",
        ["ColourBurn"] = "Color Burn",

        // Projections.
        ["Uv"] = "UV",
        ["Triplanar"] = "Tri-planar",

        // Channels, as an Output node's usage names them.
        ["baseColor"] = "Base Color",
        ["metalness"] = "Metallic",
        ["occlusion"] = "Ambient Occlusion",

        // Mesh maps.
        ["ao"] = "Ambient Occlusion",
        ["bent"] = "Bent Normal",
        ["world"] = "World Normal",
        ["id"] = "ID",

        // Port and parameter kinds.
        ["Float"] = "Number",
        ["Int"] = "Whole Number",
        ["Bool"] = "On or Off",
        ["Float2"] = "2 Numbers",
        ["Float3"] = "3 Numbers",
        ["Float4"] = "4 Numbers"
    };

    /// <summary>
    ///     What a layer stack calls some words differently from a graph. A painting tool's default blend is Normal,
    ///     where a graph's blend node copies; a mask or a fill is uniform, or a bitmap, or read from a mesh map.
    /// </summary>
    static readonly Dictionary<string, string> InStacks = new(StringComparer.Ordinal) {
        ["Copy"] = "Normal",
        ["Constant"] = "Uniform",
        ["Texture"] = "Bitmap",
        ["Bake"] = "Mesh Map",
        ["Beneath"] = "Layers Beneath",
        ["Colour"] = "Color",
        ["Hue"] = "Hue (Tint)",

        // Lanes a mask reads, with the axis of a direction each one holds.
        ["Red"] = "Red (X)",
        ["Green"] = "Green (Y)",
        ["Blue"] = "Blue (Z)"
    };

    /// <summary>What an export calls its padding modes, which say how the background of a layout is filled.</summary>
    static readonly Dictionary<string, string> InExports = new(StringComparer.Ordinal) {
        ["infinite"] = "Dilation, infinite",
        ["transparent"] = "Dilation + transparent",
        ["colour"] = "Dilation + colour",
        ["diffusion"] = "Dilation + diffusion"
    };

    /// <summary>
    ///     What a brush calls the words of its settings and of a symmetry, where a capital is not enough.
    /// </summary>
    static readonly Dictionary<string, string> InBrushes = new(StringComparer.Ordinal) {
        ["along"] = "Follow Stroke",
        ["both"] = "Both Axes",
        ["across"] = "Across the Stroke",
        ["texture"] = "Texture Texels",
        ["model"] = "Model Size"
    };

    /// <summary>What the material shelf calls the types of what it lists, and where each is from.</summary>
    static readonly Dictionary<string, string> InShelves = new(StringComparer.Ordinal) {
        ["material"] = "Materials",
        ["mask"] = "Masks",
        ["filter"] = "Filters",
        ["brush"] = "Brushes",
        ["empty"] = "Empty Graphs",
        ["shipped"] = "Starter",
        ["project"] = "This project",
        ["library"] = "Other projects"
    };

    /// <summary>What a symmetry's plane or axis passes through.</summary>
    static readonly Dictionary<string, string> InCentres = new(StringComparer.Ordinal) {
        ["model"] = "Model Centre", ["origin"] = "Mesh Origin"
    };

    /// <summary>
    ///     What a person calls a value: its own name if it has one, or the value split into capitalised words.
    /// </summary>
    /// <param name="value">
    ///     A value as a file or a command holds it, or a library path, whose last part is its name.
    /// </param>
    public static string Label(string value) {
        if (Named.TryGetValue(value, out var named)) {
            return named;
        }

        var name = value[(value.LastIndexOf('/') + 1)..];
        StringBuilder words = new(name.Length + 4);
        for (var at = 0; at < name.Length; at++) {
            var letter = name[at];
            if (at > 0 && char.IsUpper(letter) && char.IsLower(name[at - 1])) {
                words.Append(' ');
            }

            words.Append(at == 0 || name[at - 1] == ' ' ? char.ToUpperInvariant(letter) : letter);
        }

        return words.ToString();
    }

    /// <summary>Values of a layer stack's fields with their labels.</summary>
    public static IReadOnlyList<StackTerm> InStack(IEnumerable<string> values) => [
        ..values.Select(static value => new StackTerm(
                value,
                InStacks.TryGetValue(value, out var named) ? named : Label(value)
            )
        )
    ];

    /// <summary>Values of a brush's settings with their labels, or of what a symmetry passes through.</summary>
    public static IReadOnlyList<StackTerm> InBrush(IEnumerable<string> values, bool centres = false) {
        var names = centres ? InCentres : InBrushes;
        return [
            ..values.Select(value => new StackTerm(
                    value,
                    names.TryGetValue(value, out var named) ? named : Label(value)
                )
            )
        ];
    }

    /// <summary>Values of an export's settings with their labels.</summary>
    public static IReadOnlyList<StackTerm> InExport(IEnumerable<string> values) => [
        ..values.Select(static value => new StackTerm(
                value,
                InExports.TryGetValue(value, out var named) ? named : Label(value)
            )
        )
    ];

    /// <summary>What the material shelf calls a type or an origin.</summary>
    public static string InShelf(string value) => InShelves.TryGetValue(value, out var named) ? named : Label(value);

    /// <summary>Types or origins of the material shelf with their labels.</summary>
    public static IReadOnlyList<StackTerm> InShelf(IEnumerable<string> values) => [
        ..values.Select(static value => new StackTerm(value, InShelf(value)))
    ];

    /// <summary>The labels of a setting's accepted values, in their order, or null when it takes any value.</summary>
    public static IReadOnlyList<string>? LabelsOf(IReadOnlyList<string> accepted) =>
        accepted.Count == 0 ? null : [..accepted.Select(Label)];
}
