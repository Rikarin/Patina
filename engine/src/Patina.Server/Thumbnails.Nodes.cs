using Patina.Contracts;
using Patina.Ipc;
using Patina.Server.Graphs;
using System.Globalization;
using System.Reflection;
using Vixen.NodeGraph;

namespace Patina.Server;

/// <summary>
///     Pictures of node types, for a palette and a Nodes list (#183, #38): each type drawn once at its defaults, its
///     picture inputs fed one standard noise, through the same Worker path and cache as a material's picture. A type
///     is named by its path, the Vixen it was drawn by, and for a compound the files it is made of, so a picture is
///     drawn again only when what decides it changes.
/// </summary>
public sealed partial class Thumbnails {
    /// <summary>The side of a node's picture, which is the size the node is computed at for it.</summary>
    public const int NodeSide = 64;

    /// <summary>What a node's picture is authored at, as a new graph is, so a radius means the same.</summary>
    const string NodeBase = "1024";

    /// <summary>Goes up when a node's picture would come out differently for the same node.</summary>
    const string NodeVersion = "node-thumbnail-1";

    /// <summary>What feeds every picture input of a node being drawn.</summary>
    const string StandardInput = "Source/Noise";

    const int StandardNode = 1;
    const int DrawnNode = 2;

    /// <summary>The Vixen and Patina the pictures were drawn by: a new kernel is a new picture.</summary>
    static readonly string Builds =
        $"{Informational(typeof(NodeGraphAsset).Assembly)}|{Informational(typeof(Thumbnails).Assembly)}";

    /// <summary>The pictures of some node types a graph can use, drawing those the cache lacks.</summary>
    /// <param name="session">The graph, whose compounds and pictures a type may be made of.</param>
    /// <param name="types">The types, by path.</param>
    /// <param name="cancellationToken">Stops it between pictures.</param>
    /// <returns>Each type with its picture's hash, or none for a type with no picture output or none at all.</returns>
    /// <exception cref="TimeoutException">No Worker came to draw what had to be drawn.</exception>
    public async Task<IReadOnlyList<Thumbnail>> DrawNodesAsync(
        GraphSession session,
        IReadOnlyList<string> types,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(types);
        var known = session.NodeTypes.ToDictionary(static type => type.Path, StringComparer.Ordinal);
        List<Thumbnail> drawn = [];
        foreach (var path in types.Distinct(StringComparer.Ordinal)) {
            var ask = known.TryGetValue(path, out var type) ? OfNode(session, type) : null;
            var hash = ask is null ? null : await HashAsync(ask.Value.Key, ask.Value.Ask, cancellationToken);
            drawn.Add(new(path, hash));
        }

        return drawn;
    }

    /// <summary>
    ///     A graph of one node at its defaults, its first picture output drawn small. A type with no picture output,
    ///     such as an Output or a number, has nothing to draw.
    /// </summary>
    (string Key, MaterialThumbnailAsk Ask)? OfNode(GraphSession session, NodeTypeDefinition type) {
        if (type.Ports.FirstOrDefault(static port => port.Direction == PortDirection.Output && IsPicture(port.Kind))
            is not { } output) {
            return null;
        }

        var document = GraphDocuments.Text(Alone(type));
        // A project's own compound is made of the files in its folder, which may hold the compounds it holds.
        var own = session.Folder is { } folder
            && GraphSessions.Compounds(folder).Any(c => !c.ReadOnly && c.Type == type.Path);
        var compound = own ? Stamps(Files(session.Folder!, true)) : [];
        var key = Key(MaterialThumbnailKinds.Graph, $"{Builds}|{type.Path}", document, [NodeVersion, ..compound]);
        var ask = new MaterialThumbnailAsk(
            MaterialThumbnailKinds.Graph,
            session.Path,
            document,
            session.Folder,
            session.ImageRoots,
            DrawnNode,
            output.Name,
            store.Root
        );
        return (key, ask);
    }

    /// <summary>The graph a type is drawn from: the noise, and the type with the noise on each picture input.</summary>
    static NodeGraphAsset Alone(NodeTypeDefinition type) =>
        new() {
            Name = type.Title,
            Settings = new(StringComparer.Ordinal) {
                ["baseWidth"] = NodeBase,
                ["baseHeight"] = NodeBase,
                ["seed"] = "0",
                [TextureSizes.GraphSetting] = NodeSide.ToString(CultureInfo.InvariantCulture)
            },
            Nodes = [new() { Id = StandardNode, Type = StandardInput }, new() { Id = DrawnNode, Type = type.Path }],
            Edges = [
                ..type.Ports.Where(static port => port.Direction == PortDirection.Input && IsPicture(port.Kind))
                    .Select(static port => new GraphEdgeAsset {
                            FromNode = StandardNode, FromPort = "Out", ToNode = DrawnNode, ToPort = port.Name
                        }
                    )
            ]
        };

    static bool IsPicture(PortKind kind) => kind is PortKind.Image or PortKind.Texture;

    static string Informational(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}
