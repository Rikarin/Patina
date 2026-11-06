namespace Patina.Library;

/// <summary>
///     One Server to a library, held by the operating system, so it ends with the process however that goes. The
///     discovery registry is per user and per machine, and refuses only after start-up has touched the queue. This
///     comes first and also covers a library on a shared drive.
/// </summary>
public sealed class LibraryLock : IDisposable {
    const string FileName = "server.lock";
    readonly FileStream held;

    LibraryLock(FileStream held) {
        this.held = held;
    }

    /// <summary>Takes a library, or says that another process has it.</summary>
    /// <returns>The lock, or null when the library is taken.</returns>
    public static LibraryLock? Take(string root) {
        var cache = LibraryLayout.Full(root, LibraryLayout.CacheFolder);
        Directory.CreateDirectory(cache);
        try {
            return new(new(Path.Combine(cache, FileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        } catch (IOException) {
            return null;
        }
    }

    public void Dispose() => held.Dispose();
}

/// <summary>Clears what a crash leaves behind and what the cache no longer needs. It is all derived.</summary>
public static class CacheSweep {
    const string PartPattern = "*.part";

    /// <summary>What a dead process was part way through. Only safe holding the <see cref="LibraryLock" />.</summary>
    /// <returns>How many it removed.</returns>
    public static int Leftovers(string root) {
        var removed = 0;
        foreach (var folder in new[] {
                     LibraryLayout.CacheFolder, LibraryLayout.Relative(root, LibraryLayout.ExportsPath(root))
                 }) {
            var full = LibraryLayout.Full(root, folder);
            if (Directory.Exists(full)) {
                removed += RemoveParts(full);
            }
        }

        var paint = LibraryLayout.PaintPath(root);
        if (Directory.Exists(paint)) {
            foreach (var session in Directory.EnumerateDirectories(paint)) {
                Remove(session);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Removes built objects and reports not touched for a while. A later build makes them again.</summary>
    /// <returns>How many it removed.</returns>
    public static int Prune(string root, TimeSpan olderThan) {
        var cutoff = DateTime.UtcNow - olderThan;
        var bakes = LibraryLayout.BakesPath(root);
        List<string> folders = [LibraryLayout.ObjectsPath(root), bakes, LibraryLayout.ReportsPath(root)];
        var old = folders.Where(Directory.Exists)
            .SelectMany(static folder => new DirectoryInfo(folder).EnumerateFileSystemInfos())
            .Where(entry => entry.FullName != bakes && entry.LastWriteTimeUtc < cutoff)
            .ToList();
        foreach (var entry in old) {
            Remove(entry.FullName);
        }

        return old.Count;
    }

    static int RemoveParts(string folder) {
        var found = Directory.EnumerateFileSystemEntries(folder, PartPattern, SearchOption.AllDirectories).ToList();
        foreach (var part in found) {
            Remove(part);
        }

        return found.Count;
    }

    /// <summary>Clearing up must never stop a start: what cannot go now goes on the next one.</summary>
    static void Remove(string path) {
        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
            } else if (File.Exists(path)) {
                File.Delete(path);
            }
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            // Something still holds it, which on Windows can be the process that has just died.
        }
    }
}
