using Microsoft.Data.Sqlite;
using Patina.Contracts;
using System.Collections.Concurrent;

namespace Patina.Library;

/// <summary>What the check can find (plan §5).</summary>
public enum FindingKind {
    /// <summary>A document that does not parse. Whatever it references is invisible until it is fixed.</summary>
    Unreadable,

    /// <summary>A document written by a newer Patina.</summary>
    NewerSchema,

    /// <summary>A document at an old schema, or with keys this schema does not know. It still loads.</summary>
    SchemaDrift,

    /// <summary>Two projects or two assets with one GUID, usually a folder copied by hand.</summary>
    DuplicateId,

    /// <summary>A reference to an asset the library does not hold.</summary>
    MissingReference,

    /// <summary>A pin to bytes that are neither the asset's current ones nor kept under <c>.revisions</c>.</summary>
    MissingRevision,

    /// <summary>A sidecar whose asset file has gone.</summary>
    MissingAssetFile,

    /// <summary>A file under <c>shared/</c> with no sidecar, so nothing can reference it.</summary>
    UnidentifiedFile,

    /// <summary>Assets that depend on each other, which no build key can describe.</summary>
    DependencyCycle,

    /// <summary>A file an output declares as input that does not exist.</summary>
    MissingInput,


    /// <summary>An output with no result under its current key.</summary>
    StaleOutput,

    /// <summary>An output whose key cannot be computed, for one of the reasons above.</summary>
    UnresolvedOutput
}

/// <summary>One thing the check found.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Path">The file or project it concerns, relative to the library.</param>
/// <param name="Detail">The specifics.</param>
public sealed record Finding(FindingKind Kind, string Path, string Detail) {
    /// <summary>Whether the library is wrong, as opposed to merely having work to do or room to tidy.</summary>
    public bool IsError =>
        Kind is not (FindingKind.SchemaDrift
            or FindingKind.StaleOutput
            or FindingKind.UnidentifiedFile);
}

/// <summary>The health of a library, from <c>patina library check</c>.</summary>
/// <param name="Findings">Everything found, sorted, so that two runs on one library compare equal.</param>
/// <param name="Projects">The projects checked.</param>
/// <param name="Assets">The shared assets checked.</param>
/// <param name="Outputs">The outputs checked.</param>
public sealed record CheckReport(IReadOnlyList<Finding> Findings, int Projects, int Assets, int Outputs) {
    public bool HasErrors => Findings.Any(static f => f.IsError);
}

/// <summary>
///     The health check. The checks over references run in SQL. Stale outputs need the outputs' declarations, which
///     the index does not hold, so that part reads every <c>project.yaml</c> that has any: the one whole-library read
///     there is, bounded by batches and only ever asked for by a person or a build.
/// </summary>
sealed class LibraryCheck(string root, LibraryDocumentState document, IndexDatabase database, ToolCatalog tools) {
    const int BatchSize = 256;

    const string MissingReferences = """
                                     SELECT 'projects/' || p.path, r.asset
                                     FROM project_reference r JOIN project p ON p.rid = r.project
                                     WHERE NOT EXISTS(SELECT 1 FROM asset a WHERE a.id = r.asset)
                                     UNION ALL
                                     SELECT a.path, d.target
                                     FROM asset_dependency d JOIN asset a ON a.rid = d.asset
                                     WHERE NOT EXISTS(SELECT 1 FROM asset t WHERE t.id = d.target)
                                     """;

    const string Pins = """
                        SELECT 'projects/' || p.path, r.asset, r.revision, f.hash
                        FROM project_reference r JOIN project p ON p.rid = r.project
                        LEFT JOIN asset a ON a.id = r.asset
                        LEFT JOIN file f ON f.path = a.path
                        WHERE r.mode = 1
                        UNION ALL
                        SELECT o.path, d.target, d.revision, f.hash
                        FROM asset_dependency d JOIN asset o ON o.rid = d.asset
                        LEFT JOIN asset a ON a.id = d.target
                        LEFT JOIN file f ON f.path = a.path
                        WHERE d.mode = 1
                        """;

