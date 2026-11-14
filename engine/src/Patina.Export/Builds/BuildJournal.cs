using Patina.Contracts;
using Patina.Library;
using System.Text;
using System.Text.Json;

namespace Patina.Export.Builds;

/// <summary>
///     What a build has done, in a folder of its own under <c>.ptcache/reports</c>: an append-only log while it runs,
///     then one file per project and one for the library. A restarted build reads the log, because its second plan no
///     longer shows what the first attempt finished. It is what keeps a step that crashes Workers from looping too: a
///     step started
///     <see cref="MaxStarts" /> times and never ended is reported as failed and passed over.
/// </summary>
public sealed class BuildJournal {
    /// <summary>How many Workers a step may take with it.</summary>
    public const int MaxStarts = 3;

    const string LogFile = "journal.jsonl";
    const string LibraryFile = "library.json";
    const string ProjectsFolder = "projects";

    /// <summary>What a build that ran says when it wrote no file.</summary>
    public const string NoFiles = "The preset reads no map these projects make, so no file was written.";

    readonly string build;
    readonly string log;
    readonly Dictionary<string, int> starts = new(StringComparer.Ordinal);
    readonly HashSet<string> ended = new(StringComparer.Ordinal);
    readonly Dictionary<Guid, Entry> projects = [];
    readonly DateTimeOffset started;

    /// <summary>Opens a build's journal, picking up whatever an earlier attempt left.</summary>
    /// <param name="folder">The build's folder.</param>
    /// <param name="build">The build's name.</param>
    /// <param name="started">When it started, if this is the first anyone has heard of it.</param>
    public BuildJournal(string folder, string build, DateTimeOffset started) {
        Folder = folder;
        this.build = build;
        Directory.CreateDirectory(folder);
        log = Path.Combine(folder, LogFile);
        this.started = started;
        if (File.Exists(log)) {
            foreach (var line in SharedFile.ReadLines(log)) {
                Replay(line);
            }
        }
    }

    /// <summary>The folder holding the reports.</summary>
    public string Folder { get; }

    /// <summary>Whether Workers keep dying on a step. A step can be started again until this says otherwise.</summary>
    public bool Exhausted(string step) => starts.GetValueOrDefault(step) >= MaxStarts;

    /// <summary>
    ///     Says a step is about to go to a Worker. On disk before it goes, since the Worker may never
    ///     answer.
    /// </summary>
    public void Start(string step) {
        starts[step] = starts.GetValueOrDefault(step) + 1;
        Append(new(step, null, null, null, null, null));
    }

    /// <summary>Records how an output came out.</summary>
    public void Ended(string step, ProjectIdentity project, OutputReport output) {
        Note(step, project, output, null);
        Append(new(step, project.Id, project.Name, project.Path, output, null));
    }

    /// <summary>Records how an export came out.</summary>
    public void Ended(string step, ProjectIdentity project, ExportReport export) {
        Note(step, project, null, export);
        Append(new(step, project.Id, project.Name, project.Path, null, export));
    }

    /// <summary>
    ///     Records an output or an export the build found fresh and left alone (#280), so that its report has every
    ///     project it selected and a comparison can tell fresh from not in the build. Kept off the log and never put
    ///     over how a step ended: a restarted build finds an earlier attempt's work fresh, and reports it as done.
    /// </summary>
    /// <param name="project">Whose it is.</param>
    /// <param name="output">The fresh output, or null.</param>
    /// <param name="export">The fresh export, or null.</param>
    public void Fresh(ProjectIdentity project, OutputReport? output, ExportReport? export) {
        ArgumentNullException.ThrowIfNull(project);
        if (projects.TryGetValue(project.Id, out var entry)
            && ((output is not null && entry.Outputs.Exists(o => o.Name == output.Name))
                || (export is not null && entry.Exports.Exists(e => e.Preset == export.Preset)))) {
            return;
        }

        Place(project, output, export);
    }

    /// <summary>What has been done so far.</summary>
    /// <param name="fresh">Outputs nothing touched.</param>
    /// <param name="exportsFresh">Exports nothing touched.</param>
    /// <param name="finished">When it finished, if it has.</param>
    public LibraryReport Totals(int fresh, int exportsFresh, DateTimeOffset? finished = null) {
        // What was fresh is in the reports and not in the totals, which count what the build did.
        var outputs = projects.Values.SelectMany(static p => p.Outputs)
            .Where(static o => o.Status != BuildStatuses.Fresh)
            .ToList();
        var exports = projects.Values.SelectMany(static p => p.Exports)
            .Where(static e => e.Status != BuildStatuses.Fresh)
            .ToList();
        return new() {
            Build = build,
            Started = started,
            Finished = finished,
            Projects = projects.Values.Count(static p => p.Outputs.Exists(static o => o.Status != BuildStatuses.Fresh)
                || p.Exports.Exists(static e => e.Status != BuildStatuses.Fresh)
            ),
            Built = outputs.Count(static o => o.Status == BuildStatuses.Built),
            Reused = outputs.Count(static o => o.Status == BuildStatuses.Reused),
            Fresh = fresh,
            Failed = outputs.Count(static o => o.Status == BuildStatuses.Failed)
                + exports.Count(static e => e.Status == BuildStatuses.Failed),
            Exported = exports.Count(static e => e.Status is BuildStatuses.Built or BuildStatuses.Reused),
            ExportsFresh = exportsFresh,
            Files = exports.Sum(static e => (long)e.Files.Count),
            Folder = Folder
        };
    }

