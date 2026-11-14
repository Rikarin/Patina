using Patina.Contracts;
using System.Globalization;

namespace Patina.Library;

/// <summary>
///     The files in the library's uploads that nothing names any more (#297). Everything that takes an upload copies
///     it, a project's mesh or a picture brought into a project, so once that is done only a file that names the
///     upload's path still needs it: a project whose mesh came from it, or a bake's high mesh. A file counts as named
///     when any file of the library or an open document holds its path below the library, as a full path does; a file
///     outside every project too large to read keeps them all. One sent in the last hour is taken to be about to be
///     used, by a client that sent it a moment ago and has not yet named it.
/// </summary>
static class UnusedUploads {
    /// <summary>How long an upload counts as just sent, and so as used.</summary>
    static readonly TimeSpan Fresh = TimeSpan.FromHours(1);

    /// <summary>The uploads nothing names, as paths below the library with forward slashes, in order.</summary>
    /// <param name="root">The library.</param>
    /// <param name="open">The open documents' text by their full path.</param>
    /// <param name="names">The names in the library's files.</param>
    public static IReadOnlyList<UnusedFile> Find(
        string root,
        IReadOnlyDictionary<string, string> open,
        LibraryNameSource names
    ) {
        ArgumentNullException.ThrowIfNull(open);
        root = Path.GetFullPath(root);
        var folder = Path.Combine(root, LibraryUploads.Folder);
        if (!Directory.Exists(folder)) {
            return [];
        }

        var sent = DateTime.UtcNow - Fresh;
        var uploads = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(static file => LibraryUploads.Takes(Path.GetFileName(file)))
            .Where(file => File.GetLastWriteTimeUtc(file) < sent)
            .Select(file => LibraryLayout.Relative(root, file))
            .ToList();
        if (uploads.Count == 0) {
            return [];
        }

        var readers = LibraryReaders.Of(root, open, names);
        if (readers.Exists(static reader => reader is { Project: null, Names: null })) {
            return [];
        }

        HashSet<string> named = [
            ..readers.SelectMany(static reader => reader.Names ?? []).Select(static name => name.Name)
        ];
        return [
            ..uploads.Where(upload => !named.Contains(LibraryNameReader.Normal(upload)))
                .Order(StringComparer.Ordinal)
                .Select(upload => new UnusedFile(upload, new FileInfo(LibraryLayout.Beside(root, upload)).Length))
        ];
    }

    /// <summary>Moves the uploads nothing names to the bin, as one entry, and lets go of emptied folders.</summary>
    /// <returns>The entry, or null when every upload is named, how many went, and the room they take.</returns>
    public static (BinItem? Item, int Count, long Bytes) Bin(
        string root,
        IReadOnlyDictionary<string, string> open,
        LibraryNameSource names
    ) {
        var found = Find(root, open, names);
        if (found.Count == 0) {
            return (null, 0, 0);
        }

        var (item, bytes) = LibraryBin.PutFiles(
            root,
            LibraryBin.UploadsKind,
            found.Count == 1
                ? Path.GetFileName(found[0].Path)
                : string.Create(CultureInfo.InvariantCulture, $"{found.Count} uploads nothing used"),
            LibraryUploads.Folder,
            [..found.Select(static file => file.Path)]
        );
        foreach (var upload in found) {
            var digest = Path.GetDirectoryName(LibraryLayout.Beside(root, upload.Path))!;
            if (Directory.Exists(digest) && !Directory.EnumerateFileSystemEntries(digest).Any()) {
                Directory.Delete(digest);
            }
        }

        return (item, found.Count, bytes);
    }
}
