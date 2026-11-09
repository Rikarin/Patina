namespace Patina.Contracts;

// SK7005: a wire record's constructor is its JSON shape, one parameter per field, so fewer parameters means a new API.
#pragma warning disable SK7005
/// <summary>The reply to <c>GET /api/v1/library</c>: what opening a library reads, and nothing more.</summary>
/// <param name="Id">The library's identity, from <c>library.yaml</c>.</param>
/// <param name="Name">The library's display name.</param>
/// <param name="Path">The library's folder on the Server's machine.</param>
/// <param name="SchemaVersion">The version of <c>library.yaml</c> as it stands on disk.</param>
/// <param name="TexelDensity">The texel density new projects start from, in texels per metre.</param>
/// <param name="Channels">The channels the library authors.</param>
/// <param name="DefaultShader">The shader new projects start from.</param>
/// <param name="ExportTargets">The export targets the library builds for.</param>
/// <param name="ProjectCount">The projects in the index.</param>
/// <param name="AssetCount">The shared assets in the index.</param>
/// <param name="Indexing">Whether a rescan is running; counts and pages may lag the files until it ends.</param>
/// <param name="Export">
///     What an export of the whole library was last set to, from <c>library.yaml</c>, or null. A project's export is
///     in its own contents.
/// </param>
public sealed record LibraryInfo(
    Guid Id,
    string Name,
    string Path,
    int SchemaVersion,
    double TexelDensity,
    IReadOnlyList<string> Channels,
    string DefaultShader,
    IReadOnlyList<string> ExportTargets,
    int ProjectCount,
    int AssetCount,
    bool Indexing,
    ProjectExportSettings? Export
);
#pragma warning restore SK7005

/// <summary>One project as the browser shows it. The project itself loads on open.</summary>
/// <param name="Id">The project's identity.</param>
/// <param name="Name">The project's display name.</param>
/// <param name="Path">The project's folder, relative to <c>projects/</c>, with forward slashes.</param>
/// <param name="Tags">The project's tags, sorted.</param>
/// <param name="ReferenceCount">How many shared assets the project references directly.</param>
public sealed record ProjectSummary(Guid Id, string Name, string Path, IReadOnlyList<string> Tags, int ReferenceCount);

/// <summary>One page of projects.</summary>
/// <param name="Items">The projects on this page.</param>
/// <param name="Total">How many projects match, on all pages.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record ProjectPage(IReadOnlyList<ProjectSummary> Items, int Total, int Offset, int Limit);

/// <summary>The body of <c>POST /api/v1/library/projects</c>.</summary>
/// <param name="Name">The project's display name. The Server makes its folder from it.</param>
/// <param name="Folder">
///     Folders below <c>projects/</c> to put it in, with forward slashes, such as <c>props</c>.
/// </param>
/// <param name="Tags">The project's tags.</param>
/// <param name="Mesh">A mesh file on the Server's machine to copy in. Null leaves the project to get one later.</param>
/// <param name="Resolution">
///     The width and height of a stack it starts with, or the side a graph it starts with evaluates at. Null is 2048.
/// </param>
/// <param name="Kind">
///     The document it starts with: <c>texturing</c>, <c>layered-material</c>, <c>image-material</c> or <c>graph</c>.
///     Null is <c>texturing</c> with a mesh and nothing without one.
/// </param>
/// <param name="Image">The picture an <c>image-material</c> starts from, on the Server\'s machine.</param>
/// <param name="High">The detailed mesh it bakes from, on the Server's machine.</param>
public sealed record ProjectCreateRequest(
    string Name,
    string? Folder = null,
    IReadOnlyList<string>? Tags = null,
    string? Mesh = null,
    int? Resolution = null,
    string? Kind = null,
    string? Image = null,
    string? High = null
);

/// <summary>A result of a bake in a project's folder.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Name">Its name.</param>
/// <param name="Path">Its path below the library.</param>
/// <param name="Stale">For a bake, whether the mesh has changed since it was made.</param>
public sealed record ProjectFile(string Kind, string Name, string Path, bool Stale = false);

