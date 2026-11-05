using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Worker;

/// <summary>A folder's compounds, published as node types.</summary>
/// <param name="Registry">Every node type, the compounds included.</param>
/// <param name="Source">What a compiler flattens a compound from.</param>
sealed record PublishedCompounds(NodeTypeRegistry Registry, ISubGraphSource Source);

/// <summary>
///     The node types of each compound folder. Publishing parses every shipped compound, and every structural edit
///     loads a graph again, so a folder is published once and again only when its files change. It is all derived.
/// </summary>
sealed class CompoundShelf {
    readonly Dictionary<string, (string Stamp, PublishedCompounds Published)> folders = new(StringComparer.Ordinal);

    public PublishedCompounds Of(string? folder) {
        var key = folder ?? string.Empty;
        var stamp = Stamp(folder);
        if (folders.TryGetValue(key, out var held) && string.Equals(held.Stamp, stamp, StringComparison.Ordinal)) {
            return held.Published;
        }

        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        PublishedCompounds published = new(registry, TextureCompoundLibrary.Publish(registry, folder, out _));
        folders[key] = (stamp, published);
        return published;
    }

    /// <summary>
    ///     A compound's document as a graph in its own right. Its boundary nodes stand for a graph that holds it, which
    ///     here does not exist, so they go, and the inputs they fed take their defaults.
    /// </summary>
    public static NodeGraphAsset WithoutBoundaries(NodeGraphAsset document) {
        HashSet<int> boundary = [
            ..document.Nodes.Where(static node => SubGraphs.IsBoundary(node.Type)).Select(static node => node.Id)
        ];
        return boundary.Count == 0
            ? document
            : document with {
                Nodes = [..document.Nodes.Where(node => !boundary.Contains(node.Id))],
                Edges = [
                    ..document.Edges.Where(edge => !boundary.Contains(edge.FromNode) && !boundary.Contains(edge.ToNode))
                ]
            };
    }

    static string Stamp(string? folder) {
        if (folder is null || !Directory.Exists(folder)) {
            return string.Empty;
        }

        return string.Join(
            '|',
            new DirectoryInfo(folder).EnumerateFiles($"*{TextureCompoundLibrary.Extension}", SearchOption.AllDirectories)
                .OrderBy(static file => file.FullName, StringComparer.Ordinal)
                .Select(static file => $"{file.FullName}:{file.Length}:{file.LastWriteTimeUtc.Ticks}")
        );
    }
}
