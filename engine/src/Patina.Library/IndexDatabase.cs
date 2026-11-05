using Microsoft.Data.Sqlite;

namespace Patina.Library;

/// <summary>
///     The index file, <c>.ptcache/index.sqlite</c>. It is derived, so its schema has no migrations: an index of
///     another version, or a damaged one, is thrown away and the next rescan rebuilds it. Each operation opens its
///     own connection, and write-ahead logging lets the browser read while a rescan writes.
/// </summary>
sealed class IndexDatabase : IDisposable {
    /// <summary>
    ///     Raise this whenever the tables change or the scanner would read an unchanged file differently, such as
    ///     when a document gains a migration: the rescan skips unchanged files, so only a rebuild would notice.
    /// </summary>
    const int Version = 1;

    const int NotADatabase = 26;
    const int Corrupt = 11;

    const string Tables = """
                          CREATE TABLE file(
                              path TEXT PRIMARY KEY,
                              kind INTEGER NOT NULL,
                              size INTEGER NOT NULL,
                              mtime INTEGER NOT NULL,
                              scanned INTEGER NOT NULL,
                              hash TEXT
                          ) WITHOUT ROWID;
                          CREATE INDEX file_kind ON file(kind);

                          CREATE TABLE project(
                              rid INTEGER PRIMARY KEY,
                              id TEXT NOT NULL,
                              path TEXT NOT NULL UNIQUE,
                              name TEXT NOT NULL
                          );
                          CREATE INDEX project_id ON project(id);
                          CREATE INDEX project_order ON project(name COLLATE NOCASE, path);

                          CREATE TABLE project_tag(
                              project INTEGER NOT NULL REFERENCES project(rid) ON DELETE CASCADE,
                              tag TEXT NOT NULL,
                              PRIMARY KEY(project, tag)
                          ) WITHOUT ROWID;
                          CREATE INDEX project_tag_by_tag ON project_tag(tag, project);

                          CREATE TABLE project_reference(
                              project INTEGER NOT NULL REFERENCES project(rid) ON DELETE CASCADE,
                              asset TEXT NOT NULL,
                              mode INTEGER NOT NULL,
                              revision TEXT,
                              PRIMARY KEY(project, asset)
                          ) WITHOUT ROWID;
                          CREATE INDEX project_reference_by_asset ON project_reference(asset, mode, project);

                          CREATE TABLE asset(
                              rid INTEGER PRIMARY KEY,
                              id TEXT NOT NULL,
                              path TEXT NOT NULL UNIQUE,
                              kind TEXT NOT NULL,
                              name TEXT NOT NULL
                          );
                          CREATE INDEX asset_id ON asset(id);
                          CREATE INDEX asset_order ON asset(name COLLATE NOCASE, path);

                          CREATE TABLE asset_tag(
                              asset INTEGER NOT NULL REFERENCES asset(rid) ON DELETE CASCADE,
                              tag TEXT NOT NULL,
                              PRIMARY KEY(asset, tag)
                          ) WITHOUT ROWID;
                          CREATE INDEX asset_tag_by_tag ON asset_tag(tag, asset);

                          CREATE TABLE asset_dependency(
                              asset INTEGER NOT NULL REFERENCES asset(rid) ON DELETE CASCADE,
                              target TEXT NOT NULL,
                              mode INTEGER NOT NULL,
                              revision TEXT,
                              PRIMARY KEY(asset, target)
                          ) WITHOUT ROWID;
                          CREATE INDEX asset_dependency_by_target ON asset_dependency(target, mode, asset);

                          CREATE TABLE problem(
                              path TEXT PRIMARY KEY,
                              code INTEGER NOT NULL,
                              detail TEXT NOT NULL
                          ) WITHOUT ROWID;

                          CREATE TABLE input_hash(
                              path TEXT PRIMARY KEY,
                              size INTEGER NOT NULL,
                              mtime INTEGER NOT NULL,
                              scanned INTEGER NOT NULL,
                              hash TEXT NOT NULL
                          ) WITHOUT ROWID;

                          CREATE VIRTUAL TABLE project_search USING fts5(
                              name, tags, path, content='', contentless_delete=1, prefix='2 3'
                          );
                          CREATE VIRTUAL TABLE asset_search USING fts5(
                              name, tags, path, content='', contentless_delete=1, prefix='2 3'
                          );
                          """;

    readonly string path;
    readonly string connectionString;

    public IndexDatabase(string path) {
        this.path = path;
        connectionString = new SqliteConnectionStringBuilder {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = true,
            ForeignKeys = true
        }.ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try {
            Prepare();
        } catch (SqliteException e) when (e.SqliteErrorCode is NotADatabase or Corrupt) {
            Discard();
            Prepare();
        }
    }

    public SqliteConnection Open() {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    public void Dispose() {
        // Pooled connections hold the file open, which would keep the folder from being deleted on Windows.
        using var connection = new SqliteConnection(connectionString);
        SqliteConnection.ClearPool(connection);
    }

    void Prepare() {
        var version = ReadVersion();
        if (version == Version) {
            return;
        }

        if (version != 0) {
            Discard();
        }

        using var connection = Open();
        Run(connection, "PRAGMA journal_mode = WAL");
        using var transaction = connection.BeginTransaction();
        Run(connection, Tables);
        Run(connection, $"PRAGMA user_version = {Version}");
        transaction.Commit();
    }

    int ReadVersion() {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    void Discard() {
        Dispose();
        foreach (var suffix in (string[])[string.Empty, "-wal", "-shm"]) {
            File.Delete(path + suffix);
        }
    }

    static void Run(SqliteConnection connection, string sql) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
