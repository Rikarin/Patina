using Patina.Contracts;
using Patina.Graphs;
using Patina.Library;
using Patina.Server.Stacks;
using System.Globalization;
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
    const int ElsewhereLimit = 300;
    const int ProjectPage = 200;
    const string ElsewhereOrigin = "library";
    const string PackageOrigin = "package";
    const string StarterPrefix = "starter:";
    const string StarterResource = "Starter/";

    /// <summary>The types a shelf narrows to, and where a material can be from.</summary>
    static readonly string[] Roles = [
        GraphRoles.Material, GraphRoles.Mask, GraphRoles.Filter, GraphRoles.Brush, PresetRole, PictureRole,
        ShelfEnvironments.Role, ShelfAlphas.Role
    ];

    /// <summary>
    ///     The type of a brush or tool preset on the shelf, which a client takes up rather than adds (#124).
    /// </summary>
    public const string PresetRole = "preset";

    /// <summary>
    ///     The type of a picture of the project's on the shelf (#203): its source is the file's path below the library,
    ///     which a picture slot, a decal and a fill take as a picture chosen any other way.
    /// </summary>
    public const string PictureRole = "picture";

    static readonly string[] Origins = ["shipped", PackageOrigin, "project", ElsewhereOrigin];

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
            ..ShippedSmartMaterials.Names.Select(static name => new MaterialInfo(
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
        found.AddRange(ShelfCompounds.All);
        found.AddRange(Packaged(library));
        if (project is not null) {
            var contents = library.Contents(project);
            found.AddRange(contents.Documents.SelectMany(document => Described(library, document)));
            List<MaterialInfo> pictures = [..contents.Pictures.Select(p => Pictured(p, contents.Name, "project"))];
            found.AddRange(pictures);
            found.AddRange(ShelfEnvironments.Among(pictures));
        }

        found.AddRange(Presets(library, project, search));
        found.AddRange(ShelfAlphas.All);
        found.RemoveAll(material => !Matches(material, words));
        found.AddRange(Elsewhere(library, project, words).Take(ElsewhereLimit));
        return found;
    }

    /// <summary>
    ///     The brush and tool presets the project's brush panel lists, as the shelf shows them (#124): tagged with
    ///     their tool, so a search for one finds them, and never added to a stack, since taking one up is the client's.
    /// </summary>
    static IEnumerable<MaterialInfo> Presets(OpenLibrary library, string? project, string? search) =>
        BrushShelf.List(library, project, search)
            .Select(static preset => new MaterialInfo(
                    preset.Name,
                    DocumentKinds.BrushPreset,
                    preset.Source,
                    PresetRole,
                    preset.Origin,
                    [],
                    Project: preset.Project,
                    Tags: [preset.Preset.Tool]
                )
            );

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
            material.Source,
            string.Join(' ', material.Tags ?? [])
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

    /// <summary>The names of the starter presets that ship with Patina, in order.</summary>
    public static IReadOnlyList<string> StarterNames { get; } = [
        ..typeof(MaterialShelf).Assembly.GetManifestResourceNames()
            .Where(static resource => resource.StartsWith(StarterResource, StringComparison.Ordinal))
            .Select(static resource => resource[StarterResource.Length..])
            .Order(StringComparer.Ordinal)
    ];

    /// <summary>The graphs that ship with Patina. They are CC0, and using one puts a copy in the project.</summary>
    static IEnumerable<MaterialInfo> Starters() =>
        StarterNames.Select(static name => {
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

    /// <summary>The library's packages (ADR 0027), each as its graph. One that will not read is left out.</summary>
    static IEnumerable<MaterialInfo> Packaged(OpenLibrary library) {
        foreach (var package in PackageManifests.In(library.RootPath)) {
            MaterialInfo? found;
            try {
                var manifest = PackageManifests.Read(package);
                var graph = LibraryLayout.Relative(library.RootPath, Path.Combine(package, manifest.Graph));
                found = FromGraph(GraphOf(library, graph), manifest.Name, graph, PackageOrigin, manifest.Role);
            } catch (Exception e) when (e is IOException or LibraryFormatException or GraphEditRefusedException) {
                found = null;
            }

            if (found is not null) {
                yield return found;
            }
        }
    }

    /// <summary>
    ///     A package's compounds come with it into the project that uses it, under the package's own folder, which is
    ///     the package's to replace: a newer package replaces them all.
    /// </summary>
    static void BringPackage(OpenLibrary library, string project, string graph) {
        if (PackageManifests.PackageOf(graph) is not { } package) {
            return;
        }

        var name = PackageManifests.Read(package).Name;
        var compounds = library.ProjectFile(project, LibraryLayout.CompoundsFolder);
        var folder = Path.Combine(compounds, GraphPackages.CompoundPrefix.TrimEnd('/'), name);
        List<(string Path, string Text)> brought = [..GraphPackages.CompoundsFor(package, library.RootPath)];
        if (Directory.Exists(folder)) {
            foreach (var stale in Directory.EnumerateFiles(
                             folder,
                             "*" + LibraryLayout.GraphExtension,
                             SearchOption.AllDirectories
                         )
                         .Where(file => !brought.Exists(b => string.Equals(
                                     Path.Combine(compounds, b.Path),
                                     file,
                                     StringComparison.Ordinal
                                 )
                             )
                         )) {
                File.Delete(stale);
            }
        }

        foreach (var (path, text) in brought) {
            var file = Path.Combine(compounds, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            if (!File.Exists(file) || !string.Equals(SharedFile.ReadAllText(file), text, StringComparison.Ordinal)) {
                LibraryFiles.Replace(file, text);
            }
        }
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
        // A material preset is found by its material's name too, which only its file holds (#124).
        if (document.Kind == DocumentKinds.MaterialPreset) {
            return true;
        }

        var graph = document.Kind is DocumentKinds.Graph or DocumentKinds.ImageMaterial;
        var role = document.Role ?? GraphRoles.Material;
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
            graph ? ShelfEnvironments.Role : string.Empty,
            graph ? Terms.InShelf(ShelfEnvironments.Role) : string.Empty,
            ElsewhereOrigin,
            Terms.InShelf(ElsewhereOrigin),
            project,
            string.Join(' ', document.Tags)
        );
    }

    static IEnumerable<MaterialInfo> Elsewhere(OpenLibrary library, string? project, string[] words) {
        // Every project's documents and pictures at any depth are a query of the index each, not a read of each
        // project's file and a walk of its folder (#306, #311).
        var pictures = library.Pictures().ToLookup(static picture => picture.Project, StringComparer.Ordinal);
        var documents = DocumentsByProject(library);
        for (var offset = 0;; offset += ProjectPage) {
            var page = library.Projects(new(Offset: offset, Limit: ProjectPage)).Items;
            foreach (var summary in page.Where(summary => summary.Path != project)) {
                Held held = new(documents[summary.Path], pictures[summary.Path]);
                foreach (var material in OfProject(library, summary, held, words)) {
                    yield return material;
                }
            }

            if (page.Count < ProjectPage) {
                yield break;
            }
        }
    }

    /// <summary>
    ///     Every project's documents as the index lists them (#311), by project: the brush shelf reads other projects'
    ///     through this too (#124).
    /// </summary>
    internal static ILookup<string, ProjectDocumentInfo> DocumentsByProject(OpenLibrary library) =>
        library.Documents()
            .ToLookup(static found => found.Project, static found => found.Document, StringComparer.Ordinal);

    /// <summary>What the index holds of one project.</summary>
    readonly record struct Held(IEnumerable<ProjectDocumentInfo> Documents, IEnumerable<LibraryPicture> Pictures);

    /// <summary>
    ///     What another project offers that a search finds: its materials, and its pictures too (#203), which a click
    ///     or a drag brings into the open project first.
    /// </summary>
    static IEnumerable<MaterialInfo> OfProject(OpenLibrary library, ProjectSummary summary, Held held, string[] words) {
        var materials = held.Documents
            .Where(document => Maybe(document, summary.Name, words))
            .SelectMany(document => Readable(library, document))
            .Select(read => read with { Origin = ElsewhereOrigin, Project = summary.Name });
        List<MaterialInfo> pictured = [..held.Pictures.Select(static picture => Pictured(picture))];
        return materials.Concat(pictured)
            .Concat(ShelfEnvironments.Among(pictured))
            .Where(material => Matches(material, words));
    }

    /// <summary>A picture of a project's as the shelf lists it, found by its file type as well as its name.</summary>
    static MaterialInfo Pictured(ProjectFile picture, string project, string origin) =>
        new(
            Path.GetFileNameWithoutExtension(picture.Name),
            picture.Kind,
            picture.Path,
            PictureRole,
            origin,
            [],
            Project: project,
            Tags: [Path.GetExtension(picture.Name).TrimStart('.').ToUpperInvariant()]
        );

    /// <summary>
    ///     Another project's picture as the index has it (#306), found by its file type and its size as well as its
    ///     name: "2048" finds every picture that wide or high.
    /// </summary>
    static MaterialInfo Pictured(LibraryPicture picture) {
        var name = Path.GetFileName(picture.Path);
        var listed = Pictured(new("picture", name, picture.Path), picture.ProjectName, ElsewhereOrigin);
        if (picture is not { Width: { } width, Height: { } height }) {
            return listed;
        }

        var size = string.Create(CultureInfo.InvariantCulture, $"{width} × {height}");
        return listed with { Tags = [..listed.Tags ?? [], size] };
    }

    /// <summary>One broken file in another project must not empty this project's shelf.</summary>
    static IReadOnlyList<MaterialInfo> Readable(OpenLibrary library, ProjectDocumentInfo document) {
        try {
            return Described(library, document);
        } catch (Exception e) when (e is not OperationCanceledException) {
            return [];
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
    /// <returns>The compound's node type, which for a compound the shelf ships is its own (#122).</returns>
    /// <exception cref="StackEditRefusedException">The graph has no output a layer could use.</exception>
    public static string Publish(OpenLibrary library, string project, string source) {
        if (ShelfCompounds.NodeOf(source) is { } shipped) {
            return shipped;
        }

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
        BringPackage(library, project, full);
        var text = GraphDocuments.Text(AsCompound(GraphFiles.Rooted(graph, full, library.RootPath)));
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

    /// <summary>
    ///     Where the shelf has the material a graph fill of a project reads (#124): a shipped compound by its type,
    ///     or what the project's copy of a published graph was published from. Null for a graph the shelf did not give.
    /// </summary>
    internal static string? SourceOf(OpenLibrary library, string project, string graph) {
        if (ShelfCompounds.NodeOf(ShelfCompounds.Prefix + graph) is not null) {
            return ShelfCompounds.Prefix + graph;
        }

        var name = graph.StartsWith(MaterialsFolder + "/", StringComparison.Ordinal)
            ? graph[(MaterialsFolder.Length + 1)..]
            : null;
        var file = name is null || name.Contains('/', StringComparison.Ordinal)
            ? null
            : CompoundFile(library, project, name) + SourceSuffix;
        return file is not null && File.Exists(file) ? SharedFile.ReadAllText(file).Trim() : null;
    }

    /// <summary>Whether a source names a graph file in the library.</summary>
    internal static bool IsLibraryGraph(OpenLibrary library, string source) {
        var full = Path.GetFullPath(source, library.RootPath);
        var root = Path.GetFullPath(library.RootPath) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.Ordinal)
            && full.EndsWith(LibraryLayout.GraphExtension, StringComparison.OrdinalIgnoreCase)
            && File.Exists(full);
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
            var text = ShippedSmartMaterials.Source(name)
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

    /// <summary>What a project's document puts on the shelf: nothing, a material, or a graph and the sky it makes.</summary>
    static IReadOnlyList<MaterialInfo> Described(OpenLibrary library, ProjectDocumentInfo document) =>
        document.Kind switch {
            DocumentKinds.Graph or DocumentKinds.ImageMaterial => Graphed(library, document),
            DocumentKinds.MaterialPreset => [MaterialPresetShelf.Described(library, document)],
            DocumentKinds.LayeredMaterial => [
                new(
                    document.Name,
                    document.Kind,
                    document.Path,
                    document.Role ?? GraphRoles.Material,
                    "project",
                    [],
                    Tags: document.Tags
                )
            ],
            _ => []
        };

    /// <summary>A graph on the shelf, and an environment too when it makes a sky to light the viewport (#203).</summary>
    static IReadOnlyList<MaterialInfo> Graphed(OpenLibrary library, ProjectDocumentInfo document) {
        var graph = GraphOf(library, document.Path);
        var material = FromGraph(graph, document.Name, document.Path, "project", document.Role, document.Tags);
        return ShelfEnvironments.Lights(graph) ? [material, ShelfEnvironments.Of(material)] : [material];
    }

    static NodeGraphAsset GraphOf(OpenLibrary library, string path) =>
        GraphDocuments.Parse(SharedFile.ReadAllText(Path.GetFullPath(path, library.RootPath)));

    /// <summary>
    ///     A graph on the shelf. Its tags are its project's words for it (ADR 0018), which a search of another project
    ///     reads without opening the file, and the graph's own, which a package carries.
    /// </summary>
    static MaterialInfo FromGraph(
        NodeGraphAsset graph,
        string name,
        string path,
        string origin,
        string? role = null,
        IReadOnlyList<string>? tags = null
    ) {
        var usages = Usages(graph);
        return new(
            name,
            DocumentKinds.Graph,
            path,
            usages.Count == 0 ? "empty" : role ?? GraphRoles.Material,
            origin,
            usages,
            [..graph.Parameters.Select(GraphDocuments.Definition)],
            Tags: [..(tags ?? []).Concat(graph.Tags).Distinct(StringComparer.OrdinalIgnoreCase)],
            Description: graph.Description.Length > 0 ? graph.Description : null
        );
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
