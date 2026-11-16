using Patina.Content;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using System.Globalization;
using System.Reflection;
using Vixen.NodeGraph;
using Vixen.Texturing.Layers;

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
    public Task<IReadOnlyList<Thumbnail>> DrawNodesAsync(
        GraphSession session,
        IReadOnlyList<string> types,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(session);
        return DrawNodesAsync(
            new NodePictureScope(session.Path, session.Folder, session.ImageRoots, session.NodeTypes) {
                MeshMaps = session.MeshMaps
            },
            types,
            cancellationToken
        );
    }

    /// <summary>The pictures of some node types a document can use, drawing those the cache lacks.</summary>
    /// <param name="scope">The document, whose compounds and pictures a type may be made of, and its types.</param>
    /// <param name="types">The types, by path.</param>
    /// <param name="cancellationToken">Stops it between pictures.</param>
    /// <returns>Each type with its picture's hash, or none for a type with no picture output or none at all.</returns>
    /// <exception cref="TimeoutException">No Worker came to draw what had to be drawn.</exception>
    /// <remarks>
    ///     A type is keyed the same whichever document asks, so a layer's generator and the same node in a graph's
    ///     palette are one picture (#290).
    /// </remarks>
    public async Task<IReadOnlyList<Thumbnail>> DrawNodesAsync(
        NodePictureScope scope,
        IReadOnlyList<string> types,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(types);
        var known = scope.Types.ToDictionary(static type => type.Path, StringComparer.Ordinal);
        List<Thumbnail> drawn = [];
        foreach (var path in types.Distinct(StringComparer.Ordinal)) {
            var ask = known.TryGetValue(path, out var type) ? OfNode(scope, type) : null;
            var hash = ask is null ? null : await HashAsync(ask.Value.Key, ask.Value.Ask, cancellationToken);
            drawn.Add(new(path, hash));
        }

        return drawn;
    }

    /// <summary>
    ///     A shipped compound on the shelf (#298), drawn as a palette draws its node, at the shelf's size: a mask as it
    ///     makes itself, a filter over the standard noise. Nothing of a project's is part of it, so one picture serves
    ///     every library on this build; a generator reads the sample knot's bake (#301), so it shows what it makes on
    ///     a model.
    /// </summary>
    (string Key, MaterialThumbnailAsk Ask)? OfCompound(OpenLibrary library, string node) {
        FactoryCompounds.Register();
        if (!LayerStackCompiler.Library(out _).TryGet(node, out var type)) {
            return null;
        }

        var root = library.RootPath;
        var scope = new NodePictureScope(Path.Combine(root, LibraryLayout.ProjectsFolder), null, [root], []) {
            MeshMaps = ShelfBake.In(store.Root)
        };
        return OfNode(scope, type, Side);
    }

    /// <summary>
    ///     A graph of one node at its defaults, its first picture output drawn small. A type with no picture output,
    ///     such as an Output or a number, has nothing to draw.
    /// </summary>
    (string Key, MaterialThumbnailAsk Ask)? OfNode(
        NodePictureScope scope,
        NodeTypeDefinition type,
        int side = NodeSide
    ) {
        if (type.Ports.FirstOrDefault(static port => port.Direction == PortDirection.Output && IsPicture(port.Kind))
            is not { } output) {
            return null;
        }

        var document = GraphDocuments.Text(Alone(type, side));
        // A project's own compound is made of the files in its folder, which may hold the compounds it holds.
        var own = scope.Folder is { } folder
            && GraphSessions.Compounds(folder).Any(c => !c.ReadOnly && c.Type == type.Path);
        var compound = own ? Stamps(Files(scope.Folder!, true)) : [];
        // A generator reads the mesh maps, so the bake it was drawn from is part of what it looks like.
        var bake = scope.MeshMaps is { } maps ? Stamps(Files(maps, false)) : [];
        var key = Key(
            MaterialThumbnailKinds.Graph,
            $"{Builds}|{type.Path}",
            document,
            [NodeVersion, ..compound, ..bake]
        );
        var ask = new MaterialThumbnailAsk(
            MaterialThumbnailKinds.Graph,
            scope.Path,
            document,
            scope.Folder,
            scope.ImageRoots,
            DrawnNode,
            output.Name,
            store.Root,
            scope.MeshMaps
        );
        return (key, ask);
    }

    /// <summary>The graph a type is drawn from: the noise, and the type with the noise on each picture input.</summary>
    static NodeGraphAsset Alone(NodeTypeDefinition type, int side) =>
        new() {
            Name = type.Title,
            Settings = new(StringComparer.Ordinal) {
                ["baseWidth"] = NodeBase,
                ["baseHeight"] = NodeBase,
                ["seed"] = "0",
                [TextureSizes.GraphSetting] = side.ToString(CultureInfo.InvariantCulture)
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

/// <summary>Where node types are drawn for: a graph or a layer stack, and the types it can use.</summary>
/// <param name="Path">Its file, which pictures are found beside.</param>
/// <param name="Folder">The folder its own compounds come from, or null.</param>
/// <param name="ImageRoots">The folders a relative picture is read from, in order.</param>
/// <param name="Types">The node types it can use.</param>
public sealed record NodePictureScope(
    string Path,
    string? Folder,
    IReadOnlyList<string> ImageRoots,
    IReadOnlyList<NodeTypeDefinition> Types
) {
    /// <summary>The folder of baked maps its Mesh Map nodes read, or null for none.</summary>
    public string? MeshMaps { get; init; }
}
