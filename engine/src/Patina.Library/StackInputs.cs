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
    const string PaintExtension = ".vxpaint";
    const string PaintKey = "paint";
    const string ModelKey = "model";

    /// <summary>A stack's Width and Height when its file leaves them out, as Vixen reads it.</summary>
    const int DefaultSide = 1024;

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

    /// <summary>The paint files a stack names, as paths beside it.</summary>
    public static IReadOnlyList<string> PaintsOf(string stack, string text) =>
        stack.EndsWith(Extension, StringComparison.Ordinal)
            ? [..Of(stack, text).Where(static name => name.EndsWith(PaintExtension, StringComparison.Ordinal))]
            : [];

    /// <summary>Refuses a stack whose paint would be read from outside its folder.</summary>
    /// <exception cref="InvalidDataException">One is.</exception>
    public static void CheckPaint(string stack, string text) {
        // By the file's name alone, or every name comes back rooted, and a rooted name is what this refuses.
        foreach (var input in Of(Path.GetFileName(stack), text)
                     .Where(static name => name.EndsWith(PaintExtension, StringComparison.Ordinal))) {
            LibraryLayout.Beside(Path.GetDirectoryName(stack) ?? string.Empty, input);
        }
    }

    /// <summary>
    ///     What a stack's texture set reads its bake by: the UV set the set named <paramref name="set" />, or its
    ///     first, is laid out in, and the stack's Width and Height (#262). The first UV set when the file does not say,
    ///     and no shape and the first when it cannot be read.
    /// </summary>
    public static BakeFit.SetLayout LayoutOf(string text, string? set) {
        YamlStream yaml = [];
        try {
            using StringReader reader = new(text);
            yaml.Load(reader);
        } catch (YamlException) {
            return new(0);
        }

        if (yaml.Documents.FirstOrDefault()?.RootNode is not YamlMappingNode root) {
            return new(0);
        }

        var width = Whole(root, "baseWidth", DefaultSide);
        var height = Whole(root, "baseHeight", DefaultSide);
        if (!root.Children.TryGetValue(new YamlScalarNode("sets"), out var sets) || sets is not YamlSequenceNode list) {
            return new(0, width, height);
        }

        var chosen = list.Children.OfType<YamlMappingNode>()
                .FirstOrDefault(entry => set is null || Scalar(entry, "name") == set)
            ?? list.Children.OfType<YamlMappingNode>().FirstOrDefault();
        return new(chosen is null ? 0 : Whole(chosen, "uvSet", 0), width, height);
    }

    static int Whole(YamlMappingNode mapping, string key, int otherwise) =>
        int.TryParse(Scalar(mapping, key), out var value) ? value : otherwise;

    static string? Scalar(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar
            ? scalar.Value
            : null;

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
