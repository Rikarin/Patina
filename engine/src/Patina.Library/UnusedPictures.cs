using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     The pictures in projects' <c>pictures/</c> folders that nothing reads any more (#162): brought in for a layer or
///     a mask since deleted, or left by a material import. A picture counts as read when any file of the library names
///     it, saved or held unsaved in an open document, so this errs towards keeping: a document of its own project
///     names it by <c>pictures/…</c>, and one elsewhere by its project's folder and that. Clean moves what is left
///     into the bin in one entry, so a picture taken by mistake comes back with Restore.
/// </summary>
public static class UnusedPictures {
    /// <summary>The pictures nothing reads, as paths below the library with forward slashes, in order.</summary>
    /// <param name="root">The library.</param>
    /// <param name="open">The open documents' text by their full path, which may name what no file does yet.</param>
    public static IReadOnlyList<string> Find(string root, IReadOnlyDictionary<string, string> open) =>
        Find(root, open, LibraryReaders.ReadAll);

    /// <summary>
    ///     The pictures nothing reads, with the names in the library's files from <paramref name="names" />, such as
    ///     the index's (#297).
    /// </summary>
    internal static IReadOnlyList<string> Find(
        string root,
        IReadOnlyDictionary<string, string> open,
        LibraryNameSource names
    ) {
        ArgumentNullException.ThrowIfNull(open);
        root = Path.GetFullPath(root);
        if (!Directory.Exists(Path.Combine(root, LibraryLayout.ProjectsFolder))) {
            return [];
        }

        var pictures = Pictures(root);
        if (pictures.Count == 0) {
            return [];
        }

        var readers = LibraryReaders.Of(root, open, names);
        return [
            ..pictures.Where(picture => !readers.Exists(reader => Names(reader, picture)))
                .Select(static picture => picture.Below)
                .Order(StringComparer.Ordinal)
        ];
    }

    /// <summary>Moves the pictures nothing reads into the bin, as one entry.</summary>
    /// <param name="root">The library.</param>
    /// <param name="open">The open documents' text by their full path.</param>
    /// <returns>The entry, or null when every picture is read, how many went, and the room they take.</returns>
    public static (BinItem? Item, int Count, long Bytes) Bin(string root, IReadOnlyDictionary<string, string> open) =>
        Bin(root, open, LibraryReaders.ReadAll);

    internal static (BinItem? Item, int Count, long Bytes) Bin(
        string root,
        IReadOnlyDictionary<string, string> open,
        LibraryNameSource names
    ) {
        var found = Find(root, open, names);
        if (found.Count == 0) {
            return (null, 0, 0);
        }

        var (item, bytes) = LibraryBin.PutPictures(root, found);
        return (item, found.Count, bytes);
    }

    /// <summary>Every picture in a project's own pictures folder, with what names it.</summary>
    static List<Picture> Pictures(string root) {
        List<Picture> found = [];
        foreach (var project in LibraryReaders.ProjectFolders(root)) {
            var folder = Path.Combine(project, ProjectFiles.PictureFolder);
            if (!Directory.Exists(folder)) {
                continue;
            }

            foreach (var picture in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                         .Where(PictureFormats.IsPicture)) {
                var own = $"{ProjectFiles.PictureFolder}/{LibraryLayout.Relative(folder, picture)}";
                found.Add(
                    new(
                        LibraryLayout.Relative(root, picture),
                        project,
                        LibraryNameReader.Normal(own),
                        LibraryNameReader.Normal(Path.GetFileName(project))
                    )
                );
            }
        }

        return found;
    }

    /// <summary>Whether a reader names a picture: by its own name in its project, by its project's elsewhere.</summary>
    static bool Names(LibraryReader reader, Picture picture) {
        var mine = reader.Project is { } project && string.Equals(project, picture.Project, StringComparison.Ordinal);
        if (reader.Names is not { } names) {
            return mine || reader.Project is null; // too large to read: what it may reach keeps every picture
        }

        return names.Any(name => name.Name == picture.Own
            && (mine || name.Before.EndsWith(picture.Folder, StringComparison.Ordinal))
        );
    }

    /// <summary>A picture, and the names a file would give it.</summary>
    /// <param name="Below">Its path below the library.</param>
    /// <param name="Project">Its project's folder.</param>
    /// <param name="Own">Its name in its own project, as <see cref="LibraryNameReader" /> writes names.</param>
    /// <param name="Folder">Its project folder's name, which a file elsewhere writes before <paramref name="Own" />.</param>
    sealed record Picture(string Below, string Project, string Own, string Folder);
}

/// <summary>The names in each of a library's files, by full path; null for one too large to read.</summary>
delegate Dictionary<string, HashSet<LibraryName>?> LibraryNameSource(string root, IReadOnlyList<string> files);

/// <summary>A file that may name pictures or uploads.</summary>
/// <param name="Project">The folder of the project it is in, or null outside every project.</param>
/// <param name="Names">What it names, or null when it was too large to read.</param>
sealed record LibraryReader(string? Project, HashSet<LibraryName>? Names);

/// <summary>The files of a library that may name a picture or an upload, and the projects they are in.</summary>
static class LibraryReaders {
    /// <summary>Folders of the library that hold no reader: the cache, the bin, the uploads and Git's own.</summary>
    static readonly string[] Unread = [
        LibraryLayout.CacheFolder, LibraryLayout.TrashFolder, LibraryUploads.Folder, ".git"
    ];

    /// <summary>Reads every file, with no index to remember what each named.</summary>
    public static Dictionary<string, HashSet<LibraryName>?> ReadAll(string root, IReadOnlyList<string> files) =>
        files.ToDictionary(static file => file, LibraryNameReader.ReadFile, StringComparer.Ordinal);

    /// <summary>Every reader: the open documents by what they hold now, and every file by what it holds saved.</summary>
    public static List<LibraryReader> Of(
        string root,
        IReadOnlyDictionary<string, string> open,
        LibraryNameSource names
    ) {
        var projects = ProjectFolders(root);
        List<LibraryReader> readers = [
            ..open.Select(document => new LibraryReader(
                    ProjectOf(projects, document.Key),
                    LibraryNameReader.Read(document.Value)
                )
            )
        ];
        foreach (var (file, named) in names(root, Files(root))) {
            readers.Add(new(ProjectOf(projects, file), named));
        }

        return readers;
    }

    /// <summary>The library's files that are not pictures or paint, outside the cache, the bin, uploads and Git.</summary>
    static List<string> Files(string root) {
        var skipped = Unread.Select(folder => Path.Combine(root, folder) + Path.DirectorySeparatorChar).ToList();
        return [
            ..Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(file => !skipped.Exists(folder => file.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
                .Where(static file => !PictureFormats.IsPicture(file)
                    && !file.EndsWith(".vxpaint", StringComparison.OrdinalIgnoreCase)
                )
        ];
    }

    /// <summary>Every project's folder, the deepest first, so a file is given to the project nearest it.</summary>
    public static List<string> ProjectFolders(string root) {
        var projects = Path.Combine(root, LibraryLayout.ProjectsFolder);
        return Directory.Exists(projects)
            ? [
                ..Directory.EnumerateFiles(projects, LibraryLayout.ProjectFile, SearchOption.AllDirectories)
                    .Select(static file => Path.GetDirectoryName(file)!)
                    .OrderByDescending(static folder => folder.Length)
            ]
            : [];
    }

    static string? ProjectOf(List<string> projects, string file) {
        var full = Path.GetFullPath(file);
        return projects.Find(folder => full.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }
}
