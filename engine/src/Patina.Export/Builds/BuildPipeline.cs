using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Jobs;
using Patina.Library;
using System.Diagnostics;

namespace Patina.Export.Builds;

/// <summary>Why a build was refused: it names something that does not exist.</summary>
public sealed class BuildRefusedException : Exception {
    public BuildRefusedException() { }

    public BuildRefusedException(string message) : base(message) { }

    public BuildRefusedException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>A step could not be finished because what was running it went away. The step can run again.</summary>
public sealed class BuildExecutorLostException : Exception {
    public BuildExecutorLostException() { }

    public BuildExecutorLostException(string message) : base(message) { }

    public BuildExecutorLostException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Runs the steps of a build: a Worker for a Server, this process for <c>patina build</c>.</summary>
public interface IBuildExecutor {
    /// <summary>Builds an output into <see cref="OutputStep.Folder" />.</summary>
    /// <exception cref="BuildExecutorLostException">It went away, and the step should run again.</exception>
    Task<BuildManifest> OutputAsync(OutputStep work, CancellationToken cancellationToken);

    /// <summary>Runs a preset into <see cref="ExportStep.Folder" />.</summary>
    /// <exception cref="BuildExecutorLostException">It went away, and the step should run again.</exception>
    Task<ExportManifest> ExportAsync(ExportStep work, CancellationToken cancellationToken);
}

/// <summary>
///     The one pipeline, for the Server's job and for <c>patina build</c>: plan by keys, run what is stale, deliver,
///     report. It keeps nothing a key does not: being stopped anywhere leaves finished objects, and the next plan
///     starts after them. <paramref name="tools" /> are the versions a key carries.
/// </summary>
/// <param name="library">The library.</param>
/// <param name="executor">What runs a step.</param>
/// <param name="tools">The installed tool versions.</param>
public sealed class BuildPipeline(OpenLibrary library, IBuildExecutor executor, ToolCatalog tools) {
    readonly ObjectStore objects = new(library.RootPath);

    /// <summary>Works out what a build would do, running nothing.</summary>
    /// <exception cref="BuildRefusedException">A preset named does not exist.</exception>
    public static BuildPlan Plan(
        OpenLibrary library,
        BuildRequest request,
        ToolCatalog tools,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(request);
        var presets = Presets(library, request);
        var selection = new OutputSelection(
            request.Asset is { } asset ? AssetId.From(asset) : null,
            request.Projects?.Select(ProjectId.From).ToList()
        );
        return BuildPlan.Make(
            library.ScanOutputs(selection, tools, cancellationToken),
            new(library.RootPath),
            [..presets.Select(static p => new PlannedPreset(p.Id, p.Revision))],
            ExportsOf(library, request),
            tools
        );
    }

