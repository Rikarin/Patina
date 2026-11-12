namespace Patina.Contracts;

/// <summary>
///     How a stroke on a mesh copies itself: through a plane, or round an axis. The plane or the axis passes through
///     the model's centre or the mesh's own origin, so a model that is not centred on its origin still mirrors onto
///     itself.
/// </summary>
/// <param name="Kind"><c>mirror</c> through a plane, or <c>radial</c> round an axis.</param>
/// <param name="Axis"><c>x</c>, <c>y</c> or <c>z</c>: the plane's normal, or the axis copies turn round.</param>
/// <param name="Centre"><c>model</c>, the centre of the model's bounds, or <c>origin</c>, the mesh's own.</param>
/// <param name="Offset">
///     How far a mirror's plane moves along its axis from that centre, as a share of the model's bounding radius, -1
///     to 1. A radial symmetry turns round a line along its axis, which moving along that axis does not change.
/// </param>
/// <param name="Count">How many strokes a radial symmetry makes, the stroke among them: 2 to 64.</param>
public sealed record StackSymmetry(
    string Kind = "mirror",
    string Axis = "x",
    string Centre = "model",
    float Offset = 0f,
    int Count = 2
);

/// <summary>
///     What a brush's fields take and how a new brush starts, so a client keeps no list or default of its own.
/// </summary>
/// <param name="Default">A new brush, which Reset to Default goes back to.</param>
/// <param name="Falloffs">How an edge can fall away.</param>
/// <param name="Rotations">How stamps can turn.</param>
/// <param name="SizeSpaces">What a radius can be measured in.</param>
/// <param name="ScatterAxes">Which way scatter can move a stamp.</param>
/// <param name="Symmetries">The kinds of symmetry.</param>
/// <param name="Axes">The axes a symmetry can use.</param>
/// <param name="Centres">What a symmetry's plane or axis can pass through.</param>
/// <param name="Tools">What a preset can hold the pointer as.</param>
/// <param name="Fills">How much of a mesh one click of a polygon fill can take.</param>
/// <param name="MaximumRadius">The largest radius a stroke takes, in its own units.</param>
/// <param name="MaximumAspect">The longest a stamp can be, against its width.</param>
/// <param name="MaximumCount">The most strokes a radial symmetry makes.</param>
/// <param name="MaterialProjections">How a material brush can lay its maps on the mesh.</param>
// SK7005: a wire record's constructor is its JSON shape, one parameter per field.
#pragma warning disable SK7005
public sealed record StackBrushTerms(
    StackBrush Default,
    IReadOnlyList<StackTerm> Falloffs,
    IReadOnlyList<StackTerm> Rotations,
    IReadOnlyList<StackTerm> SizeSpaces,
    IReadOnlyList<StackTerm> ScatterAxes,
    IReadOnlyList<StackTerm> Symmetries,
    IReadOnlyList<StackTerm> Axes,
    IReadOnlyList<StackTerm> Centres,
    IReadOnlyList<StackTerm> Tools,
    IReadOnlyList<StackTerm> Fills,
    float MaximumRadius,
    float MaximumAspect,
    int MaximumCount,
    IReadOnlyList<StackTerm> MaterialProjections
);
#pragma warning restore SK7005

/// <summary>
///     How a project's painting was left: the colours last used and kept, the brush and the symmetry. A project keeps
///     it in its <c>project.yaml</c>, so it survives a restart and any client starts from it.
/// </summary>
public sealed record ProjectPaintSettings {
    /// <summary>The most recent colours kept.</summary>
    public const int RecentLimit = 16;

    /// <summary>The most colours a palette holds.</summary>
    public const int PaletteLimit = 64;

    /// <summary>The colours strokes last used, newest first, as <c>#rrggbb</c>.</summary>
    public IReadOnlyList<string> RecentColours { get; init; } = [];

    /// <summary>Colours kept on purpose, in the order they were added, as <c>#rrggbb</c>.</summary>
    public IReadOnlyList<string> Palette { get; init; } = [];

    /// <summary>The brush last painted with, or null for a new one.</summary>
    public StackBrush? Brush { get; init; }

