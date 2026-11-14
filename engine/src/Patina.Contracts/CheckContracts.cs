namespace Patina.Contracts;

/// <summary>
///     What a project holds its texture sets to before they are exported (#159), kept in its <c>project.yaml</c>. A
///     zero is no limit, and a texel density of zero is the library's.
/// </summary>
public sealed record ProjectCheckSettings {
    /// <summary>
    ///     The most GPU memory one texture set's exported files may take, in mebibytes, or zero for no limit.
    /// </summary>
    public double MemoryBudget { get; init; }

    /// <summary>The longest side a texture set's maps may have, in texels, or zero for no limit.</summary>
    public int MaxSize { get; init; }

    /// <summary>
    ///     The texel density the sets are held to, in texels per unit of the model (a metre in glTF), or zero for the
    ///     library's.
    /// </summary>
    public double TexelDensity { get; init; }

    /// <summary>
    ///     How far a set's density may stray from it before it is flagged, as a share: 0.25 is a quarter.
    /// </summary>
    public double DensityTolerance { get; init; } = 0.25;

    /// <summary>
    ///     Limits of their own for the platforms the project ships to, each named by the export preset that writes for
    ///     it. A set measured with that preset is held to them in place of the project's.
    /// </summary>
    public IReadOnlyList<PlatformCheckLimits> Platforms { get; init; } = [];

    /// <summary>What is wrong with these settings, or null when they can be used.</summary>
    public string? Problem() {
        (bool Wrong, string Why)[] checks = [
            (!(DensityTolerance is > 0 and <= 10),
                $"A density tolerance is above 0 and at most 10, not {DensityTolerance}."),
            (Platforms.Any(static platform => string.IsNullOrWhiteSpace(platform.Preset)),
                "A platform's limits name the export preset that writes for it."),
            (Platforms.DistinctBy(static platform => platform.Preset, StringComparer.Ordinal).Count()
                != Platforms.Count, "Each platform has one set of limits.")
        ];
        return PlatformCheckLimits.ProblemOf(MemoryBudget, MaxSize, TexelDensity)
            ?? checks.FirstOrDefault(static check => check.Wrong).Why
            ?? Platforms.Select(static platform => platform.Problem()).FirstOrDefault(static why => why is not null);
    }

    /// <summary>
    ///     The limits a set measured with a preset is held to: its platform's own where it has them, and the project's
    ///     for any it leaves at zero.
    /// </summary>
    /// <param name="preset">The export preset's id, or null for the project's own limits.</param>
    public ProjectCheckSettings For(string? preset) =>
        Platforms.FirstOrDefault(platform => platform.Preset == preset) is { } own
            ? this with {
                MemoryBudget = own.MemoryBudget > 0 ? own.MemoryBudget : MemoryBudget,
                MaxSize = own.MaxSize > 0 ? own.MaxSize : MaxSize,
                TexelDensity = own.TexelDensity > 0 ? own.TexelDensity : TexelDensity
            }
            : this;

    /// <summary>Two settings are equal when their numbers are, and their platforms in the same order.</summary>
    public bool Equals(ProjectCheckSettings? other) =>
        other is not null
        && MemoryBudget.Equals(other.MemoryBudget)
        && MaxSize == other.MaxSize
        && TexelDensity.Equals(other.TexelDensity)
        && DensityTolerance.Equals(other.DensityTolerance)
        && Platforms.SequenceEqual(other.Platforms);

    public override int GetHashCode() => HashCode.Combine(MemoryBudget, MaxSize, TexelDensity, DensityTolerance);
}

/// <summary>
///     What one platform holds a project's texture sets to (#159), named by the export preset that writes for it. A
///     zero is the project's own limit.
/// </summary>
/// <param name="Preset">The export preset's id.</param>
public sealed record PlatformCheckLimits(string Preset) {
    /// <summary>The most GPU memory one set's files may take on it, in mebibytes, or zero for the project's.</summary>
    public double MemoryBudget { get; init; }

    /// <summary>The longest side a set's maps may have on it, in texels, or zero for the project's.</summary>
    public int MaxSize { get; init; }

    /// <summary>The texel density it holds sets to, or zero for the project's.</summary>
    public double TexelDensity { get; init; }

