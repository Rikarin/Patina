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
    /// <param name="command">The command.</param>
    /// <param name="layer">A layer as it stands before the step, by id, or null when there is none.</param>
    public static string Label(StackCommand command, Func<string, LayerAsset?> layer) => StackLabels.Of(command, layer);

    /// <summary>Applies an instance or mask-stack command, or says it is neither.</summary>
    public static bool Run(StackEditor editor, StackCommand command, string set, Action<IAuthoringCommand> run) {
        switch (command) {
            case StackAddInstance instance:
                run(new StackInstanceEdits(editor).AddInstance(instance.Source, instance.Above, set).Command);
                return true;
            case StackSetInstance repoint:
                run(new StackInstanceEdits(editor).Repointed(repoint, set));
                return true;
            case StackApplyMask applying:
                run(StackSmartAssets.ApplyMask(editor, applying, set));
                return true;
            case StackAddMaskLayer or StackRemoveMaskLayer or StackMoveMaskLayer or StackSetMaskLayer:
                run(new StackMaskLayerEdits(editor).MaskLayers(command, set));
                return true;
            default:
                return false;
        }
    }

    /// <summary>A layer's decal as a change asks for it (#133): the one sent, or the one it had.</summary>
    public static LayerDecal? Decal(StackSetLayer change, LayerAsset layer) =>
        change.Decal is { } decal ? StackSurfaces.Kept(decal) : layer.Decal;

    /// <summary>A layer's path as a change asks for it (#130): the one sent, or the one it had.</summary>
    public static LayerSurfacePath? SurfacePath(StackSetLayer change, LayerAsset layer) =>
        change.Path is { } path ? StackSurfaces.Kept(path) : layer.SurfacePath;

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

    /// <summary>The canvas a brush paints over a mask that is a picture, or null for a mask that is not one.</summary>
    public static string? CanvasOverMask(
        StackEditor editor,
        LayerAsset layer,
        string set,
        Action<IAuthoringCommand> run
    ) =>
        StackMaskLayers.IsPicture(layer.Mask.Source)
            ? new StackMaskLayerEdits(editor).CanvasOverMask(layer, set, run)
            : null;

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
