using Patina.Contracts;
using Patina.Library;
using System.Security.Cryptography;
using Vixen.Core.Yaml;
using Vixen.NodeGraph;
using Vixen.TextureGraph;
using Vixen.TextureGraph.Drawing;

namespace Patina.Graphs;

/// <summary>
///     Publishes a graph as a package (ADR 0027): a <c>.ptpkg</c> folder holding the graph, the compounds of its
///     project that it reads, the pictures it reads, and a <c>package.yaml</c> saying what it carries and what made it.
///     Everything in it is text but the pictures, so a package diffs in Git, and it evaluates wherever it is put: its
///     compounds sit in the <c>Compounds</c> folder beside its graph, and its pictures beside it, which is where ADR
///     0007 already looks.
/// </summary>
public static class GraphPackages {
    /// <summary>Where a package's own compounds go, under its name, so two packages never share one by accident.</summary>
    public const string CompoundPrefix = "Packages/";

    const string BitmapNode = "Source/Bitmap";
    const string SourceSetting = "Source";
    const string MeshMapScheme = "meshmap:";
    const string Staging = ".publishing";

    /// <summary>The roles a package can have, which are the shelf's types.</summary>
    static readonly string[] Roles = ["material", "mask", "filter", "brush"];

    /// <summary>What publishing a graph into a library's <c>packages/</c> would do, without doing it.</summary>
    /// <param name="graph">The graph's file.</param>
    /// <param name="library">The library's folder.</param>
    /// <param name="role">What it is used as.</param>
    public static PackagePlan Plan(string graph, string library, string role) {
        var gathered = Gather(graph, library);
        var path = LibraryPath(library, gathered.Name);
        return new(
            gathered.Name,
            role,
            path,
            Directory.Exists(Path.Combine(library, path)),
            gathered.Dependencies,
            gathered.Diagnostics
        );
    }

    /// <summary>Publishes a graph into a library's <c>packages/</c>, replacing a package of its name.</summary>
    /// <exception cref="PackageRefusedException">It reads something a package cannot carry, or does not compile.</exception>
    /// <param name="graph">The graph's file.</param>
    /// <param name="library">The library's folder.</param>
    /// <param name="role">What it is used as.</param>
    /// <param name="tags">Its project's words for it, which the package's graph carries beside its own.</param>
    public static PackagePublished Publish(
        string graph,
        string library,
        string role,
        IReadOnlyList<string>? tags = null
    ) {
        var gathered = Gather(graph, library);
        var path = LibraryPath(library, gathered.Name);
        return new(path, Write(gathered, Path.Combine(library, path), role, tags ?? []));
    }

    /// <summary>Publishes a graph into a folder of packages, replacing a package of its name.</summary>
    /// <param name="graph">The graph's file.</param>
    /// <param name="folder">Where the <c>.ptpkg</c> goes.</param>
    /// <param name="library">The library its pictures may be under, or null for the one around it, if any.</param>
    /// <param name="role">What it is used as.</param>
    /// <returns>The package's folder and its manifest.</returns>
    /// <exception cref="PackageRefusedException">It reads something a package cannot carry, or does not compile.</exception>
    public static (string Package, PackageManifest Manifest) PublishTo(
        string graph,
        string folder,
        string? library,
        string role
    ) {
        var gathered = Gather(graph, library);
        var package = Path.Combine(folder, gathered.Name + PackageManifests.Extension);
        return (package, Write(gathered, package, role, []));
    }

    /// <summary>A role a package can have, or a refusal.</summary>
    /// <exception cref="PackageRefusedException">It is none.</exception>
    public static string RoleOf(string role) =>
        Roles.Contains(role, StringComparer.Ordinal)
            ? role
            : throw new PackageRefusedException(
                $"A package is a {string.Join(", a ", Roles[..^1])} or a {Roles[^1]}, not '{role}'."
            );

    /// <summary>
    ///     The compounds a package carries, as a project that uses it gets them: under the same types, their pictures
    ///     named from the library so that they are found from the project.
    /// </summary>
    /// <param name="package">The package's folder.</param>
    /// <param name="library">The library's folder, which the package is in.</param>
    /// <returns>Each compound's path below a compounds folder, and its text.</returns>
    public static IEnumerable<(string Path, string Text)> CompoundsFor(string package, string library) {
        var manifest = PackageManifests.Read(package);
        var graph = Path.Combine(package, manifest.Graph);
        var compounds = Path.Combine(package, LibraryLayout.CompoundsFolder);
        foreach (var compound in manifest.Compounds) {
            var file = Path.Combine(compounds, compound.Type + LibraryLayout.GraphExtension);
            var asset = YamlSerializer.Parse<NodeGraphAsset>(SharedFile.ReadAllText(file));
            yield return (compound.Type + LibraryLayout.GraphExtension,
                YamlSerializer.ToYaml(Rooted(asset, graph, library)));
        }
    }

