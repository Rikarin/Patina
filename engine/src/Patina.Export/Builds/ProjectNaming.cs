using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Library;

namespace Patina.Export.Builds;

/// <summary>What a project's exported files are named by, read from the project as it is now.</summary>
public static class ProjectNaming {
    /// <summary>
    ///     What a project's files are named by: its name, and each texture set's mesh, material and UDIM tile, read
    ///     from the project as it is now. A project that cannot be read names files by its name alone.
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="path">The project.</param>
    /// <param name="name">Its name.</param>
    /// <param name="preset">The preset that names the files.</param>
    /// <param name="reads">What reads each set's tile: the mesh, or in the Server what the Worker read of it.</param>
    public static NamingValues Of(OpenLibrary library, string path, string name, string preset, IMeshSetReads reads) {
        ArgumentNullException.ThrowIfNull(library);
        var values = new NamingValues(name, preset);
        ProjectContents contents;
        try {
            contents = library.Contents(path);
        } catch (Exception e) when (e is FileNotFoundException or LibraryFormatException or IOException) {
            return values;
        }

        var meshes = contents.Meshes.Where(static m => m.Id is not null).ToDictionary(static m => m.Id!.Value);
        Dictionary<string, SetNaming> sets = new(StringComparer.Ordinal);
        foreach (var document in contents.Documents) {
            var set = document.Material ?? document.Name;
            if (!sets.ContainsKey(set)) {
                var mesh = document.Mesh is { } id ? meshes.GetValueOrDefault(id) : null;
                sets[set] = new(mesh is null ? string.Empty : MeshName(mesh), document.Material ?? string.Empty) {
                    Udim = mesh is null ? OneTile.First : TileOf(library, document, mesh, reads)
                };
            }
        }

        return values with {
            Mesh = contents.Meshes.Count > 0 ? MeshName(contents.Meshes[0]) : string.Empty, Sets = sets
        };
    }

    /// <summary>A mesh's name in a file name: the file it came from, without its extension.</summary>
    public static string MeshName(ProjectMeshInfo mesh) =>
        Path.GetFileNameWithoutExtension((mesh.Source ?? mesh.Path).Replace('\\', '/').Split('/')[^1]);

    /// <summary>
    ///     The UDIM tile a texture set's layout sits on, in the UV set its file chose (#281), so a preset's
    ///     <c>{udim}</c> and <c>{tile}</c> name it before anything is built.
    /// </summary>
    static int TileOf(
        OpenLibrary library,
        ProjectDocumentInfo document,
        ProjectMeshInfo mesh,
        IMeshSetReads meshes
    ) {
        var file = LibraryLayout.Full(library.RootPath, document.Path);
        var text = document.Kind == DocumentKinds.Texturing && File.Exists(file)
            ? SharedFile.ReadAllText(file)
            : string.Empty;
        var uvSet = StackInputs.LayoutOf(text, document.Material).UvSet;
        return SetUdim.Of(meshes, LibraryLayout.Full(library.RootPath, mesh.Path), document.Material, uvSet);
    }
}
