using Microsoft.Data.Sqlite;
using Patina.Contracts;
using System.Globalization;

namespace Patina.Library;

/// <summary>
///     What the library's documents cost (#207), from what the scan keeps of each stack: its size and how many maps it
///     makes. Every answer is an aggregate or a page, so no query reads a document or walks the library.
/// </summary>
sealed class LibraryCostQueries(IndexDatabase database) {
    /// <summary>The stacks below the folder, each with the rest of its path after the folder and its cost.</summary>
    const string Below = """
                         SELECT substr(path, length(@from) + 1) AS rest, width * height * maps * 4 AS bytes,
                             path, width, height, maps
                         FROM file
                         WHERE width IS NOT NULL AND path >= @from AND path < @to
                         """;

    /// <summary>The folder or project directly in the folder that a stack is below.</summary>
    const string Child = "CASE instr(rest, '/') WHEN 0 THEN rest ELSE substr(rest, 1, instr(rest, '/') - 1) END";

    /// <summary>What a folder costs, and each folder or project directly in it, costliest first.</summary>
    /// <param name="folder">The folder below <c>projects/</c>, empty for the whole library.</param>
    public LibraryCosts Costs(string folder) {
        using var connection = database.Open();
        using var command = Command(
            connection,
            folder,
            $"SELECT {Child} AS child, count(*), total(bytes) FROM ({Below}) GROUP BY child ORDER BY 3 DESC, child"
        );
        List<FolderCost> items = [];
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            items.Add(new(reader.GetString(0), reader.GetInt32(1), (long)reader.GetDouble(2)));
        }

        return new(folder, items.Sum(static item => item.Documents), items.Sum(static item => item.Bytes), items);
    }

    /// <summary>One page of the documents below a folder that cost more than a budget, costliest first.</summary>
    public DocumentCostPage Over(string folder, long budget, int offset, int limit) {
        using var connection = database.Open();
        using var count = Command(connection, folder, $"SELECT count(*) FROM ({Below}) WHERE bytes > @budget");
        count.Parameters.AddWithValue("@budget", budget);
        var total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);

        using var command = Command(
            connection,
            folder,
            $"""
             SELECT path, width, height, maps, bytes FROM ({Below})
             WHERE bytes > @budget
             ORDER BY bytes DESC, path
             LIMIT @limit OFFSET @offset
             """
        );
        command.Parameters.AddWithValue("@budget", budget);
        command.Parameters.AddWithValue("@limit", limit);
        command.Parameters.AddWithValue("@offset", offset);
        List<DocumentCost> items = [];
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            items.Add(Read(reader));
        }

        return new(items, total, offset, limit);
    }

    static DocumentCost Read(SqliteDataReader reader) =>
        new(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt64(4));

    /// <summary>
    ///     A command over the stacks below a folder, with the range <see cref="Below" /> reads: the paths below the
    ///     folder share its prefix, so the range walks the key. Only constants of this class reach the SQL.
    /// </summary>
    static SqliteCommand Command(SqliteConnection connection, string folder, string sql) {
        var inner = folder.Trim('/');
        var prefix = inner.Length > 0
            ? $"{LibraryLayout.ProjectsFolder}/{inner}/"
            : $"{LibraryLayout.ProjectsFolder}/";
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@from", prefix);
        command.Parameters.AddWithValue("@to", prefix[..^1] + "0");
        return command;
    }
}
