using Patina.Contracts;

namespace Patina.Server.Stacks;

/// <summary>What the History list calls a step, by the command that starts it, when the batch names nothing.</summary>
static class StackLabels {
    public static string Of(StackCommand command) =>
        command switch {
            StackAddLayer add => $"Add {add.Name}",
            StackRemoveLayer => "Remove layer",
            StackNestLayer => "Move layer",
            StackMoveLayer => "Move layer",
            StackSetLayer set => Changed(set),
            StackSetModel => "Bind mesh",
            StackAddChannel add => $"Add {add.Usage}",
            StackSetSeed => "Change seed",
            StackSetHeightNormal => "Height to normal",
            StackStroke => "Stroke",
            StackDuplicateLayers => "Duplicate layers",
            StackPasteLayers { AsInstances: true } => "Paste as instances",
            StackPasteLayers => "Paste layers",
            StackFlattenLayer { Layer: null } => "Flatten stack",
            StackFlattenLayer => "Flatten folder",
            StackUnflattenLayer => "Unflatten folder",
            StackAddMaskLayer add => $"Add {Terms.InStack([add.Source])[0].Label} to mask",
            StackAddInstance => "Add instance",
            StackSetInstance => "Change instance source",
            StackRemoveMaskLayer => "Remove mask layer",
            StackMoveMaskLayer => "Move mask layer",
            StackSetMaskLayer => "Change mask layer",
            _ => "Edit"
        };

    /// <summary>A layer's change, named for what it changes when that is one thing a person would name.</summary>
    static string Changed(StackSetLayer set) =>
        set switch {
            { Inputs: not null, Filter: null, FilterNode: null, Graph: null } => "Change input",
            { Mask: not null } => "Change mask",
            _ => "Change layer"
        };
}