    const string DuplicateIds = """
                                SELECT 'projects/' || path, id FROM project
                                WHERE id IN (SELECT id FROM project GROUP BY id HAVING count(*) > 1)
                                UNION ALL
                                SELECT path, id FROM asset
                                WHERE id IN (SELECT id FROM asset GROUP BY id HAVING count(*) > 1)
                                """;

    const string UnidentifiedFiles = """
                                     SELECT f.path, '' FROM file f
                                     WHERE f.kind = 2
                                     AND f.path NOT LIKE '%.vxlayers'
                                     AND NOT EXISTS(SELECT 1 FROM asset a WHERE a.path = f.path)
                                     """;

    const string MissingAssetFiles = """
                                     SELECT a.path, a.name FROM asset a
                                     WHERE NOT EXISTS(SELECT 1 FROM file f WHERE f.path = a.path)
                                     """;

    public CheckReport Run(CancellationToken cancellationToken) {
        using var connection = database.Open();
        List<Finding> findings = [..document.Findings];
        AddProblems(connection, findings);
        Add(connection, findings, FindingKind.DuplicateId, DuplicateIds);
        Add(connection, findings, FindingKind.MissingReference, MissingReferences);
        Add(connection, findings, FindingKind.UnidentifiedFile, UnidentifiedFiles);
        Add(connection, findings, FindingKind.MissingAssetFile, MissingAssetFiles);
        AddMissingRevisions(connection, findings);

        var graph = LoadGraph(connection);
        var paths = LoadAssetPaths(connection);
        findings.AddRange(
            graph.FindCycles()
                .Select(id => new Finding(
                        FindingKind.DependencyCycle,
                        paths.GetValueOrDefault(id, string.Empty),
                        $"asset {id}"
                    )
                )
        );

        var projects = Strings(connection, "SELECT path FROM project ORDER BY path");
        var outputs = CheckOutputs(connection, projects, graph, findings, cancellationToken);
        findings.Sort(Compare);
        return new(findings, projects.Count, paths.Count, outputs);
    }

    static int Compare(Finding left, Finding right) {
        var order = left.Kind.CompareTo(right.Kind);
        order = order != 0 ? order : string.CompareOrdinal(left.Path, right.Path);
        return order != 0 ? order : string.CompareOrdinal(left.Detail, right.Detail);
    }

