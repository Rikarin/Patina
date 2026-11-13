using System.Globalization;

namespace Patina.Contracts;

/// <summary>
///     A high-to-low bake. Everything is a path on the Server's machine, and the results are files there: a client
///     sends paths and gets paths, whichever client it is (plan §8).
/// </summary>
public sealed record BakeRequest {
    /// <summary>The detailed meshes, in order: one is usual, and a model split across files lists them all.</summary>
    public required IReadOnlyList<string> High { get; set; }

    /// <summary>The mesh baked onto, which must have texture coordinates.</summary>
    public required string Low { get; set; }

    /// <summary>The folder the maps are written to.</summary>
    public required string Output { get; set; }

    /// <summary>A mesh with the low mesh's faces, moved outward, which the rays start from.</summary>
    public string? Cage { get; set; }

    /// <summary>The material of the low mesh to bake, for one texture set of a model that has several.</summary>
    public string? Material { get; set; }

    /// <summary>
    ///     The low mesh's texture coordinate set the maps are baked in, by the file's number: zero for the first
    ///     (#210). Null bakes a texture set in the set it is laid out in, when the low mesh is a project's and
    ///     <see cref="Material" /> names one of its texture sets, and in the first otherwise. The high mesh is read
    ///     whole, so its sets do not matter, but for a picture moved from its layout, which is always its first.
    /// </summary>
    public int? UvSet { get; set; }

    /// <summary>The stem of each file's name, or null for the low mesh's.</summary>
    public string? Name { get; set; }

    /// <summary>What to bake and how: the part a project keeps between bakes.</summary>
    public BakeOptions Options { get; set; } = new();
}

/// <summary>
///     How a bake runs, with no meshes in it: what a project keeps between bakes and every client sends. Each map's
///     own settings are a part of their own.
/// </summary>
/// <remarks>
///     ⚠ <b>Settable rather than init-only, in this file only, and on purpose.</b> The source-generated JSON reader
///     sets an init-only member the JSON leaves out to zero or null instead of keeping its default, so a client that
///     sent only the front distance baked with a rear distance of nothing. A settable one is left alone.
/// </remarks>
public sealed record BakeOptions {
    /// <summary>The words <see cref="Format" /> takes.</summary>
    public static IReadOnlyList<string> Formats { get; } = ["png8", "png16", "exr16", "exr32"];

    /// <summary>The words <see cref="Space" /> takes.</summary>
    public static IReadOnlyList<string> Spaces { get; } = ["tangent", "object"];

    /// <summary>Every map a bake can write, in the order they are reported.</summary>
    public static IReadOnlyList<string> AllMaps { get; } =
        ["normal", "height", "ao", "bent", "curvature", "thickness", "position", "world", "id", "colour"];

    /// <summary>The atlas's width, in texels, and its height too unless <see cref="Rows" /> says otherwise.</summary>
    public int Resolution { get; set; } = 2048;

    /// <summary>
    ///     The atlas's height in texels, or null for as many as <see cref="Resolution" />: a square bake. A texture set
    ///     authored wider than it is tall bakes at its own shape, so its maps sit under its layers unresampled (#147).
    /// </summary>
    public int? Rows { get; set; }

    /// <summary>
    ///     Whether a bake of a project's texture set takes its height from the set's own shape at
    ///     <see cref="Resolution" />, in place of <see cref="Rows" />, so that a wide set and a square one baked together
    ///     each bake at their own (#253). Null follows too; only false keeps <see cref="Rows" />. A bake of no texture set keeps
    ///     <see cref="Rows" />, and a finished bake's options say the height it was made at and leave this out.
    /// </summary>
    public bool? FollowSetShape { get; set; }

    /// <summary><c>png8</c>, <c>png16</c>, <c>exr16</c> or <c>exr32</c>.</summary>
    public string Format { get; set; } = "png16";

    /// <summary>How many samples along each side of a texel.</summary>
    public int Supersampling { get; set; } = 1;

    /// <summary>How many texels of dilation surround each chart.</summary>
    public int Gutter { get; set; } = 4;

