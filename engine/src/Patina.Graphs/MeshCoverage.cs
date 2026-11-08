using Patina.Export;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
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
    /// <param name="width">The maps' width.</param>
    /// <param name="height">The maps' height.</param>
    /// <returns>The picture, or null for a model with no layout, which has no background to pad.</returns>
    /// <exception cref="InvalidDataException">The model has no such material.</exception>
    public static FloatImage? Of(string mesh, string? material, int width, int height) {
        var model = MeshMapBakeRun.Read(mesh, material).Mesh;
        var corners = model.TexCoords;
        if (corners.Length == 0 || corners.Length != model.CornerCount) {
            return null;
        }

        // Each face fanned into triangles, three corners at a time.
        List<Vector2> triangles = [];
        foreach (var face in model.Faces) {
            for (var corner = 1; corner + 1 < face.Count; corner++) {
                triangles.Add(corners[face.Start]);
                triangles.Add(corners[face.Start + corner]);
                triangles.Add(corners[face.Start + corner + 1]);
            }
        }

        var covered = PaintCoverage.FromTriangles(width, height, triangles);
        var samples = new float[width * height];
        for (var at = 0; at < samples.Length; at++) {
            samples[at] = covered.IsCovered(at) ? 1f : 0f;
        }

        return new(width, height, 1, samples);
    }
}
