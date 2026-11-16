using Patina.Library;
using Vixen.NodeGraph;
using Vixen.TextureGraph;
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

    /// <summary>How a Bitmap names a map of the bake its graph reads, which is no file found beside it.</summary>
    public const string MeshMapScheme = "meshmap:";

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
            || TextureGraphParameters.IsReference(file, out _)
            || rename(file) is not { } renamed) {
            return node;
        }

        return node with { Texts = new(node.Texts, StringComparer.Ordinal) { [setting] = renamed } };
    }

    /// <summary>
    ///     The files a graph's nodes read, each with the type of the node reading it: the file a node names, or, for a
    ///     node naming one of the graph's parameters instead (<c>$Font</c>), every file that parameter can hold, which
    ///     is its default, its list and its presets' values, since whichever is chosen must be found (#306).
    /// </summary>
    public static IEnumerable<(string File, string Type)> Read(NodeGraphAsset graph) {
        foreach (var node in graph.Nodes) {
            if (Of(node) is not { Length: > 0 } named) {
                continue;
            }

            if (!TextureGraphParameters.IsReference(named, out var knob)) {
                yield return (named, node.Type);
                continue;
            }

            foreach (var file in Choices(graph, knob)) {
                yield return (file, node.Type);
            }
        }
    }

    /// <summary>
    ///     A graph naming other files in place of those it names: in its nodes, and in the parameters its nodes read a
    ///     file through, with their presets.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="rename">The file a file becomes, or null to keep it.</param>
    public static NodeGraphAsset Renamed(NodeGraphAsset graph, Func<string, string?> rename) {
        HashSet<string> knobs = new(StringComparer.Ordinal);
        foreach (var node in graph.Nodes) {
            if (TextureGraphParameters.IsReference(Of(node), out var knob)) {
                knobs.Add(knob);
            }
        }

        return graph with {
            Nodes = [..graph.Nodes.Select(node => Renamed(node, rename))],
            Parameters = [
                ..graph.Parameters.Select(parameter => knobs.Contains(parameter.Name)
                        ? parameter with {
                            Default = KeptOrRenamed(parameter.Default, rename),
                            Accepted = [..parameter.Accepted.Select(text => KeptOrRenamed(text, rename))]
                        }
                        : parameter
                )
            ],
            Presets = [
                ..graph.Presets.Select(preset => preset with {
                        Values = preset.Values.ToDictionary(
                            static value => value.Key,
                            value => knobs.Contains(value.Key) ? KeptOrRenamed(value.Value, rename) : value.Value,
                            StringComparer.Ordinal
                        )
                    }
                )
            ]
        };
    }

    /// <summary>A parameter's text naming a file renamed, or as it was.</summary>
    static string KeptOrRenamed(string text, Func<string, string?> rename) =>
        text.Trim() is { Length: > 0 } file && rename(file) is { } renamed ? renamed : text;

    /// <summary>Every file a parameter of the graph can hold, each once.</summary>
    static IEnumerable<string> Choices(NodeGraphAsset graph, string knob) {
        if (graph.Parameters.FirstOrDefault(parameter => parameter.Name == knob) is not { } declared) {
            return [];
        }

        return new[] { declared.Default }.Concat(declared.Accepted)
            .Concat(graph.Presets.Select(preset => preset.Values.GetValueOrDefault(knob, string.Empty)))
            .Select(static file => file.Trim())
            .Where(static file => file.Length > 0)
            .Distinct(StringComparer.Ordinal);
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

        string? Rooted(string named) =>
            Path.IsPathRooted(named) || IsScheme(named)
                ? null
                : LibraryLayout.Relative(library, Path.GetFullPath(named, folder));

        var renamed = Renamed(graph, Rooted);

        // A compound's own parameter may be handed a file by the node standing for it, as a package's graph hands its
        // compounds the files it carries: a text naming a file beside the graph is one.
        return renamed with {
            Nodes = [
                ..renamed.Nodes.Select(node => SettingOf(node.Type) is not null
                        ? node
                        : node with {
                            Texts = node.Texts.ToDictionary(
                                static text => text.Key,
                                text => Beside(folder, text.Value) && Rooted(text.Value.Trim()) is { } rooted
                                    ? rooted
                                    : text.Value,
                                StringComparer.Ordinal
                            )
                        }
                )
            ]
        };
    }

    /// <summary>Whether a text names a file beside a graph, as a package's own files are.</summary>
    static bool Beside(string folder, string text) {
        var named = text.Trim();
        return named.Length > 0
            && !Path.IsPathRooted(named)
            && !IsScheme(named)
            && !TextureGraphParameters.IsReference(named, out _)
            && named.IndexOfAny(Path.GetInvalidPathChars()) < 0
            && File.Exists(Path.GetFullPath(named, folder));
    }
}
