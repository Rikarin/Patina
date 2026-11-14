using Patina.Contracts;
using System.Globalization;
using Vixen.Authoring;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     A layer kept to the session's quick mask (#282): a fill, a decal or a path is no stroke, so nothing keeps it
///     as it lands, and it is kept by its mask instead. A paint entry on top of the mask's stack starts as a copy of
///     the quick mask as it stands, laid over black, multiplied over what is beneath, or subtracted from it to keep the
///     layer outside. The copy is a journal entry, so a Worker that takes over makes it after the strokes that painted
///     the quick mask, and the canvas is the layer's from then, saved with it, though the quick mask never is.
/// </summary>
/// <param name="editor">The stack.</param>
/// <param name="set">The texture set.</param>
sealed class StackQuickMaskKeep(StackEditor editor, string set) {
    /// <summary>Gives the layer its entry, as one change of the layer, and journals the copy that fills it.</summary>
    /// <exception cref="StackEditRefusedException">No such layer, or the quick mask is unpainted.</exception>
    public void Run(StackKeepQuickMask keep, Action<IAuthoringCommand> run, List<StackJournalEntry> journal) {
        var layer = editor.Require(keep.Layer);
        var quick = editor.QuickMask;
        if (!quick.Painted(editor.Standing.Concat(journal))) {
            throw new StackEditRefusedException(
                $"The quick mask has not been painted, so it would keep '{layer.Name}' nowhere.",
                Api.Refusals.QuickMaskUnpainted,
                new Dictionary<string, string> { ["layer"] = layer.Name }
            );
        }

        var index = StackMaskLayers.Count(layer.Mask);
        var paint = editor.FreeCanvas(
            Path.GetFileNameWithoutExtension(editor.AssetPath),
            set,
            string.Create(CultureInfo.InvariantCulture, $"{layer.Id}-{index + 1}"),
            true,
            new HashSet<string>()
        );
        var entry = new MaskLayerAsset {
            Source = LayerMaskSource.Paint,
            Paint = paint,
            Blend = keep.Outside ? LayerBlendMode.Subtract : LayerBlendMode.Multiply
        };
        run(editor.Named(layer, layer with { Mask = StackMaskLayers.Added(layer, entry, null) }, set));
        // Over opaque black: where the quick mask is unpainted the entry holds nothing, and a paint entry over others
        // composites by what its strokes covered, so a transparent texel would leave the layer showing there.
        var document = editor.Document;
        journal.Add(
            StackJournalEntry.CopyOver(
                editor.TakeEntry(),
                (paint, quick.Paint),
                document.Model,
                (document.BaseWidth, document.BaseHeight),
                Vixen.Texturing.Painting.PaintImage.Pack(0f, 0f, 0f, 1f)
            )
        );
    }
}
