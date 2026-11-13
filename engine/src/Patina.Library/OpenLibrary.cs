using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     The library on disk. Opening reads <c>library.yaml</c> and connects to the index, and nothing else, which is
///     why it stays fast at any size. The index may lag the files until <see cref="Rescan" /> has run, but it can
///     always answer; callers decide when to rescan, and a Server does so in the background while it serves.
/// </summary>
public sealed class OpenLibrary : ILibrary, IDisposable {
    readonly IndexDatabase database;
    readonly LibraryQueries queries;
    readonly LibraryCostQueries costs;
    readonly LibraryScanner scanner;
    readonly LibraryDocumentState state;
    readonly Lock rescanGate = new();
    readonly Lock createGate = new();
    readonly Lock documentGate = new();
    int fileSchema;
    LibraryDocument document;
    int rescans;
    int unscannedWrites;

    OpenLibrary(string root, ReadDocument<LibraryDocument> read) {
        RootPath = root;
        document = read.Document;
        fileSchema = read.FileSchema;
        state = LibraryDocumentState.Of(read);
        database = new(LibraryLayout.IndexPath(root));
        queries = new(database);
        costs = new(database);
        scanner = new(root, database);
    }

    public LibraryId Id => Document.Id;

    public string RootPath { get; }

    /// <summary>The library's settings.</summary>
    public LibraryDocument Document => Volatile.Read(ref document);

    /// <summary>
    ///     Whether the index lags what this process knows of: a rescan is running, or a write awaits one.
    /// </summary>
    public bool Indexing => Volatile.Read(ref rescans) > 0 || Volatile.Read(ref unscannedWrites) > 0;

    /// <summary>Whether <paramref name="root" /> holds a library.</summary>
    public static bool Exists(string root) => File.Exists(Path.Combine(root, LibraryLayout.LibraryFile));

    /// <summary>Opens the library in <paramref name="root" />.</summary>
    /// <exception cref="LibraryFormatException"><c>library.yaml</c> cannot be used.</exception>
    public static OpenLibrary Open(string root) {
        var file = Path.Combine(root, LibraryLayout.LibraryFile);
        SharedFolderMove.Run(root);
        return new(root, DocumentCodec.ReadLibrary(SharedFile.ReadAllText(file), LibraryLayout.LibraryFile));
    }

    /// <summary>Brings the index up to the files. One rescan runs at a time in a process.</summary>
    public ScanResult Rescan(CancellationToken cancellationToken = default) {
        Interlocked.Increment(ref rescans);
        try {
            lock (rescanGate) {
                Interlocked.Exchange(ref unscannedWrites, 0); // this scan starts after them, so it sees them
                return scanner.Rescan(cancellationToken);
            }
        } finally {
            Interlocked.Decrement(ref rescans);
        }
    }

    /// <summary>
    ///     Writes a new project's files. Files are the truth, so this leaves the index alone: <see cref="Indexing" />
    ///     stays true until the next <see cref="Rescan" />, which is the caller's to run.
    /// </summary>
    /// <returns>The project as the index will describe it.</returns>
    /// <exception cref="ProjectRefusedException">The request breaks a rule, or its folder is taken.</exception>
    public ProjectSummary CreateProject(NewProject project) {
        var (path, document) = NewProjects.Resolve(project);
        lock (createGate) {
            LibraryFiles.CreateProject(RootPath, path, document);
        }

        Interlocked.Increment(ref unscannedWrites);
        return new(document.Id.Value, document.Name, path, document.Tags, 0);
    }

    /// <summary>
    ///     Writes a new graph's file, leaving the index to the next <see cref="Rescan" /> as
    ///     <see cref="CreateProject" /> does.
    /// </summary>
    /// <param name="graph">What to call it and where it goes.</param>
    /// <param name="text">The document.</param>
    /// <returns>Its path below the library, with forward slashes.</returns>
    /// <exception cref="ProjectRefusedException">The request breaks a rule, or the file is taken.</exception>
    public string CreateGraph(NewGraph graph, string text) {
        var path = NewProjects.GraphPath(graph);
        lock (createGate) {
            LibraryFiles.CreateGraph(RootPath, path, text);
        }

        Interlocked.Increment(ref unscannedWrites);
        if (graph.Project?.Trim().Trim('/') is { Length: > 0 } project) {
            AddDocument(project, DocumentKinds.Graph, Path.GetFileName(path), graph.Name.Trim(), null);
        }

        return path;
    }

