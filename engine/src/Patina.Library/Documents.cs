using Patina.Contracts;

namespace Patina.Library;

/// <summary>How a reference follows a shared asset (plan §5).</summary>
public enum ReferenceMode {
    /// <summary>Follows the asset: the default. The asset's newest bytes are the ones used.</summary>
    Floating,

    /// <summary>Stays at one revision, whatever happens to the asset.</summary>
    Pinned
}

/// <summary>
///     A reference to a shared asset, by GUID so that moving files breaks nothing. A pin freezes the bytes of that
///     one file; what the file depends on still resolves through its own references. Build keys and the impact query
///     both follow that rule, and a test holds them to each other.
/// </summary>
public sealed record AssetReference(AssetId Asset, ReferenceMode Mode, Revision? Revision) {
    public static AssetReference Floating(AssetId asset) => new(asset, ReferenceMode.Floating, null);

    public static AssetReference Pinned(AssetId asset, Revision revision) => new(asset, ReferenceMode.Pinned, revision);
}

/// <summary>
///     An output a project builds, such as a baked map. Everything its key depends on is named here, so a change to
///     anything else in the project leaves it fresh. The tool's version is deliberately not here: it belongs to the
///     installed software, and a kernel update must stale an output without anyone touching the project.
/// </summary>
/// <param name="Name">The output's name, unique in the project. A label: renaming one rebuilds nothing.</param>
/// <param name="Tool">The tool or kernel that builds it, as <see cref="ToolCatalog" /> knows it.</param>
/// <param name="Files">Project files it reads, relative to the project, with forward slashes.</param>
/// <param name="Uses">The references it reads; each must be among the project's references.</param>
/// <param name="Settings">The tool's settings, as text.</param>
public sealed record OutputDeclaration(
    string Name,
    string Tool,
    IReadOnlyList<string> Files,
    IReadOnlyList<AssetId> Uses,
    IReadOnlyDictionary<string, string> Settings
);

/// <summary><c>library.yaml</c>: the one file opening a library reads.</summary>
public sealed record LibraryDocument(
    LibraryId Id,
    string Name,
    double TexelDensity,
    IReadOnlyList<string> Channels,
    string DefaultShader,
    IReadOnlyList<string> ExportTargets
) {
    /// <summary>
    ///     What an export of the whole library was last set to, so the next one starts there, or null. A project's own
    ///     export is in its <c>project.yaml</c>; this one belongs to no project.
    /// </summary>
    public ProjectExportSettings? Export { get; init; }

    /// <summary>
    ///     The export preset a project's export starts from when it has kept none, and the one its checks measure
    ///     against, or null for the factory's starter (#240).
    /// </summary>
    public string? DefaultPreset { get; init; }
}

/// <summary><c>project.yaml</c>.</summary>
public sealed record ProjectDocument(
    ProjectId Id,
    string Name,
    IReadOnlyList<string> Tags,
    IReadOnlyList<AssetReference> References,
    IReadOnlyList<OutputDeclaration> Outputs
) {
    /// <summary>The meshes in the project's folder.</summary>
    public IReadOnlyList<ProjectMesh> Meshes { get; init; } = [];

    /// <summary>What can be opened and edited in the project (ADR 0012).</summary>
    public IReadOnlyList<ProjectItem> Documents { get; init; } = [];

    /// <summary>Whether a mesh with no UV layout is given one when it is set.</summary>
    public bool AutoUnwrap { get; init; } = true;

    /// <summary>Whether paint moves to a new mesh's layout, by where it sits on the surface.</summary>
    public bool PreservePaint { get; init; } = true;

    /// <summary>How Auto Unwrap lays a mesh out.</summary>
    public UnwrapOptions Unwrap { get; init; } = new();

    /// <summary>What the project's last export was set to, so the next one starts there, or null.</summary>
    public ProjectExportSettings? Export { get; init; }

    /// <summary>How its bakes run, or null for the defaults, which writes nothing.</summary>
    public ProjectBakeSettings? Bake { get; init; }

    /// <summary>How the viewport was left in this project, or null for as it opens.</summary>
    public ViewportDisplay? Viewport { get; init; }

    /// <summary>How its painting was left: recent colours, palette, brush and symmetry. Null writes nothing.</summary>
    public ProjectPaintSettings? Paint { get; init; }

    /// <summary>What its texture sets are held to before export, or null for no limits, which writes nothing.</summary>
    public ProjectCheckSettings? Checks { get; init; }
}

