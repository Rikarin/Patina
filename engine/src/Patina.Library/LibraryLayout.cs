namespace Patina.Library;

/// <summary>
///     Where things live inside a library folder (plan §5). Paths stored anywhere in the library use forward slashes,
///     so an index or a document means the same on every platform.
/// </summary>
public static class LibraryLayout {
    /// <summary>Where the graphs a graph may hold as nodes live, beside it. Vixen's convention and name.</summary>
    public const string CompoundsFolder = "Compounds";

    /// <summary>The kind a graph's sidecar declares.</summary>
    public const string GraphKind = "graph";

    /// <summary>What a graph file ends in.</summary>
    public const string GraphExtension = ".vxtexgraph";

    public const string LibraryFile = "library.yaml";
    public const string ProjectFile = "project.yaml";
    public const string SharedFolder = "shared";
    public const string ProjectsFolder = "projects";
    public const string CacheFolder = ".ptcache";

    /// <summary>
    ///     The sidecar beside a shared asset. Vixen's formats carry no GUID and a binary alpha cannot, so identity
    ///     lives in a sidecar for every kind of asset instead of in a rule per format:
    ///     <c>rust.vxtexgraph.asset.yaml</c>.
    /// </summary>
    public const string SidecarSuffix = ".asset.yaml";

    /// <summary>
    ///     Old bytes of pinned assets, under <c>shared/</c>. A pin must survive the asset moving on and a deleted
    ///     cache, so these are files of the library, not cache. Hidden, so the scan does not take them for assets.
    /// </summary>
    public const string RevisionsFolder = ".revisions";

    /// <summary>The folders a new library starts with.</summary>
    public static IReadOnlyList<string> SharedFolders { get; } = [
        "graphs", "smart-materials", "brushes", "alphas", "environments", "export-presets"
    ];

    public static string IndexPath(string root) => Path.Combine(root, CacheFolder, "index.sqlite");

    public static string ObjectsPath(string root) => Path.Combine(root, CacheFolder, "objects");

    public static string RevisionPath(string root, Revision revision) =>
        Path.Combine(root, SharedFolder, RevisionsFolder, revision.Hex[..2], revision.Hex);

    /// <summary>Node previews, content-addressed and rebuilt by evaluating (ADR 0005).</summary>
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
