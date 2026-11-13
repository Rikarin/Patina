namespace Patina.Contracts;

/// <summary>Where the brush goes: in texels of the image, or on the picture of a stroke with a view.</summary>
/// <param name="X">Its column.</param>
/// <param name="Y">Its row.</param>
/// <param name="Pressure">How hard, 0 to 1.</param>
/// <param name="Tilt">How far a pen leans, 0 upright to 1 lying flat, or null for upright, as a mouse is.</param>
/// <param name="Azimuth">
///     Which way a pen leans, in radians on the picture as a brush's angle is measured: 0 towards +x, a quarter turn
///     towards +y. Null is 0.
/// </param>
/// <param name="Twist">How far a pen's barrel is turned, in radians, measured as a brush's angle is. Null is 0.</param>
public sealed record StackStamp(
    float X,
    float Y,
    float Pressure = 1f,
    float? Tilt = null,
    float? Azimuth = null,
    float? Twist = null
);

// SK7005: a wire record's constructor is its JSON shape, one parameter per field.
#pragma warning disable SK7005

/// <summary>A brush. Everything but the radius has the value a new brush has.</summary>
/// <param name="Radius">
///     In <paramref name="SizeSpace" />: by default texels for a stroke with no view and the units of the picture for
///     one with a view. Above zero and at most <see cref="StackBrushes.MaximumRadius" />.
/// </param>
/// <param name="Flow">How much one stamp lays down, 0 to 1.</param>
/// <param name="Opacity">How much a whole stroke can lay down, 0 to 1.</param>
/// <param name="Spacing">
///     How far apart stamps are, as a share of the radius: above zero, and above 2 leaves gaps.
/// </param>
/// <param name="Falloff">How soft its edge is, 0 to 1.</param>
/// <param name="Alpha">A named alpha (Round, Square, Chisel, Cross) or an image relative to the stack, or null.</param>
/// <param name="Angle">Its fixed rotation, in radians.</param>
/// <param name="Rotation">fixed, along, random, pen (the barrel's turn) or tilt (the way the pen leans).</param>
/// <param name="PositionJitter">How far a stamp strays, as a share of the radius, 0 to 1.</param>
/// <param name="AngleJitter">How far a stamp turns either way, in radians.</param>
/// <param name="SizeJitter">How much of its radius a stamp can lose, 0 to 1.</param>
/// <param name="PressureSize">How much of the radius pressure takes off, 0 to 1.</param>
/// <param name="PressureFlow">How much of the flow pressure takes off, 0 to 1.</param>
/// <param name="Lazy">
///     How far the brush trails the pointer, in the units of the radius: a lazy mouse. 0 follows it. A line or a path
///     takes none, so that it reaches its last point.
/// </param>
/// <param name="Curve">How its edge falls away: smooth, linear, spherical or tip.</param>
/// <param name="Aspect">
///     How much longer than wide a stamp is, 1 to 16; 1 is round. It keeps the area of the round stamp, so a longer one
///     is narrower.
/// </param>
/// <param name="AspectAngle">
///     Which way the long side points, in radians, on the picture or the map it is made on.
/// </param>
/// <param name="FlowJitter">How much of its flow a stamp can lose, 0 to 1.</param>
/// <param name="ScatterAxis">
///     Which way <paramref name="PositionJitter" /> moves a stamp: both, or across the stroke only.
/// </param>
/// <param name="Accumulate">
///     Whether stamps build up where they overlap, to the opacity. Off, a place keeps the strongest stamp to reach it,
///     so a slow drag paints what a fast one does.
/// </param>
/// <param name="SizeSpace">
///     What the radius is measured in: <c>screen</c>, the stroke's own picture or map; <c>texture</c>, texels of the
///     map whatever the view; or <c>model</c>, thousandths of the model's size (its bounds' diagonal), which needs a
///     mesh. A size other than screen is measured where the stroke starts and kept for the whole of it.
/// </param>
/// <param name="Occlusion">
///     Whether a stroke with a view paints only what its camera sees. Off, every texel within the depth under the brush
///     is painted, hidden or not: a face close under a strap, the inside of a lip.
/// </param>
/// <param name="OcclusionTolerance">
///     How far in front of a texel something may lie and still not hide it, in thousandths of the model's size (its
///     bounds' diagonal), 0 to 1000: a decal floating a hair over a face is not the face's occluder.
/// </param>
/// <param name="Facing">
///     How far a face may turn from the camera and still be painted, in radians from the line of sight, 0 to π/2. π/2
///     paints every face the camera looks at; less keeps a stroke with a view off the faces it only grazes, where a
///     stamp smears into a streak. Measured per triangle.
/// </param>
/// <param name="Resume">
///     Whether a stroke that starts where the last one stopped carries it on. A client that sees one start there sends
///     where the brush stood as <see cref="StackStroke.Resume" />; the brush only says whether to.
/// </param>
/// <param name="TiltSize">
///     How much of the radius a pen held upright takes off, 0 to 1: a pen laid over paints the whole radius, as a
///     pencil's side paints wider. A mouse is upright, so a brush with any of this paints smaller under one.
/// </param>
public sealed record StackBrush(
    float Radius,
    float Flow = 1f,
    float Opacity = 1f,
    float Spacing = 0.15f,
    float Falloff = 0.5f,
    string? Alpha = null,
    float Angle = 0f,
    string Rotation = "fixed",
    float PositionJitter = 0f,
    float AngleJitter = 0f,
    float SizeJitter = 0f,
    float PressureSize = 0f,
    float PressureFlow = 0f,
    float Lazy = 0f,
    string Curve = "smooth",
    float Aspect = 1f,
    float AspectAngle = 0f,
    float FlowJitter = 0f,
    string ScatterAxis = "both",
    bool Accumulate = true,
    string SizeSpace = "screen",
    bool Occlusion = true,
    float OcclusionTolerance = 2f,
    float Facing = StackBrushes.AnyFacing,
    bool Resume = false,
    float TiltSize = 0f
);
#pragma warning restore SK7005

