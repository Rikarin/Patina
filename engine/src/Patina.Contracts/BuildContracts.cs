namespace Patina.Contracts;

/// <summary>One map an output made, as a lossless file in its object.</summary>
/// <param name="Map">Its name: one of a graph's Output usages.</param>
/// <param name="File">The file, relative to the object's folder.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
/// <param name="Green">For a normal, which way its green runs: up or down. Null for anything else.</param>
public sealed record BuiltMap(string Map, string File, int Width, int Height, string? Green);

/// <summary>What an output's object holds.</summary>
/// <param name="Tool">What built it.</param>
/// <param name="Maps">Its maps. A mesh or anything else that is not a map is under <paramref name="Files" />.</param>
/// <param name="Files">Other files it made, relative to the object's folder.</param>
/// <param name="Warnings">What could not be done as asked.</param>
public sealed record BuildManifest(
    string Tool,
    IReadOnlyList<BuiltMap> Maps,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Warnings
) {
    /// <summary>
    ///     The UDIM tile its texture set's layout sits on, which a preset's <c>{udim}</c> and <c>{tile}</c> name (#20):
    ///     1001 for a set on the first tile, on several, or on no mesh.
    /// </summary>
    public int Udim { get; init; } = 1001;
}

// SK7005: a wire record's constructor is its JSON shape, one parameter per field, so fewer parameters means a new API.
#pragma warning disable SK7005
/// <summary>One file a preset made, before a project's name is put on it.</summary>
/// <param name="Template">The naming template.</param>
/// <param name="File">Its name in the object's folder.</param>
/// <param name="Format">The file format.</param>
/// <param name="Compression">What is inside it.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
/// <param name="Levels">How many mip levels.</param>
/// <param name="Bytes">Its size.</param>
/// <param name="Set">The texture set it belongs to, or nothing for a project that has no named sets.</param>
public sealed record ExportedFile(
    string Template,
    string File,
    string Format,
    string Compression,
    int Width,
    int Height,
    int Levels,
    long Bytes,
    string Set = ""
) {
    /// <summary>What its numbers are: srgb or linear.</summary>
    public string Space { get; init; } = "linear";

    /// <summary>The maps it read that the set had; a lane of any other map holds that map's default.</summary>
    public IReadOnlyList<string> Maps { get; init; } = [];

    /// <summary>The UDIM tile of the texture set it was made from (#20).</summary>
    public int Udim { get; init; } = 1001;
}
#pragma warning restore SK7005

/// <summary>What a preset made from one project's maps.</summary>
/// <param name="Preset">The preset's id.</param>
/// <param name="Files">The files.</param>
/// <param name="Skipped">Templates of the textures left out, because the project has none of their maps.</param>
/// <param name="Warnings">What needs a person: a lane filled with a default, a map resampled.</param>
public sealed record ExportManifest(
    string Preset,
    IReadOnlyList<ExportedFile> Files,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Warnings
) {
    /// <summary>
    ///     Whether the project has more than one texture set, so that file names carry the set's even when only one set
    ///     was exported: a file's name does not change with what else was chosen.
    /// </summary>
    public bool PerSet { get; init; }
}

/// <summary>One file of a preset, as a client shows it.</summary>
/// <param name="Template">Its naming template.</param>
/// <param name="Format">Its format: one of <see cref="ExportPresetList.Formats" />.</param>
/// <param name="Compression">What is inside a KTX2 or DDS file, or none.</param>
/// <param name="Space">srgb or linear.</param>
/// <param name="Mips">Whether it carries a mip chain.</param>
/// <param name="Maps">The maps it reads.</param>
/// <param name="Lanes">How many lanes it has, which decides the formats it can change to.</param>
/// <param name="Quality">For JPEG, 1 to 100; for block compression, fast, balanced or best.</param>
public sealed record ExportPresetFile(
    string Template,
    string Format,
    string Compression,
    string Space,
    bool Mips,
    IReadOnlyList<string> Maps,
    int Lanes = 4,
    string Quality = "balanced"
) {
    /// <summary>Its side as a share of every other file's: 1, 0.5, 0.25, 0.125 or 0.0625.</summary>
    public double Scale { get; init; } = 1;
}

