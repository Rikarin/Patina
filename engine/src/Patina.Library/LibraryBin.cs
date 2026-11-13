using Patina.Contracts;
using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace Patina.Library;

/// <summary>
///     Patina's bin: what was deleted in a library, kept until someone empties it, so that a slip costs nothing. It is
///     <c>.trash/</c> in the library folder, beside <c>projects/</c> and not in the cache, because deleting the cache
///     must lose nothing but time. The default <c>.gitignore</c> leaves it out, so the bin is this machine's. Each
///     entry is a folder of its own: <c>bin.yaml</c> says what it was and where it lived, <c>files/</c> holds it, and a
///     document keeps its line of <c>project.yaml</c> beside them, so that it comes back with its identity.
/// </summary>
public static class LibraryBin {
    /// <summary>What a deleted project is.</summary>
    public const string ProjectKind = "project";

    /// <summary>What a deleted document is.</summary>
    public const string DocumentKind = "document";

    const string RecordFile = "bin.yaml";
    const string FilesFolder = "files";
    const string KindKey = "kind";
    const string NameKey = "name";
    const string FromKey = "from";
    const string DeletedKey = "deleted";

    /// <summary>Where the bin is.</summary>
    public static string Folder(string root) => Path.Combine(root, LibraryLayout.TrashFolder);

    /// <summary>Everything in the bin, the latest first, and how much room it takes.</summary>
    public static BinList List(string root) {
        var folder = Folder(root);
        if (!Directory.Exists(folder)) {
            return new([], 0);
        }

        List<BinItem> items = [];
        foreach (var entry in Directory.EnumerateDirectories(folder)) {
            if (Read(entry) is { } item) {
                items.Add(item);
            }
        }

        items.Sort(static (a, b) => b.Deleted.CompareTo(a.Deleted));
        return new(items, Size(folder));
    }

    /// <summary>Moves a project into the bin.</summary>
    /// <param name="root">The library.</param>
    /// <param name="path">The project, below <c>projects/</c>.</param>
    /// <param name="full">Its folder.</param>
    /// <param name="name">Its display name.</param>
    public static BinItem PutProject(string root, string path, string full, string name) {
        var (entry, item) = Open(root, ProjectKind, name, $"{LibraryLayout.ProjectsFolder}/{path}");
        SharedFile.MoveFolder(full, Path.Combine(entry, FilesFolder));
        Write(entry, item);
        return item;
    }

    /// <summary>
    ///     Moves a document's files into the bin, with its line of <c>project.yaml</c>. The caller takes it off the
    ///     project's list.
    /// </summary>
    /// <param name="root">The library.</param>
    /// <param name="path">The project, below <c>projects/</c>.</param>
    /// <param name="full">The project's folder.</param>
    /// <param name="project">The project's document, holding only the one being deleted.</param>
    /// <param name="files">Its files, relative to the project.</param>
    public static BinItem PutDocument(
        string root,
        string path,
        string full,
        ProjectDocument project,
        IReadOnlyList<string> files
    ) {
        var document = project.Documents[0];
        var (entry, item) = Open(
            root,
            DocumentKind,
            document.Name,
            $"{LibraryLayout.ProjectsFolder}/{path}/{document.File}"
        );
        var into = Path.Combine(entry, FilesFolder);
        foreach (var file in files) {
            var from = LibraryLayout.Beside(full, file);
            if (File.Exists(from)) {
                var to = LibraryLayout.Beside(into, file);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Move(from, to);
            }
        }

        LibraryFiles.Replace(Path.Combine(entry, LibraryLayout.ProjectFile), DocumentCodec.Write(project));
        Write(entry, item);
        return item;
    }

    /// <summary>An entry of the bin and its folder, for putting it back.</summary>
    /// <exception cref="FileNotFoundException">There is no such entry.</exception>
    public static (BinItem Item, string Entry) Find(string root, string id) {
        var entry = Entry(root, id);
        return Read(entry) is { } item ? (item, entry) : throw Missing(id);
    }

    /// <summary>The files an entry holds.</summary>
    public static string FilesOf(string entry) => Path.Combine(entry, FilesFolder);

    /// <summary>A deleted document's line of <c>project.yaml</c>.</summary>
    /// <exception cref="LibraryFormatException">It is not there or cannot be read.</exception>
    public static ProjectItem DocumentOf(string entry) {
        var file = Path.Combine(entry, LibraryLayout.ProjectFile);
        if (!File.Exists(file)) {
            throw new LibraryFormatException(file, "a deleted document has lost what it was.", false);
        }

        return DocumentCodec.ReadProject(SharedFile.ReadAllText(file), file).Document.Documents is [var item, ..]
            ? item
            : throw new LibraryFormatException(file, "a deleted document has lost what it was.", false);
    }

    /// <summary>Forgets an entry once what it held is back.</summary>
    public static void Forget(string entry) {
        if (Directory.Exists(entry)) {
            Directory.Delete(entry, true);
        }
    }

    /// <summary>Deletes one entry for good.</summary>
    /// <exception cref="FileNotFoundException">There is no such entry.</exception>
    public static void Delete(string root, string id) {
        var entry = Entry(root, id);
        if (!Directory.Exists(entry)) {
            throw Missing(id);
        }

        Directory.Delete(entry, true);
    }

    /// <summary>Deletes everything in the bin for good.</summary>
    /// <returns>How many entries went.</returns>
    public static int Empty(string root) {
        var folder = Folder(root);
        if (!Directory.Exists(folder)) {
            return 0;
        }

        var entries = Directory.GetDirectories(folder);
        foreach (var entry in entries) {
            Directory.Delete(entry, true);
        }

        foreach (var file in Directory.EnumerateFiles(folder)) {
            File.Delete(file);
        }

        return entries.Length;
    }

    static (string Entry, BinItem Item) Open(string root, string kind, string name, string from) {
        var id = Guid.CreateVersion7().ToString("N");
        var entry = Path.Combine(Folder(root), id);
        Directory.CreateDirectory(entry);
        return (entry, new(id, kind, name, from, DateTimeOffset.UtcNow));
    }

    static string Entry(string root, string id) =>
        Guid.TryParseExact(id, "N", out _) ? Path.Combine(Folder(root), id) : throw Missing(id);

    static FileNotFoundException Missing(string id) => new($"The bin holds nothing called '{id}'.");

    static void Write(string entry, BinItem item) {
        var map = new YamlMappingNode {
            { KindKey, YamlText.Plain(item.Kind) },
            { NameKey, YamlText.Text(item.Name) },
            { FromKey, YamlText.Text(item.From) },
            { DeletedKey, YamlText.Plain(item.Deleted.ToString("O", CultureInfo.InvariantCulture)) }
        };
        LibraryFiles.Replace(Path.Combine(entry, RecordFile), YamlText.Write(map));
    }

    /// <summary>An entry, or null for a folder that is not one: something half moved, or put there by hand.</summary>
    static BinItem? Read(string entry) {
        var file = Path.Combine(entry, RecordFile);
        if (!File.Exists(file)) {
            return null;
        }

        try {
            var fields = new YamlFields(YamlText.Parse(SharedFile.ReadAllText(file), file), file);
            var deleted = DateTimeOffset.Parse(
                fields.Require(DeletedKey),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind
            );
            return new(
                Path.GetFileName(entry),
                fields.Require(KindKey),
                fields.Require(NameKey),
                fields.Require(FromKey),
                deleted
            );
        } catch (Exception e) when (e is LibraryFormatException or FormatException or IOException) {
            return null;
        }
    }

    static long Size(string folder) =>
        new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(static file => file.Length);
}
