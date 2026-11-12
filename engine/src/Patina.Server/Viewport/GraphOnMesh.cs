using Patina.Contracts;
using Patina.Ipc;
using Patina.Server.Graphs;

namespace Patina.Server.Viewport;

/// <summary>One graph node's picture, shown in place of the material, and its file.</summary>
/// <param name="Shown">Which node and output.</param>
/// <param name="File">The picture at the size the graph computes it.</param>
public sealed record NodePicture(ViewportNodeShown Shown, ViewportMapFile File);

/// <summary>What a graph lays on the mesh: its Output nodes' pictures, or one node's picture alone.</summary>
/// <param name="Maps">The Output nodes' pictures, by slot.</param>
/// <param name="Node">One node's picture in place of the material, or null.</param>
public sealed record GraphOnMesh(ViewportMapSet Maps, NodePicture? Node = null) {
    /// <summary>An Output node's usages the viewport has a slot for.</summary>
    static readonly IReadOnlySet<string> Slots =
        new HashSet<string>(
            ["baseColor", "normal", "roughness", "metalness", "occlusion", "emissive", "opacity"],
            StringComparer.Ordinal
        );

    /// <summary>
    ///     Has the Worker write each Output node's picture at full size, as the graph stands now, or gives null when
    ///     the graph has no Output node the viewport can show.
    /// </summary>
    /// <exception cref="IpcException">The Worker holds no such picture.</exception>
    /// <exception cref="TimeoutException">No Worker came, or it did not answer.</exception>
    public static async Task<GraphOnMesh?> OutputsAsync(
        GraphSession graph,
        GraphEvaluator evaluator,
        string root,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(evaluator);
        var outputs = graph.Outputs(Slots);
        if (outputs.Count == 0) {
            return null;
        }

        Dictionary<string, ViewportMapFile> files = new(StringComparer.Ordinal);
        foreach (var (node, usage) in outputs) {
            var image = await evaluator.ImageAsync(graph, node, usage, cancellationToken);
            files[usage] = new(GraphPreviewFiles.PathOf(root, image.Hash), image.Hash);
        }

        return new GraphOnMesh(
            new ViewportMapSet(
                files.GetValueOrDefault("baseColor"),
                files.GetValueOrDefault("normal"),
                files.GetValueOrDefault("roughness"),
                files.GetValueOrDefault("metalness"),
                files.GetValueOrDefault("occlusion"),
                files.GetValueOrDefault("emissive"),
                files.GetValueOrDefault("opacity")
            ) {
                // A graph's Height to Normal points green down, and the shader reads green up (ADR 0011).
                GreenDown = true
            }
        );
    }

    /// <summary>
    ///     The output a node's picture comes from: the one asked for, or else the one its preview shows, which a client
    ///     need not know. Null when the node has no picture yet.
    /// </summary>
    public static string? PortOf(GraphSession graph, int node, string? port) {
        ArgumentNullException.ThrowIfNull(graph);
        return port ?? graph.State.Previews.FirstOrDefault(preview => preview.Node == node)?.Port;
    }

    /// <summary>Has the Worker write one node's picture at full size, to show alone.</summary>
    /// <exception cref="IpcException">The Worker holds no such picture.</exception>
    /// <exception cref="TimeoutException">No Worker came, or it did not answer.</exception>
    public static async Task<GraphOnMesh> NodeAsync(
        GraphSession graph,
        int node,
        string port,
        GraphEvaluator evaluator,
        string root,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(evaluator);
        var image = await evaluator.ImageAsync(graph, node, port, cancellationToken);
        return new(new(), new(new(graph.Id, node, port), new(GraphPreviewFiles.PathOf(root, image.Hash), image.Hash)));
    }
}