    /// <summary>
    ///     A graph's pictures named from the library, as found from where <paramref name="file" /> is: a graph's
    ///     pictures are found beside the graph holding it, and a project's stack is not beside the package.
    /// </summary>
    public static NodeGraphAsset Rooted(NodeGraphAsset graph, string file, string library) =>
        graph with {
            Nodes = [
                ..graph.Nodes.Select(node => {
                        if (node.Type != BitmapNode
                            || !node.Texts.TryGetValue(SourceSetting, out var picture)
                            || picture.Length == 0
                            || Path.IsPathRooted(picture)) {
                            return node;
                        }

                        var beside = Path.GetFullPath(picture, Path.GetDirectoryName(Path.GetFullPath(file))!);
                        return node with {
                            Texts = new(node.Texts, StringComparer.Ordinal) {
                                [SourceSetting] = LibraryLayout.Relative(library, beside)
                            }
                        };
                    }
                )
            ]
        };

    /// <summary>
    ///     What a package is called: its graph's name, which a person gave it, unless that cannot be a folder's name,
    ///     when it is the file's.
    /// </summary>
    static string NameOf(NodeGraphAsset graph, string file) {
        var name = graph.Name.Trim();
        return name.Length > 0
            && name is not ("." or "..")
            && name.IndexOfAny([..Path.GetInvalidFileNameChars(), '/', '\\', ':', '*', '?', '"', '<', '>', '|']) < 0
                ? name
                : Path.GetFileNameWithoutExtension(file);
    }

    static string LibraryPath(string library, string name) =>
        $"{PackageManifests.LibraryFolder}/{name}{PackageManifests.Extension}";

    /// <summary>A compound the graph reads from its project's folder, and the file it is in.</summary>
    sealed record Compound(string Type, string File, NodeGraphAsset Graph);

    /// <summary>A picture one of the graph's files names, and the file it was found at.</summary>
    sealed record Picture(string Reference, string Found);

    sealed record Gathered(
        string Name,
        string File,
        NodeGraphAsset Graph,
        IReadOnlyList<Compound> Compounds,
        IReadOnlyList<Picture> Pictures,
        IReadOnlyList<string> Shipped,
        IReadOnlyList<PackageDependency> Dependencies,
        IReadOnlyList<GraphDiagnostic> Diagnostics
    );

    /// <summary>Everything the graph reads, walked through its project's compounds, and what compiling it says.</summary>
    static Gathered Gather(string graph, string? library) {
        if (!File.Exists(graph)) {
            throw new FileNotFoundException($"There is no graph at '{graph}'.", graph);
        }

        var root = YamlSerializer.Parse<NodeGraphAsset>(SharedFile.ReadAllText(graph));
        var folder = CompoundFolders.Of(graph);
        var published = new CompoundShelf().Of(folder);
        Walk walk = new(folder, published, CompoundFolders.PictureRoots(graph, library));
        walk.Through(root);
        var diagnostics = Compile(root, published, walk.Dependencies);
        return new(
            NameOf(root, graph),
            graph,
            root,
            walk.Compounds,
            [..walk.Pictures.Values],
            [..walk.Shipped],
            [
                ..walk.Dependencies.OrderBy(static d => d.Kind, StringComparer.Ordinal)
                    .ThenBy(static d => d.Name, StringComparer.Ordinal)
            ],
            diagnostics
        );
    }

    /// <summary>
    ///     A walk through a graph and every compound of its folder that it reads, however deep, gathering what each
    ///     names: compounds, the shipped ones and pictures, and what none of them is.
    /// </summary>
    /// <param name="folder">The compounds folder the graph reads.</param>
    /// <param name="published">The node types that folder and Patina publish.</param>
    /// <param name="roots">Where a picture is found: beside the graph, then in the library.</param>
    sealed class Walk(string folder, PublishedCompounds published, IReadOnlyList<string> roots) {
        readonly HashSet<string> seen = new(StringComparer.Ordinal);
        readonly Queue<NodeGraphAsset> pending = [];
        readonly string? library = roots.Count > 1 ? roots[1] : null;

