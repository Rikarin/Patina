using Patina.Contracts;
using System.Globalization;
using Vixen.Authoring;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Server.Stacks;

/// <summary>Strokes: what a stroke paints, and what is refused before it begins.</summary>
public sealed partial class StackEditor {
    void Stroke(StackStroke stroke, UndoStep step, string set, Action<IAuthoringCommand> run) {
        if (stroke.Brush.Problem() is { } problem) {
            throw new StackEditRefusedException(problem, Api.Refusals.BrushRefused, Api.Refusals.Reason(problem));
        }

        // A quick mask is the session's and no layer's, so a stroke on it names any layer, or one since removed.
        var quick = stroke.QuickMask == StackQuickMasks.Paint;
        var layer = quick ? null : Require(stroke.Layer);
        if (layer is not null && !stroke.Mask) {
            Writable(stroke, layer);
        }

        Aimed(stroke);

        var (paint, start) = layer is null ? (QuickMask.Paint, null) : Canvas(stroke, layer, set, run);

        // ⚠ Live, two strokes of one batch take turns; replayed, they run one after the other. Where they meet in one
        // channel that is two pictures, and a journal has to be one.
        if (step.Strokes.Exists(other => other.Paint == paint && other.Usage == Channel(stroke))) {
            throw new StackEditRefusedException(
                "A batch paints each channel of a layer once.",
                Api.Refusals.RepeatedChannel
            );
        }

        // A Worker refuses to begin one its canvas cannot take at this size, and the stack would fail until it is
        // undone.
        var (width, height) = layer is null
            ? (Document.BaseWidth, Document.BaseHeight)
            : canvasSides.Check(layer.Name, paint, Document.BaseWidth, Document.BaseHeight);
        var depth = canvasSides.Depth(layer, paint, Channel(stroke), Set);
        // A larger canvas is painted at its own size (#19): a stroke in the 2D view is carried onto its texels, and a
        // brush sized in texels keeps its size on the model.
        var (painted, by) = StackCanvasSides.Carried(stroke, width, Document.BaseWidth);

        // A quick mask nothing has painted keeps nothing back, rather than everything.
        var kept = stroke.QuickMask is StackQuickMasks.Inside or StackQuickMasks.Outside
            && QuickMask.Painted(Standing.Concat(step.Strokes));
        step.Strokes.Add(
            new(nextEntry++, painted, paint, Channel(stroke), Packed(stroke.Colour), Document.Model, start) {
                SourceDocument = StackDocuments.SourceText(stroke.Source, Document, Set, Layered(stroke.Source)),
                Width = width,
                Height = height,
                Format = depth,
                Lanes = depth is null ? null : [..stroke.Colour],
                StampScale = by,
                // Only a stroke on the mesh has faces to keep to; one in the 2D view reaches the whole layout.
                Isolation = stroke.View is null ? null : Isolation,
                Keep = kept ? QuickMask.Paint : null,
                KeepOutside = kept && stroke.QuickMask == StackQuickMasks.Outside
            }
        );
    }

    /// <summary>The canvas a stroke on a layer paints, and what it starts as if the stroke makes it.</summary>
    (string Paint, uint? Start) Canvas(
        StackStroke stroke,
        LayerAsset layer,
        string set,
        Action<IAuthoringCommand> run
    ) =>
        stroke switch {
            { Mask: true, MaskLayer: { } entry } => (StackLayerEdits.MaskLayerCanvas(this, layer, entry, set, run),
                null),
            { Mask: true } => MaskCanvas(layer, set, run),
            _ => (LayerCanvas(layer, set, run), null)
        };

    /// <summary>Refuses a stroke on a layer's own pixels that would land nowhere, or nowhere that shows.</summary>
    void Writable(StackStroke stroke, LayerAsset layer) {
        // A mask is any layer's, and only a paint layer has pixels of its own besides — or a flattened group, whose
        // canvas is its picture until it is unflattened.
        if (layer.Kind != LayerKind.Paint && !layer.Flattened) {
            var kind = layer.Kind.ToString().ToLowerInvariant();
            throw new StackEditRefusedException(
                $"Only a paint layer can be painted, and '{layer.Name}' is a {kind} layer.",
                Api.Refusals.NotPaintLayer,
                new Dictionary<string, string> { ["layer"] = layer.Name, ["kind"] = layer.Kind.ToString() }
            );
        }

        var channel = StackDocuments.ChannelName(stroke.Channel);
        // A layer keeps what it held for a channel taken off the set (#141), so it may still write it (#261).
        if (Set.Channels.TrueForAll(held => held.Usage != stroke.Channel)) {
            throw new StackEditRefusedException(
                $"This document has no {channel} channel.",
                Api.Refusals.ChannelMissing,
                new Dictionary<string, string> { ["channel"] = channel }
            );
        }

        if (!layer.Writes(stroke.Channel)) {
            throw new StackEditRefusedException(
                $"'{layer.Name}' does not write {channel}, so a stroke there would never show."
            );
        }
    }

