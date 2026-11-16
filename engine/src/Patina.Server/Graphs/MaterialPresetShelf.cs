using Patina.Contracts;
using Patina.Library;

namespace Patina.Server.Graphs;

/// <summary>
///     Material presets as documents of a project (#124): a material from the shelf and the values a graph fill set
///     its parameters to, kept in a <c>.ptpreset</c> file. The shelf lists them among the materials, every project's
///     as it lists every project's materials, and using one fills a layer with the material at those values.
/// </summary>
public static class MaterialPresetShelf {
    const string Own = "project";

    /// <summary>
    ///     Keeps a graph fill's material and parameters as a new preset of the project. The fill names its material as
    ///     the project's copy of it, which is taken back to where the shelf has it, so the preset still finds it from
    ///     another project.
    /// </summary>
    /// <exception cref="ProjectRefusedException">The fill's graph is no material of the shelf's.</exception>
    /// <exception cref="ProjectExistsException">The project has a document of that name.</exception>
    public static MaterialInfo Create(OpenLibrary library, MaterialPresetCreateRequest request) {
        var material = MaterialShelf.SourceOf(library, request.Project, request.Graph)
            ?? throw new ProjectRefusedException(
                $"'{request.Graph}' did not come from the shelf, so a preset would not find it again."
            );
        MaterialPreset preset = new(
            material,
            [
                ..request.Parameters.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(static pair => new MaterialPresetValue(pair.Key, pair.Value))
            ]
        );
        if (preset.Problem() is { } problem) {
            throw new ProjectRefusedException(problem);
        }

        var path = library.CreateMaterialPreset(request.Project, request.Name, preset);
        var document = library.Contents(request.Project).Documents.First(document => document.Path == path);
        return Described(document, preset);
    }

    /// <summary>A preset as the shelf lists it: a material, found by its material's name too.</summary>
    /// <exception cref="LibraryFormatException">Its file is not a material preset.</exception>
    internal static MaterialInfo Described(OpenLibrary library, ProjectDocumentInfo document) =>
        Described(document, MaterialPresets.Read(Path.GetFullPath(document.Path, library.RootPath)));

    /// <summary>
    ///     Makes a preset's material ready for the project's layers, as using the material would, with the values a
    ///     fill's <c>parameters</c> take.
    /// </summary>
    /// <exception cref="FileNotFoundException">There is no such preset.</exception>
    /// <exception cref="ProjectRefusedException">Its file is not a material preset, or names another preset.</exception>
    public static MaterialUse Use(OpenLibrary library, string project, string source) {
        var full = Path.GetFullPath(source, library.RootPath);
        if (!full.StartsWith(Path.GetFullPath(library.RootPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !File.Exists(full)) {
            throw new FileNotFoundException($"There is no material preset '{source}' in this library.", source);
        }

        MaterialPreset preset;
        try {
            preset = MaterialPresets.Read(full);
        } catch (LibraryFormatException e) {
            throw new ProjectRefusedException(e.Message, e);
        }

        if (MaterialPresets.Names(preset.Material)) {
            throw new ProjectRefusedException("A material preset sets a material, not another preset.");
        }

        var graph = MaterialShelf.Publish(library, project, preset.Material);
        return new(
            graph,
            preset.Values.ToDictionary(static value => value.Name, static value => value.Value, StringComparer.Ordinal)
        );
    }

    static MaterialInfo Described(ProjectDocumentInfo document, MaterialPreset preset) =>
        new(
            document.Name,
            DocumentKinds.MaterialPreset,
            document.Path,
            GraphRoles.Material,
            Own,
            [],
            Tags: [..document.Tags, MaterialName(preset.Material)]
        );

    /// <summary>What a material is called, from how the shelf names it: its file's name, or a shipped one's own.</summary>
    static string MaterialName(string material) {
        var name = material[(material.IndexOf(':', StringComparison.Ordinal) + 1)..];
        return Path.GetFileNameWithoutExtension(name.Replace('\\', '/').Split('/')[^1]);
    }
}
