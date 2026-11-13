using Patina.Ipc;
using Vixen.Core.Mathematics;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>How a session lays a stroke out: where its stamps land, a line's points, the pictures it reads.</summary>
sealed partial class StackHost {
    internal sealed partial class Session {
        /// <summary>Gathers a line's or a path's points, and runs the stroke through them when it ends.</summary>
        void Bend(PaintStroke stroke, PaintPath curve, StackPaintPart part) {
            foreach (var stamp in part.Stamps) {
                curve.Add(new(stamp.X, stamp.Y), stamp.Pressure);
            }

            if (part.Open) {
                return;
            }

            List<Vector2> points = [];
            List<float> pressures = [];
            curve.Sample(points, pressures);
            foreach (var (point, pressure) in points.Zip(pressures)) {
                stroke.MoveTo(point, dirtied, pressure);
            }
        }

        /// <summary>
        ///     Where a stroke's stamps land: a fill's clicks or outline on the mesh, or the stroke's own placement.
        /// </summary>
        IPaintPlacement? PlacementOf(StackPaintPart part, PaintMeshSurface? surface) {
            if (part.Curve != "fill") {
                return StrokeShapes.Placement(part, surface, Bounds(part.Model));
            }

            var mesh = surface ?? throw new IpcException("A fill needs a mesh.");
            // A fill is copied as a brush is: a mirror fills the mirror image of what a click or an outline takes.
            var symmetry = StrokeShapes.Symmetry(part.Symmetry, Bounds(part.Model) ?? mesh.Projection.Bounds);
            PaintFillOutline? outline = part.Outline
                ? new([..part.Stamps.Select(static stamp => new Vector2(stamp.X, stamp.Y))], part.Through)
                : null;
            return mesh.Fill(
                StrokeSources.Fill(part.Fill),
                part.View is { } view ? StrokeShapes.View(view) : null,
                symmetry,
                outline
            );
        }

        /// <summary>
        ///     A picture a stroke projects or is masked by: absolute, or found where a layer's picture would be, the
        ///     library's folders first and then beside the stack.
        /// </summary>
        PaintImage Picture(string name) {
            var file = Path.IsPathRooted(name)
                ? name
                : imageRoots.Select(root => Path.GetFullPath(name, root)).FirstOrDefault(File.Exists)
                ?? Path.GetFullPath(name, folder);
            if (!pictures.TryGetValue(file, out var held)) {
                pictures[file] = held = StrokeSources.Read(file);
            }

            return held;
        }

        /// <summary>
        ///     What a stack written out composites in one channel, as 8-bit texels at this stack's size: a clone's
        ///     picture, or a material's map. Kept by the text, so the channels of one material are evaluated once.
        /// </summary>
        PaintImage? Composite(string text, string usage) {
            if (!composites.TryGetValue(text, out var maps)) {
                // The last few, since a material brush paints each of its channels from one text, one after another.
                if (composites.Count >= 2) {
                    composites.Clear();
                }

                composites[text] = maps = Evaluated(LayerStackYaml.Read(text));
            }

            return maps.GetValueOrDefault(usage);
        }

        Dictionary<string, PaintImage> Evaluated(LayerStackAsset written) {
            Dictionary<string, PaintImage> maps = new(StringComparer.Ordinal);
            if (LayerStackEdit.SetFor(written, set) is not { } chosen) {
                return maps;
            }

            // A draft, as the picture the artist was looking at is one; a refusal reads as nothing to lay.
            using LayerStackEvaluator run = new(evaluator, canvases, path, fill) {
                Draft = true, PositionBox = positionBox
            };
            if (run.Prepare(written, chosen, compounds.Registry, compounds.Source).Count > 0) {
                return maps;
            }

            using var bake = run.Evaluate();
            foreach (var output in run.Outputs) {
                var map = bake.Read(output.Image);
                PaintImage image = new(map.Width, map.Height);
                image.CopyFrom(map.Pixels);
                maps[output.Usage] = image;
            }

            return maps;
        }
    }
}