    /// <summary>The symmetry strokes are copied with, or null for none.</summary>
    public StackSymmetry? Symmetry { get; init; }

    /// <summary>What is wrong with these settings, or null when a project can keep them.</summary>
    public string? Problem() {
        if (RecentColours.Count > RecentLimit || Palette.Count > PaletteLimit) {
            return $"A project keeps {RecentLimit} recent colours and {PaletteLimit} in its palette.";
        }

        if (RecentColours.Concat(Palette).FirstOrDefault(static colour => !IsColour(colour)) is { } wrong) {
            return $"'{wrong}' is not a colour written #rrggbb.";
        }

        return Brush?.Problem() ?? Symmetry?.Problem();
    }

    /// <summary>Whether text is a colour written <c>#rrggbb</c>.</summary>
    public static bool IsColour(string text) => text is ['#', _, _, _, _, _, _] && text[1..].All(char.IsAsciiHexDigit);
}

/// <summary>The body of <c>PUT /api/v1/library/project/paint</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Paint">How its painting stands now.</param>
public sealed record ProjectPaintRequest(string Project, ProjectPaintSettings Paint);

/// <summary>
///     A brush or tool preset: what the pointer paints as and with. It is a document of a project, a <c>.ptbrush</c>
///     file, and the file is the truth.
/// </summary>
/// <param name="Tool">What the pointer does: brush, eraser, line or path.</param>
/// <param name="Brush">The brush, its size among it.</param>
/// <param name="Colour">The colour it paints, as <c>#rrggbb</c>, or null to keep the colour chosen.</param>
/// <param name="Favourite">Whether it is one of the favourites, which a shelf shows first.</param>
public sealed record BrushPreset(string Tool, StackBrush Brush, string? Colour = null, bool Favourite = false) {
    /// <summary>What is wrong with it, or null when it can be kept.</summary>
    public string? Problem() =>
        !StackBrushes.Tools.Contains(Tool) ? $"'{Tool}' is not a tool a preset holds."
        : Colour is not null && !ProjectPaintSettings.IsColour(Colour) ? $"'{Colour}' is not a colour written #rrggbb."
        : Brush.Problem();
}

/// <summary>A preset on the shelf.</summary>
/// <param name="Name">What it is called.</param>
/// <param name="Source">
///     What names it to the routes: <c>starter:</c> and a name, or its file's path in the library.
/// </param>
/// <param name="Origin"><c>shipped</c>, <c>project</c>, or <c>library</c> for another project.</param>
/// <param name="Preset">What it holds.</param>
/// <param name="Project">The project it lives in, when that is another one.</param>
public sealed record BrushPresetInfo(
    string Name,
    string Source,
    string Origin,
    BrushPreset Preset,
    string? Project = null
);

/// <summary>The presets a project can use: the shipped ones, its own, and other projects'.</summary>
/// <param name="Items">Them, favourites first, then by origin and name.</param>
public sealed record BrushPresetList(IReadOnlyList<BrushPresetInfo> Items);

/// <summary>The body of <c>POST /api/v1/library/brushes</c>, which saves a new preset in a project.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Name">What to call it, which names its file.</param>
/// <param name="Preset">What it holds: usually the brush as it stands.</param>
public sealed record BrushPresetCreateRequest(string Project, string Name, BrushPreset Preset);

/// <summary>
///     The body of <c>PUT /api/v1/library/brushes</c>, which rewrites a preset: from the brush as it stands, or to make
///     it a favourite or not. A shipped preset cannot change, so changing one keeps a copy in the project instead.
/// </summary>
/// <param name="Project">The project a shipped preset's copy goes in.</param>
/// <param name="Source">Which preset, as the shelf names it.</param>
/// <param name="Preset">What it holds now.</param>
public sealed record BrushPresetUpdateRequest(string Project, string Source, BrushPreset Preset);

