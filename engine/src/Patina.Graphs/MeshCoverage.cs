using Patina.Export;
using Vixen.Core.Imaging;
using Vixen.Texturing.Painting;

namespace Patina.Graphs;

/// <summary>Which texels a mesh's layout covers, as a picture: what an export's padding grows from.</summary>
public static class MeshCoverage {
    /// <summary>
    ///     One where a triangle of the layout touches a texel, zero elsewhere, row zero at v = 0 as in every map. The
    ///     rasteriser is the one paint uses, so padding starts exactly where a brush stops.
    /// </summary>
    /// <param name="mesh">The model file.</param>
    /// <param name="material">The texture set's material, or null for the whole model.</param>
    /// <param name="uvSet">The coordinate set the texture set is laid out in (#210).</param>
    /// <param name="width">The maps' width.</param>
    /// <param name="height">The maps' height.</param>
    /// <returns>The picture, or null for a model with no layout, which has no background to pad.</returns>
    /// <exception cref="InvalidDataException">The model has no such material.</exception>
    public static FloatImage? Of(string mesh, string? material, int uvSet, int width, int height) =>
        Read(mesh, material, uvSet, width, height).Picture;

    /// <summary>
    ///     The coverage as <see cref="Of" /> draws it, and the UDIM tile the set's layout sits on (#20), which its
    ///     files are named by: 1001 when it reaches several.
    /// </summary>
    /// <exception cref="InvalidDataException">The model has no such material.</exception>
    public static (FloatImage? Picture, int Udim) Read(
        string mesh,
        string? material,
        int uvSet,
        int width,
        int height
    ) {
        var (model, udim) = MeshMapBakeRun.Placed(mesh, material, uvSet);
        var triangles = OneTile.Triangles(model.Mesh);
        if (triangles.Length == 0) {
            return (null, udim);
        }

        var covered = PaintCoverage.FromTriangles(width, height, triangles);
        var samples = new float[width * height];
        for (var at = 0; at < samples.Length; at++) {
            samples[at] = covered.IsCovered(at) ? 1f : 0f;
        }

        return (new(width, height, 1, samples), udim);
    }
}
