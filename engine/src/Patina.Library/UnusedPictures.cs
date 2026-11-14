using Patina.Contracts;
using System.Text;

namespace Patina.Library;

/// <summary>
///     The pictures in projects' <c>pictures/</c> folders that nothing reads any more (#162): brought in for a layer or
///     a mask since deleted, or left by a material import. A picture counts as read when any file of the library names
///     it, saved or held unsaved in an open document, so this errs towards keeping: a document of its own project
///     names it by <c>pictures/…</c>, and one elsewhere by its project's folder and that. Clean moves what is left
///     into the bin in one entry, so a picture taken by mistake comes back with Restore.
/// </summary>
public static class UnusedPictures {
    /// <summary>The largest file read for names. A project with a larger one keeps all its pictures.</summary>
    const long MostRead = 64L << 20;

    /// <summary>Folders of the library that hold no reader: the cache, the bin and Git's own.</summary>
    static readonly string[] Unread = [LibraryLayout.CacheFolder, LibraryLayout.TrashFolder, ".git"];

    /// <summary>The pictures nothing reads, as paths below the library with forward slashes, in order.</summary>
    /// <param name="root">The library.</param>
    /// <param name="open">The open documents' text by their full path, which may name what no file does yet.</param>
    public static IReadOnlyList<string> Find(string root, IReadOnlyDictionary<string, string> open) {
        ArgumentNullException.ThrowIfNull(open);
        root = Path.GetFullPath(root);
        var projects = Path.Combine(root, LibraryLayout.ProjectsFolder);
        if (!Directory.Exists(projects)) {
            return [];
        }

        var pictures = Pictures(root);
        if (pictures.Count == 0) {
            return [];
        }

        var readers = Readers(root, open);
        return [
            ..pictures.Where(picture => !readers.Any(reader => Names(reader, picture)))
                .Select(static picture => picture.Below)
                .Order(StringComparer.Ordinal)
        ];
    }

    /// <summary>Moves the pictures nothing reads into the bin, as one entry.</summary>
    /// <param name="root">The library.</param>
    /// <param name="open">The open documents' text by their full path.</param>
    /// <returns>The entry, or null when every picture is read, how many went, and the room they take.</returns>
    public static (BinItem? Item, int Count, long Bytes) Bin(string root, IReadOnlyDictionary<string, string> open) {
        var found = Find(root, open);
        if (found.Count == 0) {
            return (null, 0, 0);
        }

        var (item, bytes) = LibraryBin.PutPictures(root, found);
        return (item, found.Count, bytes);
    }

    /// <summary>Every picture in a project's own pictures folder, with what names it.</summary>
    static List<Picture> Pictures(string root) {
        List<Picture> found = [];
        foreach (var project in ProjectFolders(root)) {
            var folder = Path.Combine(project, ProjectFiles.PictureFolder);
            if (!Directory.Exists(folder)) {
                continue;
            }

            foreach (var picture in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                         .Where(PictureFormats.IsPicture)) {
                var own = $"{ProjectFiles.PictureFolder}/{LibraryLayout.Relative(folder, picture)}";
                found.Add(
                    new(LibraryLayout.Relative(root, picture), project, own, $"{Path.GetFileName(project)}/{own}")
                );
            }
        }

        return found;
    }

    /// <summary>Whether a reader names a picture: by its own name in its project, by its project's elsewhere.</summary>
    static bool Names(Reader reader, Picture picture) {
        var mine = reader.Project is { } project && string.Equals(project, picture.Project, StringComparison.Ordinal);
        if (reader.Text is not { } text) {
            return mine || reader.Project is null; // too large to read: what it may reach keeps every picture
        }

        var name = mine ? picture.Own : picture.Elsewhere;
        return Spellings(name).Any(spelling => text.Contains(spelling, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>How a file may write a name: with either slash, and as a link writes a space.</summary>
    static IEnumerable<string> Spellings(string name) =>
        [name, name.Replace('/', '\\'), name.Replace(" ", "%20", StringComparison.Ordinal)];

    /// <summary>Every file that may name a picture, with the project it is in, and the open documents' text.</summary>
    static List<Reader> Readers(string root, IReadOnlyDictionary<string, string> open) {
        var projects = ProjectFolders(root);
        List<Reader> readers = [
            ..open.Select(document => new Reader(ProjectOf(projects, document.Key), document.Value))
        ];
        foreach (var file in Files(root)) {
            var info = new FileInfo(file);
            var text = info.Length > MostRead ? null : Read(file);
            readers.Add(new(ProjectOf(projects, file), text));
        }

        return readers;
    }

    /// <summary>A file's bytes as text, as far as any name in them goes; nothing when it went as it was read.</summary>
    static string Read(string file) {
        try {
            return Encoding.UTF8.GetString(SharedFile.ReadAllBytes(file));
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            return string.Empty;
        }
    }

    /// <summary>The library's files that are not pictures or paint, outside the cache, the bin and Git.</summary>
    static IEnumerable<string> Files(string root) {
        var skipped = Unread.Select(folder => Path.Combine(root, folder) + Path.DirectorySeparatorChar).ToList();
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !skipped.Exists(folder => file.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
            .Where(static file => !PictureFormats.IsPicture(file)
                && !file.EndsWith(".vxpaint", StringComparison.OrdinalIgnoreCase)
            );
    }

    /// <summary>Every project's folder, the deepest first, so a file is given to the project nearest it.</summary>
    static List<string> ProjectFolders(string root) {
        var projects = Path.Combine(root, LibraryLayout.ProjectsFolder);
        return [
            ..Directory.EnumerateFiles(projects, LibraryLayout.ProjectFile, SearchOption.AllDirectories)
                .Select(static file => Path.GetDirectoryName(file)!)
                .OrderByDescending(static folder => folder.Length)
        ];
    }

    static string? ProjectOf(List<string> projects, string file) {
        var full = Path.GetFullPath(file);
        return projects.Find(folder => full.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    /// <summary>A picture, and the names a file would give it.</summary>
    /// <param name="Below">Its path below the library.</param>
    /// <param name="Project">Its project's folder.</param>
    /// <param name="Own">Its name in its own project.</param>
    /// <param name="Elsewhere">Its name from another project or a shared material.</param>
    sealed record Picture(string Below, string Project, string Own, string Elsewhere);

    /// <summary>A file that may name pictures.</summary>
    /// <param name="Project">The folder of the project it is in, or null outside every project.</param>
    /// <param name="Text">What it holds, or null when it was too large to read.</param>
    sealed record Reader(string? Project, string? Text);
}
