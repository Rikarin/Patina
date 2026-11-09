using Patina.Contracts;
using Patina.Library;
using Patina.Server.Stacks;
using System.Collections.Concurrent;
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
    const string BitmapNode = "Source/Bitmap";
    const string SourceSetting = "Source";
    const string UsageSetting = "Usage";
    const string DefaultUsage = "baseColor";
    const string MaterialsFolder = "Materials";
    const string SourceSuffix = ".source";
    const int ElsewhereLimit = 300;
    const int ProjectPage = 200;
    const string ElsewhereOrigin = "library";
    const string StarterPrefix = "starter:";
    const string StarterResource = "Starter/";

    /// <summary>What each project lists, kept while its file stands, so a search does not read a library.</summary>
    static readonly ConcurrentDictionary<string, (DateTime Stamp, IReadOnlyList<ProjectDocumentInfo> Documents)>
        Kept =
        new(StringComparer.Ordinal);

    /// <summary>The types a shelf narrows to, and where a material can be from.</summary>
    static readonly string[] Roles = [GraphRoles.Material, GraphRoles.Mask, GraphRoles.Filter, GraphRoles.Brush];

    static readonly string[] Origins = ["shipped", "project", ElsewhereOrigin];

    /// <summary>A list as the API answers it, with the words its types and origins are shown and searched by.</summary>
    public static MaterialList Listed(IReadOnlyList<MaterialInfo> items) =>
        new(items, Terms.InShelf(Roles), Terms.InShelf(Origins));

    /// <summary>
    ///     What a project can use: the shipped materials, its own and any other project's. Nothing
    ///     has to be promoted first (ADR 0012). A library holds thousands of projects, so the last are capped and a
    ///     search is what reaches the rest. A search keeps what <see cref="Matches" /> finds, the same way wherever a
    ///     material is from, so the shelf has one definition of a match (#230).
    /// </summary>
    public static IReadOnlyList<MaterialInfo> List(OpenLibrary library, string? project, string? search = null) {
        var words = search?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
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
        found.AddRange(Starters());
        if (project is not null) {
            found.AddRange(
                library.Contents(project)
                    .Documents.Select(document => Described(library, document))
                    .OfType<MaterialInfo>()
            );
        }

        found.RemoveAll(material => !Matches(material, words));
        found.AddRange(Elsewhere(library, project, words).Take(ElsewhereLimit));
        return found;
    }

    /// <summary>
    ///     Whether a material has every word of a search, ignoring case, in one of what the shelf shows of it: its
    ///     name, its type and what that is called, its kind, where it is from and what that is called, its project or
    ///     its path.
    /// </summary>
    public static bool Matches(MaterialInfo material, IReadOnlyCollection<string> words) =>
        words.Count == 0
        || Has(
            words,
            material.Name,
            material.Role,
            Terms.InShelf(material.Role),
            material.Kind,
            material.Origin,
            Terms.InShelf(material.Origin),
            material.Project ?? string.Empty,
            material.Source
        );

    static bool Has(IEnumerable<string> words, params ReadOnlySpan<string> fields) {
        foreach (var word in words) {
            var found = false;
            foreach (var field in fields) {
                found |= field.Contains(word, StringComparison.OrdinalIgnoreCase);
            }

            if (!found) {
                return false;
            }
        }

        return true;
    }

    /// <summary>The graphs that ship with Patina. They are CC0, and using one puts a copy in the project.</summary>
    static IEnumerable<MaterialInfo> Starters() =>
        typeof(MaterialShelf).Assembly.GetManifestResourceNames()
            .Where(static resource => resource.StartsWith(StarterResource, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(static resource => {
                    var name = resource[StarterResource.Length..];
                    return new MaterialInfo(
                        name,
                        DocumentKinds.Graph,
                        StarterPrefix + name,
                        GraphRoles.Material,
                        "shipped",
                        Usages(GraphDocuments.Parse(StarterText(name)))
                    );
                }
            );

    /// <summary>A starter material's document.</summary>
    /// <exception cref="StackEditRefusedException">No starter has that name.</exception>
    internal static string StarterText(string name) {
        using var stream = typeof(MaterialShelf).Assembly.GetManifestResourceStream(StarterResource + name)
            ?? throw new StackEditRefusedException($"There is no material '{name}'.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    /// <summary>A starter material becomes a document of the project the first time it is used there.</summary>
    static string Bring(OpenLibrary library, string project, string source) {
        var name = source[StarterPrefix.Length..];
        var held = library.Contents(project).Documents.FirstOrDefault(document => document.Name == name);
        return held?.Path ?? library.CreateGraph(new(name, project), StarterText(name));
    }

    /// <summary>
    ///     Whether a document of another project could match before its file is read, which a search of a large library
    ///     cannot afford for every document. It looks at every field <see cref="Matches" /> does, with each type and
    ///     kind the file could turn out to give, so it passes everything that matches and the file decides the rest.
    /// </summary>
    static bool Maybe(ProjectDocumentInfo document, string project, string[] words) {
        var graph = document.Kind is DocumentKinds.Graph or DocumentKinds.ImageMaterial;
        var role = document.Kind == DocumentKinds.LayeredMaterial
            ? GraphRoles.Material
            : document.Role ?? GraphRoles.Material;
        return Has(
            words,
            document.Name,
            document.Path,
            document.Kind,
            graph ? DocumentKinds.Graph : string.Empty,
            role,
            Terms.InShelf(role),
            graph ? "empty" : string.Empty,
            graph ? Terms.InShelf("empty") : string.Empty,
            ElsewhereOrigin,
            Terms.InShelf(ElsewhereOrigin),
            project
        );
    }

    static IEnumerable<MaterialInfo> Elsewhere(OpenLibrary library, string? project, string[] words) {
        for (var offset = 0;; offset += ProjectPage) {
            var page = library.Projects(new(Offset: offset, Limit: ProjectPage)).Items;
            foreach (var summary in page.Where(summary => summary.Path != project)) {
                foreach (var document in DocumentsOf(library, summary.Path)) {
                    if (Maybe(document, summary.Name, words)
                        && Readable(library, document) is { } read
                        && read with { Origin = ElsewhereOrigin, Project = summary.Name } is var material
                        && Matches(material, words)) {
                        yield return material;
                    }
                }
            }

            if (page.Count < ProjectPage) {
                yield break;
            }
        }
    }

    static IReadOnlyList<ProjectDocumentInfo> DocumentsOf(OpenLibrary library, string project) {
        var file = library.ProjectFile(project, LibraryLayout.ProjectFile);
        var stamp = File.GetLastWriteTimeUtc(file);
        if (Kept.TryGetValue(file, out var held) && held.Stamp == stamp) {
            return held.Documents;
        }

        var documents = library.Contents(project).Documents;
        Kept[file] = (stamp, documents);
        return documents;
    }

    /// <summary>One broken file in another project must not empty this project's shelf.</summary>
    static MaterialInfo? Readable(OpenLibrary library, ProjectDocumentInfo document) {
        try {
            return Described(library, document);
        } catch (Exception e) when (e is not OperationCanceledException) {
            return null;
        }
    }

    /// <summary>
    ///     A layered material's graph fills name compounds of the project it was made in, so they come with it, and a
    ///     saved edit keeps reaching them through their <c>.source</c>.
    /// </summary>
    /// <returns>Whether any came.</returns>
    public static bool BringCompounds(string? library, string source, string stack) {
        if (library is null || source.StartsWith(ShippedPrefix, StringComparison.Ordinal)) {
            return false;
        }

        var from = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(source, library))!,
            LibraryLayout.CompoundsFolder,
            MaterialsFolder
        );
        var to = Path.Combine(Path.GetDirectoryName(stack)!, LibraryLayout.CompoundsFolder, MaterialsFolder);
        if (!Directory.Exists(from) || string.Equals(from, to, StringComparison.Ordinal)) {
            return false;
        }

        Directory.CreateDirectory(to);
        var brought = false;
        foreach (var file in Directory.EnumerateFiles(from)) {
            var target = Path.Combine(to, Path.GetFileName(file));
            if (!File.Exists(target)) {
                File.Copy(file, target);
                brought = true;
            }
        }

        return brought;
    }

    /// <summary>Stacks open in a project read their compounds again, since one has just been published there.</summary>
    public static void Follow(OpenLibrary library, string project, StackSessions stacks) {
        var folder = Path.GetDirectoryName(library.ProjectFile(project, LibraryLayout.ProjectFile));
        foreach (var stack in stacks.All.Where(stack => stack.Folder == folder)) {
            stack.CompoundsChanged();
        }
    }

    /// <summary>
    ///     Publishes a material graph into a project, as the compound a graph fill reads, so that a saved edit reaches
    ///     every layer that uses it. Republishing replaces it.
    /// </summary>
    /// <returns>The compound's node type.</returns>
    /// <exception cref="StackEditRefusedException">The graph has no output a layer could use.</exception>
    public static string Publish(OpenLibrary library, string project, string source) {
        source = source.StartsWith(StarterPrefix, StringComparison.Ordinal) ? Bring(library, project, source) : source;
        var full = Path.GetFullPath(source, library.RootPath);
        var root = Path.GetFullPath(library.RootPath) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.Ordinal) || !File.Exists(full)) {
            throw new FileNotFoundException($"There is no graph '{source}' in this library.", source);
        }

        var graph = GraphDocuments.Parse(SharedFile.ReadAllText(full));
        var name = Path.GetFileNameWithoutExtension(full);
        var file = CompoundFile(library, project, name);

        // Two projects can each have a Rust, and a material from elsewhere must not replace this project's.
        for (var other = 2; Taken(file, source); other++) {
            name = $"{Path.GetFileNameWithoutExtension(full)}-{other}";
            file = CompoundFile(library, project, name);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        Reference(library, project, source);
        var text = GraphDocuments.Text(AsCompound(Rooted(graph, full, library.RootPath)));
        var path = $"{MaterialsFolder}/{name}";
        if (File.Exists(file) && string.Equals(SharedFile.ReadAllText(file), text, StringComparison.Ordinal)) {
            return path;
        }

        LibraryFiles.Replace(file, text);
        LibraryFiles.Replace(file + SourceSuffix, source);
        return path;
    }

    /// <summary>Using another project's document is a reference, so its impact preview names this one.</summary>
    static void Reference(OpenLibrary library, string project, string source) {
        var below = $"{LibraryLayout.ProjectsFolder}/";
        if (!source.StartsWith(below, StringComparison.Ordinal)) {
            return;
        }

        var owner = Path.GetDirectoryName(source[below.Length..])!.Replace('\\', '/');
        if (owner != project
            && library.Contents(owner).Documents.FirstOrDefault(d => d.Path == source) is { Id: { } id }) {
            library.AddReference(project, new(id));
        }
    }

    static string CompoundFile(OpenLibrary library, string project, string name) =>
        library.ProjectFile(
            project,
            $"{LibraryLayout.CompoundsFolder}/{MaterialsFolder}/{name}{LibraryLayout.GraphExtension}"
        );

    static bool Taken(string file, string source) =>
        File.Exists(file + SourceSuffix)
        && !string.Equals(SharedFile.ReadAllText(file + SourceSuffix).Trim(), source, StringComparison.Ordinal);

    /// <summary>Publishes again every graph a project has published, for edits saved since.</summary>
    /// <returns>Whether any of them changed.</returns>
    public static bool Refresh(OpenLibrary library, string project) {
        var folder = library.ProjectFile(project, $"{LibraryLayout.CompoundsFolder}/{MaterialsFolder}");
        if (!Directory.Exists(folder)) {
            return false;
        }

        var changed = false;
        foreach (var file in Directory.EnumerateFiles(folder, "*" + SourceSuffix)) {
            var source = SharedFile.ReadAllText(file).Trim();
            if (File.Exists(Path.GetFullPath(source, library.RootPath))) {
                changed |= Republished(library, project, source, file[..^SourceSuffix.Length]);
            }
        }

        return changed;
    }

    /// <summary>Publishes again for every open stack, which then follows, as a save must.</summary>
    public static void RefreshOpen(OpenLibrary library, StackSessions stacks) {
        var projects = Path.Combine(library.RootPath, LibraryLayout.ProjectsFolder);
        foreach (var open in stacks.All.GroupBy(static stack => stack.Folder, StringComparer.Ordinal)) {
            var project = Path.GetRelativePath(projects, open.Key ?? projects).Replace('\\', '/');
            if (project.StartsWith("..", StringComparison.Ordinal) || !Refresh(library, project)) {
                continue;
            }

            foreach (var stack in open) {
                stack.CompoundsChanged();
            }
        }
    }

    /// <summary>
    ///     A graph that lost its Outputs leaves what it last published in place, because layers read that, and it is
    ///     only a graph being worked on.
    /// </summary>
    static bool Republished(OpenLibrary library, string project, string source, string compound) {
        var before = File.Exists(compound) ? SharedFile.ReadAllText(compound) : string.Empty;
        try {
            Publish(library, project, source);
        } catch (StackEditRefusedException) {
            return false;
        }

        return !string.Equals(before, SharedFile.ReadAllText(compound), StringComparison.Ordinal);
    }

    /// <summary>Whether a graph has the shape a role needs: one map, and a filter reads one too.</summary>
    /// <exception cref="ProjectRefusedException">It does not, and this says what is missing.</exception>
    public static void CheckRole(OpenLibrary library, string path, string role) {
        var graph = GraphDocuments.Parse(SharedFile.ReadAllText(Path.GetFullPath(path, library.RootPath)));
        var outputs = graph.Nodes.Count(static node => node.Type == OutputNode);
        var inputs = graph.Interface.Count(static port => port is {
                Direction: PortDirection.Input, Kind: PortKind.Image
            }
        );
        var wanted = role == GraphRoles.Filter ? 1 : 0;
        if (role != GraphRoles.Material && (outputs != 1 || inputs != wanted)) {
            throw new ProjectRefusedException(
                $"A {role} has one Output and {wanted} image inputs, and this graph has {outputs} and {inputs}."
            );
        }
    }

    /// <summary>The Output a brush renders, as a graph session names its picture.</summary>
    /// <exception cref="ProjectRefusedException">The graph has none.</exception>
    public static (int Node, string Usage) BrushOutput(string text) {
        var node = GraphDocuments.Parse(text).Nodes.FirstOrDefault(static node => node.Type == OutputNode)
            ?? throw new ProjectRefusedException("A brush needs an Output to render.");
        return (node.Id, node.Texts.GetValueOrDefault(UsageSetting, DefaultUsage));
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

        var stack = StackDocuments.Parse(SharedFile.ReadAllText(full));
        return SmartMaterial.Extract(stack, stack.Name).Material
            ?? throw new StackEditRefusedException($"'{stack.Name}' has no layers to use.");
    }

    static MaterialInfo? Described(OpenLibrary library, ProjectDocumentInfo document) =>
        document.Kind switch {
            DocumentKinds.Graph or DocumentKinds.ImageMaterial => FromGraph(
                library,
                document.Name,
                document.Path,
                "project",
                document.Role
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

    static MaterialInfo FromGraph(OpenLibrary library, string name, string path, string origin, string? role = null) {
        var graph = GraphDocuments.Parse(SharedFile.ReadAllText(Path.GetFullPath(path, library.RootPath)));
        var usages = Usages(graph);
        return new(
            name,
            DocumentKinds.Graph,
            path,
            usages.Count == 0 ? "empty" : role ?? GraphRoles.Material,
            origin,
            usages,
            [..graph.Parameters.Select(GraphDocuments.Definition)]
        );
    }

    static List<string> Usages(NodeGraphAsset graph) => [
        ..graph.Nodes.Where(static node => node.Type == OutputNode)
            .Select(static node => node.Texts.GetValueOrDefault(UsageSetting, DefaultUsage))
            .Distinct(StringComparer.Ordinal)
    ];

    /// <summary>
    ///     A graph's pictures are found beside it, and a compound lives elsewhere, so they are named from the library,
    ///     which is where a stack in any project looks.
    /// </summary>
    static NodeGraphAsset Rooted(NodeGraphAsset graph, string file, string library) =>
        graph with {
            Nodes = [
                ..graph.Nodes.Select(node => {
                        if (node.Type != BitmapNode
                            || !node.Texts.TryGetValue(SourceSetting, out var picture)
                            || Path.IsPathRooted(picture)) {
                            return node;
                        }

                        var beside = Path.GetFullPath(picture, Path.GetDirectoryName(file)!);
                        return node with {
                            Texts = new(node.Texts, StringComparer.Ordinal) {
                                [SourceSetting] = LibraryLayout.Relative(library, beside)
                            }
                        };
                    }
                )
            ]
        };

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
