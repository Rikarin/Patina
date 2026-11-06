using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Text;

namespace Patina.Library;

/// <summary>What a rescan did.</summary>
/// <param name="Files">Files the index tracks after the scan.</param>
/// <param name="Hashed">Files whose size or time could not be trusted, so they were read and hashed.</param>
/// <param name="Changed">Files whose content really changed, or which are new.</param>
/// <param name="Removed">Files that have gone.</param>
/// <param name="Elapsed">How long the scan took.</param>
public sealed record ScanResult(int Files, int Hashed, int Changed, int Removed, TimeSpan Elapsed);

enum TrackedKind {
    Project = 0,
    Content = 2
}

/// <summary>Why a document is not, or not quite, in the index.</summary>
enum ProblemCode {
    Unreadable,
    NewerSchema,
    Drift
}

/// <summary>A file as the index last saw it.</summary>
readonly record struct FileStamp(TrackedKind Kind, long Size, long Time, long Scanned, string? Hash) {
    /// <summary>
    ///     A file written just after a scan read it can keep its size and its timestamp, so a matching stamp proves
    ///     nothing unless the file was already old when it was read. Git calls this the racy-clean problem. Two
    ///     seconds covers the coarsest filesystem a library is likely to sit on.
    /// </summary>
    static readonly long RacyWindow = TimeSpan.FromSeconds(2).Ticks;

    public bool StillDescribes(long size, long time) => size == Size && time == Time && time < Scanned - RacyWindow;
}

/// <summary>
///     Brings the index up to the files. A full build is a rescan of an empty index, so there is one code path and
///     the equivalence of the two is a property a test can hold it to. Each file's rows derive from that file alone,
///     which is what lets a changed file be replaced without looking at any other.
/// </summary>
/// <remarks>
///     Unchanged files cost one stat: size and time, then the hash only if those differ. All writes go in one
///     transaction, so an interrupted scan leaves the last good index and readers see no half-way state. Documents
///     are parsed in bounded batches, so a full build never holds the library in memory.
/// </remarks>
sealed class LibraryScanner(string root, IndexDatabase database) {
    const int BatchSize = 1024;

    static readonly EnumerationOptions Visible = new() {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        IgnoreInaccessible = true
    };

    static readonly EnumerationOptions VisibleTree = new() {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true
    };

    static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public ScanResult Rescan(CancellationToken cancellationToken) {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var scanned = DateTime.UtcNow.Ticks;

        using var connection = database.Open();
        Pragma(connection, "PRAGMA synchronous = NORMAL");
        using var transaction = connection.BeginTransaction();
        using var writer = new IndexWriter(connection);

        var known = LoadStamps(connection);
        var found = Walk();
        var seen = found.Select(static f => f.Path).ToHashSet(StringComparer.Ordinal);
        List<string> gone = [..known.Keys.Where(path => !seen.Contains(path))];
        foreach (var path in gone) {
            writer.Remove(path, known[path].Kind);
        }

        var suspects = found
            .Where(f => !(known.TryGetValue(f.Path, out var stamp) && stamp.StillDescribes(f.Size, f.Time)))
            .ToList();
        var changed = 0;
        var removed = gone.Count;
        foreach (var batch in suspects.Chunk(BatchSize)) {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var outcome in ReadBatch(batch, known, cancellationToken)) {
                switch (outcome.Effect) {
                    case Effect.Gone:
                        writer.Remove(outcome.File.Path, outcome.File.Kind);
                        removed++;
                        break;
                    case Effect.Same:
                        writer.Stamp(outcome.File, scanned, outcome.Hash);
                        break;
                    default:
                        writer.Replace(outcome, scanned);
                        changed++;
                        break;
                }
            }
        }

