using Patina.Contracts;
using Patina.Export.Presets;

namespace Patina.Export.Builds;

/// <summary>
///     A build's export step, in a Worker or in <c>patina build</c>: a preset over the objects a project's outputs
///     made. It reads files and needs no device.
/// </summary>
public static class ExportSteps {
    /// <summary>Runs a preset over a project's maps.</summary>
    /// <exception cref="BuildStepFailedException">A preset or a map cannot be used.</exception>
    public static ExportManifest Run(ExportStep step, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(step);
        try {
            var preset = ExportOverrides.Apply(ExportPresetReader.Read(step.Preset, "the preset"), step.Options);
            return ExportRun.Run(preset, SetsOf(step.Sources), step.Folder, null, cancellationToken);
        } catch (Exception e) when (e is InvalidDataException or ExportPresetException or ArgumentException) {
            throw new BuildStepFailedException(e.Message, e);
        }
    }

    /// <summary>
    ///     Each texture set's maps, merged by set so that one set's maps cannot replace another's. A source with no
    ///     set is every set's, under what each makes for itself.
    /// </summary>
    static List<ExportSet> SetsOf(IReadOnlyList<ExportSource> sources) {
        var named = sources.Select(static s => s.Set).Where(static s => s.Length > 0).Distinct(StringComparer.Ordinal);
        List<string> sets = [..named.DefaultIfEmpty(string.Empty)];
        return [
            ..sets.Select(set => {
                    Dictionary<string, MapPicture> maps = new(StringComparer.Ordinal);
                    foreach (var source in sources.Where(s => s.Set.Length == 0 || s.Set == set)) {
                        foreach (var map in MapPictures.Read(source.Folder, source.Manifest)) {
                            maps[map.Name] = map;
                        }
                    }

                    return new ExportSet(set, maps);
                }
            )
        ];
    }
}
