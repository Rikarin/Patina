// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;

namespace Patina.Viewport;

/// <summary>
///     A symmetry's mirror as a stroke takes it: the axis its plane faces along, and where it stands against the whole
///     model's box, which the viewport finds on the mesh it draws.
/// </summary>
/// <param name="Axis">0 for x, 1 for y, 2 for z.</param>
/// <param name="Origin">Whether it passes through the origin rather than the box's centre.</param>
/// <param name="Offset">How far it moves along its axis, as a share of the box's half-diagonal.</param>
public readonly record struct ViewportMirror(int Axis, bool Origin, float Offset);

/// <summary>A mirror's plane in the model's space: the axis it faces along, and where it crosses that axis.</summary>
/// <param name="Axis">0 for x, 1 for y, 2 for z.</param>
/// <param name="At">Where on that axis the plane stands, in the model's units.</param>
readonly record struct ViewportPlane(int Axis, float At);

/// <summary>
///     Where a symmetry's mirror meets the model (#132): the line its plane cuts across the surface, drawn on the
///     model and hidden where the model hides it, so the artist sees the seam a mirrored stroke meets itself at rather
///     than a rectangle across the model's box.
/// </summary>
/// <remarks>
///     ⚠ <b>Each triangle's own cut, not a walk of the mesh.</b> A triangle the plane crosses is cut between the two
///     edges whose ends lie on either side of it, which is one segment whatever the triangles around it are, so a mesh
///     split at its seams or a soup of triangles is cut as well as a closed one. A corner exactly on the plane counts
///     as in front of it, so an edge along the plane is drawn once.
/// </remarks>
static class ViewportSlice {
    /// <summary>The line's colour: a cool cyan, which neither a warm mark nor a red quick mask is.</summary>
    static readonly Vector4 Colour = new(0.2f, 0.85f, 1f, 1f);

    /// <summary>The line where a mirror's plane cuts the parts drawn, or nothing for no mirror.</summary>
    /// <param name="mesh">The model as the viewport draws it, in its own space.</param>
    /// <param name="mirror">The mirror, or null.</param>
    /// <param name="shown">The parts drawn, which a hidden part's cut is left out with.</param>
    public static ViewportShapes Shapes(ViewportMesh mesh, ViewportMirror? mirror, IEnumerable<ViewportPart> shown) {
        if (mirror is not { } held) {
            return ViewportShapes.None;
        }

        var cut = Plane(held, mesh.Bounds);

        List<float> lines = [];
        foreach (var part in shown) {
            for (var corner = part.FirstIndex; corner + 2 < part.FirstIndex + part.IndexCount; corner += 3) {
                Cut(mesh, corner, cut, lines);
            }
        }

        return new([], [..lines]);
    }

    /// <summary>
    ///     Where a mirror stands for a model's box: through the box's centre or the origin, moved along its axis by a
    ///     share of the box's half-diagonal, as a stroke's symmetry is (the Worker's <c>StrokeShapes.Symmetry</c>).
    /// </summary>
    /// <param name="mirror">The mirror.</param>
    /// <param name="bounds">The whole model's box.</param>
    public static ViewportPlane Plane(ViewportMirror mirror, BoundingBox bounds) {
        var centre = mirror.Origin ? Vector3.Zero : bounds.Center;
        var radius = Vector3.Distance(bounds.Minimum, bounds.Maximum) * 0.5f;
        return new(mirror.Axis, Along(centre, mirror.Axis) + mirror.Offset * radius);
    }

    static void Cut(ViewportMesh mesh, int corner, ViewportPlane plane, List<float> lines) {
        Span<Vector3> at = [Position(mesh, corner), Position(mesh, corner + 1), Position(mesh, corner + 2)];
        Span<float> side = [
            Along(at[0], plane.Axis) - plane.At,
            Along(at[1], plane.Axis) - plane.At,
            Along(at[2], plane.Axis) - plane.At
        ];
        var found = 0;
        Span<Vector3> ends = stackalloc Vector3[2];
        for (var edge = 0; edge < 3 && found < 2; edge++) {
            var (a, b) = (side[edge], side[(edge + 1) % 3]);
            if (a < 0f == b < 0f) {
                continue;
            }

            ends[found++] = Vector3.Lerp(at[edge], at[(edge + 1) % 3], a / (a - b));
        }

        if (found == 2 && ends[0] != ends[1]) {
            Vertex(lines, ends[0]);
            Vertex(lines, ends[1]);
        }
    }

    static float Along(Vector3 point, int axis) =>
        axis switch {
            1 => point.Y,
            2 => point.Z,
            _ => point.X
        };

    static Vector3 Position(ViewportMesh mesh, int corner) {
        var at = (int)mesh.Indices[corner] * ViewportMesh.FloatsPerVertex;
        return new(mesh.Vertices[at], mesh.Vertices[at + 1], mesh.Vertices[at + 2]);
    }

    static void Vertex(List<float> into, Vector3 position) {
        into.Add(position.X);
        into.Add(position.Y);
        into.Add(position.Z);
        into.Add(Colour.X);
        into.Add(Colour.Y);
        into.Add(Colour.Z);
        into.Add(Colour.W);
    }
}