        transaction.Commit();
        return new(found.Count, suspects.Count, changed, removed, clock.Elapsed);
    }

    static Outcome[] ReadBatch(
        ScannedFile[] batch,
        Dictionary<string, FileStamp> known,
        CancellationToken cancellationToken
    ) {
        var outcomes = new Outcome[batch.Length];
        Parallel.For(
            0,
            batch.Length,
            new() { CancellationToken = cancellationToken },
            i => outcomes[i] = Read(batch[i], known.GetValueOrDefault(batch[i].Path).Hash)
        );
        return outcomes;
    }

    static void Pragma(SqliteConnection connection, string sql) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    static Dictionary<string, FileStamp> LoadStamps(SqliteConnection connection) {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path, kind, size, mtime, scanned, hash FROM file";
        using var reader = command.ExecuteReader();
        var stamps = new Dictionary<string, FileStamp>(StringComparer.Ordinal);
        while (reader.Read()) {
            stamps[reader.GetString(0)] = new(
                (TrackedKind)reader.GetInt32(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)
            );
        }

        return stamps;
    }

    List<ScannedFile> Walk() {
        var found = new ConcurrentBag<ScannedFile>();
        var projects = Path.Combine(root, LibraryLayout.ProjectsFolder);
        if (Directory.Exists(projects)) {
            Parallel.ForEach(
                VisibleFolders(projects),
                folder => WalkProjects(folder, found)
            );
        }

        return [..found];
    }

    /// <summary>
    ///     A folder holding <c>project.yaml</c> is a project and is not entered: projects do not nest, and this keeps
    ///     a scan from walking meshes and paint it has no use for.
    /// </summary>
    void WalkProjects(string folder, ConcurrentBag<ScannedFile> found) {
        var document = new FileInfo(Path.Combine(folder, LibraryLayout.ProjectFile));
        if (document.Exists) {
            found.Add(Describe(document, TrackedKind.Project));
            foreach (var file in new DirectoryInfo(folder).EnumerateFiles()
                         .Where(static file => IsDocument(file.Name))) {
                found.Add(Describe(file, TrackedKind.Content));
            }

            return;
        }

        foreach (var child in VisibleFolders(folder)) {
            WalkProjects(child, found);
        }
    }

    /// <summary>
    ///     The folders below <paramref name="folder" /> a scan enters. A leading dot hides a folder only on Unix, so
    ///     the name is checked as well as the attribute: on Windows <c>.revisions</c> would otherwise be scanned.
    /// </summary>
    internal static IEnumerable<string> VisibleFolders(string folder) =>
        Directory.EnumerateDirectories(folder, "*", Visible).Where(static d => !IsDotted(Path.GetFileName(d)));

    /// <summary>What a project can list, whose bytes a build keys on. A mesh or paint beside it is not read here.</summary>
    static bool IsDocument(string name) =>
        name.EndsWith(LibraryLayout.GraphExtension, StringComparison.Ordinal)
        || name.EndsWith(StackInputs.Extension, StringComparison.Ordinal);

    static bool IsDotted(string name) => name.StartsWith('.');

    ScannedFile Describe(FileInfo file, TrackedKind kind) =>
        new(LibraryLayout.Relative(root, file.FullName), file.FullName, kind, file.Length, file.LastWriteTimeUtc.Ticks);

    static Outcome Read(ScannedFile file, string? knownHash) {
        try {
            if (file.Kind == TrackedKind.Content) {
                var revision = Revision.OfFile(file.FullPath).Text;
                return new(file, revision == knownHash ? Effect.Same : Effect.Changed, revision);
            }

            var bytes = File.ReadAllBytes(file.FullPath);
            var hash = Revision.Of(bytes).Text;
            return hash == knownHash ? new(file, Effect.Same, hash) : Parse(file, bytes, hash);
        } catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException) {
            return new(file, Effect.Gone, null); // deleted since the walk; the next scan would agree
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            return new(file, Effect.Changed, null) { Problem = (ProblemCode.Unreadable, e.Message) };
        }
    }

    /// <summary>
    ///     A file that does not parse (a merge conflict, a hand edit) must not stop the scan: it is recorded, and the
    ///     check reports it, while the other 9,999 projects stay usable.
    /// </summary>
    static Outcome Parse(ScannedFile file, byte[] bytes, string hash) {
        try {
            var text = StrictUtf8.GetString(bytes);
            return ProjectOutcome(file, hash, DocumentCodec.ReadProject(text, file.Path));
        } catch (LibraryFormatException e) {
            var code = e.NewerSchema ? ProblemCode.NewerSchema : ProblemCode.Unreadable;
            return new(file, Effect.Changed, hash) { Problem = (code, e.Message) };
        } catch (DecoderFallbackException) {
            return new(file, Effect.Changed, hash) { Problem = (ProblemCode.Unreadable, "it is not UTF-8.") };
        }
    }

    static Outcome ProjectOutcome(ScannedFile file, string hash, ReadDocument<ProjectDocument> read) =>
        new(file, Effect.Changed, hash) {
            Project = read.Document, Problem = Drift(read.FileSchema, SchemaChain.Project, read.UnknownKeys)
        };

    /// <summary>The drift in a document that reads fine: an old schema, or keys the next save would drop.</summary>
    internal static (ProblemCode Code, string Detail)? Drift(
        int schema,
        SchemaChain chain,
        IReadOnlyList<string> unknown
    ) {
        List<string> notes = [];
        if (schema != chain.Current) {
            notes.Add($"schema {schema}, current is {chain.Current}");
        }

        if (unknown.Count > 0) {
            notes.Add($"unknown keys: {string.Join(", ", unknown)}");
        }

        return notes.Count == 0 ? null : (ProblemCode.Drift, string.Join("; ", notes));
    }

    enum Effect {
        Same,
        Changed,
        Gone
    }

    /// <summary>A file the walk found.</summary>
    readonly record struct ScannedFile(string Path, string FullPath, TrackedKind Kind, long Size, long Time);

    /// <summary>What reading a file came to.</summary>
    sealed record Outcome(ScannedFile File, Effect Effect, string? Hash) {
        public ProjectDocument? Project { get; init; }

        public (ProblemCode Code, string Detail)? Problem { get; init; }
    }

    /// <summary>The prepared statements of one scan.</summary>
    sealed class IndexWriter : IDisposable {
        const string PathParameter = "@path";
        const string PrefixParameter = "@prefix";
        const string NameParameter = "@name";

        readonly List<SqliteCommand> commands = [];
        readonly SqliteCommand stampFile;
        readonly SqliteCommand deleteFile;
        readonly SqliteCommand deleteProblem;
        readonly SqliteCommand insertProblem;
        readonly SqliteCommand findProject;
        readonly SqliteCommand deleteProject;
        readonly SqliteCommand deleteProjectSearch;
        readonly SqliteCommand insertProject;
        readonly SqliteCommand insertProjectSearch;
        readonly SqliteCommand insertProjectTag;
        readonly SqliteCommand insertReference;
        readonly SqliteCommand findDocuments;
        readonly SqliteCommand deleteAsset;
        readonly SqliteCommand deleteAssetSearch;
        readonly SqliteCommand insertAsset;
        readonly SqliteCommand insertAssetSearch;
        readonly SqliteCommand insertAssetTag;
        readonly SqliteCommand insertDependency;

        public IndexWriter(SqliteConnection connection) {
            SqliteCommand Prepare(string sql, params string[] parameters) {
                var command = connection.CreateCommand();
                command.CommandText = sql;
                foreach (var parameter in parameters) {
                    command.Parameters.Add(new(parameter, null));
                }

                command.Prepare();
                commands.Add(command);
                return command;
            }

            stampFile = Prepare(
                """
                INSERT INTO file(path, kind, size, mtime, scanned, hash)
                VALUES (@path, @kind, @size, @mtime, @scanned, @hash)
                ON CONFLICT(path) DO UPDATE
                SET kind = @kind, size = @size, mtime = @mtime, scanned = @scanned, hash = @hash
                """,
                PathParameter,
                "@kind",
                "@size",
                "@mtime",
                "@scanned",
                "@hash"
            );
            deleteFile = Prepare("DELETE FROM file WHERE path = @path", PathParameter);
            deleteProblem = Prepare("DELETE FROM problem WHERE path = @path", PathParameter);
            insertProblem = Prepare(
                "INSERT INTO problem(path, code, detail) VALUES (@path, @code, @detail)",
                PathParameter,
                "@code",
                "@detail"
            );
            findProject = Prepare("SELECT rid FROM project WHERE path = @path", PathParameter);
            deleteProject = Prepare("DELETE FROM project WHERE rid = @rid", "@rid");
            deleteProjectSearch = Prepare("DELETE FROM project_search WHERE rowid = @rid", "@rid");
            insertProject = Prepare(
                "INSERT INTO project(id, path, name) VALUES (@id, @path, @name) RETURNING rid",
                "@id",
                PathParameter,
                NameParameter
            );
            insertProjectSearch = Prepare(
                "INSERT INTO project_search(rowid, name, tags, path) VALUES (@rid, @name, @tags, @path)",
                "@rid",
                NameParameter,
                "@tags",
                PathParameter
            );
            insertProjectTag = Prepare(
                "INSERT OR IGNORE INTO project_tag(project, tag) VALUES (@rid, @tag)",
                "@rid",
                "@tag"
            );
            insertReference = Prepare(
                """
                INSERT INTO project_reference(project, asset, mode, revision) VALUES (@rid, @asset, @mode, @revision)
                """,
                "@rid",
                "@asset",
                "@mode",
                "@revision"
            );
            findDocuments = Prepare(
                "SELECT rid FROM asset WHERE substr(path, 1, length(@prefix)) = @prefix",
                PrefixParameter
            );
            deleteAsset = Prepare("DELETE FROM asset WHERE rid = @rid", "@rid");
            deleteAssetSearch = Prepare("DELETE FROM asset_search WHERE rowid = @rid", "@rid");
            insertAsset = Prepare(
                "INSERT INTO asset(id, path, kind, name) VALUES (@id, @path, @kind, @name) RETURNING rid",
                "@id",
                PathParameter,
                "@kind",
                NameParameter
            );
            insertAssetSearch = Prepare(
                "INSERT INTO asset_search(rowid, name, tags, path) VALUES (@rid, @name, @tags, @path)",
                "@rid",
                NameParameter,
                "@tags",
                PathParameter
            );
            insertAssetTag = Prepare(
                "INSERT OR IGNORE INTO asset_tag(asset, tag) VALUES (@rid, @tag)",
                "@rid",
                "@tag"
            );
            insertDependency = Prepare(
                """
                INSERT INTO asset_dependency(asset, target, mode, revision) VALUES (@rid, @target, @mode, @revision)
                """,
                "@rid",
                "@target",
                "@mode",
                "@revision"
            );
        }

        public void Dispose() {
            foreach (var command in commands) {
                command.Dispose();
            }
        }

        public void Stamp(ScannedFile file, long scanned, string? hash) {
            Set(stampFile, PathParameter, file.Path);
            Set(stampFile, "@kind", (int)file.Kind);
            Set(stampFile, "@size", file.Size);
            Set(stampFile, "@mtime", file.Time);
            Set(stampFile, "@scanned", scanned);
            Set(stampFile, "@hash", hash);
            stampFile.ExecuteNonQuery();
        }

        public void Remove(string path, TrackedKind kind) {
            Set(deleteFile, PathParameter, path);
            deleteFile.ExecuteNonQuery();
            ClearDocument(path, kind);
        }

        public void Replace(Outcome outcome, long scanned) {
            var path = outcome.File.Path;
            Stamp(outcome.File, scanned, outcome.Hash);
            ClearDocument(path, outcome.File.Kind);

            if (outcome.Problem is { } problem) {
                Set(insertProblem, PathParameter, path);
                Set(insertProblem, "@code", (int)problem.Code);
                Set(insertProblem, "@detail", problem.Detail);
                insertProblem.ExecuteNonQuery();
            }

            if (outcome.Project is { } project) {
                Insert(ProjectPath(path), project);
                foreach (var item in project.Documents) {
                    Insert(ProjectPath(path), item);
                }
            }
        }

        void ClearDocument(string path, TrackedKind kind) {
            if (kind == TrackedKind.Content) {
                return;
            }

            Set(deleteProblem, PathParameter, path);
            deleteProblem.ExecuteNonQuery();
            DropDocuments(ProjectPath(path));
            Drop(findProject, deleteProject, deleteProjectSearch, ProjectPath(path));
        }

        static void Drop(SqliteCommand find, SqliteCommand delete, SqliteCommand deleteSearch, string path) {
            Set(find, PathParameter, path);
            if (find.ExecuteScalar() is long rid) {
                Set(deleteSearch, "@rid", rid);
                deleteSearch.ExecuteNonQuery();
                Set(delete, "@rid", rid);
                delete.ExecuteNonQuery();
            }
        }

        void Insert(string path, ProjectDocument project) {
            Set(insertProject, "@id", project.Id.ToString());
            Set(insertProject, PathParameter, path);
            Set(insertProject, NameParameter, project.Name);
            var rid = (long)insertProject.ExecuteScalar()!;

            Search(insertProjectSearch, rid, project.Name, project.Tags, path);
            Tags(insertProjectTag, rid, project.Tags);
            References(insertReference, "@asset", rid, project.References);
        }

        /// <summary>A project's documents go with its file, since that is where they are listed.</summary>
        void DropDocuments(string project) {
            Set(findDocuments, PrefixParameter, $"{LibraryLayout.ProjectsFolder}/{project}/");
            List<long> rids = [];
            using (var reader = findDocuments.ExecuteReader()) {
                while (reader.Read()) {
                    rids.Add(reader.GetInt64(0));
                }
            }

            foreach (var rid in rids) {
                Set(deleteAssetSearch, "@rid", rid);
                deleteAssetSearch.ExecuteNonQuery();
                Set(deleteAsset, "@rid", rid);
                deleteAsset.ExecuteNonQuery();
            }
        }

        /// <summary>A texturing document is an end point, which nothing else can use, so it is no asset.</summary>
        void Insert(string project, ProjectItem item) {
            if (item.Kind == DocumentKinds.Texturing) {
                return;
            }

            var path = $"{LibraryLayout.ProjectsFolder}/{project}/{item.File}";
            Set(insertAsset, "@id", item.Id.ToString());
            Set(insertAsset, PathParameter, path);
            Set(insertAsset, "@kind", item.Role ?? item.Kind);
            Set(insertAsset, NameParameter, item.Name);
            var rid = (long)insertAsset.ExecuteScalar()!;

            Search(insertAssetSearch, rid, item.Name, item.Tags, path);
            Tags(insertAssetTag, rid, item.Tags);
            References(insertDependency, "@target", rid, item.Dependencies);
        }

        static void Search(SqliteCommand insert, long rid, string name, IReadOnlyList<string> tags, string path) {
            Set(insert, "@rid", rid);
            Set(insert, NameParameter, name);
            Set(insert, "@tags", string.Join(' ', tags));
            Set(insert, PathParameter, path);
            insert.ExecuteNonQuery();
        }

        static void Tags(SqliteCommand insert, long rid, IReadOnlyList<string> tags) {
            Set(insert, "@rid", rid);
            foreach (var tag in tags) {
                Set(insert, "@tag", tag);
                insert.ExecuteNonQuery();
            }
        }

        static void References(
            SqliteCommand insert,
            string target,
            long rid,
            IReadOnlyList<AssetReference> references
        ) {
            Set(insert, "@rid", rid);
            foreach (var reference in references) {
                Set(insert, target, reference.Asset.ToString());
                Set(insert, "@mode", (int)reference.Mode);
                Set(insert, "@revision", reference.Revision?.Text);
                insert.ExecuteNonQuery();
            }
        }

        static void Set(SqliteCommand command, string parameter, object? value) =>
            command.Parameters[parameter].Value = value ?? DBNull.Value;

        static string ProjectPath(string file) =>
            file[(LibraryLayout.ProjectsFolder.Length + 1)..^(LibraryLayout.ProjectFile.Length + 1)];
    }
}