    static void AddProblems(SqliteConnection connection, List<Finding> findings) {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path, code, detail FROM problem";
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            var kind = (ProblemCode)reader.GetInt32(1) switch {
                ProblemCode.Unreadable => FindingKind.Unreadable,
                ProblemCode.NewerSchema => FindingKind.NewerSchema,
                _ => FindingKind.SchemaDrift
            };
            findings.Add(new(kind, reader.GetString(0), reader.GetString(2)));
        }
    }

    static void Add(SqliteConnection connection, List<Finding> findings, FindingKind kind, string sql) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            findings.Add(new(kind, reader.GetString(0), reader.GetString(1)));
        }
    }

    void AddMissingRevisions(SqliteConnection connection, List<Finding> findings) {
        using var command = connection.CreateCommand();
        command.CommandText = Pins;
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            var revision = Revision.Stored(reader.GetString(2));
            var current = reader.IsDBNull(3) ? null : reader.GetString(3);
            if (revision.Text != current && !File.Exists(LibraryLayout.RevisionPath(root, revision))) {
                findings.Add(
                    new(FindingKind.MissingRevision, reader.GetString(0), $"asset {reader.GetString(1)} at {revision}")
                );
            }
        }
    }

    internal static AssetGraph LoadGraph(SqliteConnection connection) {
        var dependencies = new Dictionary<long, List<AssetReference>>();
        using (var command = connection.CreateCommand()) {
            command.CommandText = "SELECT asset, target, mode, revision FROM asset_dependency";
            using var reader = command.ExecuteReader();
            while (reader.Read()) {
                var rid = reader.GetInt64(0);
                if (!dependencies.TryGetValue(rid, out var list)) {
                    dependencies[rid] = list = [];
                }

                list.Add(ReadReference(reader));
            }
        }

        return new(ReadNodes(connection, dependencies));
    }

    static List<AssetNode> ReadNodes(SqliteConnection connection, Dictionary<long, List<AssetReference>> dependencies) {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT a.rid, a.id, f.hash
                              FROM asset a LEFT JOIN file f ON f.path = a.path ORDER BY a.path
                              """;
        using var reader = command.ExecuteReader();
        List<AssetNode> nodes = [];
        while (reader.Read()) {
            nodes.Add(
                new(
                    GuidId.Parse<AssetId>(reader.GetString(1)),
                    reader.IsDBNull(2) ? null : Revision.Stored(reader.GetString(2)),
                    dependencies.TryGetValue(reader.GetInt64(0), out var list) ? list : []
                )
            );
        }

        return nodes;
    }

    static AssetReference ReadReference(SqliteDataReader reader) {
        var asset = GuidId.Parse<AssetId>(reader.GetString(1));
        return reader.GetInt32(2) == (int)ReferenceMode.Pinned
            ? AssetReference.Pinned(asset, Revision.Stored(reader.GetString(3)))
            : AssetReference.Floating(asset);
    }

    internal static Dictionary<AssetId, string> LoadAssetPaths(SqliteConnection connection) {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, path FROM asset ORDER BY path DESC";
        using var reader = command.ExecuteReader();
        var paths = new Dictionary<AssetId, string>();
        while (reader.Read()) {
            paths[GuidId.Parse<AssetId>(reader.GetString(0))] = reader.GetString(1);
        }

        return paths;
    }

    static List<string> Strings(SqliteConnection connection, string sql) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        List<string> values = [];
        while (reader.Read()) {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    int CheckOutputs(
        SqliteConnection connection,
        List<string> projects,
        AssetGraph graph,
        List<Finding> findings,
        CancellationToken cancellationToken
    ) {
        var hashes = new InputHashes(root, connection);
        var objects = new ObjectStore(root);
        var found = new ConcurrentBag<Finding>();
        var outputs = 0;
        foreach (var batch in projects.Chunk(BatchSize)) {
            cancellationToken.ThrowIfCancellationRequested();
            Parallel.ForEach(
                batch,
                new() { CancellationToken = cancellationToken },
                path => Interlocked.Add(ref outputs, CheckProject(path, graph, hashes, objects, found))
            );
        }

        hashes.Save(connection);
        findings.AddRange(found);
        return outputs;
    }

    int CheckProject(
        string path,
        AssetGraph graph,
        InputHashes hashes,
        ObjectStore objects,
        ConcurrentBag<Finding> found
    ) {
        var location = $"{LibraryLayout.ProjectsFolder}/{path}";
        ProjectDocument project;
        try {
            var text = SharedFile.ReadAllText(LibraryLayout.Full(root, $"{location}/{LibraryLayout.ProjectFile}"));
            project = DocumentCodec.ReadProject(text, location).Document;
        } catch (Exception e) when (e is IOException or LibraryFormatException) {
            return 0; // changed since the rescan; the next check sees it
        }

        foreach (var output in project.Outputs) {
            Revision? Input(string file) {
                var revision = hashes.Get($"{location}/{file}");
                if (revision is null) {
                    found.Add(new(FindingKind.MissingInput, location, $"output {output.Name} reads {file}"));
                }

                return revision;
            }

            if (OutputKeys.Compute(output, project, Input, graph, tools, hashes.StacksIn(location)) is not { } key) {
                found.Add(new(FindingKind.UnresolvedOutput, location, output.Name));
            } else if (!objects.Contains(key)) {
                found.Add(new(FindingKind.StaleOutput, location, $"{output.Name} wants {key.Hex}"));
            }
        }

        return project.Outputs.Count;
    }

    /// <summary>
    ///     Hashes of project files that outputs read, kept across checks. Meshes and paint packs are large, and the
    ///     rule is the scanner's: a file whose size and time are unchanged is not read again.
    /// </summary>
    internal sealed class InputHashes {
        readonly string root;
        readonly long started = DateTime.UtcNow.Ticks;
        readonly Dictionary<string, FileStamp> known = new(StringComparer.Ordinal);
        readonly ConcurrentDictionary<string, (FileInfo File, string Hash)> seen = new(StringComparer.Ordinal);

        public InputHashes(string root, SqliteConnection connection) {
            this.root = root;
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT path, size, mtime, scanned, hash FROM input_hash";
            using var reader = command.ExecuteReader();
            while (reader.Read()) {
                known[reader.GetString(0)] = new(
                    TrackedKind.Content,
                    reader.GetInt64(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3),
                    reader.GetString(4)
                );
            }
        }

        /// <summary>A text file of the library, or null when it is not there.</summary>
        public string? Text(string path) {
            var file = LibraryLayout.Full(root, path);
            return File.Exists(file) ? SharedFile.ReadAllText(file) : null;
        }

        /// <summary>How a build looks inside the stacks of a project.</summary>
        public StackFiles StacksIn(string location) =>
            new(file => Text($"{location}/{file}"), file => Get($"{location}/{file}"));

        public Revision? Get(string path) {
            var file = new FileInfo(LibraryLayout.Full(root, path));
            if (!file.Exists) {
                return null;
            }

            var hash = known.TryGetValue(path, out var stamp)
                && stamp.StillDescribes(file.Length, file.LastWriteTimeUtc.Ticks)
                    ? stamp.Hash!
                    : Revision.OfFile(file.FullName).Text;
            seen[path] = (file, hash);
            return Revision.Stored(hash);
        }

        /// <summary>Replaces the table with what this check read; files no output reads any more fall out.</summary>
        public void Save(SqliteConnection connection) {
            using var transaction = connection.BeginTransaction();
            using (var clear = connection.CreateCommand()) {
                clear.CommandText = "DELETE FROM input_hash";
                clear.ExecuteNonQuery();
            }

            using var insert = connection.CreateCommand();
            insert.CommandText = """
                                 INSERT INTO input_hash(path, size, mtime, scanned, hash)
                                 VALUES (@path, @size, @mtime, @scanned, @hash)
                                 """;
            var path = insert.Parameters.Add("@path", SqliteType.Text);
            var size = insert.Parameters.Add("@size", SqliteType.Integer);
            var time = insert.Parameters.Add("@mtime", SqliteType.Integer);
            var scanned = insert.Parameters.Add("@scanned", SqliteType.Integer);
            var hash = insert.Parameters.Add("@hash", SqliteType.Text);
            foreach (var (name, (file, value)) in seen) {
                var unchanged = known.TryGetValue(name, out var stamp)
                    && stamp.Hash == value
                    && stamp.StillDescribes(file.Length, file.LastWriteTimeUtc.Ticks);
                path.Value = name;
                size.Value = file.Length;
                time.Value = file.LastWriteTimeUtc.Ticks;
                scanned.Value = unchanged ? stamp.Scanned : started;
                hash.Value = value;
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }
}

/// <summary>What opening <c>library.yaml</c> found that the check should report.</summary>
sealed record LibraryDocumentState(IReadOnlyList<Finding> Findings) {
    public static LibraryDocumentState Of(ReadDocument<LibraryDocument> read) =>
        new(
            LibraryScanner.Drift(read.FileSchema, SchemaChain.Library, read.UnknownKeys) is { } drift
                ? [new(FindingKind.SchemaDrift, LibraryLayout.LibraryFile, drift.Detail)]
                : []
        );
}
