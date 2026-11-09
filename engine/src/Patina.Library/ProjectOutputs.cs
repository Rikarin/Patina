using Patina.Contracts;

namespace Patina.Library;

/// <summary>The tools a project's outputs can name.</summary>
public static class BuildTools {
    /// <summary>
    ///     A graph's Output nodes. It reads the file its <c>graph</c> setting names, or the shared graph first in
    ///     <c>uses</c>, or the first of <c>files</c>.
    /// </summary>
    public const string TextureGraph = "texture-graph";

    /// <summary>A layer stack's maps. It reads the first of <c>files</c>.</summary>
    public const string LayerStack = "layer-stack";

    /// <summary>A mesh at lower levels of detail. It reads the first of <c>files</c>.</summary>
    public const string MeshLod = "mesh-lod";

    /// <summary>
    ///     The maps of a bake already in a project's folder. It reads the bake's manifest among <c>files</c>.
    /// </summary>
    public const string BakedMaps = "baked-maps";
}

/// <summary>The one rule for which bake a document reads.</summary>
public static class DocumentBakes {
    /// <summary>What a delivered bake's manifest is called, after the name of its mesh.</summary>
    public const string ManifestPattern = "*.bake.json";

    const string PreviewEnding = ".preview.png";
    const string MapsFolder = "maps";

    /// <summary>
    ///     The folder of a document's bake, or null. The bake a person pointed it at comes first, then the folder a
    ///     bake of that document goes to unless someone says otherwise. A folder with no finished bake in it is no
    ///     bake.
    /// </summary>
    /// <param name="project">The project's folder.</param>
    /// <param name="item">The document.</param>
    public static string? FolderOf(string project, ProjectItem item) {
        ArgumentNullException.ThrowIfNull(item);
        if (GraphMeshMaps.Read(Path.Combine(project, item.File)) is { } chosen && Holds(chosen)) {
            return chosen;
        }

        var maps = Path.Combine(project, ProjectFiles.BakeFolder, MapsFolder);
        var own = item.Material is { } material ? Path.Combine(maps, material) : maps;
        return Holds(own) ? own : null;
    }

    /// <summary>
    ///     The bake of the document at a file, by the same rule, which is what opening one reconnects it to. A file no
    ///     project lists has only what was chosen for it.
    /// </summary>
    public static string? OfFile(string file) {
        var full = Path.GetFullPath(file);
        for (var at = Path.GetDirectoryName(full); at is not null; at = Path.GetDirectoryName(at)) {
            var listing = Path.Combine(at, LibraryLayout.ProjectFile);
            if (File.Exists(listing)) {
                return Listed(at, full, listing) is { } item ? FolderOf(at, item) : GraphMeshMaps.Read(full);
            }
        }

        return GraphMeshMaps.Read(full);
    }

    static ProjectItem? Listed(string project, string file, string listing) {
        try {
            return DocumentCodec.ReadProject(SharedFile.ReadAllText(listing), listing)
                .Document.Documents.FirstOrDefault(item => Path.GetFullPath(item.File, project) == file);
        } catch (Exception e) when (e is IOException or LibraryFormatException) {
            return null; // changing as it is read: what was chosen still stands
        }
    }

