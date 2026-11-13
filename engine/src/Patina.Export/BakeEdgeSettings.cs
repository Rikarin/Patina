using Patina.Contracts;
using Vixen.Geometry.Remeshing;

namespace Patina.Export;

/// <summary>A bake's edges as Vixen takes them: those rounded into the normal, those the edges map marks.</summary>
static class BakeEdgeSettings {
    /// <summary>The edges to round, or null when rounding is off.</summary>
    public static BakeRounding? Rounding(BakeRoundingOptions rounding) =>
        rounding.Enabled ? Of(rounding.Edges, rounding.Width, rounding.Angle, rounding.Profile) : null;

    /// <summary>The edges the edges map marks, which Vixen reads only when the map is baked.</summary>
    public static BakeRounding Marked(BakeEdgeOptions edges) =>
        Of(edges.Edges, edges.Width, edges.Angle, edges.Profile);

    static BakeRounding Of(string edges, double width, double angle, string profile) =>
        new((float)width, edges == "hard" ? RoundedEdges.Hard : RoundedEdges.Angle, (float)angle) {
            Profile = profile switch {
                "smooth" => RoundingProfile.Smooth,
                "chamfer" => RoundingProfile.Chamfer,
                _ => RoundingProfile.Even
            }
        };

    /// <summary>
    ///     What a curvature map is stored with: Vixen's own scale, or for a custom range one over the radius in
    ///     model units, so a bend that tight reads white and every bake made with it agrees.
    /// </summary>
    /// <param name="measured">The scale Vixen measured.</param>
    /// <param name="options">The bake's options.</param>
    /// <param name="diagonal">The high mesh's bounding-box diagonal, which a relative radius is a share of.</param>
    public static float CurvatureScale(float measured, BakeOptions options, float diagonal) {
        if (options.Curvature.Range != "custom") {
            return measured;
        }

        var unit = options.Rays.Units == "absolute" ? 1f : diagonal;
        var radius = (float)options.Curvature.Radius * unit;
        return radius > 0f ? 1f / radius : measured;
    }
}
