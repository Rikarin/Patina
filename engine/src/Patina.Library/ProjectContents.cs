using Patina.Contracts;
using System.Collections.Frozen;
using System.Text.Json;

namespace Patina.Library;

/// <summary>
///     What a project holds. Its contents are read when it opens, because a project's own files are not in the index
///     (plan §5). A file that <c>project.yaml</c> does not list yet, from before it listed anything or copied in by
///     hand, is still shown, with no id, so nothing in a project's folder is out of reach.
/// </summary>
static class ProjectFiles {
    const int MaxFiles = 2000;
    internal const string BakeFolder = "bakes";
    internal const string PictureFolder = "pictures";
    const string StackExtension = ".vxlayers";

    static readonly FrozenSet<string> MeshExtensions =
        new[] { ".glb", ".gltf", ".fbx", ".obj" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Everything in a project, with paths below the library.</summary>
    public static ProjectContents Read(string root, string path) {
        var (full, document) = Project(root, path);
        var files = Files(full);
        var meshes = document.Meshes.Select(mesh => Listed(root, path, mesh)).ToList();
        meshes.AddRange(
            files.Where(f => IsMesh(f) && !document.Meshes.Any(m => m.File == f || m.High == f))
                .Select(f => new ProjectMeshInfo(null, Below(path, f), null))
        );
        var documents = document.Documents.Select(item => Listed(full, path, item)).ToList();
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
            Bakes(full, path, document.Meshes),
            [
                ..files.Where(IsPicture)
                    .Select(f => new ProjectFile("picture", Path.GetFileName(f), Below(path, f)))
            ]
        ) {
            AutoUnwrap = document.AutoUnwrap,
            PreservePaint = document.PreservePaint,
            Unwrap = document.Unwrap,
            Export = document.Export,
            ExportSets = [
                ..ProjectOutputs.Of(document, full)
                    .Select(static output => output.Settings.GetValueOrDefault(ProjectOutputs.SetSetting, string.Empty))
                    .Where(static set => set.Length > 0)
                    .Distinct(StringComparer.Ordinal)
            ],
            Bake = document.Bake ?? new(),
            Viewport = document.Viewport,
            Paint = document.Paint,
            Checks = document.Checks ?? new()
        };
    }