    /// <summary>Writes the reports, which are the log's contents arranged for reading.</summary>
    /// <param name="fresh">Outputs nothing touched.</param>
    /// <param name="exportsFresh">Exports nothing touched.</param>
    /// <param name="nothing">What to say if this wrote no file. Null for a build that was stopped.</param>
    /// <param name="finished">When it finished.</param>
    public LibraryReport Finish(int fresh, int exportsFresh, string? nothing, DateTimeOffset finished) {
        var folder = Path.Combine(Folder, ProjectsFolder);
        Directory.CreateDirectory(folder);
        Parallel.ForEach(
            projects,
            project => {
                var (id, entry) = project;
                LibraryFiles.Replace(
                    Path.Combine(folder, $"{entry.Path.Replace('/', '~')}.{id:N}.json"),
                    JsonSerializer.SerializeToUtf8Bytes(
                        new(id, entry.Name, entry.Path, entry.Outputs, entry.Exports),
                        ContractsJsonContext.Default.ProjectReport
                    )
                );
            }
        );

        var report = Totals(fresh, exportsFresh, finished);
        if (report is { Failed: 0, Files: 0 }) {
            report = report with { Nothing = nothing };
        }

        LibraryFiles.Replace(
            Path.Combine(Folder, LibraryFile),
            JsonSerializer.SerializeToUtf8Bytes(report, ContractsJsonContext.Default.LibraryReport)
        );
        return report;
    }

    /// <summary>A finished build's report for the library, or null.</summary>
    public static LibraryReport? ReadLibrary(string folder) {
        var file = Path.Combine(folder, LibraryFile);
        return File.Exists(file)
            ? JsonSerializer.Deserialize(SharedFile.ReadAllBytes(file), ContractsJsonContext.Default.LibraryReport)
            : null;
    }

    /// <summary>One page of a finished build's project reports, by project path.</summary>
    public static ProjectReportPage ReadProjects(string folder, int offset, int limit) {
        var directory = Path.Combine(folder, ProjectsFolder);
        var files = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal).ToList()
            : [];
        return new(
            files.Count,
            [
                ..files.Skip(offset)
                    .Take(limit)
                    .Select(static file => JsonSerializer.Deserialize(
                            SharedFile.ReadAllBytes(file),
                            ContractsJsonContext.Default.ProjectReport
                        )!
                    )
            ],
            offset,
            limit
        );
    }

    void Replay(string line) {
        BuildLogLine? entry;
        try {
            entry = JsonSerializer.Deserialize(line, ContractsJsonContext.Default.BuildLogLine);
        } catch (JsonException) {
            return; // The tail of a line a dying process did not finish. Its step simply runs again.
        }

        if (entry is null) {
            return;
        }

        if (entry is { Project: { } id, Name: { } name, Path: { } path }) {
            Note(entry.Step, new(id, name, path), entry.Output, entry.Export);
        } else {
            starts[entry.Step] = starts.GetValueOrDefault(entry.Step) + 1;
        }
    }

    void Note(string step, ProjectIdentity project, OutputReport? output, ExportReport? export) {
        ended.Add(step);
        Place(project, output, export);
    }

    void Place(ProjectIdentity project, OutputReport? output, ExportReport? export) {
        if (!projects.TryGetValue(project.Id, out var entry)) {
            projects[project.Id] = entry = new(project.Name, project.Path, [], []);
        }

        if (output is not null) {
            entry.Outputs.RemoveAll(o => o.Name == output.Name);
            entry.Outputs.Add(output);
        }

        if (export is not null) {
            entry.Exports.RemoveAll(e => e.Preset == export.Preset);
            entry.Exports.Add(export);
        }
    }

    void Append(BuildLogLine line) {
        // A crash mid-line leaves a tail with no newline, which would swallow the line after it. A blank line is
        // skipped by a reader; a line glued to a torn one is lost with it.
        var torn = File.Exists(log) && !EndsLine();
        using var stream = new FileStream(log, FileMode.Append, FileAccess.Write, FileShare.Read);
        var compact = JsonSerializer.Serialize(line, BuildLogJson.Options);
        stream.Write(Encoding.UTF8.GetBytes((torn ? "\n" : string.Empty) + compact + "\n"));
    }

    bool EndsLine() {
        using var stream = SharedFile.OpenRead(log);
        if (stream.Length == 0) {
            return true;
        }

        stream.Seek(-1, SeekOrigin.End);
        return stream.ReadByte() == '\n';
    }

    sealed record Entry(string Name, string Path, List<OutputReport> Outputs, List<ExportReport> Exports);
}

/// <summary>Who a journal line is about.</summary>
public sealed record ProjectIdentity(Guid Id, string Name, string Path);

/// <summary>A journal line must be one line, and the contracts' options indent.</summary>
static class BuildLogJson {
    public static readonly JsonSerializerOptions Options = new(ContractsJsonContext.Default.Options) {
        WriteIndented = false
    };
}
