// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;

namespace Patina.Viewport;

/// <summary>
///     A symmetry as a stroke takes it: the axis a mirror's plane faces along or a radial symmetry turns round, and
///     where it stands against the whole model's box, which the viewport finds on the mesh it draws.
/// </summary>
/// <param name="Axis">0 for x, 1 for y, 2 for z.</param>
/// <param name="Origin">Whether it passes through the origin rather than the box's centre.</param>
/// <param name="Offset">How far it moves along its axis, as a share of the box's half-diagonal.</param>
/// <param name="Count">How many copies a radial symmetry makes, 2 or more, or 0 for a mirror.</param>
public readonly record struct ViewportMirror(int Axis, bool Origin, float Offset, int Count = 0);

/// <summary>A mirror's plane in the model's space: the axis it faces along, and where it crosses that axis.</summary>
/// <param name="Axis">0 for x, 1 for y, 2 for z.</param>
/// <param name="At">Where on that axis the plane stands, in the model's units.</param>
readonly record struct ViewportPlane(int Axis, float At);

/// <summary>
///     Where a symmetry meets the model (#132): the line a mirror's plane cuts across the surface, or the lines the
///     half-planes between a radial symmetry's copies cut, drawn on the model and hidden where the model hides it, so
///     the artist sees the seams a symmetric stroke meets its copies at rather than a shape across the model's box.
/// </summary>
/// <remarks>
///     <para>
///         ⚠ <b>Each triangle's own cut, not a walk of the mesh.</b> A triangle a plane crosses is cut between the two
///         edges whose ends lie on either side of it, which is one segment whatever the triangles around it are, so a
///         mesh split at its seams or a soup of triangles is cut as well as a closed one. A corner exactly on the plane
///         counts as in front of it, so an edge along the plane is drawn once.
///     </para>
///     <para>
///         A radial symmetry of n copies is cut by n half-planes from its axis, a turn of 1/n apart, the first towards
///         the next axis round (y, z or x for x, y or z): each is a plane's cut kept to the side of the axis it leaves
///         on. Which of the turns comes first does not matter, since the copies repeat every turn of 1/n.
///     </para>
/// </remarks>
static class ViewportSlice {
    /// <summary>The line's colour: a cool cyan, which neither a warm mark nor a red quick mask is.</summary>
    static readonly Vector4 Colour = new(0.2f, 0.85f, 1f, 1f);

    /// <summary>The lines where a symmetry cuts the parts drawn, or nothing for none.</summary>
    /// <param name="mesh">The model as the viewport draws it, in its own space.</param>
    /// <param name="mirror">The symmetry, or null.</param>
    /// <param name="shown">The parts drawn, which a hidden part's cut is left out with.</param>
    public static ViewportShapes Shapes(ViewportMesh mesh, ViewportMirror? mirror, IEnumerable<ViewportPart> shown) {
        if (mirror is not { } held) {
            return ViewportShapes.None;
        }

        var cuts = Cuts(held, mesh.Bounds);
        List<float> lines = [];
        foreach (var part in shown) {
            for (var corner = part.FirstIndex; corner + 2 < part.FirstIndex + part.IndexCount; corner += 3) {
                foreach (var cut in cuts) {
                    Cut(mesh, corner, cut, lines);
                }
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
    public static ViewportPlane Plane(ViewportMirror mirror, BoundingBox bounds) =>
        new(mirror.Axis, Along(Through(mirror, bounds), mirror.Axis));

    /// <summary>The point the symmetry's plane or axis passes through, as a stroke's does.</summary>
    static Vector3 Through(ViewportMirror mirror, BoundingBox bounds) {
        var centre = mirror.Origin ? Vector3.Zero : bounds.Center;
        var radius = Vector3.Distance(bounds.Minimum, bounds.Maximum) * 0.5f;
        return centre + Unit(mirror.Axis) * (mirror.Offset * radius);
    }

    /// <summary>
    ///     The planes a symmetry cuts with: a mirror's one, whole, or a radial symmetry's half-planes, each a plane
    ///     through the axis and the side of the axis it is kept to.
    /// </summary>
    static Section[] Cuts(ViewportMirror mirror, BoundingBox bounds) {
        var through = Through(mirror, bounds);
        if (mirror.Count < 2) {
            return [new(Unit(mirror.Axis), through, null)];
        }

        var (u, v) = (Unit((mirror.Axis + 1) % 3), Unit((mirror.Axis + 2) % 3));
        var cuts = new Section[mirror.Count];
        for (var at = 0; at < mirror.Count; at++) {
            var (sin, cos) = MathF.SinCos(MathF.Tau * at / mirror.Count);
            var leaving = u * cos + v * sin;
            cuts[at] = new(v * cos - u * sin, through, leaving);
        }

        return cuts;
    }

    static void Cut(ViewportMesh mesh, int corner, Section cut, List<float> lines) {
        Span<Vector3> at = [Position(mesh, corner), Position(mesh, corner + 1), Position(mesh, corner + 2)];
        Span<float> side = [cut.Side(at[0]), cut.Side(at[1]), cut.Side(at[2])];
        var found = 0;
        Span<Vector3> ends = stackalloc Vector3[2];
        for (var edge = 0; edge < 3 && found < 2; edge++) {
            var (a, b) = (side[edge], side[(edge + 1) % 3]);
            if (a < 0f == b < 0f) {
                continue;
            }

            ends[found++] = Vector3.Lerp(at[edge], at[(edge + 1) % 3], a / (a - b));
        }

        if (found == 2 && cut.Kept(ref ends[0], ref ends[1]) && ends[0] != ends[1]) {
            Vertex(lines, ends[0]);
            Vertex(lines, ends[1]);
        }
    }

    static Vector3 Unit(int axis) =>
        axis switch {
            1 => Vector3.UnitY,
            2 => Vector3.UnitZ,
            _ => Vector3.UnitX
        };

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

    /// <summary>A plane a symmetry cuts with, and the side of its axis it is kept to, or null for all of it.</summary>
    /// <param name="Normal">The plane's normal.</param>
    /// <param name="Through">A point on the plane, and on a radial symmetry's axis.</param>
    /// <param name="Leaving">The way a half-plane leaves the axis, or null for a whole plane.</param>
    readonly record struct Section(Vector3 Normal, Vector3 Through, Vector3? Leaving) {
        /// <summary>How far in front of the plane a point is.</summary>
        public float Side(Vector3 point) => Vector3.Dot(Normal, point - Through);

        /// <summary>A segment on the plane cut to the half kept, or false for none of it there.</summary>
        public bool Kept(ref Vector3 from, ref Vector3 to) {
            if (Leaving is not { } way) {
                return true;
            }

            var (a, b) = (Vector3.Dot(way, from - Through), Vector3.Dot(way, to - Through));
            if (a < 0f && b < 0f) {
                return false;
            }

            if (a < 0f) {
                from = Vector3.Lerp(from, to, a / (a - b));
            } else if (b < 0f) {
                to = Vector3.Lerp(from, to, a / (a - b));
            }

            return true;
        }
    }
}