/// <summary>A file format a preset or an export can name.</summary>
/// <param name="Id">Its name in a preset.</param>
/// <param name="Label">Its name for a person, with its depth.</param>
/// <param name="Extension">What its files end in.</param>
/// <param name="Lanes">The lane counts it holds.</param>
/// <param name="Linear">Whether it holds only linear numbers, as a float or 16-bit format does here.</param>
/// <param name="Container">Whether it is a GPU container, the only kind a preset can block-compress.</param>
public sealed record ExportFormatInfo(
    string Id,
    string Label,
    string Extension,
    IReadOnlyList<int> Lanes,
    bool Linear,
    bool Container
);

/// <summary>What fills the texels a mesh's layout leaves empty.</summary>
/// <param name="Mode">none, infinite, transparent, colour or diffusion.</param>
/// <param name="Distance">How many texels the islands grow by first, for every mode but none and infinite.</param>
/// <param name="Colour">For colour, the value of each lane of a file, 0 to 1.</param>
public sealed record ExportPaddingInfo(string Mode, int Distance, IReadOnlyList<float> Colour);

/// <summary>One file of a preset changed for one export.</summary>
/// <param name="Template">Which file: its naming template.</param>
/// <param name="Enabled">Whether it is written at all.</param>
/// <param name="Format">Another format for it, or null for the preset's.</param>
public sealed record ExportFileChoice(string Template, bool Enabled = true, string? Format = null) {
    /// <summary>
    ///     What a KTX2 or DDS file holds: none, half, float or bc1 to bc7. Null keeps the preset's, which a plain file
    ///     made a container takes as none.
    /// </summary>
    public string? Compression { get; init; }

    /// <summary>For JPEG, 1 to 100; for BC6H and BC7, fast, balanced or best. Null keeps the preset's.</summary>
    public string? Quality { get; init; }

    /// <summary>Whether a KTX2 or DDS file carries a mip chain. Null keeps the preset's.</summary>
    public bool? Mips { get; init; }
}

/// <summary>
///     One texture set's changes over the whole export's (#153): its own size, padding, dithering and files. Null
///     fields keep what the export says for every set.
/// </summary>
/// <param name="Set">The texture set, by name.</param>
public sealed record ExportSetOptions(string Set) {
    /// <summary>The longest side of this set's files.</summary>
    public int? Size { get; init; }

    /// <summary>How this set's files change size.</summary>
    public string? Filter { get; init; }

    /// <summary>What fills the background of this set's layout.</summary>
    public ExportPaddingInfo? Padding { get; init; }

    /// <summary>Whether this set's 8-bit files are dithered.</summary>
    public bool? Dither { get; init; }

    /// <summary>This set's files turned off or written in another format.</summary>
    public IReadOnlyList<ExportFileChoice>? Files { get; init; }
}

/// <summary>
///     A preset's settings changed for one export, as an export dialog changes them. Null fields keep the preset's.
///     Changing one gives the export another key, so it never reuses files made under other settings.
/// </summary>
public sealed record ExportOptions {
    /// <summary>
    ///     The longest side of every file, 1 to 8192: 2048 makes a 4096 map's file 2048. A graph is computed at it
    ///     rather than resampled to it; a bake's or a layer stack's maps are resampled.
    /// </summary>
    public int? Size { get; init; }

    /// <summary>How a file changes size: box, bilinear or nearest.</summary>
    public string? Filter { get; init; }

    /// <summary>What fills the background of a mesh's layout.</summary>
    public ExportPaddingInfo? Padding { get; init; }

    /// <summary>Whether 8-bit files are dithered rather than rounded.</summary>
    public bool? Dither { get; init; }

    /// <summary>The texture sets to write, or null for every one.</summary>
    public IReadOnlyList<string>? Sets { get; init; }

    /// <summary>Files turned off or written in another format.</summary>
    public IReadOnlyList<ExportFileChoice>? Files { get; init; }

    /// <summary>Changes for one texture set at a time, over these, or null for none.</summary>
    public IReadOnlyList<ExportSetOptions>? PerSet { get; init; }