    /// <summary>Runs a build.</summary>
    /// <param name="journal">Its journal, which may hold an earlier attempt.</param>
    /// <param name="request">What to build.</param>
    /// <param name="progress">Told how far through it is, and how it stands.</param>
    /// <param name="yield">Called between steps, for interactive work to go first.</param>
    /// <param name="clock">Says when it finished.</param>
    /// <param name="cancellationToken">Stops it between steps, and whatever step is running.</param>
    /// <exception cref="BuildRefusedException">A preset named does not exist.</exception>
    /// <exception cref="BuildExecutorLostException">The executor went away. Running it again carries on.</exception>
    public async Task<LibraryReport> RunAsync(
        BuildJournal journal,
        BuildRequest request,
        Action<double, LibraryReport>? progress,
        Func<Task>? yield,
        TimeProvider clock,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(clock);
        var plan = Plan(library, request, tools, cancellationToken);
        var presets = Presets(library, request).ToDictionary(static p => p.Id, StringComparer.Ordinal);
        var fresh = plan.Projects.Sum(static p => p.Fresh);
        var exportsFresh = plan.Projects.Sum(static p => p.Exports.Count(static e => e.Fresh));
        var steps = Math.Max(1, plan.Steps);
        var done = 0;

        foreach (var project in plan.Work) {
            var identity = new ProjectIdentity(project.Project.Id.Value, project.Project.Name, project.Project.Path);
            var failed = false;
            foreach (var output in project.Unresolved) {
                failed = true;
                journal.Ended(
                    $"unresolved:{project.Project.Path}:{output.Output.Name}",
                    identity,
                    new OutputReport(
                        output.Output.Name,
                        output.Output.Tool,
                        null,
                        BuildStatuses.Failed,
                        0,
                        output.Problem
                    )
                );
            }

            foreach (var output in project.Stale) {
                await Yield(yield);
                cancellationToken.ThrowIfCancellationRequested();
                failed |= !await BuildOutputAsync(journal, identity, project, output, cancellationToken);
                done++;
                progress?.Invoke(done / (double)steps, journal.Totals(fresh, exportsFresh));
            }

            foreach (var export in project.Exports.Where(static e => !e.Fresh)) {
                await Yield(yield);
                cancellationToken.ThrowIfCancellationRequested();
                await RunExportAsync(
                    journal,
                    identity,
                    project,
                    export,
                    presets[export.Preset.Id],
                    failed,
                    cancellationToken
                );
                done++;
                progress?.Invoke(done / (double)steps, journal.Totals(fresh, exportsFresh));
            }
        }

        return journal.Finish(fresh, exportsFresh, clock.GetUtcNow());
    }

    /// <summary>Every preset named, or the library's own targets.</summary>
    static List<ExportPreset> Presets(OpenLibrary library, BuildRequest request) {
        var catalog = ExportPresetCatalog.Read(library.RootPath);
        var named = request.Presets ?? library.Document.ExportTargets;
        List<ExportPreset> presets = [];
        foreach (var id in named.Distinct(StringComparer.Ordinal)) {
            presets.Add(
                catalog.Find(id)
                ?? throw new BuildRefusedException(
                    $"There is no preset '{id}'. The presets are "
                    + string.Join(", ", catalog.Presets.Select(static p => p.Id))
                    + "."
                )
            );
        }

        return presets;
    }

    static string ExportsOf(OpenLibrary library, BuildRequest request) =>
        request.Folder ?? LibraryLayout.ExportsPath(library.RootPath);

    static async Task Yield(Func<Task>? yield) {
        if (yield is not null) {
            await yield();
        }
    }

    async Task<bool> BuildOutputAsync(
        BuildJournal journal,
        ProjectIdentity identity,
        PlannedProject project,
        ScannedOutput output,
        CancellationToken cancellationToken
    ) {
        var key = output.Key!.Value;
        var step = $"output:{key.Hex}";
        var declared = output.Output;
        if (objects.Contains(key)) {
            journal.Ended(
                step,
                identity,
                new OutputReport(declared.Name, declared.Tool, key.Hex, BuildStatuses.Reused, 0, null)
            );
            return true;
        }

        if (journal.Exhausted(step)) {
            journal.Ended(
                step,
                identity,
                Failed(declared, key, $"{BuildJournal.MaxStarts} Workers died building this.")
            );
            return false;
        }

        journal.Start(step);
        var timer = Stopwatch.StartNew();
        try {
            var manifest = await executor.OutputAsync(
                new(
                    declared.Tool,
                    [..declared.Files.Select(file => ProjectFile(project, file))],
                    output.Assets,
                    declared.Settings,
                    objects.FolderOf(key),
                    library.RootPath
                ),
                cancellationToken
            );
            BuildObjects.Commit(objects, key, manifest);
            journal.Ended(
                step,
                identity,
                new OutputReport(
                    declared.Name,
                    declared.Tool,
                    key.Hex,
                    BuildStatuses.Built,
                    timer.Elapsed.TotalSeconds,
                    null
                )
            );
            return true;
        } catch (BuildStepFailedException e) {
            journal.Ended(step, identity, Failed(declared, key, e.Message));
            return false;
        }
    }