        public List<Compound> Compounds { get; } = [];
        public Dictionary<string, Picture> Pictures { get; } = new(StringComparer.Ordinal);
        public SortedSet<string> Shipped { get; } = new(StringComparer.Ordinal);
        public List<PackageDependency> Dependencies { get; } = [];

        public void Through(NodeGraphAsset graph) {
            pending.Enqueue(graph);
            while (pending.TryDequeue(out var asset)) {
                foreach (var node in asset.Nodes) {
                    Visit(node);
                }
            }
        }

        void Visit(GraphNodeAsset node) {
            if (node.Type == BitmapNode) {
                Picture(node.Texts.GetValueOrDefault(SourceSetting, string.Empty).Trim());
            } else if (!SubGraphs.IsBoundary(node.Type) && !Reroutes.IsReroute(node.Type) && seen.Add(node.Type)) {
                Type(node.Type);
            }
        }

        /// <summary>A compound of the folder is carried and walked; a shipped one is named; anything else is a node.</summary>
        void Type(string type) {
            var file = Path.Combine(
                folder,
                type.Replace('/', Path.DirectorySeparatorChar) + LibraryLayout.GraphExtension
            );
            if (File.Exists(file)) {
                var asset = YamlSerializer.Parse<NodeGraphAsset>(SharedFile.ReadAllText(file));
                Compounds.Add(new(type, file, asset));
                Dependencies.Add(new("compound", type, Shown(file)));
                pending.Enqueue(asset);
            } else if (TextureCompoundLibrary.Shipped.Contains(type)) {
                Shipped.Add(type);
                Dependencies.Add(new("shipped", type, null));
            } else if (!published.Registry.TryGet(type, out _)) {
                Dependencies.Add(new("unknown", type, null, "Nothing publishes this node type here."));
            }
        }

        void Picture(string reference) {
            if (reference.Length == 0
                || reference.StartsWith(MeshMapScheme, StringComparison.Ordinal)
                || Pictures.ContainsKey(reference)) {
                return;
            }

            var found = Located(reference, roots);
            Pictures[reference] = new(reference, found ?? string.Empty);
            Dependencies.Add(
                found is null
                    ? new(
                        "picture",
                        reference,
                        null,
                        "It is not beside the graph or in the library. Point the Bitmap at it again."
                    )
                    : new("picture", reference, Shown(found))
            );
        }

        /// <summary>A file below the library by its path there, which is what a person recognises; else whole.</summary>
        string Shown(string file) =>
            library is not null && file.StartsWith(library + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                ? LibraryLayout.Relative(library, file)
                : file;
    }

    /// <summary>
    ///     Compiles the graph as the Worker would, which is the check that what is published evaluates, and names the
    ///     fonts and SVG files its drawings read, which a package does not carry yet.
    /// </summary>
    static IReadOnlyList<GraphDiagnostic> Compile(
        NodeGraphAsset graph,
        PublishedCompounds published,
        List<PackageDependency> dependencies
    ) {
        var model = NodeGraphDocument.Load(CompoundShelf.WithoutBoundaries(graph), out _);
        var compiler = new TextureGraphCompiler(published.Registry) { SubGraphSource = published.Source };
        var compilation = compiler.Compile(model);
        foreach (var external in compiler.Externals) {
            if (TextureDrawings.Draws(external.Asset) && TextureDrawings.FileOf(external.Asset) is { } file) {
                dependencies.Add(new("drawing", file, null, "A package cannot carry a font or an SVG file yet."));
            }
        }

        return [
            ..compilation.Diagnostics.Where(static d => d.Severity == NodeSeverity.Error)
                .Select(GraphFactsOf.Diagnostic)
        ];
    }