    /// <summary>
    ///     Whether the texture sets written go into one set of files, each set where its own layout puts it, as an
    ///     atlas of sets that share one UV square. A set's own changes do not apply to it.
    /// </summary>
    public bool? Merge { get; init; }
}

/// <summary>An export preset.</summary>
/// <param name="Id">Its id, which a build names.</param>
/// <param name="Name">Its label.</param>
/// <param name="Engine">What it is for.</param>
/// <param name="Normal">The green its target reads: y-up or y-down.</param>
/// <param name="Origin">Where it comes from: factory (a Starter preset), library or project.</param>
/// <param name="Revision">The hash of its file.</param>
/// <param name="Files">What it makes.</param>
public sealed record ExportPresetInfo(
    string Id,
    string Name,
    string Engine,
    string Normal,
    string Origin,
    string Revision,
    IReadOnlyList<ExportPresetFile> Files
) {
    /// <summary>What fills the background of a mesh's layout.</summary>
    public ExportPaddingInfo Padding { get; init; } = new("none", 0, []);

    /// <summary>Whether 8-bit files are dithered.</summary>
    public bool Dither { get; init; }

    /// <summary>The longest side of every file, or null for the maps' own size.</summary>
    public int? Size { get; init; }

    /// <summary>How a file changes size.</summary>
    public string Filter { get; init; } = "box";

    /// <summary>The material files it writes beside its textures: gltf, usda or mtlx.</summary>
    public IReadOnlyList<string> Materials { get; init; } = [];
}

/// <summary>Every preset a library can use, and the files in its folder that are not presets.</summary>
/// <param name="Presets">The presets.</param>
/// <param name="Problems">One line per file that is not a preset.</param>
public sealed record ExportPresetList(IReadOnlyList<ExportPresetInfo> Presets, IReadOnlyList<string> Problems) {
    /// <summary>
    ///     The preset an export starts from when nothing is kept for it: the library's <c>defaultPreset</c> while it
    ///     names one of these, else the Starter glTF preset, and null only when there are no presets at all.
    /// </summary>
    public string? Default { get; init; }

    /// <summary>The file formats there are, for a client that offers another one.</summary>
    public IReadOnlyList<ExportFormatInfo> Formats { get; init; } = [];

    /// <summary>The padding modes there are, in order, each with what a person calls it.</summary>
    public IReadOnlyList<StackTerm> Paddings { get; init; } = [];

    /// <summary>The resize filters there are, each with what a person calls it.</summary>
    public IReadOnlyList<StackTerm> Filters { get; init; } = [];

    /// <summary>What a KTX2 or DDS file can hold, in order, each with what a person calls it.</summary>
    public IReadOnlyList<StackTerm> Compressions { get; init; } = [];

    /// <summary>The maps a lane can read, each with what a person calls it.</summary>
    public IReadOnlyList<StackTerm> Maps { get; init; } = [];

    /// <summary>The material files a preset can write beside its textures, each with what a person calls it.</summary>
    public IReadOnlyList<StackTerm> Materials { get; init; } = [];

    /// <summary>The names a file's template can hold, such as <c>project</c> for <c>{project}</c>.</summary>
    public IReadOnlyList<string> Tokens { get; init; } = [];
}

/// <summary>What to build.</summary>
public sealed record BuildRequest {
    /// <summary>Only what reads this shared asset, directly or not. Null for the whole library.</summary>
    public Guid? Asset { get; init; }

    /// <summary>Only these projects. Null or empty for all of them.</summary>
    public IReadOnlyList<Guid>? Projects { get; init; }

    /// <summary>
    ///     Only these documents of the one project named, by their file below it, each a texture set of its own
    ///     whatever it is: a graph in a texturing project or one that is not a material exports too. Null or empty for
    ///     what the project exports.
    /// </summary>
    public IReadOnlyList<string>? Documents { get; init; }

    /// <summary>
    ///     The presets to export through. Null means the library's <c>exportTargets</c>; empty, no
    ///     export.
    /// </summary>
    public IReadOnlyList<string>? Presets { get; init; }