/// <summary>Where a stroke's colour comes from, when it is not one colour.</summary>
/// <param name="Kind">
///     <c>smudge</c> drags what the canvas holds along the stroke, with the brush's flow; <c>clone</c> copies the
///     stack's composite from <paramref name="Offset" /> away, as it stood when the stroke began; <c>material</c> lays
///     what <paramref name="Graph" /> makes in the stroke's channel; <c>picture</c> projects
///     <paramref name="Picture" />.
/// </param>
/// <param name="Graph">For a material, the graph whose maps are laid, as a graph fill names it.</param>
/// <param name="Offset">
///     For a clone, how far the texel read is from the texel painted, in texels of the map: two numbers.
/// </param>
/// <param name="Picture">For a picture, the picture and where it stands.</param>
/// <param name="Projection">
///     For a material, how its maps land on the mesh, as a fill's do: <c>Uv</c> (the default) lays them over the
///     layout, so a material's size follows how the UVs are laid out; <c>Triplanar</c>, <c>Planar</c>,
///     <c>Spherical</c> and <c>Cylindrical</c> lay them in the model's space, the same size wherever the stroke goes.
///     A projection in the model's space needs a mesh.
/// </param>
/// <param name="PlanarAxis">For a planar material, the axis it projects along: X, Y or Z.</param>
/// <param name="Placement">For a material, how often its maps repeat, and how they turn and move.</param>
public sealed record StackStrokeSource(
    string Kind,
    string? Graph = null,
    IReadOnlyList<float>? Offset = null,
    StackPicture? Picture = null,
    string? Projection = null,
    string? PlanarAxis = null,
    StackPlacement? Placement = null
);

/// <summary>A picture of a brush: a stroke of it drawn small, under <c>/previews/{hash}</c>.</summary>
/// <param name="Hash">Its name in the preview cache, or null when the brush draws nothing.</param>
public sealed record StackBrushPreview(string? Hash);

/// <summary>
///     What every channel of a stack holds at one place on its map, as a material picker takes it up: numbers read
///     back from the composite at full size, never a picture.
/// </summary>
/// <param name="Channels">Each channel the set has, with its value there.</param>
public sealed record StackSample(IReadOnlyList<StackSampledChannel> Channels);

/// <summary>What one channel holds at a place.</summary>
/// <param name="Usage">The channel.</param>
/// <param name="Value">
///     Red, green, blue and alpha, 0 to 1, as the map stores them and a stroke's colour is given: a one-number channel
///     repeats its number in the first three.
/// </param>
public sealed record StackSampledChannel(string Usage, IReadOnlyList<float> Value);

/// <summary>The words and limits of a brush, which the Server checks a stroke and a preset against.</summary>
public static class StackBrushes {
    /// <summary>The largest radius, in a stroke's own units: a stamp this big is a fill, and still a stamp.</summary>
    public const float MaximumRadius = 4096f;

    /// <summary>The longest a stamp can be against its width, which is Vixen's cap on a stretched stamp.</summary>
    public const float MaximumAspect = 16f;

    /// <summary>The most strokes a radial symmetry makes, the stroke among them.</summary>
    public const int MaximumCount = 64;

    /// <summary>The facing angle that paints every face a camera looks at: a quarter turn, in radians.</summary>
    public const float AnyFacing = MathF.PI / 2f;

    /// <summary>The falloff curves.</summary>
    public static IReadOnlyList<string> Falloffs { get; } = ["smooth", "linear", "spherical", "tip"];

    /// <summary>How stamps turn: at a fixed angle, along the stroke, or at random.</summary>
    public static IReadOnlyList<string> Rotations { get; } = ["fixed", "along", "random"];

    /// <summary>What a radius is measured in.</summary>
    public static IReadOnlyList<string> SizeSpaces { get; } = ["screen", "texture", "model"];

    /// <summary>Which way scatter moves a stamp.</summary>
    public static IReadOnlyList<string> ScatterAxes { get; } = ["both", "across"];

    /// <summary>The kinds of symmetry.</summary>
    public static IReadOnlyList<string> Symmetries { get; } = ["mirror", "radial"];

    /// <summary>The axes of the mesh's own space.</summary>
    public static IReadOnlyList<string> Axes { get; } = ["x", "y", "z"];

    /// <summary>What a symmetry passes through.</summary>
    public static IReadOnlyList<string> Centres { get; } = ["model", "origin"];

    /// <summary>What a preset holds the pointer as: the tools that paint with a brush.</summary>
    public static IReadOnlyList<string> Tools { get; } =
        ["brush", "eraser", "line", "path", "smudge", "clone", "projection"];

