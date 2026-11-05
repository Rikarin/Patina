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
);

/// <summary>One file a preset made, before a project's name is put on it.</summary>
/// <param name="Template">The naming template.</param>
/// <param name="File">Its name in the object's folder.</param>
/// <param name="Extension">What the delivered file ends in.</param>
/// <param name="Format">The file format.</param>
/// <param name="Compression">What is inside it.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
/// <param name="Levels">How many mip levels.</param>
/// <param name="Bytes">Its size.</param>
public sealed record ExportedFile(
    string Template,
    string File,
    string Extension,
    string Format,
    string Compression,
    int Width,
    int Height,
    int Levels,
    long Bytes
);

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
);

/// <summary>One file of a preset, as a client shows it.</summary>
public sealed record ExportPresetFile(
    string Template,
    string Format,
    string Compression,
    string Space,
    bool Mips,
    IReadOnlyList<string> Maps
);

/// <summary>An export preset.</summary>
/// <param name="Id">Its id, which a build names.</param>
/// <param name="Name">Its label.</param>
/// <param name="Engine">What it is for.</param>
/// <param name="Normal">The green its target reads: y-up or y-down.</param>
/// <param name="Origin">Where it comes from: factory or library.</param>
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
);

/// <summary>Every preset a library can use, and the files in its folder that are not presets.</summary>
public sealed record ExportPresetList(IReadOnlyList<ExportPresetInfo> Presets, IReadOnlyList<string> Problems);

/// <summary>What to build.</summary>
public sealed record BuildRequest {
    /// <summary>Only what reads this shared asset, directly or not. Null for the whole library.</summary>
    public Guid? Asset { get; init; }

    /// <summary>Only these projects. Null or empty for all of them.</summary>
    public IReadOnlyList<Guid>? Projects { get; init; }

    /// <summary>The presets to export through. Null means the library's <c>exportTargets</c>; empty, no export.</summary>
    public IReadOnlyList<string>? Presets { get; init; }

    /// <summary>Where exports go, or null for <c>exports/</c> in the library.</summary>
    public string? Folder { get; init; }
}

/// <summary>What a build would do for one project.</summary>
/// <param name="Id">The project.</param>
/// <param name="Name">Its name.</param>
/// <param name="Path">Its path below <c>projects/</c>.</param>
/// <param name="Outputs">The outputs that would be made.</param>
/// <param name="Exports">The exports that would be written.</param>
public sealed record BuildPreviewProject(Guid Id, string Name, string Path, int Outputs, int Exports);

/// <summary>
///     What a build would do, from the build keys and with nothing run. <paramref name="Rebuilds" /> and
///     <paramref name="Pinned" /> are exact for outputs, where the asset's impact counts every project that references
///     it at all. An asset a project references and no output reads is in neither.
/// </summary>
/// <param name="Projects">Projects with something to do.</param>
/// <param name="Rebuilds">Outputs with no result under their current key, counted once per project.</param>
/// <param name="Shared">How many of those are the same work as another's, because their keys are equal.</param>
/// <param name="Fresh">Outputs already built.</param>
/// <param name="Pinned">Outputs that read the asset through a pin that holds them still.</param>
/// <param name="Exports">Exports to write.</param>
/// <param name="ExportsFresh">Exports already in place.</param>
/// <param name="Unresolved">Outputs that cannot be built: a missing input, an unreadable reference.</param>
/// <param name="Items">One page of the projects, by name.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record BuildPreview(
    int Projects,
    int Rebuilds,
    int Shared,
    int Fresh,
    int Pinned,
    int Exports,
    int ExportsFresh,
    int Unresolved,
    IReadOnlyList<BuildPreviewProject> Items,
    int Offset,
    int Limit
);

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
/// <param name="Build">The job, or a name a headless build gave itself.</param>
/// <param name="Started">When it started, in UTC.</param>
/// <param name="Finished">When it finished, while it has not null.</param>
/// <param name="Projects">Projects something happened to.</param>
/// <param name="Built">Outputs that ran.</param>
/// <param name="Reused">Outputs whose result existed.</param>
/// <param name="Fresh">Outputs nothing touched.</param>
/// <param name="Failed">Outputs and exports that could not be made.</param>
/// <param name="Exported">Exports written.</param>
/// <param name="ExportsFresh">Exports nothing touched.</param>
/// <param name="Files">Files written by exports.</param>
/// <param name="Folder">The folder holding this report and one file per project.</param>
public sealed record LibraryReport(
    string Build,
    DateTimeOffset Started,
    DateTimeOffset? Finished,
    int Projects,
    int Built,
    int Reused,
    int Fresh,
    int Failed,
    int Exported,
    int ExportsFresh,
    long Files,
    string Folder
);

/// <summary>A page of a build's per-project reports.</summary>
/// <param name="Total">How many there are.</param>
/// <param name="Items">This page.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record ProjectReportPage(int Total, IReadOnlyList<ProjectReport> Items, int Offset, int Limit);

/// <summary>The file left beside delivered exports, which is how a build knows they need not be written again.</summary>
/// <param name="Key">The export key they came from.</param>
/// <param name="Files">The files, relative to that folder.</param>
public sealed record ExportDelivery(string Key, IReadOnlyList<string> Files);

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
public sealed record ExportSource(string Folder, BuildManifest Manifest);

/// <summary>One preset over one project's outputs.</summary>
/// <param name="Preset">The preset file's text, so that a Worker needs no library and reads no changed file.</param>
/// <param name="Sources">The objects to read, later ones winning a map.</param>
/// <param name="Folder">The folder to make, which appears only when it is whole.</param>
public sealed record ExportStep(string Preset, IReadOnlyList<ExportSource> Sources, string Folder);

/// <summary>A line of a build's journal: a step starting, or with its project and a report, a step ended.</summary>
public sealed record BuildLogLine(
    string Step,
    Guid? Project,
    string? Name,
    string? Path,
    OutputReport? Output,
    ExportReport? Export
);