/// <summary>
///     A picture on a stroke's own picture: fixed on the screen, moved, scaled and turned there. A projection paints
///     its colours through the brush, and a stencil lets the brush through by its brightness.
/// </summary>
/// <param name="Image">A PNG, relative to the stack, to the library or absolute.</param>
/// <param name="Centre">Where its middle is on the stroke's picture, in the units stamps are in: two numbers.</param>
/// <param name="Scale">How many of those units one of its pixels covers, above zero.</param>
/// <param name="Angle">How far it is turned, clockwise on the screen, in radians.</param>
/// <param name="Tiled">Whether it repeats past its edges, or is nothing there.</param>
public sealed record StackPicture(
    string Image,
    IReadOnlyList<float> Centre,
    float Scale = 1f,
    float Angle = 0f,
    bool Tiled = false
);

// SK7005: a wire record's constructor is its JSON shape, one parameter per field.
#pragma warning disable SK7005

/// <summary>
///     Part of a stroke: where the brush went, never what it left behind. A drag sends several of these with one
///     <paramref name="Stroke" /> and they make one undo step, ended by one with <paramref name="Open" /> false.
/// </summary>
/// <param name="Stroke">A name the client chose for this drag.</param>
/// <param name="Layer">The paint layer, or for a mask any layer.</param>
/// <param name="Channel">Which of its channels. Ignored for a mask.</param>
/// <param name="Brush">The brush, which the first part decides.</param>
/// <param name="Colour">Red, green, blue and alpha, 0 to 1, which the first part decides. A mask reads the red.</param>
/// <param name="Stamps">Where the brush went since the last part.</param>
/// <param name="Open">Whether more of this drag is coming.</param>
/// <param name="Mask">
///     Whether this paints the layer's mask. A flat mask becomes a canvas of what it showed; a mask that is a picture
///     refuses, since nothing yet keeps that picture under paint.
/// </param>
/// <param name="View">
///     The camera, which makes this a stroke on the mesh: stamps are positions on its picture, each one is found on the
///     surface by the Worker, and it lands wherever the brush's disc falls, seams or no seams. Null paints the atlas.
/// </param>
/// <param name="Curve">
///     free follows the stamps; line and path run through them as control points when it ends; fill takes each stamp
///     as a click and fills the piece of the mesh under it, whole, as <paramref name="Fill" /> says.
/// </param>
/// <param name="Symmetry">
///     The copies the stroke makes of itself on the mesh, or null. A stroke in the atlas is copied too, wherever its
///     place on the surface lands; either way the stack needs a mesh.
/// </param>
/// <param name="MaskLayer">
///     With <paramref name="Mask" />, the entry of the mask's stack it paints, counted from the bottom, which must be a
///     paint entry. Null paints the mask's base.
/// </param>
/// <param name="Source">Where each texel's colour comes from, or null for <paramref name="Colour" />.</param>
/// <param name="Stencil">A picture on the screen that masks the brush by its brightness, or null.</param>
/// <param name="Fill">
///     With the fill curve, how much of the mesh a click takes: <c>triangle</c>, <c>polygon</c> (the flat polygon it
///     is part of), <c>part</c> (the named mesh it belongs to) or <c>shell</c> (its UV island). It needs a mesh.
/// </param>
/// <param name="Outline">
///     With the fill curve, whether the stamps are one outline rather than clicks: a rectangle's four corners or a
///     lasso, closed from the last back to the first, in the stamps' units. Every triangle it holds is filled, grown as
///     <paramref name="Fill" /> says. It is sent whole, as one part with three corners at least.
/// </param>
/// <param name="Through">
///     With an outline through a view, whether what the camera cannot see inside it is filled too: the far side of the
///     mesh, and what lies behind something nearer. False fills only what is seen. The atlas has no camera, so an
///     outline there takes everything inside it.
/// </param>
/// <param name="Resume">
///     Where the brush already stands when this stroke carries on the one before it, as two numbers in the units of
///     its stamps, or null. The stroke starts there without a stamp, as a pen lifted in the middle of one stroke and
///     put down again: its first stamp comes a step along, so nothing is laid twice where the two meet, and a lazy
///     brush keeps its string. Only a free stroke carries one on; it is still an undo step of its own.
/// </param>
public sealed record StackStroke(
    string Stroke,
    string Layer,
    string Channel,
    StackBrush Brush,
    IReadOnlyList<float> Colour,
    IReadOnlyList<StackStamp> Stamps,
    bool Open = false,
    bool Mask = false,
    StackView? View = null,
    string Curve = "free",
    StackSymmetry? Symmetry = null,
    int? MaskLayer = null,
    StackStrokeSource? Source = null,
    StackPicture? Stencil = null,
    string? Fill = null,
    IReadOnlyList<float>? Resume = null,
    bool Outline = false,
    bool Through = false
) : StackCommand;
#pragma warning restore SK7005
