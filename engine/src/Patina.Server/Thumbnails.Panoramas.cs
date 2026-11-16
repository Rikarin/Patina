using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using System.Globalization;
using Vixen.NodeGraph;

namespace Patina.Server;

/// <summary>A graph's picture drawn as a panorama that lights the viewport (#180), and where it was drawn.</summary>
public sealed partial class Thumbnails {
    /// <summary>
    ///     The longer side a graph is computed at to light the viewport: twice the width the viewport reads a
    ///     panorama at, so its own averaging still has texels to average.
    /// </summary>
    public const int PanoramaSide = 1024;

    const string PanoramaKind = "panorama";

    /// <summary>
    ///     A graph file's Output as a panorama with its numbers past one, drawn into the preview cache unless the
    ///     cache already has it for these files. Its base colour is preferred, as for its picture on the shelf; it is
    ///     computed at <see cref="PanoramaSide" /> along its longer side, which changes how many texels it has and
    ///     not what it shows.
    /// </summary>
    /// <param name="file">The graph's whole path.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The panorama, or null when the graph has no Output node to draw or draws nothing.</returns>
    /// <exception cref="TimeoutException">No Worker came to draw it.</exception>
    /// <exception cref="IpcException">The Worker could not draw it, and says why.</exception>
    public async Task<ViewportMapFile?> PanoramaAsync(string file, CancellationToken cancellationToken) =>
        await PanoramaAsync(
            file,
            GraphDocuments.Parse(await SharedFile.ReadAllTextAsync(file, cancellationToken)),
            cancellationToken
        );

    /// <summary>
    ///     A graph's Output as a panorama, as <see cref="PanoramaAsync(string, CancellationToken)" /> draws it, from
    ///     a document as it is being edited rather than as saved (#300). Its files and pictures are still found
    ///     beside the graph's file.
    /// </summary>
    /// <param name="file">The graph's whole path.</param>
    /// <param name="graph">The document to draw.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The panorama, or null when the graph has no Output node to draw or draws nothing.</returns>
    /// <exception cref="TimeoutException">No Worker came to draw it.</exception>
    /// <exception cref="IpcException">The Worker could not draw it, and says why.</exception>
    public async Task<ViewportMapFile?> PanoramaAsync(
        string file,
        NodeGraphAsset graph,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(graph);
        if (Output(graph) is not var (node, usage)) {
            return null;
        }

        var sized = graph with {
            Settings = new(graph.Settings, StringComparer.Ordinal) {
                [TextureSizes.GraphSetting] = PanoramaSide.ToString(CultureInfo.InvariantCulture)
            }
        };
        var document = GraphDocuments.Text(sized);
        var folder = CompoundFolders.Of(file);
        var roots = CompoundFolders.PictureRoots(file);
        var pictures = FilesRead(graph, roots);
        var key = Key(PanoramaKind, file, document, [..Stamps(Files(folder, true)), ..Stamps(pictures)]);
        var ask = new MaterialThumbnailAsk(
            MaterialThumbnailKinds.Graph,
            file,
            document,
            folder,
            roots,
            node,
            usage,
            store.Root
        ) { Deep = true };
        return await HashAsync(key, ask, cancellationToken) is { } hash
            ? new(GraphPreviewFiles.PathOf(store.Root, hash, GraphPreviewFiles.Radiance), hash)
            : null;
    }

    /// <summary>A graph's Output node and its usage, its base colour's if it has one, or null for none.</summary>
    static (int Node, string Usage)? Output(NodeGraphAsset graph) {
        var outputs = graph.Nodes.Where(static node => node.Type == OutputNode)
            .Select(static node => (node.Id, Usage: node.Texts.GetValueOrDefault(UsageSetting, DefaultUsage)))
            .ToList();
        return outputs.Count == 0
            ? null
            : outputs.Find(static output => output.Usage == DefaultUsage) is { Usage: not null } chosen
                ? chosen
                : outputs[0];
    }
}
