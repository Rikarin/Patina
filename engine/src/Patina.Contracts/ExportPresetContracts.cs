namespace Patina.Contracts;

/// <summary>
///     One file of a preset as its file says it, for a client that edits presets (ADR 0019). Every field is the
///     preset language's own spelling, so what a client sends is what the file holds.
/// </summary>
public sealed record ExportPresetTextureDocument {
    /// <summary>Its naming template, without an extension.</summary>
    public required string File { get; init; }

    /// <summary>Its format: one of <see cref="ExportPresetList.Formats" />.</summary>
    public required string Format { get; init; }

    /// <summary>srgb or linear.</summary>
    public required string Space { get; init; }

    /// <summary>What a KTX2 or DDS file holds: none, half, float or bc1 to bc7.</summary>
    public string Compression { get; init; } = "none";

    /// <summary>For JPEG, 1 to 100; for BC6H and BC7, fast, balanced or best. Null for the format's default.</summary>
    public string? Quality { get; init; }

    /// <summary>Whether a KTX2 or DDS file carries a mip chain.</summary>
    public bool Mips { get; init; }

    /// <summary>Its side as a share of every other file's: 1, 0.5, 0.25, 0.125 or 0.0625.</summary>
    public double Scale { get; init; } = 1;

    /// <summary>Whether this file is dithered, or null for what the preset says.</summary>
    public bool? Dither { get; init; }

    /// <summary>What fills this file's background, or null for what the preset says.</summary>
    public ExportPaddingInfo? Padding { get; init; }

    /// <summary>
    ///     One to four lanes, in file order, each as the language writes one: <c>roughness.r</c>,
    ///     <c>1-roughness.r</c> or a number such as <c>0.5</c>.
    /// </summary>
    public required IReadOnlyList<string> Channels { get; init; }
}

/// <summary>A preset as its file says it, which is what the preset editor reads and writes (ADR 0019).</summary>
public sealed record ExportPresetDocument {
    /// <summary>Its id: lower-case letters, digits and hyphens. It is also the file's name.</summary>
    public required string Id { get; init; }

    /// <summary>Its label.</summary>
    public required string Name { get; init; }

    /// <summary>What it is for.</summary>
    public string Engine { get; init; } = string.Empty;

    /// <summary>The green its target reads: y-up or y-down.</summary>
    public string Normal { get; init; } = "y-up";

    /// <summary>The longest side of every file, or null for the maps' own size.</summary>
    public int? Size { get; init; }

    /// <summary>How a file changes size: box, bilinear or nearest.</summary>
    public string Filter { get; init; } = "box";

    /// <summary>Whether 8-bit files are dithered.</summary>
    public bool Dither { get; init; }

    /// <summary>What fills the background of a mesh's layout.</summary>
    public ExportPaddingInfo Padding { get; init; } = new("none", 0, []);

    /// <summary>The material files written beside the textures: gltf, usda or mtlx.</summary>
    public IReadOnlyList<string> Materials { get; init; } = [];

    /// <summary>The files.</summary>
    public required IReadOnlyList<ExportPresetTextureDocument> Textures { get; init; }
}

/// <summary>A preset to edit, and where it comes from.</summary>
/// <param name="Document">What its file says.</param>
/// <param name="Origin">factory (a Starter preset, shipped with Patina), library or project.</param>
/// <param name="File">The file on the Server's machine, or null for a Starter preset, which ships with Patina.</param>
/// <param name="Text">The file's text, as a person would read it.</param>
public sealed record ExportPresetSource(ExportPresetDocument Document, string Origin, string? File, string Text);

/// <summary>
///     The body of <c>PUT /api/v1/export/presets/{id}</c>: a preset to keep in the library's or a project's
///     <c>export-presets</c> folder, which is a file there.
/// </summary>
/// <param name="Preset">What the file says. Its id is the one in the route.</param>
/// <param name="Project">The project it belongs to, below <c>projects/</c>, or null for the whole library.</param>
/// <param name="Replaces">An id it takes the place of, in the same place, which renames it.</param>
public sealed record ExportPresetWrite(ExportPresetDocument Preset, string? Project = null, string? Replaces = null);

