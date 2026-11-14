using Vixen.Core.Mathematics;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>
///     A tileable bake (#212): a high-poly patch baked onto a flat low patch so that the maps repeat without a seam.
///     The high meshes are laid again on each of the eight sides of the patch, one patch's width or depth away, so a
///     ray or an occlusion ray near an edge meets what lies beyond it when the material repeats, as it will on the
///     model.
/// </summary>
/// <remarks>
///     ⚠ <b>The patch is the low mesh's box, along its two widest sides.</b> A high patch that spills past it is laid
///     where it spills, which is what the repeat makes of it too. ⚠ <b>Each copy is a file of its own</b> to the bake,
///     so an ID map by material or part numbers the copies apart; a ray from the patch meets a copy only past its edge.
/// </remarks>
public static class BakeTiling {
    /// <summary>The high files, the patch's own first, then a copy moved onto each side of it.</summary>
    /// <param name="files">The high meshes, as read.</param>
    /// <param name="patch">The low mesh's box.</param>
    public static IReadOnlyList<ReadModel> Around(IReadOnlyList<ReadModel> files, BoundingBox patch) {
        ArgumentNullException.ThrowIfNull(files);
        var (across, along) = Sides(patch.Maximum - patch.Minimum);
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
