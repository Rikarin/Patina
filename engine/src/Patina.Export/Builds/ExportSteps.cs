using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Library;

namespace Patina.Export.Builds;

/// <summary>
///     A build's export step, in a Worker or in <c>patina build</c>: a preset over the objects a project's outputs
///     made. It reads files and needs no device.
/// </summary>
public static class ExportSteps {
    /// <summary>Runs a preset over a project's maps.</summary>
    /// <param name="step">What to run.</param>
    /// <param name="progress">Told how far through it is, and the file it is on, or null.</param>
    /// <param name="cancellationToken">Stops it between blocks.</param>
    /// <exception cref="BuildStepFailedException">A preset or a map cannot be used.</exception>
    public static ExportManifest Run(
        ExportStep step,
        Action<double, string>? progress,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(step);
        try {
            var preset = ExportOverrides.Apply(ExportPresetReader.Read(step.Preset, "the preset"), step.Options);
            return ExportRun.Run(preset, SetsOf(step.Sources), step.Folder, progress, cancellationToken);
        } catch (Exception e) when (e is InvalidDataException or ExportPresetException or ArgumentException) {
            throw new BuildStepFailedException(e.Message, e);
        }
    }

    /// <summary>
    ///     Each texture set's maps, merged by set so that one set's maps cannot replace another's. A source with no
    ///     set is every set's, under what each makes for itself.
    /// </summary>
    public static List<ExportSet> SetsOf(IReadOnlyList<ExportSource> sources) {
        ArgumentNullException.ThrowIfNull(sources);
        var named = sources.Select(static s => s.Set).Where(static s => s.Length > 0).Distinct(StringComparer.Ordinal);
        List<string> sets = [..named.DefaultIfEmpty(string.Empty)];
        return [
            ..sets.Select(set => {
                    Dictionary<string, MapPicture> maps = new(StringComparer.Ordinal);
                    var udim = OneTile.First;
                    foreach (var source in sources.Where(s => s.Set.Length == 0 || s.Set == set)) {
                        foreach (var map in MapPictures.Read(source.Folder, source.Manifest)) {
                            maps[map.Name] = map;
                            if (Baked(source.Manifest.Tool, map.Name) is { } alone) {
                                maps[alone] = map with { Name = alone };
                            }
                        }

                        // The stack's own object says its tile; a bake's says the first (#20).
                        udim = source.Manifest.Udim != OneTile.First ? source.Manifest.Udim : udim;
                    }

                    return new ExportSet(set, maps) { Udim = udim };
                }
            )
        ];
    }

    /// <summary>
    ///     The name a bake's normal or occlusion also goes by (#155), which a stack's own map of the same name does not
    ///     replace, or null for any other map or a source that is not a bake.
    /// </summary>
    static string? Baked(string tool, string map) =>
        tool is BakeKeys.Tool or BuildTools.BakedMaps
            ? map switch {
                ExportMaps.Normal => ExportMaps.BakedNormal,
                ExportMaps.Occlusion => ExportMaps.BakedOcclusion,
                _ => null
            }
            : null;
}