    /// <summary>The folders directly in a folder below <c>projects/</c>, with their projects at any depth.</summary>
    /// <exception cref="FileNotFoundException">There is no such folder.</exception>
    /// <exception cref="ProjectRefusedException">The path is not one a library can hold.</exception>
    public IReadOnlyList<FolderSummary> Folders(string parent) => [
        ..LibraryFolders.List(RootPath, parent)
            .Select(folder => new FolderSummary(folder.Path, folder.Name, folder.Color, queries.CountBelow(folder.Path))
            )
    ];

    /// <summary>Makes a folder.</summary>
    /// <exception cref="ProjectRefusedException">The request breaks a rule, or the name is taken.</exception>
    public FolderSummary CreateFolder(string parent, string name, string? color) {
        lock (createGate) {
            var folder = LibraryFolders.Create(RootPath, parent, name, color);
            return new(folder.Path, folder.Name, folder.Color, 0);
        }
    }

    /// <summary>Gives a folder a colour, or none.</summary>
    public void SetFolderColor(string path, string? color) {
        lock (createGate) {
            LibraryFolders.Recolour(RootPath, path, color);
        }
    }

    /// <summary>Renames a folder, which moves what is in it, so <see cref="Rescan" /> is the caller's to run.</summary>
    /// <returns>Its new path.</returns>
    public string RenameFolder(string path, string name) {
        lock (createGate) {
            var renamed = LibraryFolders.Rename(RootPath, path, name);
            Interlocked.Increment(ref unscannedWrites);
            return renamed;
        }
    }

    /// <summary>Moves folders and projects, all or none. <see cref="Rescan" /> is the caller's to run.</summary>
    /// <param name="paths">What to move, below <c>projects/</c>.</param>
    /// <param name="into">The folder to put them in, where empty is the top.</param>
    /// <returns>The new paths, in the order asked.</returns>
    public IReadOnlyList<string> Move(IReadOnlyList<string> paths, string into) {
        lock (createGate) {
            var moved = LibraryFolders.Move(RootPath, paths, into);
            Interlocked.Increment(ref unscannedWrites);
            return moved;
        }
    }

