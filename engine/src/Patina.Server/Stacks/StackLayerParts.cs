using Vixen.Authoring;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>Part of one layer put into another (#106): a copy's mask, or what it puts on top.</summary>
static class StackLayerParts {
    /// <summary>
    ///     Part of the first copied layer put into a layer that is already here (#106): its mask, keeping the layer's
    ///     limit to some meshes, or its content, keeping the layer's name, mask, blend, opacity and anchor point. Any
    ///     canvas comes as a copy of its own, as a paste's does.
    /// </summary>
    public static void PasteInto(
        StackEditor editor,
        LayerAsset target,
        List<LayerAsset> layers,
        string part,
        string set,
        StackEditor.UndoStep step,
        string folder,
        Action<IAuthoringCommand> run
    ) {
        if (layers.Count == 0) {
            throw new StackEditRefusedException("That copy holds no layer.");
        }

        StackLayerCopies copies = new(editor, set);
        // Only the part that goes in is copied, so no canvas is made that nothing reads.
        var copied = part == "Mask"
            ? layers[0] with { Paint = string.Empty, Children = [] }
            : layers[0] with { Mask = new() };
        var copy = copies.Of(copied, paint => Path.Combine(folder, Path.GetFileName(paint)));
        var after = part switch {
            "Mask" => target with {
                Mask = copy.Mask with {
                    Layers = [
                        ..copy.Mask.Layers.Where(static entry => !StackDocuments.KeepsToMeshes(entry)),
                        ..target.Mask.Layers.Where(StackDocuments.KeepsToMeshes)
                    ]
                }
            },
            "Content" => copy with {
                Id = target.Id,
                Name = target.Name,
                Enabled = target.Enabled,
                Mask = target.Mask,
                Blend = target.Blend,
                Opacity = target.Opacity,
                AnchorPoint = target.AnchorPoint
            },
            _ => throw new StackEditRefusedException($"'{part}' is no part of a layer to paste: Mask or Content.")
        };
        if (part == "Content" && target.Kind == LayerKind.Group && copy.Kind != LayerKind.Group) {
            throw new StackEditRefusedException(
                $"'{target.Name}' is a folder, and what it holds is its layers: paste them in, rather than one "
                + "layer's content."
            );
        }

        var label = part == "Mask" ? "Paste mask" : "Paste content";
        run(new SetLayerCommand(editor, new(set, target.Id), target, after, label));
        editor.Journal(copies, step);
    }
}
