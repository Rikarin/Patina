using Microsoft.Data.Sqlite;

namespace Patina.Library;

/// <summary>
///     The names of pictures and uploads each library file holds, kept in the index by the file's size and time (#297),
///     so a Clean reads only the files changed since the last: reading every mesh and document of a library on each
///     run cost seconds and up to 64 MB a file. The index is derived, so a file is simply read again when its row is
///     missing or cannot be trusted, by the rescan's own rule for that.
/// </summary>
sealed class LibraryNameIndex(IndexDatabase database) {
    const char Separator = '\t';

    /// <summary>The names in each file, by its full path; null for a file too large to read.</summary>
    /// <param name="root">The library.</param>
    /// <param name="files">Every file that may name something, by its full path.</param>
    public Dictionary<string, HashSet<LibraryName>?> Read(string root, IReadOnlyList<string> files) {
        var scanned = DateTime.UtcNow.Ticks;
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var known = Known(connection);
        Dictionary<string, HashSet<LibraryName>?> names = new(StringComparer.Ordinal);
        foreach (var file in files) {
            var info = new FileInfo(file);
            if (!info.Exists) {
                continue;
            }

            var path = LibraryLayout.Relative(root, file);
            var (size, time) = (info.Length, info.LastWriteTimeUtc.Ticks);
            if (known.Remove(path, out var row) && row.Stamp.StillDescribes(size, time)) {
                names[file] = row.Names is null ? null : Parse(row.Names);
                continue;
            }

            var read = LibraryNameReader.ReadFile(file);
            names[file] = read;
            Write(connection, path, size, time, scanned, read);
        }

        foreach (var gone in known.Keys) {
            Run(connection, "DELETE FROM named_file WHERE path = $path", ("$path", gone));
        }

        transaction.Commit();
        return names;
    }

    static Dictionary<string, (FileStamp Stamp, string? Names)> Known(SqliteConnection connection) {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path, size, mtime, scanned, names FROM named_file";
        using var reader = command.ExecuteReader();
        Dictionary<string, (FileStamp, string?)> known = new(StringComparer.Ordinal);
        while (reader.Read()) {
            known[reader.GetString(0)] = (
                new(TrackedKind.Content, reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), null),
                reader.IsDBNull(4) ? null : reader.GetString(4));
        }

        return known;
    }

    static void Write(
        SqliteConnection connection,
        string path,
        long size,
        long time,
        long scanned,
        HashSet<LibraryName>? names
    ) =>
        Run(
            connection,
            "INSERT OR REPLACE INTO named_file(path, size, mtime, scanned, names) "
            + "VALUES ($path, $size, $mtime, $scanned, $names)",
            ("$path", path),
            ("$size", size),
            ("$mtime", time),
            ("$scanned", scanned),
            ("$names", names is null ? DBNull.Value : Join(names))
        );

    static string Join(HashSet<LibraryName> names) =>
        string.Join('\n', names.Select(static name => $"{name.Before}{Separator}{name.Name}"));

    static HashSet<LibraryName> Parse(string names) => [
        ..names.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Split(Separator, 2))
            .Where(static parts => parts.Length == 2)
            .Select(static parts => new LibraryName(parts[0], parts[1]))
    ];

    static void Run(SqliteConnection connection, string sql, params (string Name, object Value)[] values) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in values) {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }
}
