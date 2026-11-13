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
    /// <param name="work">The step.</param>
    /// <param name="progress">Told how far through the step it is, and the file it is on, or null.</param>
    /// <param name="cancellationToken">Stops it.</param>
    /// <exception cref="BuildExecutorLostException">It went away, and the step should run again.</exception>
    Task<ExportManifest> ExportAsync(
        ExportStep work,
        Action<double, string>? progress,
        CancellationToken cancellationToken
    );
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
        return Plan(library, request, new(library, request), tools, cancellationToken);
    }

    static BuildPlan Plan(
        OpenLibrary library,
        BuildRequest request,
        BuildPresets presets,
        ToolCatalog tools,
        CancellationToken cancellationToken
    ) {
        if (request.Documents is { Count: > 0 } && request.Projects is not { Count: 1 }) {
            throw new BuildRefusedException("Documents are exported from one project at a time; name the project.");
        }

        var selection = new OutputSelection(
            request.Asset is { } asset ? AssetId.From(asset) : null,
            request.Projects?.Select(ProjectId.From).ToList()
        ) { Documents = request.Documents };
        var scan = library.ScanOutputs(selection, tools, cancellationToken);
        presets.Refuse(scan.Projects.Select(static p => p.Path));
        return BuildPlan.Make(
            scan,
            new(library.RootPath),
            project => presets.PlannedFor(project.Path),
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
        var presets = new BuildPresets(library, request);
        var plan = Plan(library, request, presets, tools, cancellationToken);
        var fresh = plan.Projects.Sum(static p => p.Fresh);
        var exportsFresh = plan.Projects.Sum(static p => p.Exports.Count(static e => e.Fresh));
        var steps = Math.Max(1, plan.Steps);
        var done = 0;

        try {
            await RunPlanAsync();
        } catch (Exception e) when (e is OperationCanceledException or IOException or UnauthorizedAccessException) {
            // However it stopped, what it finished is reported. A Worker going away is no end: the job comes back.
            journal.Finish(fresh, exportsFresh, null, clock.GetUtcNow());
            throw;
        }

        return journal.Finish(fresh, exportsFresh, plan.Nothing ?? BuildJournal.NoFiles, clock.GetUtcNow());

        // What a client shows while a step runs: the totals so far, and what is being made now.
        void Starting(string what) =>
            progress?.Invoke(done / (double)steps, journal.Totals(fresh, exportsFresh) with { Current = what });

        // How far through an export step, and the file it is on, which the executor tells.
        Action<double, string> Within(int before, string what) =>
            (fraction, file) => progress?.Invoke(
                (before + fraction) / steps,
                journal.Totals(fresh, exportsFresh) with { Current = file.Length > 0 ? $"{what}: {file}" : what }
            );

        async Task RunPlanAsync() {
            foreach (var project in plan.Work) {
                var identity = new ProjectIdentity(
                    project.Project.Id.Value,
                    project.Project.Name,
                    project.Project.Path
                );
                var failed = Unresolved(journal, identity, project);

                foreach (var output in project.Stale) {
                    await Yield(yield);
                    cancellationToken.ThrowIfCancellationRequested();
                    Starting($"{project.Project.Name}: {output.Output.Name}");
                    failed |= !await BuildOutputAsync(journal, identity, project, output, cancellationToken);
                    done++;
                    progress?.Invoke(done / (double)steps, journal.Totals(fresh, exportsFresh));
                }

                foreach (var export in project.Exports.Where(static e => !e.Fresh)) {
                    await Yield(yield);
                    cancellationToken.ThrowIfCancellationRequested();
                    var preset = presets.Find(project.Project.Path, export.Preset.Id);
                    var what = $"{project.Project.Name}: {preset.Name}";
                    Starting(what);
                    var before = done;
                    await RunExportAsync(
                        journal,
                        project,
                        export,
                        preset,
                        request,
                        failed,
                        Within(before, what),
                        cancellationToken
                    );
                    done++;
                    progress?.Invoke(done / (double)steps, journal.Totals(fresh, exportsFresh));
                }
            }
        }
    }

    /// <summary>Reports a project's outputs that cannot be built, and says whether there were any.</summary>
    static bool Unresolved(BuildJournal journal, ProjectIdentity identity, PlannedProject project) {
        foreach (var output in project.Unresolved) {
            journal.Ended(
                $"unresolved:{project.Project.Path}:{output.Output.Name}",
                identity,
                new OutputReport(output.Output.Name, output.Output.Tool, null, BuildStatuses.Failed, 0, output.Problem)
            );
        }

        return project.Unresolved.Count > 0;
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
        PlannedProject project,
        PlannedExport export,
        ExportPreset preset,
        BuildRequest request,
        bool failed,
        Action<double, string> progress,
        CancellationToken cancellationToken
    ) {
        var options = request.Options;
        var identity = new ProjectIdentity(project.Project.Id.Value, project.Project.Name, project.Project.Path);
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
                    new(PresetText(preset), SourcesOf(objects, project), folder, options),
                    progress,
                    cancellationToken
                );
            }

            var delivered = ExportRun.Deliver(
                export.Key.Hex,
                folder,
                manifest,
                export.Target,
                ProjectNaming.Of(library, project.Project.Path, project.Project.Name, preset.Id),
                Beside(project),
                ExportOverrides.Apply(preset, options),
                PartOf(project, request)
            );
            journal.Ended(
                step,
                identity,
                new ExportReport(
                    preset.Id,
                    status,
                    export.Target,
                    delivered.Files,
                    manifest.Skipped,
                    [..manifest.Warnings, ..delivered.Warnings],
                    null
                )
            );
        } catch (BuildStepFailedException e) {
            journal.Ended(step, identity, FailedExport(preset.Id, e.Message));
        }
    }

    /// <summary>
    ///     The sets an export is of when it is not of the whole project, which leaves the other sets' files where they
    ///     are: the documents named, or the sets chosen. Documents that are not what the project exports take their
    ///     set's name, so that a graph exported alone never writes over the project's own files.
    /// </summary>
    ExportPart? PartOf(PlannedProject project, BuildRequest request) {
        if (request.Documents is { Count: > 0 }) {
            var sets = project.Project.Outputs
                .Select(static o => o.Output.Settings.GetValueOrDefault(ProjectOutputs.SetSetting, string.Empty))
                .ToHashSet(StringComparer.Ordinal);
            var whole = library.Contents(project.Project.Path).ExportSets;
            if (!sets.Where(static set => set.Length > 0).ToHashSet(StringComparer.Ordinal).SetEquals(whole)) {
                return new ExportPart(sets, true);
            }
        }

        return request.Options?.Sets is { } chosen ? new ExportPart([..chosen], false) : null;
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
    public static List<ExportSource> SourcesOf(ObjectStore objects, PlannedProject project) {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(project);
        List<ExportSource> sources = [];
        foreach (var output in project.Project.Outputs
                     .Where(static o => o.Key is not null)
                     .OrderBy(static o => o.Output.Tool switch {
                             BakeKeys.Tool or BuildTools.BakedMaps => 0,
                             BuildTools.TextureGraph => 1,
                             _ => 2
                         }
                     )
                     .ThenBy(static o => o.Output.Name, StringComparer.Ordinal)) {
            var key = output.Key!.Value;
            if (BuildObjects.Read(objects, key) is { Maps.Count: > 0 } manifest) {
                var set = output.Output.Settings.GetValueOrDefault(ProjectOutputs.SetSetting, string.Empty);
                sources.Add(new(objects.FolderOf(key), manifest, set));
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