    /// <summary>Where exports go, or null for <c>exports/</c> in the library.</summary>
    public string? Folder { get; init; }

    /// <summary>The presets' settings changed for this export, or null for the presets as they are.</summary>
    public ExportOptions? Options { get; init; }
}

/// <summary>What a build would do for one project.</summary>
/// <param name="Id">The project.</param>
/// <param name="Name">Its name.</param>
/// <param name="Path">Its path below <c>projects/</c>.</param>
/// <param name="Outputs">The outputs that would be made.</param>
/// <param name="Exports">The exports that would be written.</param>
public sealed record BuildPreviewProject(Guid Id, string Name, string Path, int Outputs, int Exports);

/// <summary>
///     What a build would do, from the build keys and with nothing run. <see cref="Rebuilds" /> and
///     <see cref="Pinned" /> are exact for outputs, where the asset's impact counts every project that references it at
///     all. An asset a project references and no output reads is in neither.
/// </summary>
public sealed record BuildPreview {
    /// <summary>Projects with something to do.</summary>
    public required int Projects { get; init; }

    /// <summary>Outputs with no result under their current key, counted once per project.</summary>
    public required int Rebuilds { get; init; }

    /// <summary>How many of those are the same work as another's, because their keys are equal.</summary>
    public required int Shared { get; init; }

    /// <summary>Outputs already built.</summary>
    public required int Fresh { get; init; }

    /// <summary>Outputs that read the asset through a pin that holds them still.</summary>
    public required int Pinned { get; init; }

    /// <summary>Exports to write.</summary>
    public required int Exports { get; init; }

    /// <summary>Exports already in place.</summary>
    public required int ExportsFresh { get; init; }

    /// <summary>Outputs that cannot be built: a missing input, an unreadable reference.</summary>
    public required int Unresolved { get; init; }

    /// <summary>Why a build would write nothing, or null when it would write something.</summary>
    public string? Nothing { get; init; }

    /// <summary>One page of the projects, by name.</summary>
    public required IReadOnlyList<BuildPreviewProject> Items { get; init; }

    /// <summary>The offset the page starts at.</summary>
    public required int Offset { get; init; }

    /// <summary>The most items a page holds.</summary>
    public required int Limit { get; init; }
}

/// <summary>How one output or export came out.</summary>
public static class BuildStatuses {
    /// <summary>It ran.</summary>
    public const string Built = "built";

    /// <summary>Its key already had a result, from another project or an earlier build.</summary>
    public const string Reused = "reused";

    /// <summary>It was up to date and nothing touched it.</summary>
    public const string Fresh = "fresh";

    /// <summary>It could not be made.</summary>
    public const string Failed = "failed";
}

/// <summary>One output in a project's report.</summary>
/// <param name="Name">The output.</param>
/// <param name="Tool">What builds it.</param>
/// <param name="Key">Its build key, or null when it cannot be computed.</param>
/// <param name="Status">One of <see cref="BuildStatuses" />.</param>
/// <param name="Seconds">How long it took here.</param>
/// <param name="Error">Why it failed.</param>
public sealed record OutputReport(string Name, string Tool, string? Key, string Status, double Seconds, string? Error);

/// <summary>One preset in a project's report.</summary>
/// <param name="Preset">The preset.</param>
/// <param name="Status">One of <see cref="BuildStatuses" />.</param>
/// <param name="Folder">Where its files are.</param>
/// <param name="Files">Its files, absolute.</param>
/// <param name="Skipped">What it left out, and why.</param>
/// <param name="Warnings">What needs a person.</param>
/// <param name="Error">Why it failed.</param>
public sealed record ExportReport(
    string Preset,
    string Status,
    string? Folder,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Warnings,
    string? Error
);

/// <summary>What a build did to one project. Only a project something happened to has one.</summary>
/// <param name="Id">The project.</param>
/// <param name="Name">Its name.</param>
/// <param name="Path">Its path below <c>projects/</c>.</param>
/// <param name="Outputs">Its outputs that were touched.</param>
/// <param name="Exports">Its exports that were touched.</param>
public sealed record ProjectReport(
    Guid Id,
    string Name,
    string Path,
    IReadOnlyList<OutputReport> Outputs,
    IReadOnlyList<ExportReport> Exports
);

