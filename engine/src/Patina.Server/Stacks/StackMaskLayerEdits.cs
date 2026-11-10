using Patina.Contracts;
using System.Globalization;
using Vixen.Authoring;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     The edits of a layer's mask stack (#109), each one change of the layer so that undo puts the whole mask back,
///     and the canvas a stroke on a paint entry names.
/// </summary>
/// <param name="editor">The stack they edit.</param>
sealed class StackMaskLayerEdits(StackEditor editor) {
    /// <summary>
    ///     The canvas of a paint entry of a mask's stack, named by its first stroke. It starts transparent, so that
    ///     what is beneath it shows until it is painted.
    /// </summary>
    /// <exception cref="StackEditRefusedException">The entry is not a paint entry.</exception>
    public string MaskLayerCanvas(LayerAsset layer, int index, string set, Action<IAuthoringCommand> run) {
        var entry = StackMaskLayers.At(layer, index);
        if (entry.Source != LayerMaskSource.Paint) {
            throw new StackEditRefusedException(
                $"Mask layer {index} of '{layer.Name}' is a {entry.Source} layer, "
                + "and only a paint layer takes a brush.",
                Api.Refusals.MaskLayerNotPaint,
                new Dictionary<string, string> { ["layer"] = layer.Name, ["source"] = entry.Source.ToString() }
            );
        }

        var paint = entry.Paint.Trim();
        if (paint.Length > 0) {
            return paint;
        }

        paint = editor.FreeCanvas(
            Path.GetFileNameWithoutExtension(editor.AssetPath),
            set,
            string.Create(CultureInfo.InvariantCulture, $"{layer.Id}-{index + 1}"),
            true,
            new HashSet<string>()
        );
        var named = StackMaskLayers.Changed(layer, index, held => held with { Paint = paint });
        run(editor.Named(layer, layer with { Mask = named }, set));
        return paint;
    }

    /// <summary>
    ///     An edit of a layer's mask stack, as one change of the layer, so that undo puts the whole mask back.
    /// </summary>
    public SetLayerCommand MaskLayers(StackCommand command, string set) {
        var (id, mask, name) = command switch {
            StackAddMaskLayer add => (add.Layer,
                (Func<LayerAsset, MaskAsset>)(layer => StackMaskLayers.Added(layer, NewMaskLayer(add), add.Index)),
                "Add mask layer"),
            StackRemoveMaskLayer remove => (remove.Layer, layer => StackMaskLayers.Removed(layer, remove.Index),
                "Remove mask layer"),
            StackMoveMaskLayer move => (move.Layer, layer => StackMaskLayers.Moved(layer, move.Index, move.Delta),
                "Move mask layer"),
            StackSetMaskLayer change => (change.Layer,
                layer => StackMaskLayers.Changed(layer, change.Index, held => ChangedMaskLayer(held, change)),
                "Change mask layer"),
            _ => throw new StackEditRefusedException($"Unknown command '{command.GetType().Name}'.")
        };
        var layer = editor.Require(id);
        return new(editor, new(set, id), layer, layer with { Mask = mask(layer) }, name);
    }

    static MaskLayerAsset NewMaskLayer(StackAddMaskLayer add) {
        var source = MaskLayerSource(add.Source);
        return new() {
            Source = source,
            Generator = source == LayerMaskSource.Generator ? add.Generator ?? FirstGenerator() : string.Empty
        };
    }

    /// <summary>The generator a new generator entry starts as, which is the first the vocabulary offers.</summary>
    static string FirstGenerator() =>
        StackDocuments.Vocabulary.Generators is [var first, ..] ? first.Value : string.Empty;

    static LayerMaskSource MaskLayerSource(string name) {
        var source = StackEditor.Parsed(name, LayerMaskSource.None, "mask source");
        return source == LayerMaskSource.None
            ? throw new StackEditRefusedException("A mask layer has a source; remove it to have none.")
            : source;
    }

    static MaskLayerAsset ChangedMaskLayer(MaskLayerAsset held, StackSetMaskLayer change) {
        if (change.Opacity is < 0f or > 1f || change.Value is < 0f or > 1f) {
            throw new StackEditRefusedException("A mask layer's value and opacity are from 0 to 1.");
        }

        var generator = change.Generator ?? held.Generator;
        var sameGenerator = generator == held.Generator;
        return held with {
            Source = change.Source is { } source ? MaskLayerSource(source) : held.Source,
            Value = change.Value ?? held.Value,
            Asset = change.Asset ?? held.Asset,
            Anchor = change.Anchor is { } anchor ? StackDocuments.Reference(anchor) : held.Anchor,
            Generator = generator,
            GeneratorTexts = StackEditor.Merged(sameGenerator ? held.GeneratorTexts : [], change.Parameters),
            GeneratorValues = sameGenerator ? held.GeneratorValues : [],
            GeneratorInputs = StackDocuments.References(change.Inputs) ?? (sameGenerator ? held.GeneratorInputs : []),
            Map = change.Map ?? held.Map,
            Blend = StackEditor.Parsed(change.Blend, held.Blend, "blend mode"),
            Opacity = change.Opacity ?? held.Opacity,
            Enabled = change.Enabled ?? held.Enabled,
            Lane = StackEditor.Parsed(change.Lane, held.Lane, "lane")
        };
    }
}
