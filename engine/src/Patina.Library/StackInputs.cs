using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Patina.Library;

/// <summary>
///     What a stack reads besides itself, which a build has to depend on: an output declares the stack, and nothing can
///     declare the paint files strokes go on to write. The Library knows no layers, so it only looks for the two keys.
/// </summary>
/// <param name="Text">The stack file's text, or null when it cannot be read.</param>
/// <param name="Revision">The revision of a file beside it, or null when it does not exist, which is allowed.</param>
public sealed record StackFiles(Func<string, string?> Text, Func<string, Revision?> Revision);

/// <summary>Reads a stack file for the files it names.</summary>
public static class StackInputs {
    public const string Extension = ".vxlayers";
    const string PaintKey = "paint";
    const string ModelKey = "model";

    /// <summary>The paint files, the mesh and the bake a stack reads, as paths beside it, in a fixed order.</summary>
    /// <param name="stack">The stack's path.</param>
    /// <param name="text">Its text.</param>
    public static IReadOnlyList<string> Of(string stack, string text) {
        SortedSet<string> found = new(StringComparer.Ordinal) { GraphMeshMaps.FileOf(stack) };
        YamlStream yaml = [];
        try {
            using StringReader reader = new(text);
            yaml.Load(reader);
        } catch (YamlException) {
            return [..found];
        }

        var folder = Path.GetDirectoryName(stack) ?? string.Empty;
        foreach (var document in yaml.Documents) {
            Walk(document.RootNode, name => found.Add(Path.Combine(folder, name).Replace('\\', '/')));
        }

        return [..found];
    }

    static void Walk(YamlNode node, Action<string> found) {
        switch (node) {
            case YamlMappingNode mapping:
                foreach (var (key, value) in mapping.Children) {
                    if (key is YamlScalarNode { Value: PaintKey or ModelKey }
                        && value is YamlScalarNode { Value: { Length: > 0 } name }) {
                        found(name);
                    } else {
                        Walk(value, found);
                    }
                }

                break;
            case YamlSequenceNode sequence:
                foreach (var item in sequence.Children) {
                    Walk(item, found);
                }

                break;
        }
    }
}