/// <summary>A mesh in a project.</summary>
/// <param name="Id">Its id. Null until the project lists it.</param>
/// <param name="Path">Its path below the library.</param>
/// <param name="High">The detailed mesh it bakes from, below the library.</param>
/// <param name="Source">The file it was brought in from, which reloading reads again.</param>
public sealed record ProjectMeshInfo(Guid? Id, string Path, string? High, string? Source = null) {
    /// <summary>What Auto Unwrap did, for a mesh it wrote.</summary>
    public UnwrapReport? Unwrapped { get; init; }
}

/// <summary>Something in a project that can be opened and edited (ADR 0012).</summary>
/// <param name="Id">Its GUID. Null until the project lists it.</param>
/// <param name="Kind"><c>texturing</c>, <c>layered-material</c>, <c>image-material</c> or <c>graph</c>.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Path">Its path below the library, which the graph and stack routes take.</param>
/// <param name="Mesh">The id of the mesh it is on.</param>
/// <param name="Role">What a graph is for: <c>material</c>, <c>mask</c>, <c>brush</c> or <c>filter</c>.</param>
/// <param name="Material">The material of the mesh it paints, as one of the project's texture sets.</param>
public sealed record ProjectDocumentInfo(
    Guid? Id,
    string Kind,
    string Name,
    string Path,
    Guid? Mesh,
    string? Role = null,
    string? Material = null) {
    /// <summary>The folder of the bake it reads, below the library, or null.</summary>
    public string? Bake { get; init; }

    /// <summary>What it can be searched by, sorted.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>A project as it opens.</summary>
/// <param name="Id">The project's GUID.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Path">Its path below <c>projects/</c>.</param>
/// <param name="Meshes">Its meshes.</param>
/// <param name="Documents">What can be opened in it.</param>
/// <param name="BakeFolder">Where its bakes go, below the library.</param>
/// <param name="Bakes">The bakes it has.</param>
/// <param name="Pictures">The pictures brought in for layers and masks.</param>
public sealed record ProjectContents(
    Guid Id,
    string Name,
    string Path,
    IReadOnlyList<ProjectMeshInfo> Meshes,
    IReadOnlyList<ProjectDocumentInfo> Documents,
    string BakeFolder,
    IReadOnlyList<ProjectFile> Bakes,
    IReadOnlyList<ProjectFile> Pictures
) {
    /// <summary>Whether a mesh with no UV layout is given one when it is set.</summary>
    public bool AutoUnwrap { get; init; } = true;

    /// <summary>What the project can be searched by, sorted.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    ///     The texture sets an export writes, by name: what <see cref="ExportOptions.Sets" /> chooses from. Empty for a
    ///     project of one unnamed set.
    /// </summary>
    public IReadOnlyList<string> ExportSets { get; init; } = [];

    /// <summary>What the project's last export was set to, from its <c>project.yaml</c>, or null.</summary>
    public ProjectExportSettings? Export { get; init; }

    /// <summary>Whether paint moves to a new mesh's layout.</summary>
    public bool PreservePaint { get; init; } = true;

    /// <summary>How Auto Unwrap lays a mesh out.</summary>
    public UnwrapOptions Unwrap { get; init; } = new();

    /// <summary>How its bakes run, which texture sets they make maps for, and what they read beyond its mesh.</summary>
    public ProjectBakeSettings Bake { get; init; } = new();

    /// <summary>How the viewport was left in this project, for a client to put back, or null for as it opens.</summary>
    public ViewportDisplay? Viewport { get; init; }

    /// <summary>How its painting was left: recent colours, palette, brush and symmetry, or null for none yet.</summary>
    public ProjectPaintSettings? Paint { get; init; }
}

/// <summary>The body of <c>POST /api/v1/library/project/meshes</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Mesh">A mesh file on the Server's machine.</param>
/// <param name="High">The detailed mesh it bakes from.</param>
public sealed record ProjectMeshRequest(string Project, string Mesh, string? High = null);

/// <summary>Whether reloading a mesh has work to do.</summary>
/// <param name="Changed">The file differs from the one the project has.</param>
public sealed record ProjectMeshChange(bool Changed);

/// <summary>A file to bring into a project as whatever it is.</summary>
/// <param name="Project">The project's path.</param>
/// <param name="File">The file, anywhere the Server can read.</param>
public sealed record ProjectFileRequest(string Project, string File);

/// <summary>The body of <c>PATCH /api/v1/library/project/settings</c>. Null leaves a setting as it is.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Mesh">
///     A file the Server can read: the project's mesh from now on, under all its texturing documents.
/// </param>
/// <param name="High">A file the Server can read: the detailed mesh it bakes from.</param>
/// <param name="AutoUnwrap">
///     Whether a mesh with no UV layout is given one, in a new file beside it. True also applies it to the mesh the
///     project has now.
/// </param>
/// <param name="PreservePaint">
///     Whether paint moves to a new mesh's layout, by where it sits on the surface. Open documents have to be closed
///     first, as it rewrites their paint files.
/// </param>
/// <param name="Unwrap">
///     How Auto Unwrap lays a mesh out. A mesh it already wrote is unwrapped again with these, from the file it came
///     from, and its paint moves across as a new mesh's would.
/// </param>
public sealed record ProjectSettingsRequest(
    string Project,
    string? Mesh,
    string? High,
    bool? AutoUnwrap,
    bool? PreservePaint = null,
    UnwrapOptions? Unwrap = null
);

/// <summary>One preset file as a project's export last had it.</summary>
/// <param name="Preset">The preset it is a file of.</param>
/// <param name="Template">Which file: its naming template.</param>
/// <param name="Enabled">Whether it is written.</param>
/// <param name="Format">The format it is written in.</param>
public sealed record ExportFileSetting(string Preset, string Template, bool Enabled, string Format);

/// <summary>
///     A project's export settings, kept in its <c>project.yaml</c> so they travel with the library: what a client's
///     export starts from next time. Any client reads and writes them; a build reads only what a request sends, so
///     keeping them changes no key.
/// </summary>
public sealed record ProjectExportSettings {
    /// <summary>The preset's id.</summary>
    public string? Preset { get; init; }

    /// <summary>Where files go, or null for the library's exports folder.</summary>
    public string? Folder { get; init; }

    /// <summary>The longest side of a file, or null for the maps' own size.</summary>
    public int? Size { get; init; }

    /// <summary>How a file changes size: box, bilinear or nearest.</summary>
    public string? Filter { get; init; }

    /// <summary>What fills the background of the mesh's layout.</summary>
    public ExportPaddingInfo? Padding { get; init; }

    /// <summary>Whether 8-bit files are dithered.</summary>
    public bool? Dither { get; init; }

    /// <summary>The texture sets left out, by name; every other one is written.</summary>
    public IReadOnlyList<string> Skipped { get; init; } = [];

    /// <summary>Files turned off or written in another format, per preset.</summary>
    public IReadOnlyList<ExportFileSetting> Files { get; init; } = [];
}

/// <summary>The body of <c>PUT /api/v1/library/project/export</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Export">What its export is set to now.</param>
public sealed record ProjectExportRequest(string Project, ProjectExportSettings Export);

/// <summary>The body of <c>PUT /api/v1/library/export</c>.</summary>
/// <param name="Export">What an export of the whole library is set to now.</param>
public sealed record LibraryExportRequest(ProjectExportSettings Export);

/// <summary>The body of <c>PUT /api/v1/library/project/viewport</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Viewport">
///     How the viewport is to open in this project from now on. It is kept, not applied: <c>PUT /viewport/display</c>
///     shows it.
/// </param>
public sealed record ProjectViewportRequest(string Project, ViewportDisplay Viewport);

/// <summary>The body of <c>POST /api/v1/library/project/documents</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Kind"><c>texturing</c>, <c>layered-material</c> or <c>graph</c>.</param>
/// <param name="Name">Its display name. Its file is made from it.</param>
/// <param name="Mesh">The mesh a <c>texturing</c> document is on.</param>
/// <param name="Resolution">The width and height of a stack, or the side a graph evaluates at. Null is 2048.</param>
/// <param name="Image">The picture an <c>image-material</c> starts from, on the Server's machine.</param>
public sealed record ProjectDocumentRequest(
    string Project,
    string Kind,
    string Name,
    Guid? Mesh = null,
    int? Resolution = null,
    string? Image = null
);

/// <summary>The body of <c>PATCH /api/v1/library/project/document/role</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Document">The graph.</param>
/// <param name="Role"><c>material</c>, <c>mask</c>, <c>brush</c> or <c>filter</c>.</param>
public sealed record ProjectDocumentRoleRequest(string Project, Guid Document, string Role);

/// <summary>The body of <c>POST /api/v1/library/project/document/copy</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Document">The document.</param>
/// <param name="Name">What the copy is called.</param>
public sealed record ProjectDocumentCopyRequest(string Project, Guid Document, string Name);

/// <summary>Whether a document has work autosaved after its last save.</summary>
/// <param name="Saved">When it was kept, or null when there is nothing to recover.</param>
public sealed record DocumentRecovery(DateTimeOffset? Saved);

/// <summary>What to do with autosaved work, with the document closed.</summary>
/// <param name="Path">The document, below the library.</param>
/// <param name="Restore">True puts it in place of the file, and false forgets it.</param>
public sealed record DocumentRecoveryRequest(string Path, bool Restore);

/// <summary>A graph rendered to a picture a brush stamps.</summary>
/// <param name="Alpha">The picture, relative to the project's stacks, as a brush names it.</param>
public sealed record MaterialBrush(string Alpha);

/// <summary>The body of <c>POST /api/v1/library/project/pictures</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Picture">A picture file on the Server's machine.</param>
public sealed record ProjectPictureRequest(string Project, string Picture);

/// <summary>A picture a project now holds.</summary>
/// <param name="Path">Its path below the library, which a layer or a mask reads.</param>
public sealed record ProjectPicture(string Path);

/// <summary>The body of <c>PATCH /api/v1/library/project/document</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Document">The texturing document.</param>
/// <param name="Mesh">The mesh it goes on.</param>
public sealed record ProjectDocumentMeshRequest(string Project, Guid Document, Guid Mesh);

/// <summary>The body of <c>PATCH /api/v1/library/project</c>.</summary>
/// <param name="Path">The project, below <c>projects/</c>.</param>
/// <param name="Name">Its new display name. Its folder stays.</param>
public sealed record ProjectRenameRequest(string Path, string Name);

/// <summary>The body of <c>POST /api/v1/library/project/duplicate</c>.</summary>
/// <param name="Path">The project, below <c>projects/</c>.</param>
/// <param name="Name">The copy's display name.</param>
public sealed record ProjectDuplicateRequest(string Path, string Name);

/// <summary>Something a layer can be filled with (ADR 0012).</summary>
/// <param name="Name">What it is called.</param>
/// <param name="Kind"><c>graph</c>, which a fill reads, or <c>layered-material</c>, which unfolds into layers.</param>
/// <param name="Source">What names it to the routes that use it.</param>
/// <param name="Role">
///     <c>material</c>, <c>mask</c>, <c>brush</c>, <c>filter</c>, or <c>empty</c> for a graph with no
///     output yet.
/// </param>
/// <param name="Origin"><c>shipped</c>, <c>project</c>, or <c>library</c> for another project.</param>
/// <param name="Channels">The maps a graph makes, by usage.</param>
/// <param name="Parameters">The knobs a graph exposes, which a layer can set.</param>
/// <param name="Project">The project it lives in, when that is another one.</param>
public sealed record MaterialInfo(
    string Name,
    string Kind,
    string Source,
    string Role,
    string Origin,
    IReadOnlyList<string> Channels,
    IReadOnlyList<GraphSettingDefinition>? Parameters = null,
    string? Project = null
);

/// <summary>The materials a project can use.</summary>
/// <param name="Items">Them, every one with each word of the search, if there was one.</param>
/// <param name="Roles">The types a shelf can narrow to, with what to call them.</param>
/// <param name="Origins">Where a material can be from, with what to call each.</param>
public sealed record MaterialList(
    IReadOnlyList<MaterialInfo> Items,
    IReadOnlyList<StackTerm> Roles,
    IReadOnlyList<StackTerm> Origins
);

/// <summary>The body of <c>POST /api/v1/library/materials/use</c>.</summary>
/// <param name="Project">The project using it.</param>
/// <param name="Source">The graph, below the library.</param>
public sealed record MaterialUseRequest(string Project, string Source);

/// <summary>A graph ready for a layer.</summary>
/// <param name="Graph">What a fill layer's <c>graph</c> takes.</param>
public sealed record MaterialUse(string Graph);

/// <summary>A folder of projects.</summary>
/// <param name="Path">Its path below <c>projects/</c>, with forward slashes.</param>
/// <param name="Name">Its name.</param>
/// <param name="Color">Its colour as <c>#rrggbb</c>, or null for none.</param>
/// <param name="ProjectCount">How many projects it holds, at any depth.</param>
public sealed record FolderSummary(string Path, string Name, string? Color, int ProjectCount);

/// <summary>The folders directly in a folder.</summary>
/// <param name="Items">The folders, by name.</param>
public sealed record FolderList(IReadOnlyList<FolderSummary> Items);

/// <summary>The body of <c>POST /api/v1/library/folders</c>.</summary>
/// <param name="Name">The folder's name, which is its name on disk.</param>
/// <param name="Parent">The folder to make it in. Null is the top.</param>
/// <param name="Color">Its colour as <c>#rrggbb</c>.</param>
public sealed record FolderCreateRequest(string Name, string? Parent = null, string? Color = null);

/// <summary>The body of <c>PATCH /api/v1/library/folders</c>. What is left out stays.</summary>
/// <param name="Path">The folder.</param>
/// <param name="Name">Its new name.</param>
/// <param name="Color">Its new colour as <c>#rrggbb</c>, or empty for none.</param>
public sealed record FolderUpdateRequest(string Path, string? Name = null, string? Color = null);

/// <summary>The body of <c>POST /api/v1/library/move</c>.</summary>
/// <param name="Paths">Folders and projects to move, below <c>projects/</c>.</param>
/// <param name="Into">The folder to put them in. Null is the top.</param>
public sealed record LibraryMoveRequest(IReadOnlyList<string> Paths, string? Into = null);

/// <summary>Where things went.</summary>
/// <param name="Paths">The new paths, in the order asked.</param>
public sealed record LibraryMoved(IReadOnlyList<string> Paths);

/// <summary>A tag, and how many items carry it.</summary>
/// <param name="Tag">The tag.</param>
/// <param name="Count">How many projects or assets carry it.</param>
public sealed record TagCount(string Tag, int Count);

/// <summary>One page of tags, by name.</summary>
/// <param name="Items">The tags on this page.</param>
/// <param name="Total">How many tags there are, on all pages.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record TagPage(IReadOnlyList<TagCount> Items, int Total, int Offset, int Limit);

/// <summary>One document other projects can use, as the browser shows it.</summary>
/// <param name="Id">The asset's identity.</param>
/// <param name="Kind">The asset's kind, such as <c>smart-material</c>.</param>
/// <param name="Name">The asset's display name.</param>
/// <param name="Path">The asset's file, below the library, with forward slashes.</param>
/// <param name="Tags">The asset's tags, sorted.</param>
/// <param name="Revision">The content hash of the asset's file, or null when the file is missing.</param>
/// <param name="UsageCount">How many projects reference the asset directly.</param>
public sealed record AssetSummary(
    Guid Id,
    string Kind,
    string Name,
    string Path,
    IReadOnlyList<string> Tags,
    string? Revision,
    int UsageCount
);

/// <summary>One page of documents.</summary>
/// <param name="Items">The assets on this page.</param>
/// <param name="Total">How many assets match, on all pages.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record AssetPage(IReadOnlyList<AssetSummary> Items, int Total, int Offset, int Limit);

/// <summary>A project that depends on a shared asset.</summary>
/// <param name="Id">The project's identity.</param>
/// <param name="Name">The project's display name.</param>
/// <param name="Path">The project's folder, relative to <c>projects/</c>, with forward slashes.</param>
/// <param name="Pinned">True when a pin holds the project back, so a change to the asset does not reach it.</param>
public sealed record ImpactedProject(Guid Id, string Name, string Path, bool Pinned);

/// <summary>What saving a shared asset would touch: "affects 412 projects, 3 pinned" (plan §5).</summary>
/// <param name="Asset">The asset asked about.</param>
/// <param name="Projects">The projects that depend on the asset, directly or through other assets.</param>
/// <param name="Pinned">How many of <paramref name="Projects" /> a pin holds back; the rest rebuild.</param>
/// <param name="UnreadableDocuments">
///     Project and asset documents the index could not read. They may depend on the asset too, so a value above zero
///     means the counts are a lower bound.
/// </param>
/// <param name="Items">One page of the dependent projects.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record AssetImpact(
    AssetSummary Asset,
    int Projects,
    int Pinned,
    int UnreadableDocuments,
    IReadOnlyList<ImpactedProject> Items,
    int Offset,
    int Limit
);
