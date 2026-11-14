using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Vixen.Authoring;
using Vixen.Core.Mathematics;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>A stroke as the journal begins and grows it: its canvas, its surface on the mesh and its stamps.</summary>
sealed partial class StackHost {
    internal sealed partial class Session {
        Applied Begin(StackPaintPart part) {
            var file = LibraryLayout.Beside(folder, part.Paint);
            if (part.Capture is { } capture) {
                touched.Add(part.Paint);
                return known[part.Id] = new(part.Id, null, null) { Copy = new CanvasCapture(this, file, capture) };
            }

            if (part.From is { } from) {
                touched.Add(part.Paint);
                // At the size it was first made at, which a replay after the set's size changed keeps (#106).
                var made = part.Width > 0 && part.Height > 0
                    ? (part.Width, part.Height)
                    : (document.BaseWidth, document.BaseHeight);
                Made(file, made);
                CanvasCopy copy = new(canvases, file, Path.GetFullPath(from, folder), made.Item1, made.Item2) {
                    Carry = Layouts(part.Model, part.Carry)
                };
                return known[part.Id] = new(part.Id, null, null) { Copy = copy };
            }

            // The stamps are texels of the set as it was when the stroke began, which a replay keeps (#19).
            var side = part.Width > 0 && part.Height > 0
                ? (part.Width, part.Height)
                : (document.BaseWidth, document.BaseHeight);
            var canvas = Made(file, side);
            if (canvas is null) {
                canvas = new(side.Item1, side.Item2);
                canvases.Adopt(file, canvas);
            } else if ((canvas.Width, canvas.Height) != side) {
                canvas = Resized(file, part.Paint, canvas, side);
            }

            canvas = Widened(file, canvas, part);

            // Before anything reads it. A file a stroke starts is new, or left by strokes that can never stand again.
            if (part.Start is { } start) {
                canvas.Channel(part.Usage).Fill(start);
            }

            touched.Add(part.Paint);
            stroked.Add(file);
            var surface = Surface(part.Model, canvas.Width, canvas.Height, part.Isolation);
            var image = canvas.Channel(part.Usage);
            var coverage = surface?.Coverage ?? PaintCoverage.Everywhere(canvas.Width, canvas.Height);
            var placement = PlacementOf(part, surface);
            var stroke = new Applied(
                part.Id,
                new(
                    image,
                    coverage,
                    Brush(part.Brush) with { Radius = StrokeShapes.Radius(part, surface, Bounds(part.Model)) },
                    part.Colour,
                    surface is null ? 0 : Gutter,
                    // A line or a path runs through its points, and a brush trailing them would stop short of the last.
                    part.Curve == "free" ? part.Brush.Lazy : 0f,
                    placement: placement,
                    source: StrokeSources.Source(part, image, coverage, Picture, Composite),
                    stencil: StrokeSources.Stencil(part.Stencil, Picture),
                    resume: StrokeShapes.Resume(part),
                    keep: Keep(part, canvas.Width, canvas.Height)
                ) { Colour = Lanes(part) },
                part.Curve is "free" or "fill" ? null : new()
            ) { Fill = part.Curve == "fill", Outline = part.Outline };
            known[part.Id] = stroke;
            return stroke;
        }

        /// <summary>
        ///     This set's triangles laid out in two of the mesh's UV sets, which a copy is carried between (#210), or
        ///     null for a copy that is not carried.
        /// </summary>
        /// <exception cref="IpcException">The mesh will not read, or a part lacks one of the UV sets.</exception>
        (PaintProjection From, PaintProjection To)? Layouts(string model, StackCarry? carry) {
            if (carry is null) {
                return null;
            }

            var file = Path.GetFullPath(model, folder);
            return (meshes.Of(file, set, carry.FromUvSet).Projection, meshes.Of(file, set, carry.ToUvSet).Projection);
        }

