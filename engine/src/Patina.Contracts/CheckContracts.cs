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

    /// <summary>What is wrong with these settings, or null when they can be used.</summary>
    public string? Problem() {
        (bool Wrong, string Why)[] checks = [
            (!(MemoryBudget >= 0 && double.IsFinite(MemoryBudget)), "A memory budget is zero or more mebibytes."),
            (MaxSize is < 0 or > 65536, $"A size limit is 0 to 65536 texels, not {MaxSize}."),
            (!(TexelDensity >= 0 && double.IsFinite(TexelDensity)), "A texel density is zero or more."),
            (!(DensityTolerance is > 0 and <= 10),
                $"A density tolerance is above 0 and at most 10, not {DensityTolerance}.")
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
        OverBudget, TooLarge, ChannelMissing, DensityLow, DensityHigh, BakeStale, UvOutside, UvOverlap, UvDegenerate,
        UvFlipped,
        AlbedoDark, AlbedoBright, MetalDark, MetalPartial
    ];

    /// <summary>Triangles laid out past UDIM tile 1001, which is all Patina paints. Values: <c>count</c>.</summary>
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
/// <param name="Outside">Triangles with a corner past UDIM tile 1001.</param>
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
/// <param name="Stack">The stack session.</param>
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
    IReadOnlyList<CheckProblem> Problems
);
#pragma warning restore SK7005
