using Patina.Contracts;
using Patina.Library;
using Patina.Server.Stacks;
using Vixen.NodeGraph;
using Vixen.Texturing.Layers;

namespace Patina.Server.Graphs;

/// <summary>
///     What a layer can be filled with (ADR 0012). Everything here makes a material, and nothing that uses one looks
///     behind this: a graph is published as a compound, whose outputs are its maps by usage, and a layered material
///     or a shipped one unfolds into the stack as layers.
/// </summary>
public static class MaterialShelf {
    public const string ShippedPrefix = "smart:";
    const string OutputNode = "Output/Output";
    const string BoundaryOutput = "Sub-graph/Output";
    const string UsageSetting = "Usage";
    const string DefaultUsage = "baseColor";
    const string MaterialsFolder = "Materials";
    const string SourceSuffix = ".source";
    const int SharedLimit = 200;

    /// <summary>The materials a project can use: the shipped ones, its own, and the library's shared graphs.</summary>
    public static IReadOnlyList<MaterialInfo> List(OpenLibrary library, string? project) {
        List<MaterialInfo> found = [
            ..SmartMaterial.Shipped.Select(static name => new MaterialInfo(
                    name,
                    DocumentKinds.LayeredMaterial,
                    ShippedPrefix + name,
                    "material",
                    "shipped",
                    []
                )
            )
        ];
        if (project is not null) {
            found.AddRange(
                library.Contents(project)
                    .Documents.Select(document => Described(library, document))
                    .OfType<MaterialInfo>()
            );
        }

        foreach (var asset in library.Assets(new(Limit: SharedLimit))
                     .Items.Where(static a => a.Kind == DocumentKinds.Graph)) {
            var path = $"{LibraryLayout.SharedFolder}/{asset.Path}";
            found.Add(FromGraph(library, asset.Name, path, "shared"));
        }

        return found;
    }

    /// <summary>
    ///     Publishes a material graph into a project, as the compound a graph fill reads, so that a saved edit reaches
    ///     every layer that uses it. Republishing replaces it.
    /// </summary>
    /// <returns>The compound's node type.</returns>
    /// <exception cref="StackEditRefusedException">The graph has no output a layer could use.</exception>
    public static string Publish(OpenLibrary library, string project, string source) {
        var full = Path.GetFullPath(source, library.RootPath);
        var root = Path.GetFullPath(library.RootPath) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.Ordinal) || !File.Exists(full)) {
            throw new FileNotFoundException($"There is no graph '{source}' in this library.", source);
        }

        var graph = GraphDocuments.Parse(File.ReadAllText(full));
        var name = Path.GetFileNameWithoutExtension(full);
        var file = library.ProjectFile(
            project,
            $"{LibraryLayout.CompoundsFolder}/{MaterialsFolder}/{name}{LibraryLayout.GraphExtension}"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        LibraryFiles.Replace(file, GraphDocuments.Text(AsCompound(graph)));
        LibraryFiles.Replace(file + SourceSuffix, source);
        return $"{MaterialsFolder}/{name}";
    }

    /// <summary>Publishes again every graph a project has published, for edits saved since.</summary>
    public static void Refresh(OpenLibrary library, string project) {
        var folder = library.ProjectFile(project, $"{LibraryLayout.CompoundsFolder}/{MaterialsFolder}");
        if (!Directory.Exists(folder)) {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*" + SourceSuffix)) {
            var source = File.ReadAllText(file).Trim();
            if (File.Exists(Path.GetFullPath(source, library.RootPath))) {
                Publish(library, project, source);
            }
        }
    }

    /// <summary>The stack a source names, as a material: one set of layers and no mesh.</summary>
    /// <param name="library">The library's folder.</param>
    /// <param name="source">A shipped material after <see cref="ShippedPrefix" />, or a stack in the library.</param>
    /// <exception cref="StackEditRefusedException">There is no such material.</exception>
    public static LayerStackAsset Read(string? library, string source) {
        if (source.StartsWith(ShippedPrefix, StringComparison.Ordinal)) {
            var name = source[ShippedPrefix.Length..];
            var text = SmartMaterial.Source(name)
                ?? throw new StackEditRefusedException($"There is no material '{name}'.");
            return StackDocuments.Parse(text);
        }

        if (library is null) {
            throw new StackEditRefusedException("Only a library has layered materials.");
        }

        var full = Path.GetFullPath(source, library);
        if (!full.StartsWith(Path.GetFullPath(library) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !File.Exists(full)) {
            throw new StackEditRefusedException($"There is no material '{source}' in this library.");
        }

        var stack = StackDocuments.Parse(File.ReadAllText(full));
        return SmartMaterial.Extract(stack, stack.Name).Material
            ?? throw new StackEditRefusedException($"'{stack.Name}' has no layers to use.");
    }

    static MaterialInfo? Described(OpenLibrary library, ProjectDocumentInfo document) =>
        document.Kind switch {
            DocumentKinds.Graph or DocumentKinds.ImageMaterial => FromGraph(
                library,
                document.Name,
                document.Path,
                "project"
            ),
            DocumentKinds.LayeredMaterial => new(
                document.Name,
                document.Kind,
                document.Path,
                "material",
                "project",
                []
            ),
            _ => null
        };

    static MaterialInfo FromGraph(OpenLibrary library, string name, string path, string origin) {
        var usages = Usages(GraphDocuments.Parse(File.ReadAllText(Path.GetFullPath(path, library.RootPath))));
        return new(name, DocumentKinds.Graph, path, usages.Count == 0 ? "empty" : "material", origin, usages);
    }

    static List<string> Usages(NodeGraphAsset graph) => [
        ..graph.Nodes.Where(static node => node.Type == OutputNode)
            .Select(static node => node.Texts.GetValueOrDefault(UsageSetting, DefaultUsage))
            .Distinct(StringComparer.Ordinal)
    ];

    /// <summary>A graph as a compound: every Output becomes a port of one boundary, named for its usage.</summary>
    static NodeGraphAsset AsCompound(NodeGraphAsset graph) {
        var usages = graph.Nodes.Where(static node => node.Type == OutputNode)
            .ToDictionary(
                static node => node.Id,
                static node => node.Texts.GetValueOrDefault(UsageSetting, DefaultUsage)
            );
        if (usages.Count == 0) {
            throw new StackEditRefusedException(
                $"'{graph.Name}' has no Output, so a layer has nothing to read from it."
            );
        }

        var boundary = graph.Nodes.Max(static node => node.Id) + 1;
        return graph with {
            Nodes = [
                ..graph.Nodes.Where(node => !usages.ContainsKey(node.Id)),
                new GraphNodeAsset { Id = boundary, Type = BoundaryOutput }
            ],
            Edges = [
                ..graph.Edges.Select(edge => usages.TryGetValue(edge.ToNode, out var usage)
                        ? edge with { ToNode = boundary, ToPort = usage }
                        : edge
                )
            ],
            Interface = [
                ..graph.Interface,
                ..usages.Values.Distinct(StringComparer.Ordinal)
                    .Select(static usage => new GraphPortAsset {
                            Name = usage, Direction = PortDirection.Output, Kind = PortKind.Image
                        }
                    )
            ]
        };
    }
}
