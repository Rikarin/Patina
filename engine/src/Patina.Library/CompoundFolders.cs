namespace Patina.Library;

/// <summary>
///     Which folder a graph's compounds come from. Only <see cref="LibraryLayout.CompoundsFolder" /> is read, and not
///     the graph's own folder: every graph in <c>shared/graphs</c> would otherwise be a node in every other's palette,
///     and opening one would read them all.
/// </summary>
public static class CompoundFolders {
    /// <summary>The folder for the graph at <paramref name="graph" />: its own, if it is a compound.</summary>
    public static string Of(string graph) {
        var parent = Path.GetDirectoryName(Path.GetFullPath(graph))!;
        return Enclosing(parent) ?? Path.Combine(parent, LibraryLayout.CompoundsFolder);
    }

    /// <summary>Whether the graph at <paramref name="graph" /> is itself a compound.</summary>
    public static bool Holds(string graph) => Enclosing(Path.GetDirectoryName(Path.GetFullPath(graph))!) is not null;

    /// <summary>
    ///     Writes a new compound. Its file is its node type below <paramref name="folder" />.
    /// </summary>
    /// <param name="folder">The compounds folder.</param>
    /// <param name="type">Its node type's path. The folders in it become folders.</param>
    /// <param name="text">The document.</param>
    /// <returns>The file.</returns>
    /// <exception cref="ProjectRefusedException">The path is not a legal file, or the file is taken.</exception>
    public static string Create(string folder, string type, string text) {
        foreach (var segment in type.Split('/')) {
            NewProjects.Require(segment);
        }

        var file = Path.Combine(folder, type.Replace('/', Path.DirectorySeparatorChar) + LibraryLayout.GraphExtension);
        LibraryFiles.CreateCompound(file, text);
        return file;
    }

    /// <summary>
    ///     The folders a relative picture reference in the graph at <paramref name="graph" /> is read from, in order:
    ///     beside it, then the library it is in, which is <paramref name="library" /> or the one around it.
    /// </summary>
    public static IReadOnlyList<string> PictureRoots(string graph, string? library = null) {
        var parent = Path.GetDirectoryName(Path.GetFullPath(graph))!;
        return (library ?? LibraryAround(parent)) is { } root ? [parent, Path.GetFullPath(root)] : [parent];
    }

    /// <summary>
    ///     How a graph names a picture so that its <see cref="PictureRoots" /> find it: below the first root holding it,
    ///     with forward slashes, which travels with the graph and the library; else its whole path.
    /// </summary>
    /// <param name="roots">The graph's picture roots, in order.</param>
    /// <param name="picture">The picture's file.</param>
    public static string PictureName(IReadOnlyList<string> roots, string picture) {
        ArgumentNullException.ThrowIfNull(roots);
        var full = Path.GetFullPath(picture);
        foreach (var root in roots.Select(Path.GetFullPath)) {
            if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
                return LibraryLayout.Relative(root, full);
            }
        }

        return full;
    }

    static string? LibraryAround(string folder) {
        for (var at = folder; at is not null; at = Path.GetDirectoryName(at)) {
            if (File.Exists(Path.Combine(at, LibraryLayout.LibraryFile))) {
                return at;
            }
        }

        return null;
    }

    /// <summary>The compounds folder around a folder, looking no further up than its project or library.</summary>
    static string? Enclosing(string folder) {
        for (var at = folder; at is not null; at = Path.GetDirectoryName(at)) {
            if (string.Equals(Path.GetFileName(at), LibraryLayout.CompoundsFolder, StringComparison.Ordinal)) {
                return at;
            }

            if (File.Exists(Path.Combine(at, LibraryLayout.ProjectFile))
                || File.Exists(Path.Combine(at, LibraryLayout.LibraryFile))) {
                return null;
            }
        }

        return null;
    }
}