    static string? Located(string reference, IReadOnlyList<string> roots) {
        if (Path.IsPathRooted(reference)) {
            return File.Exists(reference) ? reference : null;
        }

        foreach (var root in roots) {
            var candidate = Path.GetFullPath(Path.Combine(root, reference));
            if (candidate.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && File.Exists(candidate)) {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Writes a package whole: into a folder beside it first, so a reader never sees half of one.</summary>
    static PackageManifest Write(Gathered gathered, string package, string role, IReadOnlyList<string> tags) {
        RoleOf(role);
        if (gathered.Dependencies.FirstOrDefault(static d => d.Problem is not null) is { } missing) {
            throw new PackageRefusedException($"'{missing.Name}' cannot be brought into a package: {missing.Problem}");
        }

        if (gathered.Diagnostics is [var error, ..]) {
            throw new PackageRefusedException(
                $"The graph does not compile, so it cannot be published: {error.Message}"
            );
        }

        var prefix = $"{CompoundPrefix}{gathered.Name}/";
        var types = gathered.Compounds.ToDictionary(
            static compound => compound.Type,
            compound => prefix + compound.Type,
            StringComparer.Ordinal
        );
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach (var picture in gathered.Pictures) {
            var bytes = SharedFile.ReadAllBytes(picture.Found);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes))[..16];
            files[picture.Reference] = $"{PackageManifests.FilesFolder}/{hash}{Path.GetExtension(picture.Found).ToLowerInvariant()}";
        }

        var staging = package + Staging;
        if (Directory.Exists(staging)) {
            Directory.Delete(staging, true);
        }

        Directory.CreateDirectory(Path.Combine(staging, PackageManifests.FilesFolder));
        foreach (var picture in gathered.Pictures) {
            var target = Path.Combine(staging, files[picture.Reference]);
            if (!File.Exists(target)) {
                File.WriteAllBytes(target, SharedFile.ReadAllBytes(picture.Found));
            }
        }

        foreach (var compound in gathered.Compounds) {
            var target = Path.Combine(
                staging,
                LibraryLayout.CompoundsFolder,
                types[compound.Type].Replace('/', Path.DirectorySeparatorChar) + LibraryLayout.GraphExtension
            );
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, YamlSerializer.ToYaml(Rewritten(compound.Graph, types, files)));
        }

        var graphFile = gathered.Name + LibraryLayout.GraphExtension;
        File.WriteAllText(
            Path.Combine(staging, graphFile),
            YamlSerializer.ToYaml(Rewritten(Tagged(gathered.Graph, tags), types, files))
        );
        PackageManifest manifest = new(
            PackageManifests.Format,
            gathered.Name,
            graphFile,
            role,
            $"Patina {ProductInfo.Version.Split('+')[0]}",
            [
                ..gathered.Compounds.Select(compound => new PackageCompound(types[compound.Type], compound.Type))
                    .OrderBy(static c => c.Type, StringComparer.Ordinal)
            ],
            gathered.Shipped,
            [
                ..files.Select(static file => new PackageFile(file.Value, file.Key))
                    .OrderBy(static f => f.From, StringComparer.Ordinal)
            ]
        );
        File.WriteAllText(Path.Combine(staging, PackageManifests.File), PackageManifests.Text(manifest));

        if (Directory.Exists(package)) {
            Directory.Delete(package, true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(package)!);
        Directory.Move(staging, package);
        return manifest;
    }

    /// <summary>The graph with its project's words for it beside its own, each once.</summary>
    static NodeGraphAsset Tagged(NodeGraphAsset graph, IReadOnlyList<string> tags) =>
        tags.Count == 0
            ? graph
            : graph with { Tags = [..graph.Tags.Concat(tags).Distinct(StringComparer.OrdinalIgnoreCase)] };

    /// <summary>A file of the package: its compounds under their package types, its pictures from <c>files/</c>.</summary>
    static NodeGraphAsset Rewritten(
        NodeGraphAsset graph,
        Dictionary<string, string> types,
        Dictionary<string, string> files
    ) =>
        graph with {
            Nodes = [
                ..graph.Nodes.Select(node => {
                        if (types.TryGetValue(node.Type, out var type)) {
                            return node with { Type = type };
                        }

                        return node.Type == BitmapNode
                            && node.Texts.TryGetValue(SourceSetting, out var picture)
                            && files.TryGetValue(picture.Trim(), out var file)
                                ? node with {
                                    Texts = new(node.Texts, StringComparer.Ordinal) { [SourceSetting] = file }
                                }
                                : node;
                    }
                )
            ]
        };
}

/// <summary>A graph that cannot be published as a package, with a sentence saying why.</summary>
public sealed class PackageRefusedException : Exception {
    public PackageRefusedException() { }

    public PackageRefusedException(string message) : base(message) { }

    public PackageRefusedException(string message, Exception inner) : base(message, inner) { }
}
