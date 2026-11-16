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
                             path, width, height, maps, coalesce(sets, 0) AS sets
                         FROM file
                         WHERE width IS NOT NULL AND path >= @from AND path < @to
                         """;

    /// <summary>The folder or project directly in the folder that a stack is below.</summary>
    const string ChildSql = "CASE instr(rest, '/') WHEN 0 THEN rest ELSE substr(rest, 1, instr(rest, '/') - 1) END";

    /// <summary>What a folder costs, and each folder or project directly in it, costliest first.</summary>
    /// <param name="folder">The folder below <c>projects/</c>, empty for the whole library.</param>
    public LibraryCosts Costs(string folder) {
        using var connection = database.Open();
        using var command = Command(
            connection,
            folder,
            $"SELECT {ChildSql} AS child, count(*), total(bytes) FROM ({Below}) GROUP BY child ORDER BY 3 DESC, child"
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
    ///     What a folder costs on a platform (#207), each document priced by <paramref name="price" />: a preset's
    ///     compression and sizes are no SQL, so the stacks below the folder are read, one row each, and summed here.
    /// </summary>
    public LibraryCosts Costs(string folder, DocumentPrice price) {
        List<FolderCost> items = [
            ..Priced(folder, price)
                .GroupBy(static row => Child(row.Below), StringComparer.Ordinal)
                .Select(static group => new FolderCost(
                        group.Key,
                        group.Count(),
                        group.Sum(static row => row.Cost.Bytes)
                    )
                )
                .OrderByDescending(static item => item.Bytes)
                .ThenBy(static item => item.Name, StringComparer.Ordinal)
        ];
        return new(folder, items.Sum(static item => item.Documents), items.Sum(static item => item.Bytes), items);
    }

    /// <summary>One page of the documents below a folder over a budget on a platform, costliest first (#207).</summary>
    public DocumentCostPage Over(string folder, long budget, int offset, int limit, DocumentPrice price) {
        var over = Priced(folder, price)
            .Select(static row => row.Cost)
            .Where(cost => cost.Bytes > budget)
            .OrderByDescending(static cost => cost.Bytes)
            .ThenBy(static cost => cost.Path, StringComparer.Ordinal)
            .ToList();
        return new([..over.Skip(offset).Take(limit)], over.Count, offset, limit);
    }

    /// <summary>Each stack below a folder, with the rest of its path after the folder and its price.</summary>
    static IEnumerable<(string Below, DocumentCost Cost)> Priced(
        SqliteConnection connection,
        string folder,
        DocumentPrice price
    ) {
        using var command = Command(connection, folder, $"SELECT rest, path, width, height, maps, sets FROM ({Below})");
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            var (width, height, maps) = (reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
            var size = new TextureSize(width, height, maps) { Sets = reader.GetInt32(5) };
            yield return (reader.GetString(0), new(reader.GetString(1), width, height, maps, price(size)));
        }
    }

    List<(string Below, DocumentCost Cost)> Priced(string folder, DocumentPrice price) {
        ArgumentNullException.ThrowIfNull(price);
        using var connection = database.Open();
        return [..Priced(connection, folder, price)];
    }

    /// <summary>The folder or project directly in the folder asked about, as <see cref="ChildSql" /> reads it.</summary>
    static string Child(string rest) => rest.IndexOf('/', StringComparison.Ordinal) is var slash and >= 0
        ? rest[..slash]
        : rest;

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

/// <summary>What a stack's maps take on a platform, from its size, maps and texture sets (#207).</summary>
public delegate long DocumentPrice(TextureSize size);