/// <summary>A mesh a project owns.</summary>
/// <param name="Id">Its identity, which a document refers to it by.</param>
/// <param name="File">Its file, relative to the project.</param>
/// <param name="High">The detailed mesh it bakes from, relative to the project, or null.</param>
/// <param name="Source">The file it was brought in from, which Reload Mesh reads again.</param>
/// <param name="Hash">The digest of its file, which a bake records so that it can tell it is out of date.</param>
public sealed record ProjectMesh(
    AssetId Id,
    string File,
    string? High = null,
    string? Source = null,
    string? Hash = null
) {
    /// <summary>What Auto Unwrap did, for a mesh it wrote.</summary>
    public UnwrapReport? Unwrapped { get; init; }
}

/// <summary>A document a project lists.</summary>
/// <param name="Id">Its identity, which a reference to it survives a move by.</param>
/// <param name="Kind">One of <see cref="DocumentKinds" />.</param>
/// <param name="File">Its file, relative to the project.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Mesh">The mesh it is on. A <c>texturing</c> document has one and no other kind does.</param>
/// <param name="Role">What a graph is for, one of <see cref="GraphRoles" />. Null is a material.</param>
/// <param name="Material">The material of the mesh a texturing document paints, as one of its texture sets.</param>
public sealed record ProjectItem(
    AssetId Id,
    string Kind,
    string File,
    string Name,
    AssetId? Mesh = null,
    string? Role = null,
    string? Material = null
) {
    /// <summary>What it can be searched by.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The other documents it reads, which an edit to one of them reaches.</summary>
    public IReadOnlyList<AssetReference> Dependencies { get; init; } = [];
}

/// <summary>What a graph is for, which decides where it is offered (ADR 0012).</summary>
public static class GraphRoles {
    /// <summary>Maps by usage, which fill a layer's channels.</summary>
    public const string Material = "material";

    /// <summary>One map, which decides where a layer shows.</summary>
    public const string Mask = "mask";

    /// <summary>One map, which a brush stamps.</summary>
    public const string Brush = "brush";

    /// <summary>An image in and an image out, which changes what is under it or a mask.</summary>
    public const string Filter = "filter";

    public static bool Known(string role) => role is Material or Mask or Brush or Filter;

    /// <summary>
    ///     Whether a kind of document can have a role: a graph any of them, and a layered material a material or a
    ///     mask, which is a smart mask saved from a layer (#135).
    /// </summary>
    public static bool Fits(string kind, string role) =>
        kind == DocumentKinds.Graph ? Known(role) : kind == DocumentKinds.LayeredMaterial && role is Material or Mask;
}

/// <summary>What a document is for, which decides the workspace it opens in (ADR 0012).</summary>
public static class DocumentKinds {
    /// <summary>Layers and paint on a mesh, ending in textures for one asset.</summary>
    public const string Texturing = "texturing";

    /// <summary>Layers with no mesh, making a material others can use.</summary>
    public const string LayeredMaterial = "layered-material";

    /// <summary>A material made from one photograph.</summary>
    public const string ImageMaterial = "image-material";

    /// <summary>A node graph, making a material, a mask, a brush or a filter.</summary>
    public const string Graph = "graph";

    /// <summary>
    ///     A brush or tool preset: what the pointer paints as and with. It opens no workspace; the brush takes it up.
    /// </summary>
    public const string BrushPreset = "brush-preset";

    public static bool Known(string kind) =>
        kind is Texturing or LayeredMaterial or ImageMaterial or Graph or BrushPreset;
}

/// <summary>
///     <c>folder.yaml</c>: what a folder of projects remembers. It exists even with nothing to say, because Git keeps
///     no empty folder.
/// </summary>
/// <param name="Color">The folder's colour as <c>#rrggbb</c>, or null for none.</param>
public sealed record FolderDocument(string? Color = null);

/// <summary>
///     The sidecar of an asset in a library's old <c>shared/</c> folder, which only the move to a project reads.
/// </summary>
public sealed record AssetDocument(
    AssetId Id,
    string Kind,
    string Name,
    IReadOnlyList<string> Tags,
    IReadOnlyList<AssetReference> Dependencies
);

/// <summary>A document as read, with what the check needs to know about the file behind it.</summary>
/// <param name="Document">The document at the current schema.</param>
/// <param name="FileSchema">The schema version the file was written at, before any migration.</param>
/// <param name="UnknownKeys">Keys the current schema does not know. The next save would lose them.</param>
public sealed record ReadDocument<T>(T Document, int FileSchema, IReadOnlyList<string> UnknownKeys);

/// <summary>A file that is not a document this Patina can use.</summary>
public sealed class LibraryFormatException : Exception {
    public LibraryFormatException() { }

    public LibraryFormatException(string message) : base(message) { }

    public LibraryFormatException(string message, Exception inner) : base(message, inner) { }

    public LibraryFormatException(string path, string problem, bool newerSchema) : base($"{path}: {problem}") {
        NewerSchema = newerSchema;
    }

    /// <summary>True when the file is fine but was written by a newer Patina than this one.</summary>
    public bool NewerSchema { get; }
}