    /// <summary>What is wrong with these limits, or null when they can be used.</summary>
    public string? Problem() => ProblemOf(MemoryBudget, MaxSize, TexelDensity);

    internal static string? ProblemOf(double memoryBudget, int maxSize, double texelDensity) {
        (bool Wrong, string Why)[] checks = [
            (!(memoryBudget >= 0 && double.IsFinite(memoryBudget)), "A memory budget is zero or more mebibytes."),
            (maxSize is < 0 or > 65536, $"A size limit is 0 to 65536 texels, not {maxSize}."),
            (!(texelDensity >= 0 && double.IsFinite(texelDensity)), "A texel density is zero or more.")
        ];
        return checks.FirstOrDefault(static check => check.Wrong).Why;
    }
}

/// <summary>The body of <c>PUT /api/v1/library/project/checks</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Checks">What its texture sets are held to now.</param>
public sealed record ProjectChecksRequest(string Project, ProjectCheckSettings Checks);

/// <summary>
///     The codes a check's problem carries. Each is stable, so a client says it in its own words and finds it again
///     after the text changes; the Server's sentence is beside it, and the numbers it was written from.
/// </summary>
public static class CheckCodes {
    /// <summary>
    ///     The set's exported files take more GPU memory than the budget. Values: <c>bytes</c>, <c>budget</c>.
    /// </summary>
    public const string OverBudget = "texture-over-budget";

    /// <summary>The set's maps are larger than the limit. Values: <c>width</c>, <c>height</c>, <c>limit</c>.</summary>
    public const string TooLarge = "texture-too-large";

    /// <summary>
    ///     The export preset reads a map the set does not make, so a default fills its lanes. Values: <c>map</c>,
    ///     <c>preset</c>, <c>file</c>.
    /// </summary>
    public const string ChannelMissing = "channel-missing";

    /// <summary>
    ///     The set has fewer texels per unit than the project asks. Values: <c>density</c>, <c>target</c>.
    /// </summary>
    public const string DensityLow = "density-low";

    /// <summary>
    ///     The set has more texels per unit than the project asks. Values: <c>density</c>, <c>target</c>.
    /// </summary>
    public const string DensityHigh = "density-high";

    /// <summary>
    ///     The bake the set reads was made on another mesh or in another UV set, so the maps its shading, decals, paths
    ///     and mesh-map masks read are in the wrong place until it is baked again (#238). Values: <c>reason</c>, one of
    ///     <c>mesh</c> and <c>uvSet</c>, and <c>uvSet</c>, the set's UV set from 1.
    /// </summary>
    public const string BakeStale = "bake-stale";

    // Between the groups, as ExportMaps has it: SK7020 honours no suppression, and reads thirteen constants in a row as
    // a clone of another list of them.
    /// <summary>Every code, in the order a list shows them.</summary>
    public static IReadOnlyList<string> All { get; } = [
        OverBudget, TooLarge, ChannelMissing, PackingMixed, SpaceDataSrgb, SpaceColourLinear, CompressionNormal,
        MipsMissing, DensityLow, DensityHigh, BakeStale, UvOutside, UvOverlap, UvDegenerate, UvFlipped,
        AlbedoDark, AlbedoBright, MetalDark, MetalPartial
    ];

    /// <summary>
    ///     A file packs a colour map and a data map into its colour lanes, so one of them is held in the other's
    ///     encoding and an engine reads it with the wrong one. Values: <c>file</c>, <c>color</c>, <c>data</c>, each
    ///     channel by its label.
    /// </summary>
    public const string PackingMixed = "packing-mixed";

    /// <summary>
    ///     A file in sRGB holds a data map, such as a normal or roughness, whose numbers an engine reads as linear and
    ///     whose precision sRGB spends in the darks. Values: <c>file</c>, <c>channel</c>.
    /// </summary>
    public const string SpaceDataSrgb = "space-data-srgb";

    /// <summary>
    ///     An 8-bit file in linear holds base colour, which bands in the darks and reads washed out in an engine that
    ///     takes it as sRGB. Values: <c>file</c>, <c>channel</c>.
    /// </summary>
    public const string SpaceColourLinear = "space-color-linear";

    /// <summary>
    ///     A tangent-space normal held in BC1 or BC3 colour blocks, which shift its lanes together and show as blocks
    ///     on a lit surface; BC5 or BC7 keep it. Values: <c>file</c>, <c>compression</c>, by its label.
    /// </summary>
    public const string CompressionNormal = "compression-normal";

