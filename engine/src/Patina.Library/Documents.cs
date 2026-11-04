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
);

/// <summary><c>project.yaml</c>.</summary>
public sealed record ProjectDocument(
    ProjectId Id,
    string Name,
    IReadOnlyList<string> Tags,
    IReadOnlyList<AssetReference> References,
    IReadOnlyList<OutputDeclaration> Outputs
);

/// <summary>A shared asset's sidecar: its identity, and the other shared assets it reads.</summary>
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