    /// <summary>
    ///     Which maps to write: <c>normal</c>, <c>height</c>, <c>ao</c>, <c>bent</c>, <c>curvature</c>,
    ///     <c>thickness</c>, <c>position</c>, <c>world</c>, <c>id</c>, <c>colour</c>. The default is what a project's
    ///     masks read.
    /// </summary>
    public IReadOnlyList<string> Maps { get; set; } =
        ["normal", "height", "ao", "curvature", "thickness", "position", "world", "id"];

    /// <summary>The frame of the normal and bent normal maps: <c>tangent</c> or <c>object</c>.</summary>
    public string Space { get; set; } = "tangent";

    /// <summary>How far the rays look and what they may strike, which every map rests on.</summary>
    public BakeRayOptions Rays { get; set; } = new();

    /// <summary>The height map's range.</summary>
    public BakeHeightOptions Height { get; set; } = new();

    /// <summary>The position map's range.</summary>
    public BakePositionOptions Position { get; set; } = new();

    /// <summary>The occlusion rays, which ambient occlusion, the bent normal and thickness share.</summary>
    public BakeOcclusionOptions Occlusion { get; set; } = new();

    /// <summary>The thickness map's own settings.</summary>
    public BakeThicknessOptions Thickness { get; set; } = new();

    /// <summary>What the ID map labels and how it colours them.</summary>
    public BakeIdOptions Id { get; set; } = new();

    /// <summary>Edges baked round into the normal, with no high poly to give them a bevel.</summary>
    public BakeRoundingOptions Rounding { get; set; } = new();

    /// <summary>Where the colour map's colour comes from.</summary>
    public BakeColourOptions Colour { get; set; } = new();

    /// <summary>
    ///     The code of the refusal of a minimum occluder distance at or past the occlusion distance while occlusion or
    ///     the bent normal is baked: every ray would start past its reach, and the map come out white (#228).
    /// </summary>
    public const string MinimumPastDistance = "bake-occluder-minimum";

    /// <summary>What is wrong with these options, or null when a bake can use them.</summary>
    public string? Problem() => Refusal()?.Detail;

    /// <summary>What is wrong with these options, with a code when a client explains it, or null.</summary>
    public BakeOptionsRefusal? Refusal() {
        if (Check() is { } problem) {
            return new(problem, null, new Dictionary<string, string>());
        }

        var occluded = Maps.Any(static map => map is "ao" or "bent");
        return occluded && Occlusion.Minimum >= Occlusion.Distance
            ? new(
                "The minimum occluder distance must be less than the occlusion distance.",
                MinimumPastDistance,
                new Dictionary<string, string> {
                    ["minimum"] = Occlusion.Minimum.ToString(CultureInfo.InvariantCulture),
                    ["distance"] = Occlusion.Distance.ToString(CultureInfo.InvariantCulture)
                }
            )
            : null;
    }

    string? Check() {
        var unknown = Maps.FirstOrDefault(static map => !AllMaps.Contains(map, StringComparer.Ordinal));
        (bool Wrong, string Why)[] checks = [
            (Resolution is < 16 or > 16384, $"The resolution must be 16 to 16384, not {Resolution}."),
            (Rows is < 16 or > 16384, $"The height must be 16 to 16384 texels, not {Rows}."),
            (Supersampling is < 1 or > 8, $"Supersampling must be 1 to 8, not {Supersampling}."),
            (Gutter is < 0 or > 64, $"The gutter must be 0 to 64 texels, not {Gutter}."),
            (!Formats.Contains(Format, StringComparer.Ordinal), $"'{Format}' is not a format."),
            (!Spaces.Contains(Space, StringComparer.Ordinal), $"'{Space}' is not a space."),
            (unknown is not null, $"'{unknown}' is not a map. The maps are {string.Join(", ", AllMaps)}."),
            (Maps.Count == 0, "A bake has to write at least one map.")
        ];
        return checks.FirstOrDefault(static check => check.Wrong).Why
            ?? Rays.Problem()
            ?? Height.Problem()
            ?? Position.Problem()
            ?? Occlusion.Problem()
            ?? Id.Problem()
            ?? Rounding.Problem()
            ?? Colour.Problem(Maps.Contains("colour", StringComparer.Ordinal));
    }
}

/// <summary>Why bake options cannot be used: the neutral sentence, and a code with its values, or none.</summary>
/// <param name="Detail">What is wrong, in the Server's words.</param>
/// <param name="Code">A code a client can say in its own words, or null for one it is not expected to explain.</param>
/// <param name="Values">The values the detail was written from, by name.</param>
public sealed record BakeOptionsRefusal(string Detail, string? Code, IReadOnlyDictionary<string, string> Values);