    /// <summary>
    ///     The canvas of a paint layer. The first stroke names it, and undoing that takes the name back. ⚠ Never a file
    ///     that is there already: a layer removed and saved left its pixels under the id a new layer is given.
    /// </summary>
    string LayerCanvas(LayerAsset layer, string set, Action<IAuthoringCommand> run) {
        var paint = layer.Paint.Trim();
        if (paint.Length == 0) {
            paint = FreeCanvas(
                Path.GetFileNameWithoutExtension(AssetPath),
                set,
                layer.Id,
                false,
                new HashSet<string>()
            );
            run(Named(layer, layer with { Paint = paint }, set));
        }

        return paint;
    }

    /// <summary>
    ///     The canvas of a mask, and what it starts as if this is the stroke that makes one. A mask that is a picture
    ///     stays one, live, and the brush paints a paint layer of the mask's own stack over it (#72, #109).
    /// </summary>
    (string Paint, uint? Start) MaskCanvas(LayerAsset layer, string set, Action<IAuthoringCommand> run) {
        if (StackLayerEdits.CanvasOverMask(this, layer, set, run) is { } over) {
            return (over, null);
        }

        var paint = layer.Mask.Paint.Trim();
        if (layer.Mask.Source == LayerMaskSource.Paint && paint.Length > 0) {
            return (paint, null);
        }

        var showed = Showed(layer);
        paint = FreeCanvas(Path.GetFileNameWithoutExtension(AssetPath), set, layer.Id, true, new HashSet<string>());
        var painted = layer.Mask with { Source = LayerMaskSource.Paint, Paint = paint };
        run(Named(layer, layer with { Mask = StackDocuments.MeshMasked(painted, null) }, set));
        return (paint, PaintImage.Pack(showed, showed, showed, 1f));
    }

    /// <summary>
    ///     The one number a flat mask stood at, which is what a canvas taking its place starts as, so that a stroke
    ///     changes only what it touches. A paint mask nobody has painted reads nothing, and so hides nothing.
    /// </summary>
    static float Showed(LayerAsset layer) => layer.Mask.Source == LayerMaskSource.Constant ? layer.Mask.Value : 1f;

    /// <summary>
    ///     A file for a new canvas. ⚠ Never one anything can reach: a removed layer's strokes can still be undone back
    ///     onto its canvas, a file beside the stack holds what a save kept, and the document may name it already.
    /// </summary>
    /// <param name="stem">The stack's file name without its extension.</param>
    /// <param name="set">The texture set.</param>
    /// <param name="layer">The layer it is for.</param>
    /// <param name="mask">Whether it is the layer's mask.</param>
    /// <param name="alsoTaken">Names given out in this batch, which nothing holds yet.</param>
    internal string FreeCanvas(string stem, string set, string layer, bool mask, IReadOnlySet<string> alsoTaken) {
        var folder = Path.GetDirectoryName(AssetPath)!;
        HashSet<string> taken = [
            ..Standing.Concat(Redoable).Select(static stroke => stroke.Paint),
            ..Canvases(Set.Layers),
            ..alsoTaken
        ];
        for (var count = 1;; count++) {
            var id = count == 1 ? layer : string.Create(CultureInfo.InvariantCulture, $"{layer}-{count}");
            var name = LayerPaint.NameFor(stem, set, id, mask);
            if (!taken.Contains(name) && !File.Exists(Path.Combine(folder, name))) {
                return name;
            }
        }
    }

    /// <summary>Every canvas some layers name: their own, their mask's and those of their mask's entries.</summary>
    static IEnumerable<string> Canvases(IEnumerable<LayerAsset> layers) =>
        layers.SelectMany(static layer => Canvases(layer.Children)
            .Concat(
                [
                    layer.Paint.Trim(), layer.Mask.Paint.Trim(),
                    ..layer.Mask.Layers.Select(static entry => entry.Paint.Trim())
                ]
            )
            .Where(static paint => paint.Length > 0)
        );

    /// <summary>Refuses what a Worker could not do with a stroke, while nothing has happened yet.</summary>
    void Aimed(StackStroke stroke) {
        if (stroke.Curve is not ("free" or "line" or "path" or "fill")) {
            throw new StackEditRefusedException($"'{stroke.Curve}' is not a kind of stroke.");
        }

        if ((stroke.Symmetry?.Problem() ?? stroke.PaintProblem()) is { } problem) {
            throw new StackEditRefusedException(problem, Api.Refusals.BrushRefused, Api.Refusals.Reason(problem));
        }

        // A symmetry, a view, a size in the model's units, a polygon fill and a material laid in the model's space are
        // all on the mesh.
        var onMesh = stroke.View is not null
            || stroke.Symmetry is not null
            || stroke.Brush.SizeSpace == "model"
            || stroke.Curve == "fill"
            || stroke.Source is { Kind: "material", Projection: not (null or "Uv") };
        if (onMesh && Document.Model.Trim().Length == 0) {
            throw new StackEditRefusedException("This document has no mesh to paint on.", Api.Refusals.NoMesh);
        }

        if (stroke.View is not { } view) {
            return;
        }

        if (view is not { Eye.Count: 3, Right.Count: 3, Up.Count: 3, Forward.Count: 3, Width: > 0, Height: > 0 }) {
            throw new StackEditRefusedException("A view is three vectors of three numbers and a picture with a size.");
        }
    }
}