    async Task RunExportAsync(
        BuildJournal journal,
        ProjectIdentity identity,
        PlannedProject project,
        PlannedExport export,
        ExportPreset preset,
        bool failed,
        CancellationToken cancellationToken
    ) {
        var step = $"export:{export.Preset.Id}:{export.Key.Hex}:{project.Project.Path}";
        if (failed) {
            journal.Ended(step, identity, FailedExport(preset.Id, "An output it reads could not be built."));
            return;
        }

        if (journal.Exhausted(step)) {
            journal.Ended(
                step,
                identity,
                FailedExport(preset.Id, $"{BuildJournal.MaxStarts} Workers died exporting this.")
            );
            return;
        }

        var folder = objects.FolderOf(export.Key);
        var status = BuildStatuses.Reused;
        var manifest = ExportRun.Find(folder);
        try {
            if (manifest is null) {
                status = BuildStatuses.Built;
                journal.Start(step);
                manifest = await executor.ExportAsync(
                    new(PresetText(preset), Sources(project), folder),
                    cancellationToken
                );
            }

            var files = ExportRun.Deliver(
                export.Key.Hex,
                folder,
                manifest,
                export.Target,
                new(project.Project.Name, preset.Id),
                Beside(project)
            );
            journal.Ended(
                step,
                identity,
                new ExportReport(preset.Id, status, export.Target, files, manifest.Skipped, manifest.Warnings, null)
            );
        } catch (BuildStepFailedException e) {
            journal.Ended(step, identity, FailedExport(preset.Id, e.Message));
        }
    }

    static string PresetText(ExportPreset preset) => ExportPresetCatalog.TextOf(preset);

    static OutputReport Failed(OutputDeclaration output, BuildKey key, string why) =>
        new(output.Name, output.Tool, key.Hex, BuildStatuses.Failed, 0, why);

    static ExportReport FailedExport(string preset, string why) =>
        new(preset, BuildStatuses.Failed, null, [], [], [], why);

    string ProjectFile(PlannedProject project, string file) =>
        LibraryLayout.Full(library.RootPath, $"{LibraryLayout.ProjectsFolder}/{project.Project.Path}/{file}");

    /// <summary>A project's other results, under its name: every level of detail goes with every preset.</summary>
    List<(string From, string Name)> Beside(PlannedProject project) {
        var stem = NamingTemplate.Clean(project.Project.Name);
        List<(string, string)> files = [];
        foreach (var output in project.Project.Outputs.Where(static o => o.Key is not null)) {
            var key = output.Key!.Value;
            foreach (var file in BuildObjects.Read(objects, key)?.Files ?? []) {
                files.Add((Path.Combine(objects.FolderOf(key), file), $"{stem}_{file}"));
            }
        }

        return files;
    }

    /// <summary>
    ///     The objects a project's presets read. A bake is the floor and a stack the top: an output that composes the
    ///     others wins a map they share, and a project's own names settle the rest.
    /// </summary>
    List<ExportSource> Sources(PlannedProject project) {
        List<ExportSource> sources = [];
        foreach (var output in project.Project.Outputs
                     .Where(static o => o.Key is not null)
                     .OrderBy(static o => o.Output.Tool switch {
                             BakeKeys.Tool => 0,
                             BuildTools.TextureGraph => 1,
                             _ => 2
                         }
                     )
                     .ThenBy(static o => o.Output.Name, StringComparer.Ordinal)) {
            var key = output.Key!.Value;
            if (BuildObjects.Read(objects, key) is { Maps.Count: > 0 } manifest) {
                sources.Add(new(objects.FolderOf(key), manifest));
            }
        }

        return sources;
    }
}

/// <summary>A step that cannot be done, which running it again would not change.</summary>
public sealed class BuildStepFailedException : Exception {
    public BuildStepFailedException() { }

    public BuildStepFailedException(string message) : base(message) { }

    public BuildStepFailedException(string message, Exception inner) : base(message, inner) { }
}
