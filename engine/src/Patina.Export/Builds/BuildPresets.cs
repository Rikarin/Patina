using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Jobs;
using Patina.Library;
using System.Collections.Concurrent;

namespace Patina.Export.Builds;

/// <summary>
///     The presets one build exports through, per project: a project's own preset of an id wins over the library's,
///     which wins over the factory's (<see cref="ExportPresetCatalog" />). Each project's folder is read once a build,
///     and only if it has one. Changes a preset cannot take are refused before anything runs, and not once per project.
/// </summary>
public sealed class BuildPresets {
    readonly string root;
    readonly ExportPresetCatalog.Found library;
    readonly IReadOnlyList<string> named;
    readonly ExportOptions? options;
    readonly ConcurrentDictionary<string, ExportPresetCatalog.Found?> projects = new(StringComparer.Ordinal);

    /// <summary>Reads the library's presets for a request.</summary>
    /// <exception cref="BuildRefusedException">A preset cannot take the request's changes.</exception>
    public BuildPresets(OpenLibrary library, BuildRequest request) {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(request);
        root = library.RootPath;
        this.library = ExportPresetCatalog.Read(root);
        named = [..(request.Presets ?? library.Document.ExportTargets).Distinct(StringComparer.Ordinal)];
        options = request.Options;
        foreach (var preset in named.Select(this.library.Find).OfType<ExportPreset>()) {
            Check(preset);
        }
    }

    /// <summary>The presets a project exports through, for a plan.</summary>
    public IReadOnlyList<PlannedPreset> PlannedFor(string project) =>
        [..For(project).Select(p => new PlannedPreset(p.Id, ExportOverrides.Revision(p.Revision, options)))];

    /// <summary>The presets a project exports through: each id named that the project or the library has.</summary>
    public IReadOnlyList<ExportPreset> For(string project) {
        var own = Own(project);
        return [..named.Select(id => own?.Find(id) ?? library.Find(id)).OfType<ExportPreset>()];
    }

    /// <summary>
    ///     The side a project's graphs are computed at for its presets' own size (#280, ADR 0028): the size every preset
    ///     it exports through names, so the export finds maps already that size and resamples none. Null when a preset
    ///     names none or two disagree, since a build computes a project's graphs once for all its presets; those
    ///     presets resample as before.
    /// </summary>
    public GraphSizes? SizesFor(string project) {
        var sizes = For(project).Select(static preset => preset.Size).Distinct().ToList();
        return sizes is [{ } side] ? new(side, new Dictionary<string, int>(StringComparer.Ordinal)) : null;
    }

    /// <summary>One preset of a project, as a step runs it.</summary>
    public ExportPreset Find(string project, string id) =>
        Own(project)?.Find(id) ?? library.Find(id) ?? throw new BuildRefusedException($"There is no preset '{id}'.");

    /// <summary>
    ///     Refuses a request that names a preset nothing has: not the library, and not any project the plan reached.
    ///     A preset only some projects have exports those, and the rest are left alone. A project's own preset is held
    ///     to the request's changes here, as the library's are when this is made.
    /// </summary>
    /// <exception cref="BuildRefusedException">An id nothing has, or a change a preset cannot take.</exception>
    public void Refuse(IEnumerable<string> reached) {
        ArgumentNullException.ThrowIfNull(reached);
        var paths = reached.ToList();
        foreach (var preset in paths.SelectMany(path => named.Select(id => Own(path)?.Find(id)))
                     .OfType<ExportPreset>()) {
            Check(preset);
        }

        foreach (var id in named) {
            if (library.Find(id) is null && !Anywhere(paths, id)) {
                throw new BuildRefusedException(
                    $"There is no preset '{id}'. The presets are "
                    + string.Join(", ", library.Presets.Select(static p => p.Id))
                    + "."
                );
            }
        }
    }

    /// <summary>Whether any of these projects has a preset of its own with this id.</summary>
    bool Anywhere(List<string> paths, string id) {
        foreach (var path in paths) {
            if (Own(path)?.Find(id) is not null) {
                return true;
            }
        }

        return false;
    }

    ExportPresetCatalog.Found? Own(string project) =>
        projects.GetOrAdd(
            project,
            path => {
                var folder = ExportPresetCatalog.ProjectFolder(root, path);
                if (!Directory.Exists(folder)) {
                    return null;
                }

                return ExportPresetCatalog.Read(root, path);
            }
        );

    void Check(ExportPreset preset) {
        try {
            ExportOverrides.Apply(preset, options);
        } catch (ExportPresetException e) {
            throw new BuildRefusedException(e.Message, e);
        }
    }
}