        /// <summary>
        ///     The whole model's box, every texture set of it, or null with no model. ⚠ Not the set's own: a symmetry's
        ///     centre and a size in the model's units are the model's, and one set of a character is half of it.
        /// </summary>
        BoundingBox? Bounds(string model) =>
            model.Trim().Length == 0 ? null : meshes.Of(Path.GetFullPath(model, folder)).Projection.Bounds;

        /// <summary>
        ///     The mesh as a stroke reaches it, at the size of its canvas, in the set's layout: only the faces the
        ///     viewport drew when it began, with an isolation (#136). ⚠ Built again when the shelf reads the file
        ///     again: a mesh reloaded on the same layout keeps its documents open (#89), and its triangles may have
        ///     moved under the same coordinates.
        /// </summary>
        PaintMeshSurface? Surface(string model, int width, int height, ViewportIsolation? isolation) {
            if (model.Trim().Length == 0) {
                return null;
            }

            var file = Path.GetFullPath(model, folder);
            var (parts, materials) = (isolation?.Parts ?? [], isolation?.Materials ?? []);
            var shown = string.Join('\n', [..parts, string.Empty, ..materials]);
            var key = $"{file}\n{UvSet}\n{width}x{height}\n{shown}";
            var over = meshes.Of(file, set, UvSet, [..parts], [..materials]).Projection;
            if (!surfaces.TryGetValue(key, out var held) || !ReferenceEquals(held.Over, over)) {
                surfaces[key] = held = (over, new(over, width, height));
            }

            return held.Surface;
        }

        /// <summary>
        ///     A canvas made the size a stroke paints at (#19). Larger is exact and loses nothing, and is done only to
        ///     a canvas no stroke of this session has begun on, since a stroke's undo holds the pixels it painted over.
        ///     A canvas larger than its set keeps its size and is sampled at the set's, so it is never made smaller;
        ///     one of another shape arrives with the size of the set's shape that holds it, which only enlarges it.
        /// </summary>
        /// <exception cref="IpcException">The canvas cannot be painted at that size here.</exception>
        PaintCanvas Resized(string file, string name, PaintCanvas canvas, (int Width, int Height) side) {
            var (width, height) = side;
            if (canvas.Width > width || canvas.Height > height) {
                throw new IpcException(
                    $"'{name}' holds {canvas.Width}×{canvas.Height} texels and the texture set is {width}×{height}. "
                    + "Painting it smaller would throw its detail away, so set the size back to paint this layer."
                );
            }

            if (stroked.Contains(file)) {
                throw new IpcException(
                    $"'{name}' was painted at {canvas.Width}×{canvas.Height} earlier in this session. Save, close and "
                    + $"open the document again to paint it at {width}×{height}."
                );
            }

            var enlarged = canvas.Enlarged(width, height);
            canvases.Adopt(file, enlarged);
            // The plan holds the smaller one's texture, which the larger one's tiles do not fit.
            stale = true;
            return enlarged;
        }

        /// <summary>
        ///     A canvas whose channel the stroke paints at its width (#19): a channel narrower is widened, exactly,
        ///     into a new canvas sharing the other channels, as <see cref="Resized" /> enlarges one, and a new channel
        ///     is made at it. One wider keeps its width, so no paint is rounded. The Server asks to widen only a canvas
        ///     no stroke of this session began on, since a stroke's undo holds the image it painted over.
        /// </summary>
        /// <exception cref="IpcException">A canvas a stroke of this session began on was asked to widen.</exception>
        PaintCanvas Widened(string file, PaintCanvas canvas, StackPaintPart part) {
            if (!Enum.TryParse<PaintTexelFormat>(part.Format, out var asked) || asked == PaintTexelFormat.Rgba8) {
                return canvas;
            }

            if (!canvas.Has(part.Usage)) {
                canvas.Channel(part.Usage, asked);
                return canvas;
            }

            if (PaintTexelFormats.Holds(canvas.Channel(part.Usage).Format, asked)) {
                return canvas;
            }

            if (stroked.Contains(file)) {
                throw new IpcException(
                    $"'{part.Paint}' was painted earlier in this session at another depth. Save, close and open the "
                    + "document again to paint it at this one."
                );
            }

            var widened = canvas.Formatted(part.Usage, asked);
            canvases.Adopt(file, widened);
            // The plan holds the narrower channel's texture, which the wider one's tiles do not fit.
            stale = true;
            return widened;
        }