    /// <summary>Every finished bake below a folder.</summary>
    public static IReadOnlyList<string> Below(string folder) =>
        Directory.Exists(folder)
            ? [
                ..Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories)
                    .Prepend(folder)
                    .Where(Holds)
                    .Order(StringComparer.Ordinal)
            ]
            : [];

    /// <summary>What a bake is made of: everything in its folder but the pictures made to look at it by.</summary>
    public static IReadOnlyList<string> FilesIn(string folder) => [
        ..Directory.EnumerateFiles(folder)
            .Where(static file => !file.EndsWith(PreviewEnding, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
    ];

    static bool Holds(string folder) =>
        Directory.Exists(folder) && Directory.EnumerateFiles(folder, ManifestPattern).Any();
}

/// <summary>
///     What a project builds with nothing written by hand. Documents say what a project is for, so they say what it
///     exports: each texturing document is a texture set of its own, on top of its bake. A project with no texturing
///     document is a material, and exports its layered materials, material graphs and image materials the same way. The
///     graphs and pictures in a texturing project are what its layers read, not what it ships. Outputs written in
///     <c>project.yaml</c> replace all of this, for a project that wants something else.
/// </summary>
/// <remarks>
///     Worked out on every read and never written back: an output in a file would have to be kept in step with the
///     documents by everything that changes one. A bake is taken from where it is, so an export never starts a bake of
///     its own with settings nobody chose, and a project that has not baked exports what its layers make.
/// </remarks>
public static class ProjectOutputs {
    /// <summary>
    ///     The texture set an output belongs to. Outputs without one belong to every set of their project.
    /// </summary>
    public const string SetSetting = "set";

    /// <summary>The folder of the bake a document reads, relative to it.</summary>
    public const string BakeSetting = "bake";

    /// <summary>The file a <see cref="BuildTools.TextureGraph" /> output builds, among its files.</summary>
    public const string GraphSetting = "graph";

    /// <summary>The mesh a document is painted on, among its files, relative to the project.</summary>
    public const string MeshSetting = "mesh";

    /// <summary>The mesh's material a document is the texture set of, when it is one.</summary>
    public const string MaterialSetting = "material";

    /// <summary>What <paramref name="project" /> builds.</summary>
    /// <param name="project">The project.</param>
    /// <param name="folder">Its folder.</param>
    public static IReadOnlyList<OutputDeclaration> Of(ProjectDocument project, string folder) {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Outputs.Count > 0) {
            return project.Outputs;
        }

        var texturing = project.Documents.Where(static d => d.Kind == DocumentKinds.Texturing).ToList();
        var exported = texturing.Count > 0 ? texturing : [..project.Documents.Where(IsMaterial)];
        return [..exported.SelectMany(item => OfDocument(project, item, folder))];
    }

    static bool IsMaterial(ProjectItem item) =>
        item.Kind is DocumentKinds.LayeredMaterial or DocumentKinds.ImageMaterial
        || (item.Kind == DocumentKinds.Graph && item.Role is null or GraphRoles.Material);

    static IEnumerable<OutputDeclaration> OfDocument(ProjectDocument project, ProjectItem item, string folder) {
        var bakeFolder = DocumentBakes.FolderOf(folder, item);
        string[] bake = bakeFolder is null ? [] : [..DocumentBakes.FilesIn(bakeFolder).Select(f => Below(folder, f))];
        Dictionary<string, string> set = new(StringComparer.Ordinal) { [SetSetting] = item.Material ?? item.Name };
        Dictionary<string, string> settings = new(set, StringComparer.Ordinal);
        if (bakeFolder is not null) {
            var beside = Path.GetDirectoryName(Path.Combine(folder, item.File))!;
            settings[BakeSetting] = Path.GetRelativePath(beside, bakeFolder).Replace('\\', '/');
        }

        var graph = item.Kind is DocumentKinds.Graph or DocumentKinds.ImageMaterial;
        if (graph) {
            settings[GraphSetting] = item.File;
        }

        // A document on a mesh reads its layout, which says what padding grows from, so its key moves with the mesh.
        string[] mesh = [];
        if (!graph
            && project.Meshes.FirstOrDefault(m => m.Id == item.Mesh) is { } owned
            && File.Exists(Path.Combine(folder, owned.File))) {
            mesh = [owned.File];
            settings[MeshSetting] = owned.File;
            if (item.Material is { } material) {
                settings[MaterialSetting] = material;
            }
        }

        yield return new(
            item.File,
            graph ? BuildTools.TextureGraph : BuildTools.LayerStack,
            [item.File, ..Reads(project, item, folder), ..mesh, ..bake],
            [..project.References.Select(static reference => reference.Asset).Distinct()],
            settings
        );
        if (bake.Length > 0) {
            yield return new($"{item.File} bake", BuildTools.BakedMaps, bake, [], set);
        }
    }

    /// <summary>
    ///     What a document can read in its own project beyond the files it names, which a key has to depend on. Which
    ///     of them it does read is inside layers and nodes, and an edit to one too many rebuilds a little.
    /// </summary>
    static IEnumerable<string> Reads(ProjectDocument project, ProjectItem item, string folder) =>
        project.Documents.Where(other => other.Id != item.Id && other.Kind != DocumentKinds.Texturing)
            .Select(static other => other.File)
            .Where(file => File.Exists(Path.Combine(folder, file)))
            .Concat(FilesBelow(folder, ProjectFiles.PictureFolder))
            .Concat(FilesBelow(folder, LibraryLayout.CompoundsFolder));

    static IEnumerable<string> FilesBelow(string folder, string name) {
        var below = Path.Combine(folder, name);
        return Directory.Exists(below)
            ? Directory.EnumerateFiles(below, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(file => Below(folder, file))
            : [];
    }

    static string Below(string folder, string file) => Path.GetRelativePath(folder, file).Replace('\\', '/');
}
