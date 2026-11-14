namespace Patina.Library;

/// <summary>
///     The copies of paint a new UV layout moved, which each project keeps in its <c>.paint-backup</c> folder (#162).
///     Nothing reads them back: they are there for a person to go back to by hand, and grow with every move.
/// </summary>
public static class PaintBackups {
    /// <summary>Removes every project's copies of moved paint.</summary>
    /// <returns>How many folders went, and how many bytes they held.</returns>
    public static (int Folders, long Bytes) Remove(string root) {
        var projects = Path.Combine(root, LibraryLayout.ProjectsFolder);
        if (!Directory.Exists(projects)) {
            return (0, 0);
        }

        var (folders, bytes) = (0, 0L);
        foreach (var backup in Directory.EnumerateDirectories(
                         projects,
                         LibraryLayout.PaintBackupFolder,
                         SearchOption.AllDirectories
                     )
                     .ToList()) {
            var size = FolderSize.Of(backup);
            try {
                Directory.Delete(backup, true);
            } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                continue; // held open: it stays, and the next Clean takes it
            }

            folders++;
            bytes += size;
        }

        return (folders, bytes);
    }
}

/// <summary>How much room a folder takes on disk.</summary>
public static class FolderSize {
    /// <summary>The room a folder takes, counting what goes while it is counted as nothing.</summary>
    public static long Of(string folder) =>
        Directory.Exists(folder)
            ? new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(static file => {
                        try {
                            return file.Length;
                        } catch (FileNotFoundException) {
                            return 0;
                        }
                    }
                )
            : 0;
}
