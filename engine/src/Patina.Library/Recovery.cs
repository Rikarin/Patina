using Patina.Contracts;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Patina.Library;

/// <summary>
///     What autosave left for a document. It is in the cache, so deleting that loses only the safety net, and it is on
///     the Server so that every client gets it. A document's own file is never touched until someone says to. Each
///     autosave is a generation of its own, a folder named by when it began, so the last few are kept and the newest
///     whole one is what a restore brings back: one cut short by a crash is passed over.
///     A person may choose another folder for them (#162), given here as <c>away</c>: new autosaves go there, and those
///     kept in the cache before the choice are still read, offered and let go of.
/// </summary>
public static class Recovery {
    const string DocumentFile = "document";
    const string StampFormat = "D19";

    /// <summary>
    ///     The file in a library's folder of a chosen place that says which library it is, so that its autosaves can go
    ///     back to that library's cache when the person stops choosing a place (#297).
    /// </summary>
    const string LibraryMarker = "library.txt";

    /// <summary>Where a document's autosaves go, a folder for each.</summary>
    /// <param name="root">The library.</param>
    /// <param name="file">The document.</param>
    /// <param name="away">The library's folder of autosaves outside its cache, or null for the cache.</param>
    public static string Folder(string root, string file, string? away = null) {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(LibraryLayout.Relative(root, file)));
        return Path.Combine(away ?? LibraryLayout.AutosavePath(root), Convert.ToHexStringLower(hash)[..16]);
    }

    /// <summary>
    ///     The folder a chosen place keeps one library's autosaves in: its folder's name, so a person finds it, and a
    ///     hash of where it is, so two libraries of one name never share one.
    /// </summary>
    public static string Away(string chosen, string root) {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(full.ToUpperInvariant()));
        return Path.Combine(chosen, $"{Path.GetFileName(full)}-{Convert.ToHexStringLower(hash)[..8]}");
    }

    /// <summary>A new, empty autosave of a document, for its paint and then its text to go in.</summary>
    public static string Begin(string root, string file, string? away = null) {
        if (away is not null) {
            Mark(away, root);
        }

        var folder = Folder(root, file, away);
        var stamp = DateTime.UtcNow.Ticks;
        var generation = Path.Combine(folder, stamp.ToString(StampFormat, CultureInfo.InvariantCulture));
        // Two in one tick are rare; the second goes after the first.
        while (Directory.Exists(generation)) {
            stamp++;
            generation = Path.Combine(folder, stamp.ToString(StampFormat, CultureInfo.InvariantCulture));
        }

        Directory.CreateDirectory(generation);
        return generation;
    }

    /// <summary>
    ///     Finishes an autosave with the document's text, which the paint already in it belongs to. Until this, the
    ///     autosave does not count.
    /// </summary>
    public static void Finish(string generation, string text) =>
        LibraryFiles.Replace(Path.Combine(generation, DocumentFile), text);

    /// <summary>Keeps a document's text as an autosave of its own, for a document that has no paint.</summary>
    public static void Keep(string root, string file, string text, string? away = null) =>
        Finish(Begin(root, file, away), text);

    /// <summary>Lets go of all but the newest <paramref name="keep" /> whole autosaves, and of any cut short.</summary>
    public static void Trim(string root, string file, int keep, string? away = null) {
        var whole = 0;
        foreach (var generation in Generations(root, file, away)) {
            if (File.Exists(Path.Combine(generation, DocumentFile)) && whole < keep) {
                whole++;
            } else {
                Directory.Delete(generation, true);
            }
        }
    }

    /// <summary>How many whole autosaves a document has.</summary>
    public static int Count(string root, string file, string? away = null) =>
        Generations(root, file, away).Count(static generation => File.Exists(Path.Combine(generation, DocumentFile)));

    /// <summary>When a document was autosaved last, if that is later than its last save.</summary>
    public static DateTimeOffset? Newer(string root, string file, string? away = null) {
        if (Latest(root, file, away) is not { } latest) {
            return null;
        }

        var saved = File.GetLastWriteTimeUtc(Path.Combine(latest, DocumentFile));
        return !File.Exists(file) || saved > File.GetLastWriteTimeUtc(file)
            ? new DateTimeOffset(saved, TimeSpan.Zero)
            : null;
    }

    /// <summary>
    ///     Puts the newest autosave in place of the file, its paint beside it. The autosaves stay: the Server lets go
    ///     of them through its autosave gate, as it does every other letting go.
    /// </summary>
    public static void Restore(string root, string file, string? away = null) {
        if (Latest(root, file, away) is not { } latest) {
            return;
        }

        var beside = Path.GetDirectoryName(file)!;
        foreach (var kept in Directory.EnumerateFiles(latest)) {
            var name = Path.GetFileName(kept);
            if (name == DocumentFile) {
                LibraryFiles.Replace(file, SharedFile.ReadAllBytes(kept));
            } else {
                File.Copy(kept, LibraryLayout.Beside(beside, name), true);
            }
        }
    }

    /// <summary>
    ///     Forgets a document's autosaves but those in <paramref name="sparing" />, which are still being written: a
    ///     folder deleted while a Worker writes into it fails with "Directory not empty", and the save asking fails
    ///     with it. Whoever is writing one deletes it when they find it let go.
    /// </summary>
    public static void Clear(string root, string file, IReadOnlySet<string> sparing, string? away = null) {
        ArgumentNullException.ThrowIfNull(sparing);
        foreach (var folder in Folders(root, file, away)) {
            ClearFolder(folder, sparing);
        }
    }

    static void ClearFolder(string folder, IReadOnlySet<string> sparing) {
        if (!Directory.Exists(folder)) {
            return;
        }

        foreach (var generation in Directory.GetDirectories(folder).Where(path => !sparing.Contains(path))) {
            Directory.Delete(generation, true);
        }

        foreach (var stray in Directory.GetFiles(folder)) {
            File.Delete(stray);
        }

        if (!Directory.EnumerateFileSystemEntries(folder).Any()) {
            Directory.Delete(folder);
        }
    }

    /// <summary>
    ///     Moves the autosaves kept in a place the person chose to where autosaves go now (#297), so changing the place
    ///     strands none: each library's folder to the new place, or with no place chosen now, to each library's cache.
    ///     A library's folder that cannot say which library it is stays, as does one whose library is gone; generations
    ///     in <paramref name="sparing" /> are being written and stay too.
    /// </summary>
    /// <param name="from">The place chosen before.</param>
    /// <param name="to">The place chosen now, or null for each library's cache.</param>
    /// <param name="open">The library open now, which knows its folder in <paramref name="from" /> without a marker.</param>
    /// <param name="sparing">The autosaves being written.</param>
    /// <returns>How many autosaves moved.</returns>
    public static int Move(string from, string? to, string? open, IReadOnlySet<string> sparing) {
        ArgumentNullException.ThrowIfNull(sparing);
        if (!Directory.Exists(from) || (to is not null && SamePlace(from, to))) {
            return 0;
        }

        var moved = 0;
        foreach (var library in Directory.GetDirectories(from)) {
            var target = to is not null
                ? Path.Combine(to, Path.GetFileName(library))
                : LibraryOf(library, from, open) is { } root
                    ? LibraryLayout.AutosavePath(root)
                    : null;
            if (target is null) {
                continue;
            }

            moved += MoveLibrary(library, target, to is not null, sparing);
        }

        return moved;
    }

    /// <summary>Moves one library's autosaves, document by document, and lets go of its folder once it is empty.</summary>
    static int MoveLibrary(string library, string target, bool marked, IReadOnlySet<string> sparing) {
        var marker = Path.Combine(library, LibraryMarker);
        if (marked && File.Exists(marker)) {
            Directory.CreateDirectory(target);
            File.Copy(marker, Path.Combine(target, LibraryMarker), true);
        }

        var moved = 0;
        foreach (var document in Directory.GetDirectories(library)) {
            foreach (var generation in Directory.GetDirectories(document).Where(path => !sparing.Contains(path))) {
                var into = Path.Combine(target, Path.GetFileName(document), Path.GetFileName(generation));
                if (!Directory.Exists(into)) {
                    CopyFolder(generation, into);
                    moved++;
                }

                Directory.Delete(generation, true);
            }

            if (!Directory.EnumerateFileSystemEntries(document).Any()) {
                Directory.Delete(document);
            }
        }

        if (Directory.GetDirectories(library).Length == 0) {
            Directory.Delete(library, true);
        }

        return moved;
    }

    /// <summary>
    ///     Copies an autosave's files: a chosen place may be on another drive, where a folder cannot simply be moved.
    /// </summary>
    static void CopyFolder(string from, string to) {
        var partial = to + ".partial";
        Directory.CreateDirectory(partial);
        foreach (var file in Directory.GetFiles(from)) {
            File.Copy(file, Path.Combine(partial, Path.GetFileName(file)), true);
        }

        SharedFile.MoveFolder(partial, to);
    }

    /// <summary>The library a folder of a chosen place keeps autosaves for, while that library is still there.</summary>
    static string? LibraryOf(string library, string from, string? open) {
        var marker = Path.Combine(library, LibraryMarker);
        var root = File.Exists(marker) ? File.ReadAllText(marker).Trim()
            : open is not null && SamePlace(Away(from, open), library) ? open
            : null;
        return root is not null && File.Exists(Path.Combine(root, LibraryLayout.LibraryFile)) ? root : null;
    }

    static bool SamePlace(string one, string other) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(one)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(other)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
        );

    /// <summary>Says which library a folder of a chosen place is for, once.</summary>
    static void Mark(string away, string root) {
        var marker = Path.Combine(away, LibraryMarker);
        if (!File.Exists(marker)) {
            Directory.CreateDirectory(away);
            LibraryFiles.Replace(marker, Path.GetFullPath(root));
        }
    }

    /// <summary>The newest whole autosave, or null.</summary>
    static string? Latest(string root, string file, string? away) =>
        Generations(root, file, away)
            .FirstOrDefault(static generation => File.Exists(Path.Combine(generation, DocumentFile)));

    /// <summary>A document's autosaves wherever they were kept, the newest first.</summary>
    static IEnumerable<string> Generations(string root, string file, string? away) =>
        Folders(root, file, away)
            .Where(Directory.Exists)
            .SelectMany(Directory.GetDirectories)
            .OrderByDescending(static path => Path.GetFileName(path), StringComparer.Ordinal);

    /// <summary>Where a document's autosaves may be: the chosen folder when there is one, and the cache.</summary>
    static IEnumerable<string> Folders(string root, string file, string? away) =>
        away is null ? [Folder(root, file)] : [Folder(root, file, away), Folder(root, file)];
}
