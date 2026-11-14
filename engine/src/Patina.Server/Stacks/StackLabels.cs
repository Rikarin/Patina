using Patina.Contracts;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>What the History list calls a step, by the command that starts it, when the batch names nothing.</summary>
static class StackLabels {
    /// <param name="command">The command that starts the step.</param>
    /// <param name="layer">A layer as it stands before the step, by id, or null when there is none.</param>
    public static string Of(StackCommand command, Func<string, LayerAsset?> layer) =>
        command switch {
            StackAddLayer add => $"Add {add.Name}",
            StackRemoveLayer => "Remove layer",
            StackNestLayer or StackMoveLayer => "Move layer",
            StackSetLayer set => Changed(set, layer(set.Layer)),
            StackSetModel => "Bind mesh",
            StackAddChannel add => $"Add {Terms.Label(add.Usage)}",
            StackRemoveChannel remove => $"Remove {Terms.Label(remove.Usage)}",
            StackSetChannelFormat => "Channel depth",
            StackSetSeed => "Change seed",
            StackSetHeightNormal => "Height to normal",
            StackSetSettings => "Texture set settings",
            StackStroke => "Stroke",
            StackDuplicateLayers => "Duplicate layers",
            StackPasteLayers { AsInstances: true } => "Paste as instances",
            StackPasteLayers { Into: not null } into => $"Paste {into.Part.ToLowerInvariant()}",
            StackPasteLayers => "Paste layers",
            StackFlattenLayer { Layer: null } => "Flatten stack",
            StackFlattenLayer => "Flatten folder",
            StackUnflattenLayer => "Unflatten folder",
            StackAddInstance => "Add instance",
            StackSetInstance => "Change instance source",
            _ => OfMask(command)
        };

    /// <summary>A step on a layer's mask.</summary>
    static string OfMask(StackCommand command) =>
        command switch {
            StackAddMaskLayer add => $"Add {Terms.InStack(add.Source)} to mask",
            StackRemoveMaskLayer => "Remove mask layer",
            StackMoveMaskLayer => "Move mask layer",
            StackSetMaskLayer => "Change mask layer",
            StackApplyMask => "Apply mask",
            StackKeepQuickMask { Outside: true } => "Keep outside quick mask",
            StackKeepQuickMask => "Keep inside quick mask",
            _ => "Edit"
        };

    /// <summary>A layer's change, named for what it changes when that is one thing a person would name.</summary>
    static string Changed(StackSetLayer set, LayerAsset? before) =>
        set switch {
            { Inputs: not null, Filter: null, FilterNode: null, Graph: null } => "Change input",
            { Mask: { } mask } when Switched(mask, before?.Mask) => mask.Enabled ? "Switch mask on" : "Switch mask off",
            { Mask: not null } => "Change mask",
            { Tag: not null, Name: null, Enabled: null } => "Tag layer",
            { Decal: not null, Path: null, Projection: null } => "Move decal",
            { Path: not null, Decal: null, Projection: null } => "Edit path",
            _ => "Change layer"
        };

    /// <summary>
    ///     Whether a mask change turns its switch and nothing else a person set, as a Shift-click does (#109).
    /// </summary>
    static bool Switched(StackMask mask, MaskAsset? before) =>
        before is not null
        && mask.Enabled != before.Enabled
        && mask.Source == before.Source.ToString()
        && (mask.Effects?.Count ?? 0) == before.Effects.Count(static effect => effect.Node != "Analysis/Colour Select");
}