    /// <summary>
    ///     A KTX2 or DDS file without a mip chain, which an engine samples as it is, so it shimmers at a distance.
    ///     Values: <c>file</c>.
    /// </summary>
    public const string MipsMissing = "mips-missing";

    /// <summary>Triangles laid out past their texture set's one UDIM tile. Values: <c>count</c>.</summary>
    public const string UvOutside = "uv-outside";

    /// <summary>
    ///     Triangles that cover the same texels, so a stroke on one lands on the other. Values: <c>share</c>.
    /// </summary>
    public const string UvOverlap = "uv-overlap";

    /// <summary>Triangles with no area in the layout, which no texel lands on. Values: <c>count</c>.</summary>
    public const string UvDegenerate = "uv-degenerate";

    /// <summary>
    ///     Triangles turned over in the layout against the rest, whose normal map reads its green backwards. Values:
    ///     <c>count</c>.
    /// </summary>
    public const string UvFlipped = "uv-flipped";

    /// <summary>
    ///     Base colour on a surface that is not metal darker than sRGB 30, which no real dielectric is. Values:
    ///     <c>share</c>.
    /// </summary>
    public const string AlbedoDark = "pbr-albedo-dark";

    /// <summary>Base colour on a surface that is not metal brighter than sRGB 240. Values: <c>share</c>.</summary>
    public const string AlbedoBright = "pbr-albedo-bright";

    /// <summary>Base colour on bare metal darker than sRGB 180, which no real metal is. Values: <c>share</c>.</summary>
    public const string MetalDark = "pbr-metal-dark";

    /// <summary>
    ///     Metalness between 0.1 and 0.9, which is a blend or a mistake: a surface is metal or it is not. Values:
    ///     <c>share</c>.
    /// </summary>
    public const string MetalPartial = "pbr-metal-partial";
}

/// <summary>Where a problem is, for a client to go to it.</summary>
/// <param name="View">A viewport view that shows it, one of <see cref="ViewportViews.All" />, or null.</param>
/// <param name="Camera">A camera that looks at it on the mesh, or null where it is no place on the mesh.</param>
/// <param name="U">A texture coordinate across where it is, or null.</param>
/// <param name="V">A texture coordinate down where it is, or null.</param>
/// <param name="Setting">
///     The project's setting that decides it, as <c>block.field</c> (<c>checks.memoryBudget</c>,
///     <c>export.preset</c>), or null.
/// </param>
public sealed record CheckTarget(
    string? View = null,
    ViewportCameraState? Camera = null,
    float? U = null,
    float? V = null,
    string? Setting = null
);

/// <summary>One problem a texture set has before it is exported.</summary>
/// <param name="Code">One of <see cref="CheckCodes" />.</param>
/// <param name="Severity"><c>error</c> stops an engine taking it; <c>warning</c> needs a look.</param>
/// <param name="Detail">The Server's own sentence.</param>
/// <param name="Values">The numbers and names the sentence was written from, by name.</param>
/// <param name="Target">Where it is.</param>
public sealed record CheckProblem(
    string Code,
    string Severity,
    string Detail,
    IReadOnlyDictionary<string, string> Values,
    CheckTarget Target
);

/// <summary>
///     What the Worker measures of a texture set's layout, which the Server judges: it reads the mesh, and the Server
///     does not. It crosses the Worker's socket, not the API.
/// </summary>
/// <param name="Triangles">How many triangles the set has.</param>
/// <param name="SurfaceArea">Their area on the model, in its units squared.</param>
/// <param name="MapArea">Their area in the layout, where the whole tile is one, counting an overlap each time.</param>
/// <param name="Outside">Triangles with a corner past the one UDIM tile the set paints.</param>
/// <param name="Degenerate">Triangles with area on the model and none in the layout.</param>
/// <param name="Flipped">Triangles turned over in the layout against most of the set.</param>
/// <param name="Overlap">The share of the texels the layout covers that more than one triangle covers.</param>
/// <param name="Spots">One example of each kind of problem found, by its code.</param>
public sealed record LayoutMeasures(
    int Triangles,
    double SurfaceArea,
    double MapArea,
    int Outside,
    int Degenerate,
    int Flipped,
    double Overlap,
    IReadOnlyList<CheckSpot> Spots
);

