using Patina.Contracts;
using Patina.Library;
using System.Reflection;

namespace Patina.Export.Presets;

/// <summary>
///     The presets a library can use: the factory ones, then whatever is in the library's <c>export-presets</c>, then,
///     for one project, whatever is in that project's <c>export-presets</c>, each winning over the one before under the
///     same id. A preset is a file, read whenever it is asked for, so editing one needs no restart.
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

    /// <summary>Where a preset comes from, which is also what can change it.</summary>
    public static class Origins {
        public const string Factory = "factory";
        public const string Library = "library";
        public const string Project = "project";
    }

    /// <summary>The presets a library can use, or one project of it. With no library, only the factory ones.</summary>
    /// <param name="libraryRoot">The library, or null.</param>
    /// <param name="project">A project's path below <c>projects/</c>, whose own presets win, or null for none.</param>
    public static Found Read(string? libraryRoot, string? project = null) {
        Dictionary<string, ExportPreset> presets = new(StringComparer.Ordinal);
        foreach (var preset in Factory()) {
            presets[preset.Id] = preset;
        }

        List<string> problems = [];
        if (libraryRoot is not null) {
            ReadFolder(LibraryFolder(libraryRoot), Origins.Library, presets, problems);
            if (project is not null) {
                ReadFolder(ProjectFolder(libraryRoot, project), Origins.Project, presets, problems);
            }
        }

        return new([..presets.Values.OrderBy(static p => p.Id, StringComparer.Ordinal)], problems);
    }

    /// <summary>The folder a library's own presets are in.</summary>
    public static string LibraryFolder(string libraryRoot) => Path.Combine(libraryRoot, LibraryLayout.PresetsFolder);

    /// <summary>The folder a project's own presets are in.</summary>
    /// <exception cref="ArgumentException">The path leaves the library's projects.</exception>
    public static string ProjectFolder(string libraryRoot, string project) {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Length == 0
            || project.Contains('\\', StringComparison.Ordinal)
            || project.Split('/').Any(static part => part is "" or "." or "..")) {
            throw new ArgumentException($"'{project}' is not a project's path.", nameof(project));
        }

        return LibraryLayout.Full(libraryRoot, $"{LibraryLayout.ProjectsFolder}/{project}/{LibraryLayout.PresetsFolder}");
    }

    static void ReadFolder(string folder, string origin, Dictionary<string, ExportPreset> presets, List<string> problems) {
        if (!Directory.Exists(folder)) {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.yaml").Order(StringComparer.Ordinal)) {
            try {
                var preset = ExportPresetReader.Read(SharedFile.ReadAllText(file), Path.GetFileName(file), file);
                presets[preset.Id] = preset with { Origin = origin };
            } catch (Exception e) when (e is ExportPresetException or IOException) {
                problems.Add(e.Message);
            }
        }
    }

    /// <summary>A preset's file, which a Worker reads a copy of so that it needs no library.</summary>
    public static string TextOf(ExportPreset preset) {
        ArgumentNullException.ThrowIfNull(preset);
        if (preset.Path is { } path) {
            return SharedFile.ReadAllText(path);
        }

        using var reader = new StreamReader(Resource(preset.Id));
        return reader.ReadToEnd();
    }

    static Stream Resource(string id) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream($"{ResourcePrefix}{id}.yaml")
        ?? throw new InvalidOperationException($"There is no factory preset '{id}'.");

    static IEnumerable<ExportPreset> Factory() {
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames()
            .Where(static n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal));
        foreach (var name in names) {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            yield return ExportPresetReader.Read(reader.ReadToEnd(), name);
        }
    }
}
