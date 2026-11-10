using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Library;

namespace Patina.Export.Builds;

/// <summary>What a project's exported files are named by, read from the project as it is now.</summary>
public static class ProjectNaming {
    /// <summary>
    ///     What a project's files are named by: its name, and each texture set's mesh and material, read from the
    ///     project as it is now. A project that cannot be read names files by its name alone.
    /// </summary>
    public static NamingValues Of(OpenLibrary library, string path, string name, string preset) {
        var values = new NamingValues(name, preset);
        ProjectContents contents;
        try {
            contents = library.Contents(path);
        } catch (Exception e) when (e is FileNotFoundException or LibraryFormatException or IOException) {
            return values;
        }

        var meshes = contents.Meshes.Where(static m => m.Id is not null)
            .ToDictionary(static m => m.Id!.Value, static m => MeshName(m));
        Dictionary<string, SetNaming> sets = new(StringComparer.Ordinal);
        foreach (var document in contents.Documents) {
            var set = document.Material ?? document.Name;
            if (!sets.ContainsKey(set)) {
                var mesh = document.Mesh is { } id && meshes.TryGetValue(id, out var meshName)
                    ? meshName
                    : string.Empty;
                sets[set] = new(mesh, document.Material ?? string.Empty);
            }
        }

        return values with {
            Mesh = contents.Meshes.Count > 0 ? MeshName(contents.Meshes[0]) : string.Empty, Sets = sets
        };
    }

    /// <summary>A mesh's name in a file name: the file it came from, without its extension.</summary>
    public static string MeshName(ProjectMeshInfo mesh) =>
        Path.GetFileNameWithoutExtension((mesh.Source ?? mesh.Path).Replace('\\', '/').Split('/')[^1]);
}
