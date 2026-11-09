using Patina.Contracts;
using Vixen.Authoring;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     Where <see cref="StackEditor" /> hands the edits of instances (#108), of mask stacks (#109) and of a paste into
///     a layer (#106), and asks what a step is called, so that it stays one class a person can read.
/// </summary>
static class StackLayerEdits {
    /// <summary>What the History list calls a step started by this command.</summary>
    public static string Label(StackCommand command) => StackLabels.Of(command);

    /// <summary>Applies an instance or mask-stack command, or says it is neither.</summary>
    public static bool Run(StackEditor editor, StackCommand command, string set, Action<IAuthoringCommand> run) {
        switch (command) {
            case StackAddInstance instance:
                run(new StackInstanceEdits(editor).AddInstance(instance.Source, instance.Above, set).Command);
                return true;
            case StackSetInstance repoint:
                run(new StackInstanceEdits(editor).Repointed(repoint, set));
                return true;
            case StackAddMaskLayer or StackRemoveMaskLayer or StackMoveMaskLayer or StackSetMaskLayer:
                run(new StackMaskLayerEdits(editor).MaskLayers(command, set));
                return true;
            default:
                return false;
        }
    }

    /// <summary>A layer's mask as a change asks for it, which <see cref="StackMaskLayers.Masked" /> says.</summary>
    public static MaskAsset Masked(MaskAsset held, StackMask? mask) => StackMaskLayers.Masked(held, mask);

    /// <summary>The canvas a stroke on a paint entry of a layer's mask stack paints.</summary>
    public static string MaskLayerCanvas(
        StackEditor editor,
        LayerAsset layer,
        int index,
        string set,
        Action<IAuthoringCommand> run
    ) =>
        new StackMaskLayerEdits(editor).MaskLayerCanvas(layer, index, set, run);

    /// <summary>A copy pasted as instances of what was copied.</summary>
    public static void PasteInstances(
        StackEditor editor,
        StackClipContents clip,
        string? above,
        string set,
        Action<IAuthoringCommand> run
    ) =>
        new StackInstanceEdits(editor).PasteInstances(clip, above, set, run);

    /// <summary>Part of a copied layer, its mask or its content, put into a layer that is here (#106).</summary>
    public static void PasteInto(
        StackEditor editor,
        LayerAsset target,
        List<LayerAsset> layers,
        string part,
        string set,
        StackEditor.UndoStep step,
        string folder,
        Action<IAuthoringCommand> run
    ) =>
        StackLayerParts.PasteInto(editor, target, layers, part, set, step, folder, run);
}
