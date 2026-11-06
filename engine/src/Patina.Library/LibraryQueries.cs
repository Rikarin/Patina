using Microsoft.Data.Sqlite;
using Patina.Contracts;
using System.Globalization;
using System.Text;

namespace Patina.Library;

/// <summary>A page of a project or asset listing.</summary>
/// <param name="Search">Words to find, each as a prefix; all must match. Null lists everything.</param>
/// <param name="Tags">Tags an item must all carry.</param>
/// <param name="Offset">The first item of the page.</param>
/// <param name="Limit">The most items on the page.</param>
/// <param name="Folder">
///     Only what is directly in this folder below <c>projects/</c>, where empty is the top. Null is everywhere.
/// </param>
public sealed record LibraryQuery(
    string? Search = null,
    IReadOnlyList<string>? Tags = null,
    int Offset = 0,
    int Limit = 50,
    string? Folder = null
);

/// <summary>
///     The index's read side. Every query returns one page, so the cost of a query is the cost of a page, not of the
///     library. Listings sort by name; a search sorts by relevance, with a name match worth more than a tag and a tag
///     more than a path.
/// </summary>
sealed class LibraryQueries(IndexDatabase database) {
    const char TagSeparator = '\u001f';

    const string ProjectColumns = """
                                  p.id, p.name, p.path,
                                  (SELECT group_concat(tag, char(31)) FROM project_tag t WHERE t.project = p.rid),
                                  (SELECT count(*) FROM project_reference r WHERE r.project = p.rid)
                                  """;

    const string AssetColumns = """
                                a.id, a.kind, a.name, a.path,
                                (SELECT group_concat(tag, char(31)) FROM asset_tag t WHERE t.asset = a.rid),
                                (SELECT hash FROM file f WHERE f.path = a.path),
                                (SELECT count(*) FROM project_reference r WHERE r.asset = a.id)
                                """;

    /// <summary>
    ///     The chains out of an asset, and who moves when its bytes do. See <see cref="AssetReference" />.
    /// </summary>
    /// <remarks>
    ///     <c>followers</c> are the assets whose key moves: those reading the asset through a floating edge, and
    ///     everything that reads those by any edge, because a pin freezes only the file it names. <c>dependents</c>
    ///     are all that can reach the asset. Dependent projects that no moving key reaches are the ones a pin holds
    ///     back.
    /// </remarks>
    const string ImpactChains = """
                                WITH RECURSIVE
                                followers(id) AS (
                                    SELECT a.id FROM asset_dependency d JOIN asset a ON a.rid = d.asset
                                    WHERE d.target = @asset AND d.mode = 0
                                    UNION
                                    SELECT a.id FROM followers f
                                    JOIN asset_dependency d ON d.target = f.id
                                    JOIN asset a ON a.rid = d.asset
                                ),
                                dependents(id) AS (
                                    SELECT @asset
                                    UNION
                                    SELECT a.id FROM dependents f
                                    JOIN asset_dependency d ON d.target = f.id
                                    JOIN asset a ON a.rid = d.asset
                                ),
                                reached(project) AS (
                                    SELECT DISTINCT r.project FROM project_reference r WHERE r.asset IN dependents
                                ),
                                moved(project) AS (
                                    SELECT r.project FROM project_reference r WHERE r.asset = @asset AND r.mode = 0
                                    UNION
                                    SELECT r.project FROM project_reference r WHERE r.asset IN followers
                                )
                                """;

    public int CountProjects() => Scalar("SELECT count(*) FROM project");

    public int CountAssets() => Scalar("SELECT count(*) FROM asset");

    /// <summary>How many projects are below a folder at any depth. The range walks the index on paths.</summary>
    public int CountBelow(string folder) {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM project WHERE path >= @from AND path < @to";
        command.Parameters.AddWithValue("@from", folder + "/");
        command.Parameters.AddWithValue("@to", folder + "0");
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public ProjectPage Projects(LibraryQuery query) {
        var selection = Selection(query, "project", "p", "project_tag", "project");
        var (items, total) = Listing(query, ProjectColumns, selection, ReadProject);
        return new(items, total, query.Offset, query.Limit);
    }

    public AssetPage Assets(LibraryQuery query) {
        var selection = Selection(query, "asset", "a", "asset_tag", "asset");
        var (items, total) = Listing(query, AssetColumns, selection, ReadAsset);
        return new(items, total, query.Offset, query.Limit);
    }

    /// <summary>One page of a listing, and how many items match on all pages.</summary>
    (List<T> Items, int Total) Listing<T>(
        LibraryQuery query,
        string columns,
        (string From, string Order) selection,
        Func<SqliteDataReader, T> read
    ) {
        using var connection = database.Open();
        var (from, order) = selection;
        var total = Count(connection, query, from);
        List<T> items = [];
        using var command = Command(
            connection,
            query,
            $"SELECT {columns} {from} {order} LIMIT @limit OFFSET @offset"
        );
        Page(command, query);
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            items.Add(read(reader));
        }

        return (items, total);
    }

