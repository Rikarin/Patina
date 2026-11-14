using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Geometry.Remeshing;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>
///     A tileable bake (#212): a high-poly patch baked onto a flat low patch so that the maps repeat without a seam.
///     The high meshes are laid again on each of the eight sides of the patch, one repeat away, so a ray or an
///     occlusion ray near an edge meets what lies beyond it when the material repeats, as it will on the model.
/// </summary>
/// <remarks>
///     ⚠ <b>One repeat is one unit of the patch's texture coordinates</b>, the step the texture takes when it
///     repeats (#287). It is measured from the low mesh's layout, so a patch turned on its plane, or laid out over less
///     than the whole square, tiles along its own sides. A patch whose layout says nothing, every triangle of it flat
///     in the atlas, falls back to its box's two widest sides. ⚠ <b>Each copy is the same mesh to the ID map</b>: its
///     faces take the ids of the faces they copy, by every source, so a strip met past the right edge reads the colour
///     it reads past the left.
/// </remarks>
public static class BakeTiling {
    /// <summary>How many copies of the high meshes a tileable bake holds: the patch's and eight around it.</summary>
    public const int Copies = 9;

    /// <summary>The high files, the patch's own first, then a copy moved onto each side of it.</summary>
    /// <param name="files">The high meshes, as read.</param>
    /// <param name="patch">The low mesh, with its layout.</param>
    public static IReadOnlyList<ReadModel> Around(IReadOnlyList<ReadModel> files, EditMesh patch) {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(patch);
        var (across, along) = Steps(patch) ?? Sides(patch.Bounds.Maximum - patch.Bounds.Minimum);
        List<Vector3> shifts = [];
        for (var row = -1; row <= 1; row++) {
            for (var column = -1; column <= 1; column++) {
                if (row != 0 || column != 0) {
                    shifts.Add(across * column + along * row);
                }
            }
        }

        return [..files, ..shifts.SelectMany(shift => files.Select(file => Moved(file, shift)))];
    }

    /// <summary>
    ///     Gives each copy's faces the ids of the faces it copies, and says what Vixen labels by: the source as asked,
    ///     or the groups when they now hold the shells, which are labels and not colours.
    /// </summary>
    /// <param name="high">The tiled high meshes, flattened: <see cref="Copies" /> runs of the same faces.</param>
    /// <param name="ids">What the ID map labels by, and whether the labels are packed colours.</param>
    /// <returns>What to label by now.</returns>
    /// <remarks>
    ///     ⚠ <b>The groups' own source is kept</b>, since assigning one marks them all as assigned, and an automatic
    ///     source reads that to choose between groups and shells. ⚠ <b>Shells are worked out here</b>: each copy is a
    ///     shell of its own to Vixen, so they are numbered on the whole mesh and each copy takes its original's.
    /// </remarks>
    public static (BakeIdSource Source, bool Colours) SameIds(FlatModel high, (BakeIdSource Source, bool Colours) ids) {
        ArgumentNullException.ThrowIfNull(high);
        var source = ids.Source;
        var mesh = high.Mesh;
        var faces = mesh.FaceCount / Copies;
        var kept = mesh.GroupSource;
        var shells = source is BakeIdSource.Shells
            || (source is BakeIdSource.Automatic && !(kept is MeshGroupSource.Assigned && Several(mesh, faces)));
        List<int> labels = [];
        if (shells) {
            MeshCollision.Shells(mesh, labels);
        }

        for (var face = faces; face < mesh.FaceCount; face++) {
            mesh.SetGroup(face, shells ? labels[face % faces] : mesh.Faces[face % faces].Group);
        }

        for (var face = 0; shells && face < faces; face++) {
            mesh.SetGroup(face, labels[face]);
        }

        mesh.GroupSource = shells ? MeshGroupSource.Assigned : kept;
        return shells ? (BakeIdSource.Groups, false) : ids;
    }

    /// <summary>Whether the first <paramref name="faces" /> carry several groups, which an automatic ID asks.</summary>
    static bool Several(EditMesh mesh, int faces) {
        for (var face = 1; face < faces; face++) {
            if (mesh.Faces[face].Group != mesh.Faces[0].Group) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The world step of one unit of <c>u</c> and of <c>v</c>, each triangle's weighed by its area in the atlas, or
    ///     null when the layout has no area.
    /// </summary>
    static (Vector3 Across, Vector3 Along)? Steps(EditMesh patch) {
        if (patch.TexCoords.Length != patch.CornerCount) {
            return null;
        }

        var across = Vector3.Zero;
        var along = Vector3.Zero;
        var weight = 0f;
        for (var face = 0; face < patch.FaceCount; face++) {
            var loop = patch.Faces[face];
            for (var corner = 1; corner + 1 < loop.Count; corner++) {
                var (u, v, area) = Step(patch, loop.Start, loop.Start + corner, loop.Start + corner + 1);
                across += u * area;
                along += v * area;
                weight += area;
            }
        }

        return weight > 0f && across.LengthSquared() > 0f && along.LengthSquared() > 0f
            ? (across / weight, along / weight)
            : null;
    }

    /// <summary>One triangle's world step per unit of <c>u</c> and of <c>v</c>, and its area in the atlas.</summary>
    static (Vector3 U, Vector3 V, float Area) Step(EditMesh patch, int a, int b, int c) {
        var uv = patch.TexCoords;
        var (du1, du2) = (uv[b] - uv[a], uv[c] - uv[a]);
        var determinant = du1.X * du2.Y - du2.X * du1.Y;
        if (MathF.Abs(determinant) <= 1e-12f) {
            return (Vector3.Zero, Vector3.Zero, 0f);
        }

        var corners = patch.Corners;
        var origin = patch.Positions[corners[a]];
        var (edge1, edge2) = (patch.Positions[corners[b]] - origin, patch.Positions[corners[c]] - origin);
        return (
            (edge1 * du2.Y - edge2 * du1.Y) / determinant,
            (edge2 * du1.X - edge1 * du2.X) / determinant,
            MathF.Abs(determinant) / 2f
        );
    }

    /// <summary>The patch's two widest sides, each as the step from one repeat to the next.</summary>
    static (Vector3 Across, Vector3 Along) Sides(Vector3 size) {
        Vector3[] steps = [new(size.X, 0, 0), new(0, size.Y, 0), new(0, 0, size.Z)];
        var widest = steps.OrderByDescending(static step => step.Length()).ToArray();
        return (widest[0], widest[1]);
    }

    /// <summary>A file with every root moved, so all it holds moves with it.</summary>
    static ReadModel Moved(ReadModel file, Vector3 shift) =>
        file with {
            Model = file.Model with {
                Nodes = [
                    ..file.Model.Nodes.Select(node => node.Parent < 0
                            ? node with { Transform = node.Transform * Matrix4x4.FromTranslation(shift) }
                            : node
                    )
                ]
            }
        };
}