    /// <summary>How much of a mesh one click of a polygon fill takes.</summary>
    public static IReadOnlyList<string> Fills { get; } = ["triangle", "polygon", "part", "shell"];

    /// <summary>
    ///     How a material brush lays its maps: a fill's projections, but for a decal and a path, which need a box or a
    ///     curve placed on the model first.
    /// </summary>
    public static IReadOnlyList<string> MaterialProjections { get; } =
        ["Uv", "Triplanar", "Planar", "Spherical", "Cylindrical"];

    /// <summary>The axes a planar material projects along, as a fill's planar axis names them.</summary>
    public static IReadOnlyList<string> PlanarAxes { get; } = ["X", "Y", "Z"];

    /// <summary>Where a stroke's colour can come from besides its colour.</summary>
    public static IReadOnlyList<string> Sources { get; } = ["smudge", "clone", "material", "picture"];

    /// <summary>
    ///     What is wrong with where a stroke's colour comes from, or null when a Worker can paint with it.
    /// </summary>
    /// <param name="source">The source.</param>
    public static string? Problem(this StackStrokeSource source) =>
        source.Kind switch {
            "smudge" => null,
            "clone" => source.Offset is [var x, var y] && float.IsFinite(x) && float.IsFinite(y)
                ? null
                : "A clone reads from an offset of two numbers, in texels.",
            "material" => MaterialProblem(source),
            "picture" => source.Picture is null
                ? "A projection names the picture it paints."
                : source.Picture.Problem(),
            _ => $"'{source.Kind}' is not where a stroke's colour can come from."
        };

    /// <summary>What is wrong with a material brush's graph and where its maps land, or null.</summary>
    static string? MaterialProblem(StackStrokeSource source) {
        if (string.IsNullOrWhiteSpace(source.Graph)) {
            return "A material brush names the graph it lays.";
        }

        if (source.Projection is { } projection && !MaterialProjections.Contains(projection)) {
            return $"'{projection}' is not how a material brush lays its maps.";
        }

        if (source.PlanarAxis is { } axis && !PlanarAxes.Contains(axis)) {
            return $"'{axis}' is not an axis.";
        }

        if (source.Placement is not { } placement) {
            return null;
        }

        float[] numbers =
            [placement.Scale, placement.Rotation, placement.OffsetX, placement.OffsetY, placement.Sharpness];
        return placement.Scale > 0f && numbers.All(float.IsFinite)
            ? null
            : "A material repeats a number of times above zero, and turns and moves by numbers.";
    }

    /// <summary>
    ///     What is wrong with what a stroke paints with besides its brush and its symmetry: a fill's mode and outline,
    ///     its source,
    ///     its stencil, and a mask asked to take a material or a clone, which has no such channel. Null when a Worker
    ///     can paint it.
    /// </summary>
    /// <param name="stroke">The stroke.</param>
    public static string? PaintProblem(this StackStroke stroke) {
        if (stroke.Curve == "fill" && !Fills.Contains(stroke.Fill ?? string.Empty)) {
            return "A fill takes a triangle, a polygon, a mesh part or a UV shell.";
        }

        if (stroke.Outline && (stroke.Curve != "fill" || stroke.Open || stroke.Stamps.Count < 3)) {
            return "An outline is a fill's, sent whole, with three corners at least.";
        }

        if (stroke.Resume is { } resume
            && (stroke.Curve != "free" || resume is not [var x, var y] || !float.IsFinite(x) || !float.IsFinite(y))) {
            return "A free stroke carries the last one on from where the brush stood: two numbers.";
        }

        if ((stroke.Source?.Problem() ?? stroke.Stencil?.Problem()) is { } problem) {
            return problem;
        }

        return stroke.Mask && stroke.Source?.Kind is "clone" or "material"
            ? "A mask is painted with a grey, a smudge or a picture, and not a material or a clone."
            : null;
    }