        /// <summary>The colour a stroke paints, in full when the Server sent it so, else as packed.</summary>
        static Vector4 Lanes(StackPaintPart part) =>
            part.Lanes is [var red, var green, var blue, var alpha]
                ? new(red, green, blue, alpha)
                : PaintImage.Unpack(part.Colour);

        /// <summary>
        ///     The canvas a journal entry paints or makes, as it was when the entry was first done. A blank this
        ///     Worker made at load for a canvas the journal makes is made again at the entry's size: it was made at
        ///     the set's size today, and a replay after the size changed would otherwise paint, copy or flatten a
        ///     canvas of another size than the first time (#106, #241). A blank holds nothing, so nothing is lost.
        /// </summary>
        /// <param name="file">The canvas.</param>
        /// <param name="side">The size the entry made it at.</param>
        /// <returns>The canvas, or null when there is none yet.</returns>
        PaintCanvas? Made(string file, (int Width, int Height) side) {
            var canvas = canvases.Open(file);
            if (canvas is null || !blanks.Remove(file) || (canvas.Width, canvas.Height) == side) {
                return canvas;
            }

            canvas = new(side.Width, side.Height);
            canvases.Adopt(file, canvas);
            stale = true;
            return canvas;
        }

        PaintBrush Brush(StackBrush brush) =>
            StrokeShapes.Brush(brush, StrokeShapes.Alpha(brush.Alpha, folder, alphas));

        /// <summary>
        ///     Makes a copy the journal holds. ⚠ As its turn comes and not as it begins: a replay begins every stroke
        ///     before it stamps any, and a copy made then would be of a canvas the strokes before it have not reached
        ///     yet.
        /// </summary>
        void Copy(Applied copy, IAuthoringCommand command) {
            if (copy.Done) {
                return;
            }

            command.Do(Context);
            copy.Command = command;
            copy.Done = true;
            unknown = true;
        }

        void Extend(Applied stroke, StackPaintPart part) {
            if (stroke.Copy is { } copy) {
                Copy(stroke, copy);
                return;
            }

            if (stroke.Stroke is null) {
                return;
            }

            // A canvas painted at its own larger size (#19) is dirtied in its own texels, which narrow no map's tiles.
            if (part.Width > 0 && (part.Width, part.Height) != (document.BaseWidth, document.BaseHeight)) {
                unknown = true;
            }

            if (stroke.Curve is { } curve) {
                Bend(stroke.Stroke, curve, part);
            } else if (stroke is { Fill: true, Outline: true }) {
                // One stamp for the whole outline, which the placement holds: where it lands does not matter.
                if (part.Stamps is [var first, ..]) {
                    stroke.Stroke.Dab(new(first.X, first.Y), dirtied, StrokeShapes.Pen(first));
                }
            } else if (stroke.Fill) {
                // Each click is a place of its own, and the stroke between two would fill whatever it passed over.
                foreach (var stamp in part.Stamps) {
                    stroke.Stroke.Dab(new(stamp.X, stamp.Y), dirtied, StrokeShapes.Pen(stamp));
                }
            } else {
                foreach (var stamp in part.Stamps) {
                    stroke.Stroke.MoveTo(new(stamp.X, stamp.Y), dirtied, StrokeShapes.Pen(stamp));
                }
            }

            if (!part.Open) {
                Close(stroke);
            }
        }

        static IAuthoringCommand Close(Applied stroke) {
            if (!stroke.Done) {
                stroke.Done = true;
                stroke.Command = stroke.Copy
                    ?? (stroke.Stroke is not { IsEmpty: false } painted
                        ? new Nothing()
                        : new PaintStrokeCommand([painted], "Stroke"));
            }

            return stroke.Command!;
        }
    }
}