    public TagPage ProjectTags(int offset, int limit) => Tags("project_tag", offset, limit);

    public TagPage AssetTags(int offset, int limit) => Tags("asset_tag", offset, limit);

    /// <summary>One page of a tag table. Only constants of this class reach the SQL; values are parameters.</summary>
    TagPage Tags(string table, int offset, int limit) {
        using var connection = database.Open();
        var total = Scalar(connection, $"SELECT count(DISTINCT tag) FROM {table}");
        List<TagCount> items = [];
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT tag, count(*) FROM {table} GROUP BY tag ORDER BY tag LIMIT @limit OFFSET @offset";
        command.Parameters.AddWithValue("@limit", limit);
        command.Parameters.AddWithValue("@offset", offset);
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            items.Add(new(reader.GetString(0), reader.GetInt32(1)));
        }

        return new(items, total, offset, limit);
    }

    /// <summary>
    ///     The impact of changing <paramref name="asset" />'s bytes, or null when the index does not know it.
    /// </summary>
    public AssetImpact? Impact(AssetId asset, int offset, int limit) {
        using var connection = database.Open();
        if (FindAsset(connection, asset) is not { } summary) {
            return null;
        }

        var (projects, pinned) = ImpactCounts(connection, asset);
        List<ImpactedProject> items = [];
        using var command = connection.CreateCommand();
        command.CommandText = $"""
                               {ImpactChains}
                               SELECT p.id, p.name, p.path, p.rid NOT IN moved
                               FROM reached JOIN project p ON p.rid = reached.project
                               ORDER BY p.name COLLATE NOCASE, p.path
                               LIMIT @limit OFFSET @offset
                               """;
        command.Parameters.AddWithValue("@asset", asset.ToString());
        command.Parameters.AddWithValue("@limit", limit);
        command.Parameters.AddWithValue("@offset", offset);
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            items.Add(
                new(
                    Guid.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetBoolean(3)
                )
            );
        }

        var unreadable = Scalar(
            connection,
            $"""
             SELECT count(*) FROM problem
             WHERE code IN ({(int)ProblemCode.Unreadable}, {(int)ProblemCode.NewerSchema})
             """
        );
        return new(summary, projects, pinned, unreadable, items, offset, limit);
    }

    /// <summary>The paths of every project that reaches an asset, pins included, for a build that names one.</summary>
    public IReadOnlyList<string> ProjectsReaching(AssetId asset) {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
                               {ImpactChains}
                               SELECT p.path FROM reached JOIN project p ON p.rid = reached.project ORDER BY p.path
                               """;
        command.Parameters.AddWithValue("@asset", asset.ToString());
        using var reader = command.ExecuteReader();
        List<string> paths = [];
        while (reader.Read()) {
            paths.Add(reader.GetString(0));
        }

        return paths;
    }

    /// <summary>The path of each named project the index knows.</summary>
    public IReadOnlyList<string> ProjectPaths(IEnumerable<ProjectId> projects) {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path FROM project WHERE id = @id ORDER BY path LIMIT 1";
        var id = command.Parameters.Add("@id", SqliteType.Text);
        List<string> paths = [];
        foreach (var project in projects) {
            id.Value = project.ToString();
            if (command.ExecuteScalar() is string path) {
                paths.Add(path);
            }
        }

        return paths;
    }

    static AssetSummary? FindAsset(SqliteConnection connection, AssetId asset) {
        using var command = connection.CreateCommand();
        // Two assets sharing an id are a finding of the check; the first by path is the one that answers until then.
        command.CommandText = $"SELECT {AssetColumns} FROM asset a WHERE a.id = @id ORDER BY a.path LIMIT 1";
        command.Parameters.AddWithValue("@id", asset.ToString());
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadAsset(reader) : null;
    }

    static (int Projects, int Pinned) ImpactCounts(SqliteConnection connection, AssetId asset) {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
                               {ImpactChains}
                               SELECT count(*), count(*) FILTER (WHERE project NOT IN moved) FROM reached
                               """;
        command.Parameters.AddWithValue("@asset", asset.ToString());
        using var reader = command.ExecuteReader();
        reader.Read();
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    /// <summary>
    ///     The search text as an FTS5 expression. Every word is quoted, so nothing a user types is syntax, and gets a
    ///     prefix star, so results appear while typing. Null when the text holds no words.
    /// </summary>
    internal static string? MatchExpression(string? search) {
        if (search is null) {
            return null;
        }

        var words = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(static w => new string([..w.Where(static c => c != '"' && !char.IsControl(c))]))
            .Where(static w => w.Length > 0)
            .ToList();
        if (words.Count == 0) {
            return null;
        }

        var expression = new StringBuilder();
        foreach (var word in words) {
            expression.Append('"').Append(word).Append("\"* ");
        }

        return expression.ToString().TrimEnd();
    }

    /// <summary>
    ///     The FROM and ORDER BY of a listing. Only constants of this class reach the SQL; values are parameters.
    /// </summary>
    static (string From, string Order) Selection(
        LibraryQuery query,
        string table,
        string alias,
        string tagTable,
        string tagColumn
    ) {
        List<string> conditions = [];
        for (var i = 0; i < (query.Tags?.Count ?? 0); i++) {
            conditions.Add($"EXISTS(SELECT 1 FROM {tagTable} g WHERE g.{tagColumn} = {alias}.rid AND g.tag = @tag{i})");
        }

        if (query.Folder is { } folder) {
            conditions.Add(
                folder.Length == 0
                    ? $"instr({alias}.path, '/') = 0"
                    : $"{alias}.path >= @from AND {alias}.path < @to "
                    + $"AND instr(substr({alias}.path, length(@from) + 1), '/') = 0"
            );
        }

        if (MatchExpression(query.Search) is null) {
            var filter = conditions.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", conditions)}";
            return ($"FROM {table} {alias}{filter}", $"ORDER BY {alias}.name COLLATE NOCASE, {alias}.path");
        }

        var search = $"{table}_search";
        conditions.Insert(0, $"{search} MATCH @match");
        return (
            $"FROM {search} s JOIN {table} {alias} ON {alias}.rid = s.rowid WHERE {string.Join(" AND ", conditions)}",
            $"ORDER BY bm25({search}, 10.0, 5.0, 1.0), {alias}.path"
        );
    }

    static int Count(SqliteConnection connection, LibraryQuery query, string from) {
        using var command = Command(connection, query, $"SELECT count(*) {from}");
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    static SqliteCommand Command(SqliteConnection connection, LibraryQuery query, string sql) {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        if (MatchExpression(query.Search) is { } match) {
            command.Parameters.AddWithValue("@match", match);
        }

        for (var i = 0; i < (query.Tags?.Count ?? 0); i++) {
            command.Parameters.AddWithValue($"@tag{i}", query.Tags![i]);
        }

        if (query.Folder is { Length: > 0 } folder) {
            command.Parameters.AddWithValue("@from", folder + "/");
            command.Parameters.AddWithValue("@to", folder + "0");
        }

        return command;
    }

    static void Page(SqliteCommand command, LibraryQuery query) {
        command.Parameters.AddWithValue("@limit", query.Limit);
        command.Parameters.AddWithValue("@offset", query.Offset);
    }

    static ProjectSummary ReadProject(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
            reader.GetString(1),
            reader.GetString(2),
            Tags(reader, 3),
            reader.GetInt32(4)
        );

    static AssetSummary ReadAsset(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            Tags(reader, 4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetInt32(6)
        );

    static string[] Tags(SqliteDataReader reader, int column) {
        if (reader.IsDBNull(column)) {
            return [];
        }

        var tags = reader.GetString(column).Split(TagSeparator);
        Array.Sort(tags, StringComparer.Ordinal);
        return tags;
    }

    int Scalar(string sql) {
        using var connection = database.Open();
        return Scalar(connection, sql);
    }

    static int Scalar(SqliteConnection connection, string sql) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
