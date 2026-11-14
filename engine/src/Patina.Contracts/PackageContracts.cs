using System.Text.Json.Serialization;

namespace Patina.Contracts;

/// <summary>What a package's manifest, publishing and <c>patina graph info</c> put on the wire (ADR 0027).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(PackageManifest))]
[JsonSerializable(typeof(PackagePlan))]
[JsonSerializable(typeof(PackageRequest))]
[JsonSerializable(typeof(PackagePublished))]
[JsonSerializable(typeof(PackageImportRequest))]
[JsonSerializable(typeof(PackageImported))]
[JsonSerializable(typeof(GraphRelinkRequest))]
[JsonSerializable(typeof(GraphFacts))]
public sealed partial class PackageJsonContext : JsonSerializerContext;

/// <summary>
///     A package's <c>package.yaml</c> (ADR 0027): what a <c>.ptpkg</c> folder holds besides its graph, and what made
///     it. The graph's own file holds its parameters, presets and what it says about itself, so this repeats none of
///     them.
/// </summary>
/// <param name="Format">The shape of the package. A reader refuses one newer than it knows.</param>
/// <param name="Name">What the package is called, which is its folder's and its graph's name.</param>
/// <param name="Graph">The graph's file, beside this one.</param>
/// <param name="Role">What it is used as: <c>material</c>, <c>mask</c>, <c>filter</c> or <c>brush</c>.</param>
/// <param name="Writer">The Patina that published it, as <c>Patina 0.9.0</c>.</param>
/// <param name="Compounds">The compounds it carries under <c>Compounds/</c>, and what each was called before.</param>
/// <param name="Shipped">The compounds it reads from those that ship with Patina, which it does not carry.</param>
/// <param name="Files">The pictures it carries under <c>files/</c>, and how the graph named each.</param>
public sealed record PackageManifest(
    int Format,
    string Name,
    string Graph,
    string Role,
    string Writer,
    IReadOnlyList<PackageCompound> Compounds,
    IReadOnlyList<string> Shipped,
    IReadOnlyList<PackageFile> Files
);

/// <summary>A compound a package carries.</summary>
/// <param name="Type">Its node type inside the package, under <c>Packages/&lt;name&gt;/</c>.</param>
/// <param name="From">What it was called in the folder it came from.</param>
public sealed record PackageCompound(string Type, string From);

/// <summary>A picture a package carries.</summary>
/// <param name="Path">Where it is in the package, below <c>files/</c>, named by its content.</param>
/// <param name="From">How the graph named it before it was published.</param>
public sealed record PackageFile(string Path, string From);

/// <summary>Something a graph reads that publishing it as a package has to bring, or cannot.</summary>
/// <param name="Kind">
///     <c>compound</c> (a graph of its project's, which the package carries), <c>shipped</c> (a compound that ships
///     with Patina, which it does not), <c>picture</c> (a bitmap, which it carries), or <c>unknown</c> (a node type
///     nothing publishes).
/// </param>
/// <param name="Name">The node type, or the picture as the graph names it.</param>
/// <param name="File">Where it was found, below the library when it is in one, or null when it was not.</param>
/// <param name="Problem">Why it cannot be brought, or null when it can.</param>
/// <param name="In">
///     The compound that names it, or null when the graph itself does, which is when relinking the graph's Bitmap
///     nodes (<c>POST /api/v1/graphs/{id}/relink</c>) reaches it.
/// </param>
public sealed record PackageDependency(
    string Kind,
    string Name,
    string? File,
    string? Problem = null,
    string? In = null
);

/// <summary>What publishing a graph as a package would do, for a dialog to show before it does.</summary>
/// <param name="Name">The package's name.</param>
/// <param name="Role">What it would be used as.</param>
/// <param name="Path">Where it would be written, below the library.</param>
/// <param name="Replaces">True when a package of that name is there already, and publishing replaces it.</param>
/// <param name="Dependencies">What it reads. Publishing is refused while any has a problem.</param>
/// <param name="Diagnostics">What compiling it said; an error refuses it too.</param>
public sealed record PackagePlan(
    string Name,
    string Role,
    string Path,
    bool Replaces,
    IReadOnlyList<PackageDependency> Dependencies,
    IReadOnlyList<GraphDiagnostic> Diagnostics
);

/// <summary>The body of <c>POST /api/v1/library/packages</c>.</summary>
/// <param name="Graph">The graph as it is saved: absolute, or below the library.</param>
/// <param name="Role">What it is used as, or null for the role its project gives it.</param>
public sealed record PackageRequest(string Graph, string? Role = null);

/// <summary>A package that has been written.</summary>
/// <param name="Path">Its folder, below the library.</param>
/// <param name="Manifest">What its <c>package.yaml</c> says.</param>
public sealed record PackagePublished(string Path, PackageManifest Manifest);

/// <summary>The body of <c>POST /api/v1/library/packages/import</c>.</summary>
/// <param name="Source">
///     The package to bring in: a zip of one, or its <c>.ptpkg</c> folder, as a path on the machine the Server runs on.
/// </param>
public sealed record PackageImportRequest(string Source);

/// <summary>A package that has been brought into a library.</summary>
/// <param name="Path">Its folder, below the library.</param>
/// <param name="Manifest">What its <c>package.yaml</c> says.</param>
/// <param name="Replaced">True when a package of its name was there, and this one replaced it.</param>
public sealed record PackageImported(string Path, PackageManifest Manifest, bool Replaced);

/// <summary>The body of <c>POST /api/v1/graphs/{id}/relink</c>: a picture the graph reads, and the file it is now.</summary>
/// <param name="From">The picture as the graph's Bitmap nodes name it.</param>
/// <param name="To">The file to read instead: absolute, or below the library.</param>
public sealed record GraphRelinkRequest(string From, string To);

/// <summary>
///     What <c>patina graph info</c> prints: everything a caller needs to evaluate a graph or a package without opening
///     it, as JSON.
/// </summary>
/// <param name="Name">The graph's name.</param>
/// <param name="Info">What it says about itself.</param>
/// <param name="Settings">Its settings: size, seed, bit depth and the rest.</param>
/// <param name="Parameters">What <c>patina bake --param</c> can set.</param>
/// <param name="Presets">What <c>patina bake --preset</c> can choose.</param>
/// <param name="Inputs">The images and values it takes when it is a compound or a filter.</param>
/// <param name="Outputs">The maps it makes, by usage.</param>
/// <param name="Package">The package's manifest, when it is one.</param>
public sealed record GraphFacts(
    string Name,
    GraphInfoState Info,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyList<GraphSettingDefinition> Parameters,
    IReadOnlyList<GraphPresetState> Presets,
    IReadOnlyList<GraphInterfacePort> Inputs,
    IReadOnlyList<string> Outputs,
    PackageManifest? Package = null
);
