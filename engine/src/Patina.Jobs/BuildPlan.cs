using Patina.Contracts;
using Patina.Library;
using System.Text.Json;

namespace Patina.Jobs;

/// <summary>A preset as a plan needs it: which, and which bytes.</summary>
public sealed record PlannedPreset(string Id, string Revision);

/// <summary>An export in a plan.</summary>
/// <param name="Preset">The preset.</param>
/// <param name="Key">Its key.</param>
/// <param name="Target">Where its files go.</param>
/// <param name="Fresh">Whether what is there is this key's.</param>
public sealed record PlannedExport(PlannedPreset Preset, BuildKey Key, string Target, bool Fresh);

/// <summary>A project in a plan.</summary>
/// <param name="Project">The project.</param>
/// <param name="Stale">Its outputs with no result under their key.</param>
/// <param name="Fresh">How many outputs already have one.</param>
/// <param name="Exports">Its exports, fresh or not.</param>
/// <param name="Unresolved">Its outputs with no key.</param>
/// <param name="Pinned">Its outputs a pin holds still against the selected asset.</param>
public sealed record PlannedProject(
    ScannedProject Project,
    IReadOnlyList<ScannedOutput> Stale,
    int Fresh,
    IReadOnlyList<PlannedExport> Exports,
    IReadOnlyList<ScannedOutput> Unresolved,
    int Pinned
) {
    /// <summary>Whether a build would touch it.</summary>
    public bool HasWork => Stale.Count > 0 || Unresolved.Count > 0 || Exports.Any(static e => !e.Fresh);
}

/// <summary>
///     What a build would do, from keys alone: nothing runs and nothing is written, so asking is free. It is worked out
///     again whenever a build starts or restarts, and that is what makes a build resumable: whatever an earlier attempt
///     finished has a result under its key, and so is not in the new plan.
/// </summary>
public sealed class BuildPlan {
    /// <summary>
    ///     The tools whose outputs an export takes: pictures, which a preset reads, and meshes, which go
    ///     beside.
    /// </summary>
    static readonly HashSet<string> Exported = new(
        [BuildTools.TextureGraph, BuildTools.LayerStack, BakeKeys.Tool, BuildTools.BakedMaps, BuildTools.MeshLod],
        StringComparer.Ordinal
    );

    BuildPlan(IReadOnlyList<PlannedProject> projects) {
        Projects = projects;
    }

    /// <summary>Every selected project with an output, by path.</summary>
    public IReadOnlyList<PlannedProject> Projects { get; }

    /// <summary>The projects a build would touch.</summary>
    public IEnumerable<PlannedProject> Work => Projects.Where(static p => p.HasWork);

    /// <summary>
    ///     Why a build would write nothing, in words for a person, or null when it would write something.
    /// </summary>
    public string? Nothing =>
        Projects.Count == 0 ? "No project here has anything to export: it needs a texturing document or a material."
        : Work.Any() ? null
        : Projects.Any(static p => p.Exports.Count > 0) ? "Nothing has changed since the last export."
        : "There is nothing to do here: no preset is chosen, or nothing the selection reaches has changed.";

    /// <summary>How many steps a build would run, which is what progress counts.</summary>
    public int Steps => Work.Sum(static p => p.Stale.Count + p.Exports.Count(static e => !e.Fresh));

    /// <summary>Makes a plan.</summary>
    /// <param name="scan">The library's outputs.</param>
    /// <param name="objects">What has been built.</param>
    /// <param name="presets">What to export through.</param>
    /// <param name="exports">The folder exports go under.</param>
    /// <param name="tools">The installed tool versions.</param>
    public static BuildPlan Make(
        OutputScan scan,
        ObjectStore objects,
        IReadOnlyList<PlannedPreset> presets,
        string exports,
        ToolCatalog tools
    ) {
        ArgumentNullException.ThrowIfNull(scan);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(presets);
        var planned = new PlannedProject[scan.Projects.Count];
        Parallel.For(0, planned.Length, at => planned[at] = Plan(scan.Projects[at], objects, presets, exports, tools));
        return new(planned);
    }

