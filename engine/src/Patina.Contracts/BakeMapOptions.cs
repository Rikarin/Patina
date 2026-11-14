namespace Patina.Contracts;

/// <summary>The curvature map's stored range (#148).</summary>
/// <remarks>
///     Settable for the reason <see cref="BakeOptions" /> is: a client that leaves a member out keeps its default.
/// </remarks>
public sealed record BakeCurvatureOptions {
    /// <summary>The words <see cref="Range" /> takes.</summary>
    public static IReadOnlyList<string> Ranges { get; } = ["auto", "custom"];

    /// <summary>
    ///     <c>auto</c>, scaled so 99 texels in a hundred keep their gradient, which differs from mesh to mesh; or
    ///     <c>custom</c>, where a bend as tight as <see cref="Radius" /> reads white, or black for a crease, so every
    ///     bake made with it agrees.
    /// </summary>
    public string Range { get; set; } = "auto";

    /// <summary>For <c>custom</c>: the radius of the tightest bend the map tells apart, in the rays' units.</summary>
    public double Radius { get; set; } = 0.01;

    /// <summary>What is wrong, or null.</summary>
    public string? Problem() =>
        !Ranges.Contains(Range, StringComparer.Ordinal) ? $"'{Range}' is not a curvature range."
        : Range == "custom"
        && !(Radius > 0 && double.IsFinite(Radius)) ? "A custom curvature radius is more than nothing."
        : null;
}

/// <summary>The borders map's width (#121).</summary>
public sealed record BakeBorderOptions {
    /// <summary>
    ///     How far inside its UV island a texel reads white, as a share of the texture: black on the island's border,
    ///     rising evenly to white this far in.
    /// </summary>
    public double Width { get; set; } = 0.02;

    /// <summary>What is wrong, or null.</summary>
    public string? Problem() =>
        Width is > 0 and <= 1 ? null : "The borders map's width is more than nothing and at most the whole texture.";
}

/// <summary>Which edges the edges map marks, and the band it draws along them (#148).</summary>
public sealed record BakeEdgeOptions {
    /// <summary>The words <see cref="Profile" /> and a rounded edge's profile take.</summary>
    public static IReadOnlyList<string> Profiles { get; } = ["even", "smooth", "chamfer"];

    /// <summary>The words <see cref="Mesh" /> takes.</summary>
    public static IReadOnlyList<string> Meshes { get; } = ["high", "low"];

    /// <summary>
    ///     Whose edges are marked: <c>high</c>, the high mesh's, where the rays land; or <c>low</c>, the low mesh's, at
    ///     the texel, so its hard edges show even under a high mesh with none (#278).
    /// </summary>
    public string Mesh { get; set; } = "high";

    /// <summary>
    ///     <c>hard</c>, the edges the high mesh's own normals split, or <c>angle</c>, every edge whose faces meet at
    ///     <see cref="Angle" /> or more. With no high mesh the low mesh bakes onto itself, so these are its edges.
    /// </summary>
    public string Edges { get; set; } = "hard";

    /// <summary>How far either side of an edge the band reaches, in the rays' units.</summary>
    public double Width { get; set; } = 0.01;

    /// <summary>For <c>angle</c>: the least angle between two faces that is an edge, in degrees.</summary>
    public double Angle { get; set; } = 30;

    /// <summary>
    ///     How the band falls from white on the edge to black at its side: <c>even</c>, a straight ramp; <c>smooth</c>,
    ///     a smoothstep; or <c>chamfer</c>, white all the way across.
    /// </summary>
    public string Profile { get; set; } = "even";

    /// <summary>What is wrong, or null.</summary>
    public string? Problem() =>
        !BakeRoundingOptions.EdgeWords.Contains(Edges, StringComparer.Ordinal) ? $"'{Edges}' is not angle or hard."
        : !Profiles.Contains(Profile, StringComparer.Ordinal) ? $"'{Profile}' is not a profile."
        : !Meshes.Contains(Mesh, StringComparer.Ordinal) ? $"'{Mesh}' is not high or low."
        : !(Width > 0 && double.IsFinite(Width)) ? "The edges map needs a width."
        : Angle is < 0 or > 180 ? "The edge angle is 0 to 180 degrees."
        : null;
}