/// <summary>How far a bake's rays look from the low mesh, and what they may strike.</summary>
public sealed record BakeRayOptions {
    /// <summary>The words <see cref="Units" /> takes.</summary>
    public static IReadOnlyList<string> UnitWords { get; } = ["relative", "absolute"];

    /// <summary>How far a ray looks outward, in front of the low mesh.</summary>
    public double Front { get; set; } = 0.05;

    /// <summary>How far a ray looks inward, behind the low mesh, or past it from a cage.</summary>
    public double Rear { get; set; } = 0.05;

    /// <summary>
    ///     What the distances here and in <see cref="BakeOcclusionOptions" /> are in: <c>relative</c>, fractions of the
    ///     high mesh's bounding-box diagonal, or <c>absolute</c>, the model's own units.
    /// </summary>
    public string Units { get; set; } = "relative";

    /// <summary>Whether a ray passes through a high face turned away from the low mesh's facing.</summary>
    public bool IgnoreBackfaces { get; set; }

    /// <summary>How far rays lean from the smoothed direction towards the low mesh's own normal, 0 to 1.</summary>
    public double Skew { get; set; }

    /// <summary>A grey image saying where to apply the skew, or empty for everywhere.</summary>
    public string SkewMap { get; set; } = string.Empty;

    /// <summary>
    ///     A grey image correcting how far the rays reach over the atlas, or empty for as set: mid grey leaves them,
    ///     black pulls them onto the low mesh and white sends them twice as far, front, rear and cage alike.
    /// </summary>
    public string DistanceMap { get; set; } = string.Empty;

    /// <summary>Whether a part of the low mesh only strikes the high part of the same name.</summary>
    public bool MatchByName { get; set; }

    /// <summary>What a low part's name ends in, which matching by name takes off.</summary>
    public string LowSuffix { get; set; } = "_low";

    /// <summary>What a high part's name ends in, which matching by name takes off.</summary>
    public string HighSuffix { get; set; } = "_high";

    /// <summary>What is wrong, or null.</summary>
    public string? Problem() {
        (bool Wrong, string Why)[] checks = [
            (!(Front >= 0 && double.IsFinite(Front)), "The front distance cannot be negative."),
            (!(Rear >= 0 && double.IsFinite(Rear)), "The rear distance cannot be negative."),
            (Front <= 0 && Rear <= 0, "The rays must reach some way in front or behind."),
            (!UnitWords.Contains(Units, StringComparer.Ordinal), $"'{Units}' is not relative or absolute."),
            (Skew is < 0 or > 1, "The skew must be 0 to 1.")
        ];
        return checks.FirstOrDefault(static check => check.Wrong).Why;
    }
}

/// <summary>The height map's stored range.</summary>
public sealed record BakeHeightOptions {
    /// <summary>The words <see cref="Range" /> takes.</summary>
    public static IReadOnlyList<string> Ranges { get; } = ["measured", "rays", "custom"];

    /// <summary>
    ///     <c>measured</c>, the largest height found; <c>rays</c>, the longer ray distance; or <c>custom</c>,
    ///     <see cref="Scale" />. The last two are the same for every bake made with them, so their maps agree.
    /// </summary>
    public string Range { get; set; } = "measured";

    /// <summary>The range for <c>custom</c>, in the rays' units.</summary>
    public double Scale { get; set; } = 0.05;

    /// <summary>What is wrong, or null.</summary>
    public string? Problem() =>
        !Ranges.Contains(Range, StringComparer.Ordinal) ? $"'{Range}' is not a height range."
        : Range == "custom" && !(Scale > 0 && double.IsFinite(Scale)) ? "A custom height range is more than nothing."
        : null;
}

/// <summary>How the position map turns a point on the high poly mesh into a value (#148).</summary>
public sealed record BakePositionOptions {
    /// <summary>The words <see cref="Range" /> takes.</summary>
    public static IReadOnlyList<string> Ranges { get; } = ["box", "cube", "sphere", "none"];