    /// <summary>Deletes a folder, never a project. <see cref="Rescan" /> is the caller's to run.</summary>
    /// <param name="path">The folder, below <c>projects/</c>.</param>
    /// <param name="lift">Moves what the folder holds up a level first. Otherwise only an empty folder goes.</param>
    public void DeleteFolder(string path, bool lift) {
        lock (createGate) {
            LibraryFolders.Delete(RootPath, path, lift);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>Checks a file can be a project's mesh before anything is written.</summary>
    /// <exception cref="FileNotFoundException">It is not there.</exception>
    /// <exception cref="ProjectRefusedException">It is not a mesh.</exception>
    public static void RequireMesh(string mesh) => ProjectFiles.Require(mesh);

    /// <summary>What a project's folder holds.</summary>
    /// <exception cref="FileNotFoundException">There is no such project.</exception>
    public ProjectContents Contents(string path) => ProjectFiles.Read(RootPath, path);

    /// <summary>The full path of a file a project holds, from its path relative to the project.</summary>
    public string ProjectFile(string path, string file) =>
        LibraryLayout.Full(RootPath, $"{LibraryLayout.ProjectsFolder}/{LibraryFolders.Existing(path)}/{file}");

    /// <summary>Brings a mesh into a project, and its detailed mesh if it has one.</summary>
    /// <returns>The mesh as the project lists it.</returns>
    public ProjectMesh AddMesh(
        string path,
        string mesh,
        string? high,
        string? source = null,
        UnwrapReport? unwrapped = null
    ) {
        lock (createGate) {
            var added = ProjectFiles.AddMesh(RootPath, path, mesh, high, source, unwrapped);
            Interlocked.Increment(ref unscannedWrites);
            return added;
        }
    }

    /// <summary>The file a mesh came from on this machine, or null if it is not known here.</summary>
    public string? SourceOf(ProjectMesh mesh) => ProjectFiles.SourceOf(RootPath, mesh);

    /// <summary>Whether a mesh file differs from the one the project already has, by what is in it.</summary>
    /// <exception cref="FileNotFoundException">The project or the file is not there.</exception>
    public bool MeshChanged(string path, string mesh) => ProjectFiles.MeshChanged(RootPath, path, mesh);

    /// <summary>Brings a file into a project as the mesh or the picture it is.</summary>
    /// <exception cref="FileNotFoundException">The project or the file is not there.</exception>
    /// <exception cref="ProjectRefusedException">Nothing here reads it.</exception>
    public void Import(string path, string file) {
        lock (createGate) {
            ProjectFiles.Import(RootPath, path, file);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>Brings a picture into a project, so that the project travels with what it reads.</summary>
    /// <param name="path">The project, below <c>projects/</c>.</param>
    /// <param name="picture">
    ///     A picture the library holds, by its path below the library, or a file on this machine by its full path.
    /// </param>
    /// <returns>Its path below the library.</returns>
    /// <exception cref="FileNotFoundException">The project or the picture is not there.</exception>
    /// <exception cref="ProjectRefusedException">A path below the library climbs out of it.</exception>
    public string AddPicture(string path, string picture) {
        var file = picture;
        if (!Path.IsPathFullyQualified(picture)) {
            var root = Path.GetFullPath(RootPath);
            file = Path.GetFullPath(picture, root);
            if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
                throw new ProjectRefusedException($"'{picture}' is not a picture this library holds.");
            }
        }

        lock (createGate) {
            return ProjectFiles.AddPicture(RootPath, path, file);
        }
    }

    /// <summary>
    ///     The pictures or meshes every project holds, the asked project's first, for a client that cannot see this
    ///     machine's disk to pick from (#4).
    /// </summary>
    /// <param name="wanted">Whether a file is one asked for, by its name.</param>
    /// <param name="first">The project, below <c>projects/</c>, whose files come first, if any.</param>
    /// <param name="limit">How many to give at most.</param>
    public LibraryFileList Files(Func<string, bool> wanted, string? first, int limit) {
        var (files, more) = ProjectFiles.Library(RootPath, wanted, first, limit);
        return new(files, more);
    }

    /// <summary>The file a new document of a project would have.</summary>
    /// <exception cref="ProjectExistsException">It is taken.</exception>
    /// <exception cref="ProjectRefusedException">The name cannot name a file.</exception>
    public string NewDocumentFile(string path, string name, string extension) =>
        ProjectFiles.NewFile(RootPath, path, name, extension);

    /// <summary>Lists a document whose file exists, so that it has a kind and a GUID.</summary>
    public ProjectItem AddDocument(string path, string kind, string file, string name, AssetId? mesh) {
        lock (createGate) {
            var added = ProjectFiles.AddDocument(RootPath, path, kind, file, name, mesh);
            Interlocked.Increment(ref unscannedWrites);
            return added;
        }
    }

    /// <summary>Puts a texturing document on another mesh of its project.</summary>
    /// <exception cref="FileNotFoundException">The project, document or mesh is not there.</exception>
    /// <exception cref="ProjectRefusedException">The document is not one a mesh belongs to.</exception>
    public ProjectItem SetDocumentMesh(string path, AssetId document, AssetId mesh) {
        lock (createGate) {
            var changed = ProjectFiles.SetMesh(RootPath, path, document, mesh);
            Interlocked.Increment(ref unscannedWrites);
            return changed;
        }
    }

    /// <summary>Makes a mesh the only one a project has, with every texturing document on it.</summary>
    /// <returns>The texturing documents.</returns>
    public IReadOnlyList<ProjectItem> UseMesh(string path, AssetId mesh) {
        lock (createGate) {
            var documents = ProjectFiles.UseMesh(RootPath, path, mesh);
            Interlocked.Increment(ref unscannedWrites);
            return documents;
        }
    }

    /// <summary>Says which material of the mesh a document paints, as one of the project's texture sets.</summary>
    public void SetDocumentMaterial(string path, AssetId document, string material) {
        lock (createGate) {
            ProjectFiles.SetMaterial(RootPath, path, document, material);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>Says what a project does with a mesh with no UV layout, and with paint on a new layout.</summary>
    public void SetMeshHandling(string path, bool? autoUnwrap, bool? preservePaint, UnwrapOptions? unwrap = null) {
        lock (createGate) {
            ProjectFiles.SetMeshHandling(RootPath, path, autoUnwrap, preservePaint, unwrap);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>Keeps what a project's export is set to, so any client's next export of it starts there.</summary>
    /// <exception cref="FileNotFoundException">The project is not there.</exception>
    public void SetExport(string path, ProjectExportSettings settings) {
        lock (createGate) {
            ProjectFiles.SetExport(RootPath, path, settings);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>Keeps how a project's bakes run.</summary>
    public void SetBake(string path, ProjectBakeSettings bake) {
        lock (createGate) {
            ProjectFiles.SetBake(RootPath, path, bake);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>Keeps how the viewport is to open in a project.</summary>
    public void SetViewport(string path, ViewportDisplay viewport) {
        lock (createGate) {
            ProjectFiles.SetViewport(RootPath, path, viewport);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>
    ///     Keeps what a project's texture sets are held to before export: budgets, a size limit, a density.
    /// </summary>
    /// <exception cref="FileNotFoundException">The project is not there.</exception>
    public void SetChecks(string path, ProjectCheckSettings checks) {
        lock (createGate) {
            ProjectFiles.SetChecks(RootPath, path, checks);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>Keeps how a project's painting was left: its colours, its brush and its symmetry.</summary>
    /// <exception cref="FileNotFoundException">The project is not there.</exception>
    public void SetPaint(string path, ProjectPaintSettings paint) {
        lock (createGate) {
            ProjectFiles.SetPaint(RootPath, path, paint);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>Saves a new brush preset in a project, as a document of it.</summary>
    /// <returns>Its path below the library.</returns>
    /// <exception cref="ProjectExistsException">The project has a file of that name.</exception>
    /// <exception cref="ProjectRefusedException">The name cannot name a file.</exception>
    public string CreateBrushPreset(string path, string name, BrushPreset preset) {
        lock (createGate) {
            var file = ProjectFiles.NewFile(RootPath, path, name, BrushPresets.Extension);
            BrushPresets.Write(ProjectFile(path, file), preset);
            ProjectFiles.AddDocument(RootPath, path, DocumentKinds.BrushPreset, file, name, null);
            Interlocked.Increment(ref unscannedWrites);
            return $"{LibraryLayout.ProjectsFolder}/{path.Trim('/')}/{file}";
        }
    }

    /// <summary>A new document with another's content.</summary>
    public ProjectItem CopyDocument(string path, AssetId document, string name) {
        lock (createGate) {
            var copy = ProjectFiles.Copy(RootPath, path, document, name);
            Interlocked.Increment(ref unscannedWrites);
            return copy;
        }
    }

    /// <summary>Records that a project reads a document of another one.</summary>
    public void AddReference(string path, AssetId asset) {
        lock (createGate) {
            ProjectFiles.AddReference(RootPath, path, asset);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>Says what a graph is for.</summary>
    /// <exception cref="FileNotFoundException">The project or document is not there.</exception>
    /// <exception cref="ProjectRefusedException">It is not a graph, or that is no role.</exception>
    public ProjectItem SetDocumentRole(string path, AssetId document, string role) {
        lock (createGate) {
            var changed = ProjectFiles.SetRole(RootPath, path, document, role);
            Interlocked.Increment(ref unscannedWrites);
            return changed;
        }
    }

    /// <summary>Gives a project a new display name. <see cref="Rescan" /> is the caller's to run.</summary>
    public void RenameProject(string path, string name) {
        lock (createGate) {
            ProjectFiles.Rename(RootPath, path, name);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>Moves a project to the library's bin. <see cref="Rescan" /> is the caller's to run.</summary>
    /// <param name="path">The project, below <c>projects/</c>.</param>
    public BinItem TrashProject(string path) => Write(() => ProjectFiles.Trash(RootPath, path));

    /// <summary>Moves a document of a project to the library's bin.</summary>
    /// <exception cref="FileNotFoundException">The project or document is not there.</exception>
    public BinItem TrashDocument(string path, AssetId document) =>
        Write(() => ProjectFiles.TrashDocument(RootPath, path, document));

    /// <summary>What the bin holds.</summary>
    public BinList Bin() => LibraryBin.List(RootPath);

    /// <summary>Puts something back from the bin.</summary>
    /// <returns>Where it is now, below the library.</returns>
    /// <exception cref="FileNotFoundException">The bin holds no such thing.</exception>
    /// <exception cref="ProjectRefusedException">It cannot go back where it was.</exception>
    public string Restore(string id) => Write(() => ProjectFiles.Restore(RootPath, id));

    /// <summary>Deletes one thing in the bin for good.</summary>
    /// <exception cref="FileNotFoundException">The bin holds no such thing.</exception>
    public void DeleteFromBin(string id) {
        lock (createGate) {
            LibraryBin.Delete(RootPath, id);
        }
    }

    /// <summary>Deletes everything in the bin for good.</summary>
    public int EmptyBin() {
        lock (createGate) {
            return LibraryBin.Empty(RootPath);
        }
    }

    /// <summary>Gives a document a new display name.</summary>
    /// <exception cref="FileNotFoundException">The project or document is not there.</exception>
    public ProjectItem RenameDocument(string path, AssetId document, string name) =>
        Write(() => ProjectFiles.RenameDocument(RootPath, path, document, name));

    /// <summary>Sets a project's tags, in its <c>project.yaml</c>.</summary>
    /// <exception cref="FileNotFoundException">The project is not there.</exception>
    public void SetProjectTags(string path, IReadOnlyList<string> tags) =>
        Write(() => {
                ProjectFiles.SetTags(RootPath, path, tags);
                return true;
            }
        );

    /// <summary>Sets a document's tags, in its project's <c>project.yaml</c>.</summary>
    /// <exception cref="FileNotFoundException">The project or document is not there.</exception>
    public ProjectItem SetDocumentTags(string path, AssetId document, IReadOnlyList<string> tags) =>
        Write(() => ProjectFiles.SetDocumentTags(RootPath, path, document, tags));

    /// <summary>
    ///     Pins a project's reference to an asset at the bytes the asset has now, or lets it follow the asset again.
    /// </summary>
    /// <exception cref="FileNotFoundException">The project, or the asset in the index, is not there.</exception>
    /// <exception cref="ProjectRefusedException">The project does not refer to the asset itself.</exception>
    public AssetReference SetPin(string path, AssetId asset, bool pinned) {
        string? file = null;
        if (pinned) {
            var summary = queries.Asset(asset) ?? throw new FileNotFoundException($"The library has no asset {asset}.");
            file = summary.Path[(LibraryLayout.ProjectsFolder.Length + 1)..];
        }

        return Write(() => ProjectFiles.SetPin(RootPath, path, asset, file));
    }

    /// <summary>A change to the files, which the index sees at the next rescan.</summary>
    T Write<T>(Func<T> change) {
        lock (createGate) {
            var result = change();
            Interlocked.Increment(ref unscannedWrites);
            return result;
        }
    }

    /// <summary>Copies a project beside itself. <see cref="Rescan" /> is the caller's to run.</summary>
    /// <param name="path">The project, below <c>projects/</c>.</param>
    /// <param name="name">The copy's display name.</param>
    /// <returns>The copy's path, below <c>projects/</c>.</returns>
    public string DuplicateProject(string path, string name) {
        lock (createGate) {
            var copy = ProjectFiles.Duplicate(RootPath, path, name);
            Interlocked.Increment(ref unscannedWrites);
            return copy;
        }
    }

    public ProjectPage Projects(LibraryQuery query) => queries.Projects(query);

    public AssetPage Assets(LibraryQuery query) => queries.Assets(query);

    public TagPage ProjectTags(int offset = 0, int limit = 50) => queries.ProjectTags(offset, limit);

    public TagPage AssetTags(int offset = 0, int limit = 50) => queries.AssetTags(offset, limit);

    public AssetImpact? Impact(AssetId asset, int offset = 0, int limit = 50) => queries.Impact(asset, offset, limit);

    /// <summary>What a folder's documents cost, by each folder or project directly in it (#207).</summary>
    /// <param name="folder">The folder below <c>projects/</c>, empty for the whole library.</param>
    public LibraryCosts Costs(string folder) => costs.Costs(folder);

    /// <summary>One page of the documents below a folder that cost more than a budget, in bytes (#207).</summary>
    public DocumentCostPage CostlierThan(string folder, long budget, int offset = 0, int limit = 50) =>
        costs.Over(folder, budget, offset, limit);

    /// <summary>
    ///     Keeps what an export of the whole library is set to, in <c>library.yaml</c>, where any client's next one
    ///     starts. Writing the file brings it to the current schema.
    /// </summary>
    public void SetExport(ProjectExportSettings settings) {
        ArgumentNullException.ThrowIfNull(settings);
        lock (documentGate) {
            var text = DocumentCodec.Write(Document with { Export = settings });
            LibraryFiles.Replace(Path.Combine(RootPath, LibraryLayout.LibraryFile), text);
            // What the file now holds, in its order, is what is reported, as it would be after opening it again.
            Volatile.Write(ref document, DocumentCodec.ReadLibrary(text, LibraryLayout.LibraryFile).Document);
            Volatile.Write(ref fileSchema, SchemaChain.Library.Current);
        }
    }

    /// <summary>
    ///     Keeps the export preset a project starts from when it has kept none, in <c>library.yaml</c>, or forgets it
    ///     with null, which leaves the factory's starter (#240). Whether a preset of that id exists is the caller's to
    ///     ask: a preset is a file, and one removed later only means the starter again.
    /// </summary>
    public void SetDefaultPreset(string? preset) {
        lock (documentGate) {
            var text = DocumentCodec.Write(Document with { DefaultPreset = preset });
            LibraryFiles.Replace(Path.Combine(RootPath, LibraryLayout.LibraryFile), text);
            Volatile.Write(ref document, DocumentCodec.ReadLibrary(text, LibraryLayout.LibraryFile).Document);
            Volatile.Write(ref fileSchema, SchemaChain.Library.Current);
        }
    }

    /// <summary>
    ///     The library as <c>GET /api/v1/library</c> reports it. <see cref="Indexing" /> is read before the counts:
    ///     read after them, a scan that ends in between reports old counts as caught up.
    /// </summary>
    public LibraryInfo Describe() {
        var indexing = Indexing;
        var settings = Document;
        return new(
            settings.Id.Value,
            settings.Name,
            RootPath,
            Volatile.Read(ref fileSchema),
            settings.TexelDensity,
            settings.Channels,
            settings.DefaultShader,
            settings.ExportTargets,
            queries.CountProjects(),
            queries.CountAssets(),
            indexing,
            settings.Export
        ) { DefaultPreset = settings.DefaultPreset };
    }

    /// <summary>Rescans, then reports the library's health.</summary>
    /// <param name="tools">The installed tool versions, or null for <see cref="ToolCatalog.Default" />.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    public CheckReport Check(ToolCatalog? tools = null, CancellationToken cancellationToken = default) {
        Rescan(cancellationToken);
        return new LibraryCheck(RootPath, state, database, tools ?? ToolCatalog.Default).Run(cancellationToken);
    }

    /// <summary>Rescans, then works out every output's key, in the projects a selection names.</summary>
    /// <param name="selection">Which projects.</param>
    /// <param name="tools">The installed tool versions, or null for <see cref="ToolCatalog.Default" />.</param>
    /// <param name="cancellationToken">Stops it between batches.</param>
    public OutputScan ScanOutputs(
        OutputSelection selection,
        ToolCatalog? tools = null,
        CancellationToken cancellationToken = default
    ) {
        Rescan(cancellationToken);
        return new LibraryOutputs(RootPath, database, queries, tools ?? ToolCatalog.Default).Scan(
            selection,
            cancellationToken
        );
    }

    public void Dispose() => database.Dispose();
}