/// <summary>What a whole build did.</summary>
public sealed record LibraryReport {
    /// <summary>The job, or a name a headless build gave itself.</summary>
    public required string Build { get; init; }

    /// <summary>When it started, in UTC.</summary>
    public required DateTimeOffset Started { get; init; }

    /// <summary>When it finished, while it has not null.</summary>
    public DateTimeOffset? Finished { get; init; }

    /// <summary>Projects something happened to.</summary>
    public required int Projects { get; init; }

    /// <summary>Outputs that ran.</summary>
    public required int Built { get; init; }

    /// <summary>Outputs whose result existed.</summary>
    public required int Reused { get; init; }

    /// <summary>Outputs nothing touched.</summary>
    public required int Fresh { get; init; }

    /// <summary>Outputs and exports that could not be made.</summary>
    public required int Failed { get; init; }

    /// <summary>Exports written.</summary>
    public required int Exported { get; init; }

    /// <summary>Exports nothing touched.</summary>
    public required int ExportsFresh { get; init; }

    /// <summary>Files written by exports.</summary>
    public required long Files { get; init; }

    /// <summary>The folder holding this report and one file per project.</summary>
    public required string Folder { get; init; }

    /// <summary>Why a finished build wrote nothing, or null when it wrote something or something failed.</summary>
    public string? Nothing { get; init; }

    /// <summary>What a running build is on, for a person: a project and an output or a preset.</summary>
    public string? Current { get; init; }
}

/// <summary>A page of a build's per-project reports.</summary>
/// <param name="Total">How many there are.</param>
/// <param name="Items">This page.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record ProjectReportPage(int Total, IReadOnlyList<ProjectReport> Items, int Offset, int Limit);

/// <summary>
///     The file left beside delivered exports, which is how a build knows they need not be written
///     again.
/// </summary>
/// <param name="Key">The export key they came from.</param>
/// <param name="Files">The files, relative to that folder.</param>
public sealed record ExportDelivery(string Key, IReadOnlyList<string> Files) {
    /// <summary>
    ///     The texture set each file belongs to, by file, so that an export of some sets leaves the others' files
    ///     alone. A file with no set, or one from a delivery before sets were kept, goes only when every set is
    ///     exported.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Sets { get; init; }
}

/// <summary>One output for whatever builds it: everything by full path, so that it needs no library.</summary>
/// <param name="Tool">What builds it.</param>
/// <param name="Files">Project files it reads, by full path.</param>
/// <param name="Assets">Shared files it reads, by full path.</param>
/// <param name="Settings">The tool's settings.</param>
/// <param name="Folder">The folder to make, which appears only when it is whole.</param>
/// <param name="Library">The library, for pictures a graph names relative to it.</param>
public sealed record OutputStep(
    string Tool,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Assets,
    IReadOnlyDictionary<string, string> Settings,
    string Folder,
    string? Library
);

/// <summary>One object a preset reads.</summary>
/// <param name="Folder">Its folder.</param>
/// <param name="Manifest">What it holds.</param>
/// <param name="Set">The texture set it belongs to. Nothing here means every set of its project.</param>
public sealed record ExportSource(string Folder, BuildManifest Manifest, string Set = "");

/// <summary>One preset over one project's outputs.</summary>
/// <param name="Preset">The preset file's text, so that a Worker needs no library and reads no changed file.</param>
/// <param name="Sources">The objects to read, later ones winning a map within a set.</param>
/// <param name="Folder">The folder to make, which appears only when it is whole.</param>
/// <param name="Options">The preset's settings changed for this export, or null.</param>
public sealed record ExportStep(
    string Preset,
    IReadOnlyList<ExportSource> Sources,
    string Folder,
    ExportOptions? Options = null
);

/// <summary>A line of a build's journal: a step starting, or with its project and a report, a step ended.</summary>
public sealed record BuildLogLine(
    string Step,
    Guid? Project,
    string? Name,
    string? Path,
    OutputReport? Output,
    ExportReport? Export
);