    /// <summary>
    ///     <c>box</c>, each axis across the mesh's bounding box, so each channel is used whole and shapes stretch;
    ///     <c>cube</c>, every axis across the box's longest side, so distances agree; <c>sphere</c>, across the sphere
    ///     round the box, centred on its middle; or <c>none</c>, the point in model units, which only EXR keeps.
    /// </summary>
    public string Range { get; set; } = "box";

    /// <summary>What is wrong, or null.</summary>
    public string? Problem() =>
        !Ranges.Contains(Range, StringComparer.Ordinal) ? $"'{Range}' is not a position range." : null;
}

/// <summary>The occlusion rays, which ambient occlusion, the bent normal and thickness share.</summary>
public sealed record BakeOcclusionOptions {
    /// <summary>The words <see cref="Falloff" /> takes.</summary>
    public static IReadOnlyList<string> Falloffs { get; } = ["none", "linear", "smooth"];

    /// <summary>The words <see cref="Distribution" /> takes.</summary>
    public static IReadOnlyList<string> Distributions { get; } = ["cosine", "uniform"];

    /// <summary>How many rays a texel's occlusion is estimated with.</summary>
    public int Rays { get; set; } = 64;

    /// <summary>How far an occluder counts, in the rays' units.</summary>
    public double Distance { get; set; } = 0.5;

    /// <summary>The half-angle of the cone the rays fill, in degrees: 90 is the hemisphere.</summary>
    public double Spread { get; set; } = 90;

    /// <summary>How an occluder counts for less further away: <c>none</c>, <c>linear</c> or <c>smooth</c>.</summary>
    public string Falloff { get; set; } = "none";

    /// <summary>How the rays are spread: <c>cosine</c> or <c>uniform</c>.</summary>
    public string Distribution { get; set; } = "cosine";

    /// <summary>How near an occluder may be and not count, in the rays' units.</summary>
    public double Minimum { get; set; }

    /// <summary>Whether a ray passes through the back of a face.</summary>
    public bool IgnoreBackfaces { get; set; }

    /// <summary>Whether only the texel's own part occludes it, when parts are matched by name.</summary>
    public bool WithinPart { get; set; }

    /// <summary>Whether a floor under the high mesh occludes too. Y is up.</summary>
    public bool GroundPlane { get; set; }

    /// <summary>How far above the high mesh's lowest point the floor is, in the rays' units.</summary>
    public double GroundOffset { get; set; }

    /// <summary>What is wrong, or null.</summary>
    public string? Problem() {
        (bool Wrong, string Why)[] checks = [
            (Rays is < 1 or > 8192, "Occlusion rays must be 1 to 8192."),
            (!(Distance >= 0 && double.IsFinite(Distance)), "The occlusion distance cannot be negative."),
            (Spread is <= 0 or > 90, "The occlusion spread is more than 0 and at most 90 degrees."),
            (!Falloffs.Contains(Falloff, StringComparer.Ordinal), $"'{Falloff}' is not a falloff."),
            (!Distributions.Contains(Distribution, StringComparer.Ordinal), $"'{Distribution}' is not a distribution."),
            (!(Minimum >= 0 && double.IsFinite(Minimum)), "The minimum occluder distance cannot be negative."),
            (!double.IsFinite(GroundOffset), "The ground offset is a number.")
        ];
        return checks.FirstOrDefault(static check => check.Wrong).Why;
    }
}

/// <summary>The thickness map's own settings.</summary>
public sealed record BakeThicknessOptions {
    /// <summary>Whether the map is divided by its largest value, so the thickest texel reads white.</summary>
    public bool Normalised { get; set; } = true;
}

/// <summary>What an ID map labels, and how it colours the labels.</summary>
public sealed record BakeIdOptions {
    /// <summary>The words <see cref="Source" /> takes.</summary>
    public static IReadOnlyList<string> Sources { get; } =
        ["auto", "material", "material-colour", "vertex-colour", "mesh", "shell"];

    /// <summary>The words <see cref="Palette" /> takes.</summary>
    public static IReadOnlyList<string> Palettes { get; } = ["random", "hue", "grey"];

    /// <summary>
    ///     <c>auto</c>: vertex colours, else materials when there are several, else connected shells. Or one of
    ///     <c>material</c>, <c>material-colour</c> (each material's base colour), <c>vertex-colour</c>, <c>mesh</c>
    ///     (each named part) and <c>shell</c>.
    /// </summary>
    public string Source { get; set; } = "auto";

