using Patina.Contracts;

namespace Patina.Library;

/// <summary>What is in a project's folder, read when it opens: a project's files are not in the index (plan §5).</summary>
static class ProjectFiles {
    const int MaxFiles = 2000;
    const string BakeFolder = "bakes";

    /// <summary>The file extensions a project's mesh can have, with their dots.</summary>
    public static readonly string[] MeshExtensions = [".glb", ".gltf", ".fbx", ".obj"];

    const string StackExtension = ".vxlayers";

    /// <summary>The documents in a project, by kind and then name, with paths below the library.</summary>
    public static ProjectContents Read(string root, string path) {
        var (full, document) = Project(root, path);
        List<ProjectFile> files = [];
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 };
        foreach (var file in Directory.EnumerateFiles(full, "*", options).Take(MaxFiles)) {
            var relative = Path.GetRelativePath(full, file).Replace('\\', '/');
            if (Kind(relative) is { } kind) {
                files.Add(new(kind, Path.GetFileName(file), Below(path, relative)));
            }
        }

        var bakes = Path.Combine(full, BakeFolder);
        if (Directory.Exists(bakes)) {
            files.AddRange(
                LibraryScanner.VisibleFolders(bakes)
                    .Select(bake => new ProjectFile(
                            "bake",
                            Path.GetFileName(bake),
                            Below(path, $"{BakeFolder}/{Path.GetFileName(bake)}")
                        )
                    )
            );
        }

        return new(
            document.Id.Value,
            document.Name,
            path,
            document.Mesh is { } mesh ? Below(path, mesh) : null,
            document.Stack is { } stack ? Below(path, stack) : null,
            Below(path, BakeFolder),
            [
                ..files.OrderBy(static f => f.Kind, StringComparer.Ordinal)
                    .ThenBy(static f => f.Name, StringComparer.Ordinal)
            ]
        );
    }

    /// <summary>
    ///     Brings a mesh into a project, unless it is in there already, and names it. The stack's file is the caller's to
    ///     write, because a stack is the Server's format and not the library's.
    /// </summary>
    /// <param name="root">The library.</param>
    /// <param name="path">The project, below <c>projects/</c>.</param>
    /// <param name="mesh">A mesh file to use, or null to leave it as it is.</param>
    /// <exception cref="FileNotFoundException">The project or the mesh is not there.</exception>
    /// <exception cref="ProjectRefusedException">The file is not a mesh.</exception>
    public static string? UseMesh(string root, string path, string? mesh) {
        var (full, document) = Project(root, path);
        if (mesh is null) {
            return document.Mesh;
        }

        var name = Path.GetFileName(mesh);
        var inside = Path.GetFullPath(mesh).StartsWith(full + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        Require(mesh);
        if (!inside) {
            File.Copy(mesh, Path.Combine(full, name), true);
        } else {
            name = Path.GetRelativePath(full, mesh).Replace('\\', '/');
        }

        LibraryFiles.WriteProject(root, path, document with { Mesh = name });
        return name;
    }

    /// <summary>Names the stack a project paints.</summary>
    public static void UseStack(string root, string path, string stack) {
        var (_, document) = Project(root, path);
        LibraryFiles.WriteProject(root, path, document with { Stack = stack });
    }

    /// <summary>Gives a project a new display name. Its folder stays, so what refers to it by path does too.</summary>
    /// <exception cref="ProjectRefusedException">The name cannot be used.</exception>
    public static void Rename(string root, string path, string name) {
        var (_, document) = Project(root, path);
        var trimmed = NewProjects.DisplayName(name);
        LibraryFiles.WriteProject(root, path, document with { Name = trimmed });
    }

    /// <summary>The file a mesh should be, checked before anything is written.</summary>
    /// <exception cref="FileNotFoundException">It is not there.</exception>
    /// <exception cref="ProjectRefusedException">It is not a mesh.</exception>
    public static void Require(string mesh) {
        if (!MeshExtensions.Contains(Path.GetExtension(mesh), StringComparer.OrdinalIgnoreCase)) {
            throw new ProjectRefusedException(
                $"'{Path.GetFileName(mesh)}' is not a mesh: use {string.Join(", ", MeshExtensions)}."
            );
        }

        if (!File.Exists(mesh)) {
            throw new FileNotFoundException($"There is no file '{mesh}'.", mesh);
        }
    }

    /// <summary>The name a project's first stack gets.</summary>
    public static string StackName(string path) => Path.GetFileName(path) + StackExtension;

    static (string Full, ProjectDocument Document) Project(string root, string path) {
        var clean = LibraryFolders.Existing(path);
        var file = LibraryFiles.ProjectFile(root, clean);
        if (!File.Exists(file)) {
            throw new FileNotFoundException($"There is no project '{path}' in this library.", path);
        }

        return (Path.GetDirectoryName(file)!, DocumentCodec.ReadProject(File.ReadAllText(file), file).Document);
    }

    static string? Kind(string relative) {
        if (relative.StartsWith(BakeFolder + "/", StringComparison.Ordinal)) {
            return null;
        }

        var extension = Path.GetExtension(relative);
        return MeshExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ? "mesh"
            : string.Equals(extension, StackExtension, StringComparison.OrdinalIgnoreCase) ? "stack"
            : string.Equals(extension, LibraryLayout.GraphExtension, StringComparison.OrdinalIgnoreCase) ? "graph"
            : null;
    }

    static string Below(string project, string file) => $"{LibraryLayout.ProjectsFolder}/{project}/{file}";
}