    /// <summary>Brings a mesh into a project, unless it is in there already, and lists it.</summary>
    /// <exception cref="FileNotFoundException">The project or a mesh is not there.</exception>
    /// <exception cref="ProjectRefusedException">A file is not a mesh.</exception>
    /// <param name="root">The library.</param>
    /// <param name="path">The project.</param>
    /// <param name="mesh">The mesh file.</param>
    /// <param name="high">The detailed mesh it bakes from.</param>
    /// <param name="source">The file it was brought in from, when that is not <paramref name="mesh" />.</param>
    /// <param name="unwrapped">What Auto Unwrap did, when it wrote this mesh.</param>
    public static ProjectMesh AddMesh(
        string root,
        string path,
        string mesh,
        string? high,
        string? source = null,
        UnwrapReport? unwrapped = null
    ) {
        var (full, document) = Project(root, path);
        Require(mesh);
        if (high is not null) {
            Require(high);
        }

        var file = Bring(full, mesh);
        var held = document.Meshes.FirstOrDefault(m => m.File == file);
        // The same file again keeps its id, or the documents on it would point at nothing.
        var outside = Path.GetFullPath(mesh).StartsWith(full + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? null
            : Path.GetFullPath(mesh);
        var origin = source ?? outside;
        var added = new ProjectMesh(
            held?.Id ?? AssetId.New(),
            file,
            high is null ? held?.High : Bring(full, high),
            origin is null ? held?.Source : InLibrary(root, origin),
            LibraryFiles.Digest(Path.Combine(full, file))
        ) { Unwrapped = unwrapped };
        if (origin is not null) {
            RememberSource(root, added, origin);
        }

        var others = document.Meshes.Where(m => m.File != file);
        LibraryFiles.WriteProject(root, path, document with { Meshes = [..others, added] });
        return added;
    }

    /// <summary>The file a mesh came from on this machine, or null if it is not known here.</summary>
    public static string? SourceOf(string root, ProjectMesh mesh) {
        if (mesh.Source is { } held) {
            return Path.GetFullPath(LibraryLayout.Full(root, held));
        }

        var kept = LibraryLayout.MeshSourcePath(root, mesh.Id);
        return File.Exists(kept) ? File.ReadAllText(kept) : null;
    }

    /// <summary>
    ///     A path inside the library travels with it, so the project file holds that. One outside means nothing on
    ///     another machine and would put this one's folders in Git, so it stays in the cache, and a clone chooses the
    ///     file once.
    /// </summary>
    static void RememberSource(string root, ProjectMesh mesh, string origin) {
        var kept = LibraryLayout.MeshSourcePath(root, mesh.Id);
        if (mesh.Source is null) {
            Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
            LibraryFiles.Replace(kept, Path.GetFullPath(origin));
        } else if (File.Exists(kept)) {
            File.Delete(kept);
        }
    }

    static string? InLibrary(string root, string file) {
        var full = Path.GetFullPath(file);
        return full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? LibraryLayout.Relative(root, full)
            : null;
    }

    /// <summary>
    ///     Whether a file differs from the mesh the project has. The copy brought in is what counts: an unwrapped mesh
    ///     is a new file, and the one it came from stays beside it under its own name.
    /// </summary>
    /// <exception cref="FileNotFoundException">The project or the file is not there.</exception>
    public static bool MeshChanged(string root, string path, string mesh) {
        var (full, document) = Project(root, path);
        if (!File.Exists(mesh)) {
            throw new FileNotFoundException($"There is no file '{mesh}'.", mesh);
        }

        var name = Path.GetFileName(mesh);
        var held = Path.Combine(full, name);
        var same = File.Exists(held)
            && document.Meshes.Any(m => m.File == name || Path.GetFileName(SourceOf(root, m)) == name)
            && LibraryFiles.Digest(held) == LibraryFiles.Digest(mesh);
        return !same;
    }

    /// <summary>Brings a file into a project as what it is, which only a mesh or a picture can be.</summary>
    /// <exception cref="FileNotFoundException">The project or the file is not there.</exception>
    /// <exception cref="ProjectRefusedException">Nothing here reads it.</exception>
    public static void Import(string root, string path, string file) {
        if (IsMesh(file)) {
            AddMesh(root, path, file, null);
        } else if (PictureFormats.IsPicture(file)) {
            AddPicture(root, path, file);
        } else {
            throw new ProjectRefusedException(
                $"Nothing in Patina reads '{Path.GetFileName(file)}'. A project takes meshes ({Known(MeshExtensions)}) "
                + $"and pictures ({Known(PictureFormats.Pictures)})."
            );
        }
    }

    /// <summary>Brings a picture into a project's pictures folder, unless it is in the project already.</summary>
    /// <exception cref="FileNotFoundException">The project or the picture is not there.</exception>
    /// <exception cref="ProjectRefusedException">A layer could not read it.</exception>
    public static string AddPicture(string root, string path, string picture) {
        var (full, _) = Project(root, path);
        if (!PictureFormats.IsPicture(picture)) {
            throw new ProjectRefusedException(
                $"'{Path.GetFileName(picture)}' is not a picture a layer can read: "
                + $"use {Known(PictureFormats.Pictures)}."
            );
        }

        if (!File.Exists(picture)) {
            throw new FileNotFoundException($"There is no file '{picture}'.", picture);
        }

        var source = Path.GetFullPath(picture);
        if (source.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
            return Below(path, Path.GetRelativePath(full, source).Replace('\\', '/'));
        }

        Directory.CreateDirectory(Path.Combine(full, PictureFolder));
        var name = Path.GetFileName(source);
        File.Copy(source, Path.Combine(full, PictureFolder, name), true);
        return Below(path, $"{PictureFolder}/{name}");
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

    /// <summary>
    ///     Makes a mesh the project's only one: every texturing document goes on it, and the file before stays.
    /// </summary>
    /// <returns>The texturing documents.</returns>
    /// <exception cref="FileNotFoundException">The project or mesh is not there.</exception>
    public static IReadOnlyList<ProjectItem> UseMesh(string root, string path, AssetId mesh) {
        var (_, document) = Project(root, path);
        var kept = document.Meshes.FirstOrDefault(m => m.Id == mesh)
            ?? throw new FileNotFoundException($"This project has no mesh {mesh}.");
        List<ProjectItem> documents = [
            ..document.Documents.Select(d => d.Kind == DocumentKinds.Texturing ? d with { Mesh = mesh } : d)
        ];
        LibraryFiles.WriteProject(root, path, document with { Meshes = [kept], Documents = documents });
        return [..documents.Where(static d => d.Kind == DocumentKinds.Texturing)];
    }

    /// <summary>Says which material of the mesh a document paints.</summary>
    public static void SetMaterial(string root, string path, AssetId id, string material) {
        var (_, document) = Project(root, path);
        List<ProjectItem> documents = [
            ..document.Documents.Select(d => d.Id == id ? d with { Material = material } : d)
        ];
        LibraryFiles.WriteProject(root, path, document with { Documents = documents });
    }

    /// <summary>Keeps what a project's export is set to, in its <c>project.yaml</c>.</summary>
    /// <exception cref="FileNotFoundException">The project is not there.</exception>
    public static void SetExport(string root, string path, ProjectExportSettings settings) {
        ArgumentNullException.ThrowIfNull(settings);
        var (_, document) = Project(root, path);
        LibraryFiles.WriteProject(root, path, document with { Export = settings });
    }

    /// <summary>Says what happens to a mesh with no UV layout, and to paint on a new layout.</summary>
    public static void SetMeshHandling(
        string root,
        string path,
        bool? autoUnwrap,
        bool? preservePaint,
        UnwrapOptions? unwrap = null
    ) {
        var (_, document) = Project(root, path);
        LibraryFiles.WriteProject(
            root,
            path,
            document with {
                AutoUnwrap = autoUnwrap ?? document.AutoUnwrap,
                PreservePaint = preservePaint ?? document.PreservePaint,
                Unwrap = unwrap ?? document.Unwrap
            }
        );
    }

    /// <summary>Keeps how a project's bakes run, in its own file.</summary>
    public static void SetBake(string root, string path, ProjectBakeSettings bake) {
        var (_, document) = Project(root, path);
        LibraryFiles.WriteProject(root, path, document with { Bake = bake });
    }

    /// <summary>Keeps how the viewport is to open in a project.</summary>
    public static void SetViewport(string root, string path, ViewportDisplay viewport) {
        var (_, document) = Project(root, path);
        LibraryFiles.WriteProject(root, path, document with { Viewport = viewport });
    }

    /// <summary>Keeps what a project's texture sets are held to before export.</summary>
    public static void SetChecks(string root, string path, ProjectCheckSettings checks) {
        var (_, document) = Project(root, path);
        LibraryFiles.WriteProject(root, path, document with { Checks = checks });
    }

    /// <summary>Keeps how a project's painting was left.</summary>
    public static void SetPaint(string root, string path, ProjectPaintSettings paint) {
        var (_, document) = Project(root, path);
        LibraryFiles.WriteProject(root, path, document with { Paint = paint });
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

    /// <summary>
    ///     Records that a project reads a document of another one, which is how an edit to that document knows who it
    ///     reaches. Recording it twice is harmless.
    /// </summary>
    public static void AddReference(string root, string path, AssetId asset) {
        var (_, document) = Project(root, path);
        if (document.References.All(held => held.Asset != asset)) {
            LibraryFiles.WriteProject(
                root,
                path,
                document with { References = [..document.References, AssetReference.Floating(asset)] }
            );
        }
    }

    /// <summary>
    ///     A new document with another's content, as Save As and Duplicate make. A stack's paint is copied with it and
    ///     renamed, or two stacks would paint on one file.
    /// </summary>
    /// <exception cref="FileNotFoundException">The project or document is not there.</exception>
    /// <exception cref="ProjectRefusedException">The name cannot be used, or is taken.</exception>
    public static ProjectItem Copy(string root, string path, AssetId id, string name) {
        var (full, document) = Project(root, path);
        var item = document.Documents.FirstOrDefault(d => d.Id == id)
            ?? throw new FileNotFoundException($"This project has no document {id}.");
        var slug = NewProjects.Slug(name);
        var file = slug + Path.GetExtension(item.File);
        if (slug.Length == 0 || File.Exists(Path.Combine(full, file))) {
            throw new ProjectRefusedException($"'{name}' cannot be used here.");
        }

        var stem = Path.GetFileNameWithoutExtension(item.File);
        var text = File.ReadAllText(Path.Combine(full, item.File));
        foreach (var paint in StackInputs.PaintsOf(item.File, text)) {
            var renamed = paint.Replace(stem, slug, StringComparison.Ordinal);
            if (File.Exists(Path.Combine(full, paint))) {
                File.Copy(Path.Combine(full, paint), Path.Combine(full, renamed));
            }

            text = text.Replace(paint, renamed, StringComparison.Ordinal);
        }

        LibraryFiles.Replace(Path.Combine(full, file), text);
        var bake = GraphMeshMaps.FileOf(Path.Combine(full, item.File));
        if (File.Exists(bake)) {
            File.Copy(bake, GraphMeshMaps.FileOf(Path.Combine(full, file)));
        }

        var copy = item with { Id = AssetId.New(), File = file, Name = name };
        LibraryFiles.WriteProject(root, path, document with { Documents = [..document.Documents, copy] });
        return copy;
    }

    /// <summary>Says what a graph is for.</summary>
    public static ProjectItem SetRole(string root, string path, AssetId id, string role) {
        var (_, document) = Project(root, path);
        var item = document.Documents.FirstOrDefault(d => d.Id == id)
            ?? throw new FileNotFoundException($"This project has no document {id}.");
        if (item.Kind != DocumentKinds.Graph || !GraphRoles.Known(role)) {
            throw new ProjectRefusedException($"'{item.Name}' cannot be a {role}.");
        }

        var changed = item with { Role = role };
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

    /// <summary>Moves a project into the library's hidden trash, from where it can be moved back by hand.</summary>
    /// <exception cref="FileNotFoundException">It is not there.</exception>
    public static void Trash(string root, string path) {
        var (full, _) = Project(root, path);
        var trash = Path.Combine(root, LibraryLayout.TrashFolder);
        _ = Directory.CreateDirectory(trash);
        Directory.Move(full, Path.Combine(trash, $"{Path.GetFileName(full)}-{Guid.CreateVersion7():N}"));
    }

    /// <summary>
    ///     Copies a project beside itself. What it names of its own gets a new identity, so that the copy is an asset
    ///     apart. What it refers to in other projects stays referred to.
    /// </summary>
    /// <returns>The copy's path, below <c>projects/</c>.</returns>
    /// <exception cref="FileNotFoundException">The project is not there.</exception>
    /// <exception cref="ProjectRefusedException">The name cannot be used, or is taken.</exception>
    public static string Duplicate(string root, string path, string name) {
        var (full, document) = Project(root, path);
        var slug = NewProjects.Slug(name);
        var copy = Path.Combine(Path.GetDirectoryName(full)!, slug);
        if (slug.Length == 0 || Directory.Exists(copy)) {
            throw new ProjectRefusedException($"'{name}' cannot be used here.");
        }

        CopyFolder(full, copy);
        var file = Path.Combine(copy, LibraryLayout.ProjectFile);
        var text = File.ReadAllText(file);
        List<Guid> ids = [
            document.Id.Value,
            ..document.Meshes.Select(static mesh => mesh.Id.Value),
            ..document.Documents.Select(static item => item.Id.Value)
        ];
        foreach (var id in ids.Where(static id => id != Guid.Empty)) {
            text = text.Replace(id.ToString(), Guid.CreateVersion7().ToString(), StringComparison.OrdinalIgnoreCase);
        }

        LibraryFiles.Replace(file, text);
        var parent = path.Contains('/') ? path[..path.LastIndexOf('/')] + "/" : string.Empty;
        Rename(root, parent + slug, name);
        return parent + slug;
    }

    static void CopyFolder(string from, string to) {
        _ = Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from)) {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var folder in Directory.EnumerateDirectories(from)) {
            CopyFolder(folder, Path.Combine(to, Path.GetFileName(folder)));
        }
    }

    /// <summary>Checks a file can be a mesh, before anything is written.</summary>
    /// <exception cref="FileNotFoundException">It is not there.</exception>
    /// <exception cref="ProjectRefusedException">It is not a mesh.</exception>
    public static void Require(string mesh) {
        if (!IsMesh(mesh)) {
            throw new ProjectRefusedException(
                $"'{Path.GetFileName(mesh)}' is not a mesh: use {Known(MeshExtensions)}."
            );
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
                && !file.StartsWith(LibraryLayout.PaintBackupFolder + "/", StringComparison.Ordinal)
            )
            .Take(MaxFiles)
    ];

    static List<ProjectFile> Bakes(string full, string path, IReadOnlyList<ProjectMesh> meshes) {
        var bakes = Path.Combine(full, BakeFolder);
        return [
            ..DocumentBakes.Below(bakes)
                .Select(bake => new ProjectFile(
                        "bake",
                        Path.GetRelativePath(bakes, bake).Replace('\\', '/'),
                        Below(path, Path.GetRelativePath(full, bake).Replace('\\', '/')),
                        OutOfDate(bake, meshes)
                    )
                )
        ];
    }

    /// <summary>
    ///     Whether a bake was made on a mesh the project no longer has. One that does not say is left alone.
    /// </summary>
    static bool OutOfDate(string bake, IReadOnlyList<ProjectMesh> meshes) {
        HashSet<string?> current = [..meshes.Select(static mesh => mesh.Hash)];
        current.Remove(null);
        return current.Count > 0
            && Directory.EnumerateFiles(bake, $"*{LibraryLayout.BakeManifestFile}")
                .Select(ManifestMesh)
                .Any(mesh => mesh is not null && !current.Contains(mesh));
    }

    static string? ManifestMesh(string file) {
        try {
            return JsonSerializer.Deserialize(File.ReadAllBytes(file), ContractsJsonContext.Default.BakeManifest)?.Mesh;
        } catch (JsonException) {
            return null; // something half written says nothing about the mesh
        }
    }

    /// <summary>A document's bake below the library, if it has one and it is there.</summary>
    static string? BakeOf(string full, string path, ProjectItem item) =>
        DocumentBakes.FolderOf(full, item) is { } bake
            ? Below(path, Path.GetRelativePath(full, bake).Replace('\\', '/'))
            : null;

    static ProjectMeshInfo Listed(string root, string path, ProjectMesh mesh) =>
        new(
            mesh.Id.Value,
            Below(path, mesh.File),
            mesh.High is { } high ? Below(path, high) : null,
            SourceOf(root, mesh)
        ) { Unwrapped = mesh.Unwrapped };

    static ProjectDocumentInfo Listed(string full, string path, ProjectItem item) =>
        new(item.Id.Value, item.Kind, item.Name, Below(path, item.File), item.Mesh?.Value, item.Role, item.Material) {
            Bake = BakeOf(full, path, item)
        };

    static bool IsMesh(string file) => MeshExtensions.Contains(Path.GetExtension(file));

    /// <summary>A picture of the user's: what the Server drew for a mesh mask is not one.</summary>
    static bool IsPicture(string file) =>
        PictureFormats.IsPicture(file)
        && !Path.GetFileName(file).StartsWith(LibraryLayout.MeshMaskPrefix, StringComparison.Ordinal);

    static string Known(IEnumerable<string> extensions) => string.Join(", ", extensions.Order(StringComparer.Ordinal));

    /// <summary>The kind a file not yet listed is taken for. A stack that names no mesh still opens as one.</summary>
    static string? Unlisted(string file) {
        var extension = Path.GetExtension(file);
        return string.Equals(extension, StackExtension, StringComparison.OrdinalIgnoreCase) ? DocumentKinds.Texturing
            : string.Equals(extension, LibraryLayout.GraphExtension, StringComparison.OrdinalIgnoreCase) ? DocumentKinds
                .Graph
            : string.Equals(extension, BrushPresets.Extension, StringComparison.OrdinalIgnoreCase) ? DocumentKinds
                .BrushPreset
            : null;
    }

    static string Below(string project, string file) => $"{LibraryLayout.ProjectsFolder}/{project}/{file}";
}
