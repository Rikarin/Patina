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
/// </summary>
public static class Recovery {
    const string DocumentFile = "document";
    const string StampFormat = "D19";

    /// <summary>Where a document's autosaves go, a folder for each.</summary>
    public static string Folder(string root, string file) {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(LibraryLayout.Relative(root, file)));
        return Path.Combine(LibraryLayout.AutosavePath(root), Convert.ToHexStringLower(hash)[..16]);
    }

    /// <summary>A new, empty autosave of a document, for its paint and then its text to go in.</summary>
    public static string Begin(string root, string file) {
        var folder = Folder(root, file);
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
    public static void Keep(string root, string file, string text) => Finish(Begin(root, file), text);

    /// <summary>Lets go of all but the newest <paramref name="keep" /> whole autosaves, and of any cut short.</summary>
    public static void Trim(string root, string file, int keep) {
        var whole = 0;
        foreach (var generation in Generations(Folder(root, file))) {
            if (File.Exists(Path.Combine(generation, DocumentFile)) && whole < keep) {
                whole++;
            } else {
                Directory.Delete(generation, true);
            }
        }
    }

    /// <summary>How many whole autosaves a document has.</summary>
    public static int Count(string root, string file) =>
        Generations(Folder(root, file)).Count(static generation => File.Exists(Path.Combine(generation, DocumentFile)));

    /// <summary>When a document was autosaved last, if that is later than its last save.</summary>
    public static DateTimeOffset? Newer(string root, string file) {
        if (Latest(root, file) is not { } latest) {
            return null;
        }

        var saved = File.GetLastWriteTimeUtc(Path.Combine(latest, DocumentFile));
        return !File.Exists(file) || saved > File.GetLastWriteTimeUtc(file)
            ? new DateTimeOffset(saved, TimeSpan.Zero)
            : null;
    }

    /// <summary>
    ///     Puts the newest autosave in place of the file, its paint beside it. The autosaves stay: the Server lets go of
    ///     them through its autosave gate, as it does every other letting go.
    /// </summary>
    public static void Restore(string root, string file) {
        if (Latest(root, file) is not { } latest) {
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
    public static void Clear(string root, string file, IReadOnlySet<string> sparing) {
        ArgumentNullException.ThrowIfNull(sparing);
        var folder = Folder(root, file);
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

    /// <summary>The newest whole autosave, or null.</summary>
    static string? Latest(string root, string file) =>
        Generations(Folder(root, file))
            .FirstOrDefault(static generation => File.Exists(Path.Combine(generation, DocumentFile)));

    /// <summary>A document's autosaves, the newest first.</summary>
    static IEnumerable<string> Generations(string folder) =>
        Directory.Exists(folder)
            ? Directory.GetDirectories(folder)
                .OrderByDescending(static path => Path.GetFileName(path), StringComparer.Ordinal)
            : [];
}
