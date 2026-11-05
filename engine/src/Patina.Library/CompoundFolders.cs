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
    ///     The folders a relative picture reference in the graph at <paramref name="graph" /> is read from, in order:
    ///     beside it, then the library it is in, which is <paramref name="library" /> or the one around it.
    /// </summary>
    public static IReadOnlyList<string> PictureRoots(string graph, string? library = null) {
        var parent = Path.GetDirectoryName(Path.GetFullPath(graph))!;
        return (library ?? LibraryAround(parent)) is { } root ? [parent, Path.GetFullPath(root)] : [parent];
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