    /// <summary>How labels become colours: <c>random</c>, <c>hue</c> or <c>grey</c>. Colours stay colours.</summary>
    public string Palette { get; set; } = "random";

    /// <summary>What is wrong, or null.</summary>
    public string? Problem() =>
        !Sources.Contains(Source, StringComparer.Ordinal) ? $"'{Source}' is not an ID source."
        : !Palettes.Contains(Palette, StringComparer.Ordinal) ? $"'{Palette}' is not an ID palette."
        : null;
}

/// <summary>Edges baked round into the normal map, the bevel a hard-surface model was not modelled with.</summary>
public sealed record BakeRoundingOptions {
    /// <summary>The words <see cref="Edges" /> takes.</summary>
    public static IReadOnlyList<string> EdgeWords { get; } = ["angle", "hard"];

    /// <summary>Whether edges are rounded at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>How far either side of an edge the bevel reaches, in the rays' units.</summary>
    public double Width { get; set; } = 0.01;

    /// <summary>
    ///     Which edges: <c>angle</c>, every edge whose faces meet at <see cref="Angle" /> or more, or <c>hard</c>, the
    ///     edges the mesh's own normals split, which is the nearest a mesh file has to an edge weight.
    /// </summary>
    public string Edges { get; set; } = "angle";

    /// <summary>For <c>angle</c>: the least angle between two faces that is an edge, in degrees.</summary>
    public double Angle { get; set; } = 30;

    /// <summary>What is wrong, or null.</summary>
    public string? Problem() =>
        !EdgeWords.Contains(Edges, StringComparer.Ordinal) ? $"'{Edges}' is not angle or hard."
        : !(Width >= 0 && double.IsFinite(Width)) ? "A rounded edge's width cannot be negative."
        : Enabled && Width <= 0 ? "A rounded edge needs a width."
        : Angle is < 0 or > 180 ? "The rounding angle is 0 to 180 degrees."
        : null;
}

/// <summary>Where a colour map's colour comes from.</summary>
public sealed record BakeColourOptions {
    /// <summary>The words <see cref="Source" /> takes.</summary>
    public static IReadOnlyList<string> Sources { get; } = ["auto", "vertex-colour", "material", "texture"];

    /// <summary>
    ///     <c>auto</c>: the high mesh's vertex colours, else its materials. <c>vertex-colour</c>; <c>material</c>, each
    ///     material's base colour and base colour picture; or <c>texture</c>, the picture at <see cref="Texture" />
    ///     read at the high mesh's own coordinates, which moves any map from one UV layout to another.
    /// </summary>
    public string Source { get; set; } = "auto";

    /// <summary>For <c>texture</c>: the picture to carry across, as a path on the Server's machine.</summary>
    public string Texture { get; set; } = string.Empty;

    /// <summary>What is wrong, or null.</summary>
    /// <param name="baked">Whether the colour map is among the maps a bake writes.</param>
    public string? Problem(bool baked) =>
        !Sources.Contains(Source, StringComparer.Ordinal) ? $"'{Source}' is not a colour source."
        : baked && Source == "texture" && Texture.Trim().Length == 0 ? "Choose the picture to carry across."
        : null;
}

/// <summary>
///     What a project keeps about baking, in its own file: the options, which texture sets to bake, and the files
///     beyond the project's mesh that a bake reads.
/// </summary>
public sealed record ProjectBakeSettings {
    /// <summary>How its bakes run.</summary>
    public BakeOptions Options { get; set; } = new();

    /// <summary>The texture sets a bake makes maps for, by material; empty is all of them.</summary>
    public IReadOnlyList<string> Sets { get; set; } = [];

    /// <summary>Detailed meshes after the mesh's own, in order, as paths on the Server's machine.</summary>
    public IReadOnlyList<string> High { get; set; } = [];

    /// <summary>A cage, as a path on the Server's machine, or empty for none.</summary>
    public string Cage { get; set; } = string.Empty;
}

/// <summary>The body of <c>PUT /api/v1/library/project/bake</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Bake">Its bake settings from now on.</param>
public sealed record ProjectBakeRequest(string Project, ProjectBakeSettings Bake);
