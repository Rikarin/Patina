namespace Patina.Library;

/// <summary>
///     Where things live inside a library folder (plan §5). Paths stored anywhere in the library use forward slashes,
///     so an index or a document means the same on every platform.
/// </summary>
public static class LibraryLayout {
    /// <summary>
    ///     A name a library file gives for something beside it. Library files come from anywhere a Git clone does, so a
    ///     name that is rooted or climbs out of its folder is refused, or a crafted stack could overwrite any file.
    /// </summary>
    /// <returns>The full path.</returns>
    /// <summary>Where paint is copied before a new mesh moves it, a folder per move. Nothing reads it.</summary>
    public const string PaintBackupFolder = ".paint-backup";

    /// <summary>What a finished bake leaves beside its maps.</summary>
    public const string BakeManifestFile = "bake.json";

    /// <summary>What a picture the Server drew for a mesh mask is called, which is no picture of the user's.</summary>
    public const string MeshMaskPrefix = "mesh-mask-";

    /// <exception cref="InvalidDataException">The name leaves the folder.</exception>
    public static string Beside(string folder, string name) {
        var root = Path.GetFullPath(folder);
        var full = Path.GetFullPath(name, root);
        if (Path.IsPathRooted(name) || !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
            throw new InvalidDataException($"'{name}' is not a file in its own folder.");
        }

        return full;
    }

    /// <summary>Where the graphs a graph may hold as nodes live, beside it. Vixen's convention and name.</summary>
    public const string CompoundsFolder = "Compounds";

    /// <summary>The kind a graph's sidecar declares.</summary>
    public const string GraphKind = "graph";

    /// <summary>What a graph file ends in.</summary>
    public const string GraphExtension = ".vxtexgraph";

    public const string LibraryFile = "library.yaml";
    public const string ProjectFile = "project.yaml";
    public const string PresetsFolder = "export-presets";
    public const string ProjectsFolder = "projects";

    /// <summary>A folder of projects' own file, beside the projects and folders in it.</summary>
    public const string FolderFile = "folder.yaml";

    public const string CacheFolder = ".ptcache";

    /// <summary>
    ///     Old bytes of pinned assets, under <c>shared/</c>. A pin must survive the asset moving on and a deleted
    ///     cache, so these are files of the library, not cache. Hidden, so the scan does not take them for assets.
    /// </summary>
    public const string RevisionsFolder = ".revisions";

    /// <summary>
    ///     The bin, where a deleted project or document goes so that a slip costs nothing (<see cref="LibraryBin" />).
    ///     Hidden from the scan, and from Git by the default <c>.gitignore</c>.
    /// </summary>
    public const string TrashFolder = ".trash";

    public static string IndexPath(string root) => Path.Combine(root, CacheFolder, "index.sqlite");

    public static string ObjectsPath(string root) => Path.Combine(root, CacheFolder, "objects");

    /// <summary>Where bakes are kept, a folder per build key.</summary>
    public static string BakesPath(string root) => Path.Combine(root, CacheFolder, "objects", "bakes");

    /// <summary>
    ///     Where jobs are kept. Under the cache because a job is a request and not a document: deleting the folder
    ///     loses
    ///     what was asked for and not yet made, which a client can ask again, and nothing that was made.
    /// </summary>
    public static string JobsPath(string root) => Path.Combine(root, CacheFolder, "jobs");

    /// <summary>What builds left: one folder per build, each of them a report and nothing a build depends on.</summary>
    public static string ReportsPath(string root) => Path.Combine(root, CacheFolder, "reports");

    /// <summary>
    ///     Where exports go when a build names no other folder. Not the cache: these are what a game
    ///     takes.
    /// </summary>
    public static string ExportsPath(string root) => Path.Combine(root, "exports");

    public static string RevisionPath(string root, Revision revision) =>
        Path.Combine(root, RevisionsFolder, revision.Hex[..2], revision.Hex);

    /// <summary>Node previews, content-addressed and rebuilt by evaluating (ADR 0005).</summary>
    public static string AutosavePath(string root) => Path.Combine(root, CacheFolder, "autosave");

    public static string PaintPath(string root) => Path.Combine(root, CacheFolder, "paint");

    /// <summary>Where this machine keeps the file a mesh came from, when that is outside the library.</summary>
    public static string MeshSourcePath(string root, AssetId mesh) =>
        Path.Combine(root, CacheFolder, "mesh-sources", mesh.Value.ToString("N"));

    public static string PreviewsPath(string root) => Path.Combine(root, CacheFolder, "previews");

    /// <summary>The full path of a <paramref name="relative" /> path stored with forward slashes.</summary>
    public static string Full(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    ///     The stored form of <paramref name="full" />: relative to <paramref name="root" />, forward slashes.
    /// </summary>
    public static string Relative(string root, string full) =>
        Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/');
}
