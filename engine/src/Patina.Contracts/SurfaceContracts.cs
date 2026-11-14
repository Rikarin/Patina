namespace Patina.Contracts;

// SK7005: a wire record's constructor is its JSON shape, one parameter per field.
#pragma warning disable SK7005

/// <summary>
///     A decal (#133): a picture projected through a box placed on the model, kept in the stack as numbers, so it can
///     be moved, scaled and turned again. Every length is in the model's own units, the ones
///     <c>POST /viewport/pick</c> answers in.
/// </summary>
/// <param name="Centre">The box's middle: three numbers.</param>
/// <param name="Facing">The way the decal faces, out of the surface it lands on: three numbers, any length.</param>
/// <param name="Up">Which way is up on the picture: three numbers, made square to the facing.</param>
/// <param name="Width">How wide the picture lands. Above zero.</param>
/// <param name="Height">How tall it lands. Above zero.</param>
/// <param name="Depth">How deep the box reaches, through and out of the surface. Above zero.</param>
/// <param name="Angle">
///     How far a face may turn from the facing and still take the picture, in radians, 0 to π: the backface cull.
/// </param>
/// <param name="Fade">How much of the box's depth, from each face inwards, the picture fades over, 0 to 1.</param>
/// <param name="Tiled">Whether the picture repeats past its edges, or is nothing there.</param>
/// <param name="Sampling">How the picture is read between its pixels: <c>Bilinear</c> or <c>Point</c>.</param>
/// <param name="Box">
///     The box the mesh's <c>position</c> bake is normalised in: the least corner's three numbers, then the greatest's.
///     It is the model's bounds as <c>POST /viewport/pick</c> gives them. Null or empty is the unit cube. A bake that
///     says the box its position map spans is read through that box instead, so this one only matters for a bake that
///     does not.
/// </param>
/// <param name="Warp">
///     A warp (#134, #263): where each point of a square grid over the picture lands on the decal's square, row by row
///     from the top left, two numbers 0 to 1 across and down to a point: eighteen for 3 by 3, fifty for 5 by 5, up to
///     <see cref="MaximumWarpSide" /> points on a side. A finer grid follows a surface that turns away on both sides,
///     such as round a cylinder. Null or empty bends nothing.
/// </param>
public sealed record StackDecal(
    IReadOnlyList<float> Centre,
    IReadOnlyList<float> Facing,
    IReadOnlyList<float> Up,
    float Width,
    float Height,
    float Depth,
    float Angle = 1.3f,
    float Fade = 0.25f,
    bool Tiled = false,
    string Sampling = "Bilinear",
    IReadOnlyList<float>? Box = null,
    IReadOnlyList<float>? Warp = null
) {
    /// <summary>The finest warp grid, in points on a side: what the decal's kernel reads.</summary>
    public const int MaximumWarpSide = 9;
}

/// <summary>One point a path passes through, as <c>POST /viewport/pick</c> found it on the model.</summary>
/// <param name="Position">Where, in the model's own units: three numbers.</param>
/// <param name="Normal">The surface's normal there: three numbers.</param>
/// <param name="Pressure">How much of the width and strength the path has here, 0 to 1.</param>
public sealed record StackPathPoint(IReadOnlyList<float> Position, IReadOnlyList<float> Normal, float Pressure = 1f);

/// <summary>
///     A curve on the model that a fill with the <c>Path</c> projection is laid along (#130, #131): the points stay in
///     the stack, so they can be moved after the stroke is seen, and the stroke follows. It passes through each point.
/// </summary>
/// <param name="Points">The points, in order.</param>
/// <param name="Radius">Half the path's width at full pressure, in the model's units. Above zero.</param>
/// <param name="Hardness">How much of the width is at full strength before the edge falls away, 0 to 1.</param>
/// <param name="Ribbon">
///     Whether the fill's picture runs along the path, its height across the path's width, rather than showing through
///     the path where it lies on the map.
/// </param>
/// <param name="Repeat">
///     For a ribbon, how far along the path one repeat of the picture runs, in the model's units. Zero keeps the
///     picture's own shape at the path's width.
/// </param>
/// <param name="Angle">How far a face may turn from the path's normal and still take it, in radians, 0 to π.</param>
/// <param name="Box">The box the mesh's <c>position</c> bake is normalised in, as a decal's is.</param>
public sealed record StackSurfacePath(
    IReadOnlyList<StackPathPoint> Points,
    float Radius,
    float Hardness = 0.5f,
    bool Ribbon = false,
    float Repeat = 0f,
    float Angle = 1.3f,
    IReadOnlyList<float>? Box = null
);
#pragma warning restore SK7005