    /// <summary>What is wrong with a picture on a stroke's picture, or null.</summary>
    /// <param name="picture">The picture.</param>
    public static string? Problem(this StackPicture picture) {
        if (string.IsNullOrWhiteSpace(picture.Image)) {
            return "A picture on the screen names its file.";
        }

        if (picture.Centre is not [var x, var y] || !float.IsFinite(x) || !float.IsFinite(y)) {
            return "A picture on the screen stands at two numbers.";
        }

        return picture.Scale > 0f && float.IsFinite(picture.Scale) && float.IsFinite(picture.Angle)
            ? null
            : "A picture on the screen has a size above zero and an angle.";
    }

    /// <summary>A new brush: round and soft, laying all of its flow, its size following a pen's pressure.</summary>
    public static StackBrush Default { get; } = new(24f, Alpha: "Round", PressureSize: 1f);

    /// <summary>What is wrong with a brush, or null when a Worker can stroke with it.</summary>
    /// <param name="brush">The brush.</param>
    public static string? Problem(this StackBrush brush) => OutOfRange(brush) ?? Unknown(brush);

    /// <summary>What is wrong with a symmetry, or null when a Worker can copy a stroke with it.</summary>
    /// <param name="symmetry">The symmetry.</param>
    public static string? Problem(this StackSymmetry symmetry) {
        if (!Symmetries.Contains(symmetry.Kind)) {
            return $"'{symmetry.Kind}' is not a kind of symmetry.";
        }

        if (!Axes.Contains(symmetry.Axis)) {
            return $"'{symmetry.Axis}' is not an axis.";
        }

        if (!Centres.Contains(symmetry.Centre)) {
            return $"'{symmetry.Centre}' is not something a symmetry passes through.";
        }

        if (symmetry.Offset is < -1f or > 1f or float.NaN) {
            return "A plane moves at most one bounding radius either way.";
        }

        return symmetry.Kind == "radial" && symmetry.Count is < 2 or > MaximumCount
            ? $"A radial symmetry makes 2 to {MaximumCount} strokes."
            : null;
    }

    /// <summary>A number of a brush that no stroke could take, or null.</summary>
    static string? OutOfRange(StackBrush brush) {
        if (!(brush.Radius > 0f) || brush.Radius > MaximumRadius) {
            return $"A brush's radius is above 0 and at most {MaximumRadius:0}.";
        }

        if (!(brush.Spacing > 0f) || !float.IsFinite(brush.Spacing)) {
            return "A brush's stamps are some distance apart.";
        }

        if (brush.Lazy is < 0f or float.NaN or float.PositiveInfinity) {
            return "A lazy mouse trails the pointer by a distance of 0 or more.";
        }

        if (!(brush.OcclusionTolerance >= 0f) || brush.OcclusionTolerance > 1000f) {
            return "An occlusion tolerance is 0 to 1000 thousandths of the model's size.";
        }

        // A float's quarter turn is a hair over π/2 as JSON writes it back, so the cap allows that hair.
        if (!(brush.Facing >= 0f) || brush.Facing > AnyFacing + 1e-5f) {
            return "A facing angle is 0 to a quarter turn.";
        }

        if (!(brush.Aspect >= 1f) || brush.Aspect > MaximumAspect) {
            return $"A stamp is 1 to {MaximumAspect:0} times as long as it is wide.";
        }

        float[] numbers = [
            brush.Flow, brush.Opacity, brush.Falloff, brush.Angle, brush.PositionJitter, brush.AngleJitter,
            brush.SizeJitter, brush.PressureSize, brush.PressureFlow, brush.AspectAngle, brush.FlowJitter
        ];
        return numbers.All(float.IsFinite) ? null : "A brush's settings are numbers.";
    }

    /// <summary>A word of a brush the Server does not know, or null.</summary>
    static string? Unknown(StackBrush brush) {
        if (!Rotations.Contains(brush.Rotation)) {
            return $"'{brush.Rotation}' is not a way for a brush to turn.";
        }

        if (!Falloffs.Contains(brush.Curve)) {
            return $"'{brush.Curve}' is not a falloff curve.";
        }

        if (!ScatterAxes.Contains(brush.ScatterAxis)) {
            return $"'{brush.ScatterAxis}' is not a way for a stamp to scatter.";
        }

        return SizeSpaces.Contains(brush.SizeSpace) ? null : $"'{brush.SizeSpace}' is not what a size is measured in.";
    }
}
