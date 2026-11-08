using Patina.Contracts;
using System.Globalization;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     A layer's mask as a stack of its own (#109): the entries composited over its base, as a client sees and edits them.
///     The entry that keeps a layer to some meshes is not one of them. It is the stack's, stays on top, and is never
///     counted, so an index here is a place in the list a client shows.
/// </summary>
static class StackMaskLayers {
    /// <summary>The entries a client sees, bottom first.</summary>
    public static IReadOnlyList<StackMaskLayer> Wire(MaskAsset mask) => [
        ..Shown(mask)
            .Select(static entry => new StackMaskLayer(
                    entry.Source.ToString(),
                    entry.Value,
                    entry.Asset,
                    entry.Anchor.Anchor.Length == 0 && entry.Anchor.Channel.Length == 0
                        ? null
                        : new(entry.Anchor.Anchor, entry.Anchor.Channel),
                    entry.Generator,
                    entry.Map,
                    entry.GeneratorTexts,
                    entry.GeneratorInputs.Count == 0
                        ? null
                        : entry.GeneratorInputs.ToDictionary(
                            static pair => pair.Key,
                            static pair => new StackAnchorRef(pair.Value.Anchor, pair.Value.Channel),
                            StringComparer.Ordinal
                        ),
                    entry.Paint.Trim().Length > 0,
                    entry.Blend.ToString(),
                    entry.Opacity,
                    entry.Enabled
                )
            )
    ];

    /// <summary>The entry at a place a client counts, bottom first.</summary>
    /// <exception cref="StackEditRefusedException">There is none there.</exception>
    public static MaskLayerAsset At(LayerAsset layer, int index) => Shown(layer.Mask)[Checked(layer, index, false)];

    /// <summary>The mask with an entry put in at a place, or at the top. A layer with no mask gets a white one under it.</summary>
    /// <exception cref="StackEditRefusedException">The place is not in the list.</exception>
    public static MaskAsset Added(LayerAsset layer, MaskLayerAsset entry, int? index) {
        var shown = Shown(layer.Mask);
        var at = index is { } wanted ? Checked(layer, wanted, true) : shown.Count;
        shown.Insert(at, entry);
        var mask = layer.Mask.Source == LayerMaskSource.None
            ? layer.Mask with { Source = LayerMaskSource.Constant, Value = 1f }
            : layer.Mask;
        return With(mask, shown);
    }

    /// <exception cref="StackEditRefusedException">There is no entry there.</exception>
    public static MaskAsset Removed(LayerAsset layer, int index) {
        var shown = Shown(layer.Mask);
        shown.RemoveAt(Checked(layer, index, false));
        return With(layer.Mask, shown);
    }

    /// <exception cref="StackEditRefusedException">There is no entry there, or it cannot move that far.</exception>
    public static MaskAsset Moved(LayerAsset layer, int index, int delta) {
        var shown = Shown(layer.Mask);
        var from = Checked(layer, index, false);
        var to = from + delta;
        if (delta == 0 || to < 0 || to >= shown.Count) {
            throw new StackEditRefusedException(
                string.Create(CultureInfo.InvariantCulture, $"That mask layer cannot move {delta} places.")
            );
        }

        var entry = shown[from];
        shown.RemoveAt(from);
        shown.Insert(to, entry);
        return With(layer.Mask, shown);
    }

    /// <exception cref="StackEditRefusedException">There is no entry there.</exception>
    public static MaskAsset Changed(LayerAsset layer, int index, Func<MaskLayerAsset, MaskLayerAsset> change) {
        var shown = Shown(layer.Mask);
        var at = Checked(layer, index, false);
        shown[at] = change(shown[at]);
        return With(layer.Mask, shown);
    }

    static List<MaskLayerAsset> Shown(MaskAsset mask) => [..mask.Layers.Where(static entry => !StackDocuments.KeepsToMeshes(entry))];

    /// <summary>The entries in this order, with the one keeping the layer to some meshes back on top.</summary>
    static MaskAsset With(MaskAsset mask, List<MaskLayerAsset> shown) =>
        mask with { Layers = [..shown, ..mask.Layers.Where(StackDocuments.KeepsToMeshes)] };

    static int Checked(LayerAsset layer, int index, bool end) {
        var count = Shown(layer.Mask).Count;
        if (index < 0 || index > count || (index == count && !end)) {
            throw new StackEditRefusedException(
                string.Create(CultureInfo.InvariantCulture, $"The mask of '{layer.Name}' has no layer {index}."),
                Api.Refusals.MaskLayerMissing,
                new Dictionary<string, string> {
                    ["layer"] = layer.Name, ["index"] = index.ToString(CultureInfo.InvariantCulture)
                }
            );
        }

        return index;
    }
}
