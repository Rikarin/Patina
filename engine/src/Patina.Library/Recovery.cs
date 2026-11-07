using System.Security.Cryptography;
using System.Text;

namespace Patina.Library;

/// <summary>
///     What autosave left for a document. It is in the cache, so deleting that loses only the safety net, and it is on
///     the Server so that every client gets it. A document's own file is never touched until someone says to.
/// </summary>
public static class Recovery {
    const string DocumentFile = "document";

    /// <summary>Where a document's autosave goes. Paint goes beside the text.</summary>
    public static string Folder(string root, string file) {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(LibraryLayout.Relative(root, file)));
        return Path.Combine(LibraryLayout.AutosavePath(root), Convert.ToHexStringLower(hash)[..16]);
    }

    /// <summary>Keeps a document's text, which the paint already beside it belongs to.</summary>
    public static void Keep(string root, string file, string text) =>
        LibraryFiles.Replace(Path.Combine(Folder(root, file), DocumentFile), text);

    /// <summary>When a document was autosaved, if that is later than its last save.</summary>
    public static DateTimeOffset? Newer(string root, string file) {
        var kept = Path.Combine(Folder(root, file), DocumentFile);
        if (!File.Exists(kept)) {
            return null;
        }

        var saved = File.GetLastWriteTimeUtc(kept);
        return !File.Exists(file) || saved > File.GetLastWriteTimeUtc(file)
            ? new DateTimeOffset(saved, TimeSpan.Zero)
            : null;
    }

    /// <summary>Puts what was autosaved in place of the file, and paint beside it.</summary>
    public static void Restore(string root, string file) {
        var folder = Folder(root, file);
        var beside = Path.GetDirectoryName(file)!;
        foreach (var kept in Directory.EnumerateFiles(folder)) {
            var name = Path.GetFileName(kept);
            if (name == DocumentFile) {
                LibraryFiles.Replace(file, File.ReadAllBytes(kept));
            } else {
                File.Copy(kept, LibraryLayout.Beside(beside, name), true);
            }
        }

        Clear(root, file);
    }

    /// <summary>Forgets a document's autosave, as a save or a refusal does.</summary>
    public static void Clear(string root, string file) {
        var folder = Folder(root, file);
        if (Directory.Exists(folder)) {
            Directory.Delete(folder, true);
        }
    }
}
