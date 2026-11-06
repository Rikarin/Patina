using Patina.Contracts;
using System.Collections.Frozen;

namespace Patina.Library;

/// <summary>
///     What a project holds. Its contents are read when it opens, because a project's own files are not in the index
///     (plan §5). A file that <c>project.yaml</c> does not list yet, from before it listed anything or copied in by
///     hand, is still shown, with no id, so nothing in a project's folder is out of reach.
/// </summary>
static class ProjectFiles {
    const int MaxFiles = 2000;
    const string BakeFolder = "bakes";
    const string StackExtension = ".vxlayers";

    static readonly FrozenSet<string> MeshExtensions =
        new[] { ".glb", ".gltf", ".fbx", ".obj" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Everything in a project, with paths below the library.</summary>
    public static ProjectContents Read(string root, string path) {
        var (full, document) = Project(root, path);
        var files = Files(full);
        var meshes = document.Meshes.Select(mesh => Listed(path, mesh)).ToList();
        meshes.AddRange(
            files.Where(f => IsMesh(f) && !document.Meshes.Any(m => m.File == f || m.High == f))
                .Select(f => new ProjectMeshInfo(null, Below(path, f), null))
        );
        var documents = document.Documents.Select(item => Listed(path, item)).ToList();
        documents.AddRange(
            files.Where(f => Unlisted(f) is not null && document.Documents.All(d => d.File != f))
                .Select(f => new ProjectDocumentInfo(
                        null,
                        Unlisted(f)!,
                        Path.GetFileNameWithoutExtension(f),
                        Below(path, f),
                        null
                    )
                )
        );
        return new(
            document.Id.Value,
            document.Name,
            path,
            meshes,
            documents,
            Below(path, BakeFolder),
            Bakes(full, path)
        );
    }

    /// <summary>Brings a mesh into a project, unless it is in there already, and lists it.</summary>
    /// <exception cref="FileNotFoundException">The project or a mesh is not there.</exception>
    /// <exception cref="ProjectRefusedException">A file is not a mesh.</exception>
    public static ProjectMesh AddMesh(string root, string path, string mesh, string? high) {
        var (full, document) = Project(root, path);
        Require(mesh);
        if (high is not null) {
            Require(high);
        }

        var added = new ProjectMesh(AssetId.New(), Bring(full, mesh), high is null ? null : Bring(full, high));
        var others = document.Meshes.Where(m => m.File != added.File);
        LibraryFiles.WriteProject(root, path, document with { Meshes = [..others, added] });
        return added;
    }

    /// <summary>The name a new document's file gets.</summary>
    public static string NewFile(string root, string path, string name, string extension) {
        var (full, _) = Project(root, path);
        var file = NewProjects.FileStem(name) + extension;
        return File.Exists(Path.Combine(full, file))
            ? throw new ProjectExistsException($"'{name}' already exists in this project.")
            : file;
    }

    /// <summary>Lists a document whose file exists.</summary>
    /// <exception cref="ProjectRefusedException">The kind and the mesh disagree.</exception>
    public static ProjectItem AddDocument(
        string root,
        string path,
        string kind,
        string file,
        string name,
        AssetId? mesh
    ) {
        var (_, document) = Project(root, path);
        if (kind == DocumentKinds.Texturing != mesh.HasValue) {
            throw new ProjectRefusedException("A texturing document is on a mesh, and no other kind is.");
        }

        var added = new ProjectItem(AssetId.New(), kind, file, NewProjects.DisplayName(name), mesh);
        LibraryFiles.WriteProject(root, path, document with { Documents = [..document.Documents, added] });
        return added;
    }

    /// <summary>Puts a texturing document on another mesh.</summary>
    public static ProjectItem SetMesh(string root, string path, AssetId id, AssetId mesh) {
        var (_, document) = Project(root, path);
        if (document.Meshes.All(m => m.Id != mesh)) {
            throw new FileNotFoundException($"This project has no mesh {mesh}.");
        }

        var item = document.Documents.FirstOrDefault(d => d.Id == id)
            ?? throw new FileNotFoundException($"This project has no document {id}.");
        if (item.Kind != DocumentKinds.Texturing) {
            throw new ProjectRefusedException($"'{item.Name}' is not on a mesh.");
        }

        var changed = item with { Mesh = mesh };
        List<ProjectItem> documents = [..document.Documents.Select(d => d.Id == id ? changed : d)];
        LibraryFiles.WriteProject(root, path, document with { Documents = documents });
        return changed;
    }

    /// <summary>Gives a project a new display name. Its folder stays, so what refers to it by path does too.</summary>
    /// <exception cref="ProjectRefusedException">The name cannot be used.</exception>
    public static void Rename(string root, string path, string name) {
        var (_, document) = Project(root, path);
        var trimmed = NewProjects.DisplayName(name);
        LibraryFiles.WriteProject(root, path, document with { Name = trimmed });
    }

    /// <summary>Checks a file can be a mesh, before anything is written.</summary>
    /// <exception cref="FileNotFoundException">It is not there.</exception>
    /// <exception cref="ProjectRefusedException">It is not a mesh.</exception>
    public static void Require(string mesh) {
        if (!IsMesh(mesh)) {
            var known = string.Join(", ", MeshExtensions.Order(StringComparer.Ordinal));
            throw new ProjectRefusedException($"'{Path.GetFileName(mesh)}' is not a mesh: use {known}.");
        }

        if (!File.Exists(mesh)) {
            throw new FileNotFoundException($"There is no file '{mesh}'.", mesh);
        }
    }

    static string Bring(string full, string mesh) {
        var source = Path.GetFullPath(mesh);
        if (source.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
            return Path.GetRelativePath(full, source).Replace('\\', '/');
        }

        var name = Path.GetFileName(source);
        File.Copy(source, Path.Combine(full, name), true);
        return name;
    }

    static (string Full, ProjectDocument Document) Project(string root, string path) {
        var file = LibraryFiles.ProjectFile(root, LibraryFolders.Existing(path));
        if (!File.Exists(file)) {
            throw new FileNotFoundException($"There is no project '{path}' in this library.", path);
        }

        return (Path.GetDirectoryName(file)!, DocumentCodec.ReadProject(File.ReadAllText(file), file).Document);
    }

    /// <summary>The files in a project, relative to it, leaving out what its bakes wrote.</summary>
    static List<string> Files(string full) => [
        ..Directory.EnumerateFiles(full, "*", new EnumerationOptions { RecurseSubdirectories = true })
            .Select(file => Path.GetRelativePath(full, file).Replace('\\', '/'))
            .Where(static file => !file.StartsWith(BakeFolder + "/", StringComparison.Ordinal)
                && !file.StartsWith(LibraryLayout.CompoundsFolder + "/", StringComparison.Ordinal)
            )
            .Take(MaxFiles)
    ];

    static List<ProjectFile> Bakes(string full, string path) {
        var bakes = Path.Combine(full, BakeFolder);
        return Directory.Exists(bakes)
            ? [
                ..LibraryScanner.VisibleFolders(bakes)
                    .Select(static bake => Path.GetFileName(bake))
                    .Select(bake => new ProjectFile("bake", bake, Below(path, $"{BakeFolder}/{bake}")))
            ]
            : [];
    }

    static ProjectMeshInfo Listed(string path, ProjectMesh mesh) =>
        new(mesh.Id.Value, Below(path, mesh.File), mesh.High is { } high ? Below(path, high) : null);

    static ProjectDocumentInfo Listed(string path, ProjectItem item) =>
        new(item.Id.Value, item.Kind, item.Name, Below(path, item.File), item.Mesh?.Value);

    static bool IsMesh(string file) => MeshExtensions.Contains(Path.GetExtension(file));

    /// <summary>The kind a file not yet listed is taken for. A stack that names no mesh still opens as one.</summary>
    static string? Unlisted(string file) {
        var extension = Path.GetExtension(file);
        return string.Equals(extension, StackExtension, StringComparison.OrdinalIgnoreCase) ? DocumentKinds.Texturing
            : string.Equals(extension, LibraryLayout.GraphExtension, StringComparison.OrdinalIgnoreCase) ? DocumentKinds
                .Graph
            : null;
    }

    static string Below(string project, string file) => $"{LibraryLayout.ProjectsFolder}/{project}/{file}";
}
