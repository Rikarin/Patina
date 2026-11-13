// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;

namespace Patina.Viewport;

/// <summary>
///     What the viewport marks on the model (#136): the named meshes and materials a client points at, washed in one
///     warm colour with their outline drawn, over the model as it is shaded. It draws nothing of its own where an
///     isolation hides a part, so a mark never floats where nothing is drawn.
/// </summary>
/// <remarks>
///     ⚠ <b>The outline is where the marked triangles end</b>, found by position and not by vertex: a vertex is split
///     wherever a UV seam or a hard edge runs, and an edge between two marked faces across a seam is no outline.
/// </remarks>
static class ViewportMarks {
    /// <summary>The wash over a marked face.</summary>
    static readonly Vector4 Wash = new(1f, 0.42f, 0.05f, 0.3f);

    /// <summary>The line round what is marked.</summary>
    static readonly Vector4 Edge = new(1f, 0.55f, 0.1f, 1f);

    /// <summary>
    ///     The shapes that mark these parts and materials, of those an isolation shows; nothing when none are
    ///     named.
    /// </summary>
    /// <param name="mesh">The model as the viewport draws it.</param>
    /// <param name="marked">The named meshes and materials marked.</param>
    /// <param name="isolated">The named meshes and materials drawn alone, both empty for the whole model.</param>
    public static ViewportShapes Shapes(
        ViewportMesh mesh,
        (string[] Parts, string[] Materials) marked,
        (string[] Parts, string[] Materials) isolated
    ) {
        if (marked.Parts.Length == 0 && marked.Materials.Length == 0) {
            return ViewportShapes.None;
        }

        var shown = mesh.Shown(isolated.Parts, isolated.Materials).ToHashSet();
        List<float> triangles = [];
        Dictionary<(Vector3, Vector3), int> edges = [];
        foreach (var part in mesh.Shown(marked.Parts, marked.Materials).Where(shown.Contains)) {
            for (var corner = part.FirstIndex; corner + 2 < part.FirstIndex + part.IndexCount; corner += 3) {
                Vector3[] at = [
                    Position(mesh, corner), Position(mesh, corner + 1), Position(mesh, corner + 2)
                ];
                for (var side = 0; side < 3; side++) {
                    Vertex(triangles, at[side], Wash);
                    var key = Key(at[side], at[(side + 1) % 3]);
                    edges[key] = edges.GetValueOrDefault(key) + 1;
                }
            }
        }

        List<float> lines = [];
        foreach (var ((from, to), _) in edges.Where(static edge => edge.Value == 1)) {
            Vertex(lines, from, Edge);
            Vertex(lines, to, Edge);
        }

        return new([..triangles], [..lines]);
    }

    static Vector3 Position(ViewportMesh mesh, int corner) {
        var at = (int)mesh.Indices[corner] * ViewportMesh.FloatsPerVertex;
        return new(mesh.Vertices[at], mesh.Vertices[at + 1], mesh.Vertices[at + 2]);
    }

    /// <summary>An edge the same whichever way round it is walked.</summary>
    static (Vector3, Vector3) Key(Vector3 a, Vector3 b) =>
        (a.X, a.Y, a.Z).CompareTo((b.X, b.Y, b.Z)) <= 0 ? (a, b) : (b, a);

    static void Vertex(List<float> into, Vector3 position, Vector4 colour) {
        into.Add(position.X);
        into.Add(position.Y);
        into.Add(position.Z);
        into.Add(colour.X);
        into.Add(colour.Y);
        into.Add(colour.Z);
        into.Add(colour.W);
    }
}
