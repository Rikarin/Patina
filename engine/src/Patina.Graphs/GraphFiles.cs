using Patina.Library;
using Vixen.NodeGraph;
using Vixen.Texturing.Painting;

namespace Patina.Graphs;

/// <summary>
///     The settings of a graph's nodes that name a file: a Bitmap's picture, a Text's font and a Vector's SVG, each
///     found beside the graph and then in the library (ADR 0007), so publishing and using a material move them alike.
/// </summary>
public static class GraphFiles {
    /// <summary>The node that reads a picture.</summary>
    public const string BitmapNode = "Source/Bitmap";

    /// <summary>The node that writes text in a font.</summary>
    public const string TextNode = "Source/Text";

    /// <summary>The node that draws an SVG file.</summary>
    public const string VectorNode = "Source/Vector";

    const string MeshMapScheme = "meshmap:";

    /// <summary>A package dependency's kind for a picture.</summary>
    public const string Picture = "picture";

    /// <summary>A package dependency's kind for a font or an SVG file.</summary>
    public const string Drawing = "drawing";

    /// <summary>The setting of a node type that names a file, or null for a type that names none.</summary>
    public static string? SettingOf(string type) =>
        type switch {
            BitmapNode => "Source",
            TextNode => "Font",
            VectorNode => "File",
            _ => null
        };

    /// <summary>What a package calls the file a node of this type reads.</summary>
    public static string KindOf(string type) => type == BitmapNode ? Picture : Drawing;

    /// <summary>The file a node names, trimmed, or empty when it names none.</summary>
    public static string Of(GraphNodeAsset node) =>
        SettingOf(node.Type) is { } setting ? node.Texts.GetValueOrDefault(setting, string.Empty).Trim() : string.Empty;

    /// <summary>A node naming another file in place of the one it names, or the node when it names none.</summary>
    public static GraphNodeAsset Renamed(GraphNodeAsset node, Func<string, string?> rename) {
        if (SettingOf(node.Type) is not { } setting
            || Of(node) is not { Length: > 0 } file
            || rename(file) is not { } renamed) {
            return node;
        }

        return node with { Texts = new(node.Texts, StringComparer.Ordinal) { [setting] = renamed } };
    }

    /// <summary>Whether a Bitmap names a mesh map or a layer's paint, which is no file found beside a graph.</summary>
    public static bool IsScheme(string named) =>
        named.StartsWith(MeshMapScheme, StringComparison.Ordinal) || PaintReference.Claims(named);

    /// <summary>
    ///     A graph's files named from the library, as found from where <paramref name="file" /> is: a graph's files are
    ///     found beside the graph holding it, and a compound or a stack using it is not beside it.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="file">Where it is.</param>
    /// <param name="library">The library's folder.</param>
    public static NodeGraphAsset Rooted(NodeGraphAsset graph, string file, string library) {
        var folder = Path.GetDirectoryName(Path.GetFullPath(file))!;
        return graph with {
            Nodes = [
                ..graph.Nodes.Select(node => Renamed(
                        node,
                        named => Path.IsPathRooted(named) || IsScheme(named)
                            ? null
                            : LibraryLayout.Relative(library, Path.GetFullPath(named, folder))
                    )
                )
            ]
        };
    }
}
