using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Graphs;

/// <summary>A folder's compounds, published as node types.</summary>
/// <param name="Registry">Every node type, the compounds included.</param>
/// <param name="Source">What a compiler flattens a compound from.</param>
public sealed record PublishedCompounds(NodeTypeRegistry Registry, ISubGraphSource Source);

/// <summary>
///     The node types of each compound folder. Publishing parses every shipped compound, and every structural edit
///     loads a graph again, so a folder is published once and again only when its files change. It is all derived.
/// </summary>
public sealed class CompoundShelf {
    const string Noise = "Source/Noise";
    const string NoiseOutput = "Out";

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
    ///     here does not exist, so they go. A number they fed takes its default. An image has none, and a compound
    ///     whose nodes will not compile is one its author cannot see, so a noise stands in for every image input.
    /// </summary>
    public static NodeGraphAsset WithoutBoundaries(NodeGraphAsset document) {
        HashSet<int> boundary = [
            ..document.Nodes.Where(static node => SubGraphs.IsBoundary(node.Type)).Select(static node => node.Id)
        ];
        if (boundary.Count == 0) {
            return document;
        }

        HashSet<string> images = [
            ..document.Interface
                .Where(static port => port is { Direction: PortDirection.Input, Kind: PortKind.Image })
                .Select(static port => port.Name)
        ];
        var standIn = document.Nodes.Max(static node => node.Id) + 1;
        GraphEdgeAsset[] fed = [
            ..document.Edges.Where(edge => boundary.Contains(edge.FromNode)
                    && !boundary.Contains(edge.ToNode)
                    && images.Contains(edge.FromPort)
                )
                .Select(edge => edge with { FromNode = standIn, FromPort = NoiseOutput })
        ];
        return document with {
            Nodes = [
                ..document.Nodes.Where(node => !boundary.Contains(node.Id)),
                ..fed.Length > 0 ? [new GraphNodeAsset { Id = standIn, Type = Noise }] : Array.Empty<GraphNodeAsset>()
            ],
            Edges = [
                ..document.Edges.Where(edge => !boundary.Contains(edge.FromNode) && !boundary.Contains(edge.ToNode)),
                ..fed
            ]
        };
    }

    static string Stamp(string? folder) {
        if (folder is null || !Directory.Exists(folder)) {
            return string.Empty;
        }

        return string.Join(
            '|',
            new DirectoryInfo(folder).EnumerateFiles(
                    $"*{TextureCompoundLibrary.Extension}",
                    SearchOption.AllDirectories
                )
                .OrderBy(static file => file.FullName, StringComparer.Ordinal)
                .Select(static file => $"{file.FullName}:{file.Length}:{file.LastWriteTimeUtc.Ticks}")
        );
    }
}
