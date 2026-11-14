using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     The bakes a project's own settings ask for (#186): what the Bake dialog and Quick Bake send for a project, read
///     from its <c>project.yaml</c>, so that <c>patina bake-maps --project</c> bakes the same maps to the same place
///     with no editor. One request per texture set to bake, or one for the whole mesh when it has none.
/// </summary>
public static class ProjectBakes {
    internal const string MapsFolder = "maps";

    /// <summary>The project at a folder, read.</summary>
    /// <exception cref="FileNotFoundException">The folder holds no <c>project.yaml</c>.</exception>
    /// <exception cref="LibraryFormatException">The file is not one this reads.</exception>
    public static ProjectDocument Read(string folder) {
        var file = Path.Combine(folder, LibraryLayout.ProjectFile);
        if (!File.Exists(file)) {
            throw new FileNotFoundException(
                $"There is no project at '{folder}': it holds no {LibraryLayout.ProjectFile}.",
                file
            );
        }

        return DocumentCodec.ReadProject(SharedFile.ReadAllText(file), file).Document;
    }

    /// <summary>What baking a project asks for, as the Bake dialog would send it.</summary>
    /// <param name="folder">The project's folder.</param>
    /// <param name="project">The project, as its file says.</param>
    /// <param name="document">
    ///     A document whose mesh is baked onto, by its file below the project, or null for the first mesh a document is
    ///     on, then the first mesh.
    /// </param>
    /// <param name="output">Where the maps go instead of the project's bake folder, or null.</param>
    /// <param name="sets">The texture sets to bake, by material, over those the project chose; empty for those.</param>
    /// <exception cref="ArgumentException">
    ///     The project has no mesh, or names no such document or texture set, or ticks none on the mesh.
    /// </exception>
    public static IReadOnlyList<BakeRequest> Requests(
        string folder,
        ProjectDocument project,
        string? document = null,
        string? output = null,
        IReadOnlyList<string>? sets = null
    ) {
        ArgumentNullException.ThrowIfNull(project);
        var mesh = MeshOf(project, document);
        var whole = Setup(folder, project, mesh, output);
        var ticked = project.Bake?.Sets ?? [];
        return [
            ..Chosen(project, mesh, sets is { Count: > 0 } ? sets : ticked, sets is { Count: > 0 })
                .Select(material => whole with {
                        Output = material is null ? whole.Output : Path.Combine(whole.Output, material),
                        Material = material
                    }
                )
        ];
    }

    /// <summary>
    ///     The bake of a project's whole low mesh, with no texture set: its meshes, cage and options, which the
    ///     viewport's preview of the bake's setup shows (#296).
    /// </summary>
    /// <param name="folder">The project's folder.</param>
    /// <param name="project">The project, as its file says.</param>
    /// <param name="document">A document whose mesh is baked onto, by its file below the project, or null.</param>
    /// <exception cref="ArgumentException">The project has no mesh, or names no such document.</exception>
    public static BakeRequest Setup(string folder, ProjectDocument project, string? document = null) {
        ArgumentNullException.ThrowIfNull(project);
        return Setup(folder, project, MeshOf(project, document), null);
    }

    static BakeRequest Setup(string folder, ProjectDocument project, ProjectMesh mesh, string? output) {
        var settings = project.Bake ?? new ProjectBakeSettings();
        var low = Path.GetFullPath(mesh.File, folder);
        var cage = settings.Cage.Trim();
        return new() {
            High = [
                mesh.High is { } detailed ? Path.GetFullPath(detailed, folder) : low,
                ..settings.High.Select(path => Path.GetFullPath(path, folder))
            ],
            Low = low,
            Output = output is null
                ? Path.Combine(folder, ProjectFiles.BakeFolder, MapsFolder)
                : Path.GetFullPath(output),
            Cage = cage.Length > 0 ? Path.GetFullPath(cage, folder) : null,
            Options = settings.Options
        };
    }

    /// <summary>The mesh a document is on, or the first one a document is on, or the first.</summary>
    static ProjectMesh MeshOf(ProjectDocument project, string? document) {
        if (document is not null) {
            var file = document.Replace('\\', '/');
            var item = project.Documents.FirstOrDefault(d => d.File == file)
                ?? throw new ArgumentException($"The project lists no document '{document}'.");
            return project.Meshes.FirstOrDefault(m => m.Id == item.Mesh)
                ?? throw new ArgumentException($"'{document}' is on no mesh, so there is nothing to bake onto.");
        }

        var used = project.Documents.Select(static d => d.Mesh).OfType<AssetId>().ToHashSet();
        return project.Meshes.FirstOrDefault(m => used.Contains(m.Id))
            ?? (project.Meshes.Count > 0 ? project.Meshes[0] : null)
            ?? throw new ArgumentException("The project has no mesh, so there is nothing to bake onto.");
    }

    /// <summary>
    ///     The texture sets to bake on a mesh: those asked for, or all of them; a single null for a mesh with none.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="mesh">The mesh baked onto.</param>
    /// <param name="asked">The sets asked for.</param>
    /// <param name="named">
    ///     Whether they were named for this bake. Named sets must all be on the mesh. The project's ticked sets may be
    ///     on its other meshes, so only those on this one count, and only none at all is refused (#296).
    /// </param>
    static List<string?> Chosen(
        ProjectDocument project,
        ProjectMesh mesh,
        IReadOnlyList<string> asked,
        bool named
    ) {
        List<string> all = [
            ..project.Documents.Where(d => d.Mesh == mesh.Id)
                .Select(static d => d.Material)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
        ];
        if (all.Count == 0 && !named) {
            return [null];
        }

        var has = all.Count > 0 ? string.Join(", ", all) : "none";
        if (named && asked.FirstOrDefault(set => !all.Contains(set, StringComparer.Ordinal)) is { } unknown) {
            throw new ArgumentException($"The mesh has no texture set '{unknown}'. It has {has}.");
        }

        List<string?> here = [..all.Where(asked.Contains)];
        return asked.Count == 0 ? [..all]
            : here.Count > 0 ? here
            : throw new ArgumentException(
                $"None of the texture sets the project ticks ({string.Join(", ", asked)}) is on this mesh. "
                + $"It has {has}."
            );
    }
}