    /// <summary>
    ///     The preview a client shows. <paramref name="offset" /> and <paramref name="limit" /> page the
    ///     projects.
    /// </summary>
    public BuildPreview Preview(int offset, int limit) {
        var work = Work.OrderBy(static p => p.Project.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static p => p.Project.Path, StringComparer.Ordinal)
            .ToList();
        var stale = Projects.SelectMany(static p => p.Stale).ToList();
        return new() {
            Projects = work.Count,
            Rebuilds = stale.Count,
            Shared = stale.Count - stale.Select(static o => o.Key).Distinct().Count(),
            Fresh = Projects.Sum(static p => p.Fresh),
            Pinned = Projects.Sum(static p => p.Pinned),
            Exports = Projects.Sum(static p => p.Exports.Count(static e => !e.Fresh)),
            ExportsFresh = Projects.Sum(static p => p.Exports.Count(static e => e.Fresh)),
            Unresolved = Projects.Sum(static p => p.Unresolved.Count),
            Nothing = Nothing,
            Items = [
                ..work.Skip(offset)
                    .Take(limit)
                    .Select(static p => new BuildPreviewProject(
                            p.Project.Id.Value,
                            p.Project.Name,
                            p.Project.Path,
                            p.Stale.Count,
                            p.Exports.Count(static e => !e.Fresh)
                        )
                    )
            ],
            Offset = offset,
            Limit = limit
        };
    }

    /// <summary>The folder a project's export through a preset goes to.</summary>
    public static string TargetOf(string exports, string preset, string projectPath) =>
        Path.Combine(exports, preset, Path.Combine(projectPath.Split('/')));

    static PlannedProject Plan(
        ScannedProject project,
        ObjectStore objects,
        IReadOnlyList<PlannedPreset> presets,
        string exports,
        ToolCatalog tools
    ) {
        // An asset's edit that a pin holds back changes no key, so nothing else here needs to know about it.
        var resolved = project.Outputs.Where(static o => o.Key is not null).ToList();
        var stale = resolved.Where(o => !objects.Contains(o.Key!.Value) && o.Reach == AssetReach.Moves).ToList();
        var fresh = resolved.Count(o => objects.Contains(o.Key!.Value));
        var pinned = project.Outputs.Count(static o => o.Reach == AssetReach.Held);
        var unresolved = project.Outputs.Where(static o => o.Key is null).ToList();

        var maps = resolved.Where(static o => Exported.Contains(o.Output.Tool))
            .Select(static o => o.Key!.Value)
            .ToList();
        List<PlannedExport> planned = [];
        if (maps.Count > 0 && unresolved.Count == 0 && project.Outputs.Any(static o => o.Reach == AssetReach.Moves)) {
            foreach (var preset in presets) {
                var key = ExportKeys.Compute(preset.Revision, maps, tools);
                var target = TargetOf(exports, preset.Id, project.Path);
                planned.Add(new(preset, key, target, BuildObjects.DeliveredKey(target) == key.Hex));
            }
        }

        return new(project, stale, fresh, planned, unresolved, pinned);
    }
}

/// <summary>Where a build's results live, and what says they are whole.</summary>
public static class BuildObjects {
    /// <summary>
    ///     Moves a finished <c>.part</c> folder into place. If another got there first, it has the same
    ///     bytes.
    /// </summary>
    public static void Publish(string part, string folder) {
        Directory.CreateDirectory(Path.GetDirectoryName(folder)!);
        try {
            Directory.Move(part, folder);
        } catch (IOException) when (Directory.Exists(folder)) {
            Directory.Delete(part, true);
        }
    }

    /// <summary>What an output's object says it holds, or null.</summary>
    public static BuildManifest? Read(ObjectStore objects, BuildKey key) {
        ArgumentNullException.ThrowIfNull(objects);
        var path = objects.PathOf(key);
        return File.Exists(path)
            ? JsonSerializer.Deserialize(File.ReadAllBytes(path), ContractsJsonContext.Default.BuildManifest)
            : null;
    }

    /// <summary>
    ///     Marks an output's object whole. Written last, so that a key in the store is a finished
    ///     result.
    /// </summary>
    public static void Commit(ObjectStore objects, BuildKey key, BuildManifest manifest) {
        ArgumentNullException.ThrowIfNull(objects);
        objects.Put(key, JsonSerializer.SerializeToUtf8Bytes(manifest, ContractsJsonContext.Default.BuildManifest));
    }

    /// <summary>The key delivered to a folder, or null. Every file must still be there for it to count.</summary>
    public static string? DeliveredKey(string target) {
        if (Delivered(target) is not { } delivery) {
            return null;
        }

        return delivery.Files.All(file => File.Exists(Path.Combine(target, file))) ? delivery.Key : null;
    }

    /// <summary>What was last delivered to a folder, or null.</summary>
    public static ExportDelivery? Delivered(string target) {
        var marker = Path.Combine(target, DeliveredFile);
        try {
            return File.Exists(marker)
                ? JsonSerializer.Deserialize(File.ReadAllBytes(marker), ContractsJsonContext.Default.ExportDelivery)
                : null;
        } catch (JsonException) {
            return null; // Not ours to trust: the export is written again.
        }
    }

    /// <summary>The file left beside delivered exports.</summary>
    public const string DeliveredFile = ".patina-export.json";
}
