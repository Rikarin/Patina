using Patina.Library;
using System.Reflection;

namespace Patina.Export.Presets;

/// <summary>
///     The presets a library can use: the factory ones, then whatever is in <c>shared/export-presets</c>, which wins
///     under the same id. A preset is a file, read whenever it is asked for, so editing one needs no restart.
/// </summary>
public static class ExportPresetCatalog {
    /// <summary>The folder a library's own presets live in.</summary>
    public const string Folder = "export-presets";

    const string ResourcePrefix = "preset.";

    /// <summary>What was found, and what could not be used.</summary>
    /// <param name="Presets">By id.</param>
    /// <param name="Problems">One line per file that is not a preset.</param>
    public sealed record Found(IReadOnlyList<ExportPreset> Presets, IReadOnlyList<string> Problems) {
        /// <summary>The preset with an id, or null.</summary>
        public ExportPreset? Find(string id) => Presets.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>The presets a library can use. With no library, only the factory ones.</summary>
    public static Found Read(string? libraryRoot) {
        Dictionary<string, ExportPreset> presets = new(StringComparer.Ordinal);
        foreach (var preset in Factory()) {
            presets[preset.Id] = preset;
        }

        List<string> problems = [];
        var folder = libraryRoot is null ? null : Path.Combine(libraryRoot, LibraryLayout.SharedFolder, Folder);
        if (folder is not null && Directory.Exists(folder)) {
            foreach (var file in Directory.EnumerateFiles(folder, "*.yaml").Order(StringComparer.Ordinal)) {
                if (file.EndsWith(LibraryLayout.SidecarSuffix, StringComparison.Ordinal)) {
                    continue;
                }

                try {
                    var preset = ExportPresetReader.Read(File.ReadAllText(file), Path.GetFileName(file), file);
                    presets[preset.Id] = preset;
                } catch (Exception e) when (e is ExportPresetException or IOException) {
                    problems.Add(e.Message);
                }
            }
        }

        return new([..presets.Values.OrderBy(static p => p.Id, StringComparer.Ordinal)], problems);
    }

    /// <summary>A preset's file, which a Worker reads a copy of so that it needs no library.</summary>
    public static string TextOf(ExportPreset preset) {
        ArgumentNullException.ThrowIfNull(preset);
        if (preset.Path is { } path) {
            return File.ReadAllText(path);
        }

        using var reader = new StreamReader(Resource(preset.Id));
        return reader.ReadToEnd();
    }

    static Stream Resource(string id) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream($"{ResourcePrefix}{id}.yaml")
        ?? throw new InvalidOperationException($"There is no factory preset '{id}'.");

    static IEnumerable<ExportPreset> Factory() {
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames().Where(static n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal));
        foreach (var name in names) {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            yield return ExportPresetReader.Read(reader.ReadToEnd(), name);
        }
    }
}
