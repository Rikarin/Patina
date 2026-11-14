using Patina.Ipc;
using Patina.Viewport;
using Vixen.Core.Mathematics;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>The viewport's questions that look through its camera: where a pick and a marquee look from.</summary>
sealed partial class ViewportHost {
    /// <summary>
    ///     The named meshes that show inside a rectangle on the picture (#110), through what the viewport draws as a
    ///     pick is: a ray through each point of a <see cref="MarqueeGrid" />, so a mesh behind another, or isolated
    ///     away, is not reached.
    /// </summary>
    public IpcMessage Marquee(IpcMessage request) {
        var pick = request.Pick ?? throw new IpcException("viewport.marquee says nothing.");
        if (pick.To is not { } to) {
            throw new IpcException("viewport.marquee has one corner; it needs the opposite one too.");
        }

        var shelf = meshes.Of(pick.Mesh);
        var view = Look(pick, shelf);
        var (through, triangles) = Through(pick.Mesh, shelf);
        var reached = new SortedSet<string>(StringComparer.Ordinal);
        var corner = new Vector2((float)pick.X, (float)pick.Y);
        var opposite = new Vector2((float)to.X, (float)to.Y);
        foreach (var point in MarqueeGrid.Points(corner, opposite, pick.Width, pick.Height)) {
            if (through.TryHit(view.RayAt(point), out var hit)
                && shelf.Geometry.PartOf(Original(triangles, hit.Triangle)) is { Length: > 0 } part) {
                reached.Add(part);
            }
        }

        return new() { Type = IpcTypes.Ok, Marqueed = new([..reached]) };
    }

    /// <summary>The camera a question about the picture looks through, in the mesh's own space.</summary>
    static PaintView Look(ViewportPickAsk pick, MeshShelf.Mesh shelf) {
        var orbit = Orbit(pick.Camera);
        var (right, up, forward) = orbit.Axes();
        return new(
            orbit.Eye(shelf.Geometry.Bounds, (float)pick.Width / Math.Max(pick.Height, 1)),
            right,
            up,
            forward,
            orbit.Lens,
            pick.Width,
            pick.Height
        );
    }

    /// <summary>
    ///     What a ray reaches: the whole model, or under an isolation only what it draws (#136), with the model's own
    ///     number for each triangle.
    /// </summary>
    (PaintProjection Through, int[]? Triangles) Through(string mesh, MeshShelf.Mesh shelf) =>
        isolatedParts.Length > 0 || isolatedMaterials.Length > 0
            ? meshes.Shown(mesh, isolatedParts, isolatedMaterials)
            : (shelf.Projection, null);
}
