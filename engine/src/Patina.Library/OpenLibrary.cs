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
    readonly LibraryScanner scanner;
    readonly LibraryDocumentState state;
    readonly Lock rescanGate = new();
    readonly Lock createGate = new();
    readonly int fileSchema;
    int rescans;
    int unscannedWrites;

    OpenLibrary(string root, ReadDocument<LibraryDocument> read) {
        RootPath = root;
        Document = read.Document;
        fileSchema = read.FileSchema;
        state = LibraryDocumentState.Of(read);
        database = new(LibraryLayout.IndexPath(root));
        queries = new(database);
        scanner = new(root, database);
    }

    public LibraryId Id => Document.Id;

    public string RootPath { get; }

    /// <summary>The library's settings.</summary>
    public LibraryDocument Document { get; }

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
        return new(root, DocumentCodec.ReadLibrary(File.ReadAllText(file), LibraryLayout.LibraryFile));
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
            LibraryFiles.CreateGraph(RootPath, path, text, graph.Name.Trim());
        }

        Interlocked.Increment(ref unscannedWrites);
        return path;
    }

    /// <summary>The folders directly in a folder below <c>projects/</c>, with how many projects each holds at any depth.</summary>
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

    /// <summary>The name a project's first stack gets.</summary>
    public static string StackName(string project) => ProjectFiles.StackName(project);

    /// <summary>What a project's folder holds.</summary>
    /// <exception cref="FileNotFoundException">There is no such project.</exception>
    public ProjectContents Contents(string path) => ProjectFiles.Read(RootPath, path);

    /// <summary>Brings a mesh into a project and names it. <see cref="Rescan" /> is the caller's to run.</summary>
    /// <returns>The mesh's file in the project, or null when it still has none.</returns>
    public string? UseMesh(string path, string? mesh) {
        lock (createGate) {
            var used = ProjectFiles.UseMesh(RootPath, path, mesh);
            Interlocked.Increment(ref unscannedWrites);
            return used;
        }
    }

    /// <summary>Names the stack a project paints. <see cref="Rescan" /> is the caller's to run.</summary>
    public void UseStack(string path, string stack) {
        lock (createGate) {
            ProjectFiles.UseStack(RootPath, path, stack);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    /// <summary>Gives a project a new display name. <see cref="Rescan" /> is the caller's to run.</summary>
    public void RenameProject(string path, string name) {
        lock (createGate) {
            ProjectFiles.Rename(RootPath, path, name);
            Interlocked.Increment(ref unscannedWrites);
        }
    }

    public ProjectPage Projects(LibraryQuery query) => queries.Projects(query);

    public AssetPage Assets(LibraryQuery query) => queries.Assets(query);

    public TagPage ProjectTags(int offset = 0, int limit = 50) => queries.ProjectTags(offset, limit);

    public TagPage AssetTags(int offset = 0, int limit = 50) => queries.AssetTags(offset, limit);

    public AssetImpact? Impact(AssetId asset, int offset = 0, int limit = 50) => queries.Impact(asset, offset, limit);

    /// <summary>
    ///     The library as <c>GET /api/v1/library</c> reports it. <see cref="Indexing" /> is read before the counts:
    ///     read after them, a scan that ends in between reports old counts as caught up.
    /// </summary>
    public LibraryInfo Describe() {
        var indexing = Indexing;
        return new(
            Document.Id.Value,
            Document.Name,
            RootPath,
            fileSchema,
            Document.TexelDensity,
            Document.Channels,
            Document.DefaultShader,
            Document.ExportTargets,
            queries.CountProjects(),
            queries.CountAssets(),
            indexing
        );
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
