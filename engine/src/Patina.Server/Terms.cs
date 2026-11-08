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
        ["Copy"] = "Normal", ["Constant"] = "Uniform", ["Texture"] = "Bitmap", ["Bake"] = "Mesh Map"
    };

    /// <summary>What an export calls its padding modes, which say how the background of a layout is filled.</summary>
    static readonly Dictionary<string, string> InExports = new(StringComparer.Ordinal) {
        ["infinite"] = "Dilation, infinite",
        ["transparent"] = "Dilation + transparent",
        ["colour"] = "Dilation + colour",
        ["diffusion"] = "Dilation + diffusion"
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

    /// <summary>Values of an export's settings with their labels.</summary>
    public static IReadOnlyList<StackTerm> InExport(IEnumerable<string> values) => [
        ..values.Select(static value => new StackTerm(
                value,
                InExports.TryGetValue(value, out var named) ? named : Label(value)
            )
        )
    ];

    /// <summary>The labels of a setting's accepted values, in their order, or null when it takes any value.</summary>
    public static IReadOnlyList<string>? LabelsOf(IReadOnlyList<string> accepted) =>
        accepted.Count == 0 ? null : [..accepted.Select(Label)];
}