/// <summary>Where one example of a problem is: a triangle, a place on the map, and a camera that looks at it.</summary>
/// <param name="Code">One of <see cref="CheckCodes" />.</param>
/// <param name="Triangle">The triangle, or -1 for a texel that is no one triangle's.</param>
/// <param name="U">Across the map.</param>
/// <param name="V">Down the map.</param>
/// <param name="Camera">A camera that looks at it, or null where it is no place on the mesh.</param>
public sealed record CheckSpot(string Code, int Triangle, float U, float V, ViewportCameraState? Camera);

/// <summary>
///     What share of a texture set's texels has a value no physical material has, measured by the Worker on the maps as
///     an export would write them: only texels the layout covers count. It crosses the Worker's socket, not the API.
/// </summary>
/// <param name="Code">The value's problem: one of the <c>pbr-</c> codes in <see cref="CheckCodes" />.</param>
/// <param name="Share">The share of the covered texels that have it.</param>
/// <param name="Spot">The first texel that has it.</param>
public sealed record MapMeasure(string Code, double Share, CheckSpot Spot);

/// <summary>The reply to <c>GET /api/v1/stacks/{id}/checks</c>: one texture set measured against its project.</summary>
/// <param name="Stack">The stack session, or empty for a set nobody has open.</param>
/// <param name="Set">The texture set, or empty for a model with no named sets.</param>
/// <param name="Width">Its maps' width, in texels.</param>
/// <param name="Height">Their height.</param>
/// <param name="TexelDensity">Texels per unit of the model across the whole set, or zero where it has no area.</param>
/// <param name="TargetDensity">What the project holds it to, or zero for nothing.</param>
/// <param name="MemoryBytes">The GPU memory its exported files take with the project's preset, mips included.</param>
/// <param name="MemoryBudget">The project's budget in bytes, or zero for none.</param>
/// <param name="Preset">The export preset measured against.</param>
/// <param name="Problems">
///     What is wrong, in the order of <see cref="CheckCodes.All" />, which puts the project's limits, the errors,
///     first.
/// </param>
/// <param name="Files">Each file the export writes for the set, with the GPU memory it takes.</param>
/// <param name="Document">The stack's file below the library, as a project lists it.</param>
/// <param name="Measured">
///     Whether the Worker measured its layout and values. A set nobody has open is judged on what the Server knows of
///     it, its limits, its files and its bake, and not on its density, its UVs or its values.
/// </param>
// SK7005: a wire record's constructor is its JSON shape, one parameter per field.
#pragma warning disable SK7005
public sealed record StackChecks(
    string Stack,
    string Set,
    int Width,
    int Height,
    double TexelDensity,
    double TargetDensity,
    long MemoryBytes,
    long MemoryBudget,
    string Preset,
    IReadOnlyList<CheckProblem> Problems,
    IReadOnlyList<CheckFile> Files,
    string Document,
    bool Measured
);
#pragma warning restore SK7005

/// <summary>
///     The reply to <c>GET /api/v1/library/project/checks</c>: every texture set and layered material of a project
///     checked at once, against one platform (#159).
/// </summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Preset">The export preset measured against, by its name.</param>
/// <param name="MemoryBytes">The GPU memory all the sets' exported files take together.</param>
/// <param name="Sets">Each set, in the order the project lists its documents.</param>
public sealed record ProjectChecks(string Project, string Preset, long MemoryBytes, IReadOnlyList<StackChecks> Sets);

/// <summary>One file an export writes for a texture set, and what it takes on a GPU (#159).</summary>
/// <param name="File">Its name as the export writes it, with its extension.</param>
/// <param name="Format">Its format, one of the export formats.</param>
/// <param name="Compression">What its blocks hold, or <c>none</c>.</param>
/// <param name="Width">Its width, in texels, as it is written.</param>
/// <param name="Height">Its height.</param>
/// <param name="Mips">Whether it carries a mip chain.</param>
/// <param name="Bytes">The GPU memory it takes, its mips included.</param>
#pragma warning disable SK7005
public sealed record CheckFile(
    string File,
    string Format,
    string Compression,
    int Width,
    int Height,
    bool Mips,
    long Bytes
);
#pragma warning restore SK7005
