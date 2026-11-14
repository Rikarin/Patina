using Patina.Export;
using Patina.Ipc;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;

namespace Patina.Worker;

/// <summary>A set measured before export, which only the Worker can do: it alone reads the mesh and the maps.</summary>
sealed partial class StackHost {
    /// <summary>
    ///     Measures a set before export (#159): its layout from the mesh, and from its maps as an export would write
    ///     them,
    ///     the share of covered texels no physical material has. The maps are read here and only numbers go back.
    /// </summary>
    public IpcMessage Check(IpcMessage request) {
        var session = request.StackCheck ?? throw new IpcException("stack.check names no stack.");
        var stack = Find(session);

        // As an export would write them, whatever the proxy (#208).
        var run = stack.Whole();

        var (width, height) = stack.Size;
        var model = stack.ModelFile;
        LayoutCheck.Result? layout = null;
        MeshShelf.Mesh? mesh = null;
        BoundingBox whole = default;
        if (model is not null) {
            mesh = Meshes.Of(model, stack.SetName, stack.UvSet);
            whole = Meshes.Of(model).Geometry.Bounds;
            var geometry = mesh.Geometry;
            layout = LayoutCheck.Measure(
                geometry.Positions,
                geometry.Coordinates,
                geometry.Indices,
                whole,
                Math.Max(width, height)
            );
        }

        using var bake = run.Evaluate();
        var outputs = run.Outputs;
        var wanted = outputs.Where(static output => output.Usage is "baseColor" or "metalness").ToArray();
        var read = bake.Read([..wanted.Select(static output => output.Image)]);

        Bitmap? Map(string usage) {
            var at = Array.FindIndex(wanted, output => output.Usage == usage);
            return at < 0 ? null : read[at];
        }

        var maps = MapCheck.Measure(Map("baseColor"), Map("metalness"), layout?.Covered, layout?.Side ?? 0);
        if (mesh is not null) {
            var geometry = mesh.Geometry;
            maps = [
                ..maps.Select(measure => measure with {
                        Spot = LayoutCheck.Located(
                            measure.Spot,
                            geometry.Positions,
                            geometry.Coordinates,
                            geometry.Indices,
                            whole
                        )
                    }
                )
            ];
        }

        return new() { Type = IpcTypes.Ok, StackMeasured = new(width, height, layout?.Measures, maps) };
    }
}