/// <summary>The body of <c>POST /api/v1/export/presets/check</c>: a preset to try without keeping it.</summary>
/// <param name="Preset">What the file would say.</param>
/// <param name="Project">A project whose names the file list uses, below <c>projects/</c>, or null.</param>
public sealed record ExportPresetCheckRequest(ExportPresetDocument Preset, string? Project = null);

/// <summary>One file a preset would write for a project, as named.</summary>
/// <param name="Template">The file's template.</param>
/// <param name="Set">The texture set it is of, or nothing for a project of one.</param>
/// <param name="Name">The file's path below the export folder.</param>
public sealed record ExportPresetFileName(string Template, string Set, string Name);

/// <summary>What a preset would do, before it is kept.</summary>
/// <param name="Problem">Why it cannot be kept, or null when it can.</param>
/// <param name="Files">Each file it would write for the project, or for a project called by its id.</param>
/// <param name="Text">The file it would be.</param>
public sealed record ExportPresetCheck(string? Problem, IReadOnlyList<ExportPresetFileName> Files, string Text);

/// <summary>The body of <c>POST /api/v1/export/presets/import</c>: a preset file to bring in.</summary>
/// <param name="File">A preset file on the Server's machine.</param>
/// <param name="Project">The project it goes into, below <c>projects/</c>, or null for the library.</param>
public sealed record ExportPresetImport(string File, string? Project = null);

/// <summary>
///     The body of <c>POST /api/v1/export/compression</c> (#217): one file of an export, to see a region of it as the
///     file holds it beside what it was made from.
/// </summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Preset">The preset's id.</param>
/// <param name="File">The file, by its template.</param>
public sealed record CompressionPreviewRequest(string Project, string Preset, string File) {
    /// <summary>The texture set, or null for the project's first.</summary>
    public string? Set { get; init; }

    /// <summary>The export's changes, as a build would be sent them: another compression, size or quality.</summary>
    public ExportOptions? Options { get; init; }

    /// <summary>The mip level, from zero.</summary>
    public int Level { get; init; }

    /// <summary>Where the region is centred across the level, from 0 to 1.</summary>
    public double X { get; init; } = 0.5;

    /// <summary>Where the region is centred down the level, from 0 to 1.</summary>
    public double Y { get; init; } = 0.5;

    /// <summary>The region's largest side in texels, 4 to 512.</summary>
    public int Side { get; init; } = 256;
}

/// <summary>How far one lane of a file is from what it was made from.</summary>
/// <param name="Lane">r, g, b or a.</param>
/// <param name="Psnr">Peak signal-to-noise ratio in decibels, or null where nothing changed.</param>
/// <param name="MaxError">The largest difference, in the file's numbers from 0 to 1.</param>
/// <param name="MeanError">The mean difference.</param>
public sealed record CompressionLaneError(string Lane, double? Psnr, double MaxError, double MeanError);

// SK7005: a wire record's constructor is its JSON shape, one parameter per field.
#pragma warning disable SK7005
/// <summary>
///     What a file's encoding does to a region of it. The pictures are not in this: each is a PNG at
///     <c>GET /api/v1/export/compression/{id}/{picture}</c>, for <c>source</c>, <c>written</c> and <c>difference</c>.
/// </summary>
/// <param name="Id">The preview, by the hash of what it was made from, for its pictures.</param>
/// <param name="Format">The file's format.</param>
/// <param name="Compression">What it holds.</param>
/// <param name="Level">The level shown.</param>
/// <param name="Levels">How many levels the file has.</param>
/// <param name="Width">The level's width.</param>
/// <param name="Height">The level's height.</param>
/// <param name="Region">The region shown, as x, y, width and height in the level's texels.</param>
/// <param name="Lanes">How far each lane is.</param>
/// <param name="Gain">What the difference picture's errors are multiplied by.</param>
public sealed record CompressionPreviewInfo(
    string Id,
    string Format,
    string Compression,
    int Level,
    int Levels,
    int Width,
    int Height,
    IReadOnlyList<int> Region,
    IReadOnlyList<CompressionLaneError> Lanes,
    double Gain
) {
    /// <summary>
    ///     What the whole file takes in GPU memory as written, by the measure the pre-export checks use
    ///     (<c>texture-over-budget</c>).
    /// </summary>
    public long Bytes { get; init; }

    /// <summary>What the same file would take with no block compression, by the same measure.</summary>
    public long Uncompressed { get; init; }
}
#pragma warning restore SK7005
