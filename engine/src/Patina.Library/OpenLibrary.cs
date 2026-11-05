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

    public ProjectPage Projects(LibraryQuery query) => queries.Projects(query);

    public AssetPage Assets(LibraryQuery query) => queries.Assets(query);

    public TagPage ProjectTags(int offset = 0, int limit = 50) => queries.ProjectTags(offset, limit);

    public TagPage AssetTags(int offset = 0, int limit = 50) => queries.AssetTags(offset, limit);

    public AssetImpact? Impact(AssetId asset, int offset = 0, int limit = 50) => queries.Impact(asset, offset, limit);

    /// <summary>The library as <c>GET /api/v1/library</c> reports it.</summary>
    public LibraryInfo Describe() =>
        new(
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
            Indexing
        );

    /// <summary>Rescans, then reports the library's health.</summary>
    /// <param name="tools">The installed tool versions, or null for <see cref="ToolCatalog.Default" />.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    public CheckReport Check(ToolCatalog? tools = null, CancellationToken cancellationToken = default) {
        Rescan(cancellationToken);
        return new LibraryCheck(RootPath, state, database, tools ?? ToolCatalog.Default).Run(cancellationToken);
    }

    public void Dispose() => database.Dispose();
}
