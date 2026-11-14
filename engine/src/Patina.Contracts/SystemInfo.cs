namespace Patina.Contracts;

/// <summary>The reply to <c>GET /api/v1/system</c>.</summary>
/// <param name="Version">The Server's release version.</param>
/// <param name="ApiVersion">The major version of the REST API; it is the <c>v1</c> in the URL.</param>
/// <param name="Library">The library this Server owns, or null when it was started without one.</param>
/// <param name="Host">The machine and runtime the Server runs on.</param>
/// <param name="Worker">The Worker process as the Server sees it.</param>
/// <param name="TextureSides">The texture sides a client offers, smallest first.</param>
/// <param name="DefaultTextureSide">The side a new document gets when nobody says.</param>
public sealed record SystemInfo(
    string Version,
    int ApiVersion,
    string? Library,
    HostInfo Host,
    WorkerStatus Worker,
    IReadOnlyList<int> TextureSides,
    int DefaultTextureSide
) {
    /// <summary>The version of Vixen, the engine library the graphs, layers and bakes run on (#190).</summary>
    public string? Vixen { get; init; }

    /// <summary>What can be read, for a client's file dialogs.</summary>
    public FileFormats Formats { get; init; } =
        new(PictureFormats.Pictures, PictureFormats.Environments, MeshFormats.Meshes);
}

/// <summary>What a client may offer, with the dot.</summary>
/// <param name="Pictures">What a layer, a mask, an image material and a skew map read.</param>
/// <param name="Environments">What lights the viewport.</param>
/// <param name="Meshes">What a project takes as its mesh, its detailed mesh or a cage.</param>
public sealed record FileFormats(
    IReadOnlyList<string> Pictures,
    IReadOnlyList<string> Environments,
    IReadOnlyList<string> Meshes
) {
    /// <summary>What a project brings materials in from, as graphs (#157).</summary>
    public IReadOnlyList<string> Materials { get; init; } = MaterialFormats.Materials;
}

/// <summary>
///     The one list of the files Patina reads materials from (#157): a model's materials through Assimp in the Worker,
///     and MaterialX and USD's text form, which the Server reads. A file dialog offers them from here.
/// </summary>
public static class MaterialFormats {
    /// <summary>MaterialX.</summary>
    public const string MaterialX = ".mtlx";

    public static IReadOnlyList<string> Materials { get; } =
        [".gltf", ".glb", ".fbx", ".obj", ".dae", MaterialX, ".usda", ".usd", Package];

    /// <summary>A USD package, which the Server unpacks and reads when its layer is text.</summary>
    public const string Package = ".usdz";

    /// <summary>The ones the Server reads as text: MaterialX, and USD, which must be in its text form.</summary>
    public static IReadOnlyList<string> Texts { get; } = [MaterialX, ".usda", ".usd"];

    /// <summary>Whether a file is one Patina reads materials from, by its name.</summary>
    public static bool IsMaterial(string path) =>
        Materials.Contains(Path.GetExtension(path).ToLowerInvariant(), StringComparer.Ordinal);
}

/// <summary>
///     The one list of the meshes Patina reads, through Assimp. A project's folder, the Import command and a client's
///     file dialogs all take it from here (#146). Each has a test that reads a file of it.
/// </summary>
public static class MeshFormats {
    public static IReadOnlyList<string> Meshes { get; } =
        [".glb", ".gltf", ".fbx", ".obj", ".dae", ".ply", ".stl"];

    /// <summary>Whether a file is a mesh Patina reads, by its name.</summary>
    public static bool IsMesh(string path) =>
        Meshes.Contains(Path.GetExtension(path).ToLowerInvariant(), StringComparer.Ordinal);

    /// <summary>What a format is called, for a person, by its extension with the dot.</summary>
    public static string Name(string extension) =>
        extension.ToLowerInvariant() switch {
            ".glb" => "glTF binary",
            ".gltf" => "glTF",
            ".fbx" => "FBX",
            ".obj" => "Wavefront OBJ",
            ".dae" => "COLLADA",
            ".ply" => "PLY",
            ".stl" => "STL",
            var other => other.TrimStart('.').ToUpperInvariant()
        };
}

/// <summary>
///     The one list of the pictures Patina reads. Every path that takes a person's picture reads all of these, and a
///     file dialog offers them from here, so nothing is offered that fails later. KTX2 and DDS have decoders and are
///     not here: they arrive block-compressed, which nothing after them can read.
/// </summary>
public static class PictureFormats {
    public static IReadOnlyList<string> Pictures { get; } =
        [".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd", ".gif", ".exr", ".hdr", ".tif", ".tiff", ".webp"];

    public static IReadOnlyList<string> Environments { get; } = [".hdr", ".exr"];

    /// <summary>Whether a file is a picture Patina reads, by its name.</summary>
    public static bool IsPicture(string path) => Is(path, Pictures);

    /// <summary>Whether a file is a panorama Patina reads, by its name.</summary>
    public static bool IsEnvironment(string path) => Is(path, Environments);

    static bool Is(string path, IReadOnlyList<string> formats) =>
        formats.Contains(Path.GetExtension(path).ToLowerInvariant(), StringComparer.Ordinal);
}

/// <summary>The machine and runtime a process runs on.</summary>
/// <param name="Os">The operating system's description.</param>
/// <param name="Architecture">The process architecture, such as <c>Arm64</c>.</param>
/// <param name="Runtime">The .NET runtime's description.</param>
public sealed record HostInfo(string Os, string Architecture, string Runtime);

/// <summary>The Worker as the Server sees it. All but <c>Connected</c> are null while there is none.</summary>
/// <param name="Connected">Whether a Worker is connected and answering.</param>
/// <param name="Version">The Worker's release version, from its hello.</param>
/// <param name="ProcessId">The Worker's process id, from its hello.</param>
/// <param name="UptimeSeconds">How long the Worker has run, from its last health reply.</param>
/// <param name="WorkingSetBytes">The Worker's memory, from its last health reply.</param>
/// <param name="BusySeconds">How long its current device work has gone without progress, from its last health.</param>
public sealed record WorkerStatus(
    bool Connected,
    string? Version = null,
    int? ProcessId = null,
    double? UptimeSeconds = null,
    long? WorkingSetBytes = null,
    double? BusySeconds = null
);
