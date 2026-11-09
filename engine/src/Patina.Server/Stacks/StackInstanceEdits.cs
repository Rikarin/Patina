using Patina.Contracts;
using Vixen.Authoring;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     The edits that make and point instance layers (#108), in this texture set or another of the model, refusing a
///     source that is not there and one that would stand for itself.
/// </summary>
/// <param name="editor">The stack they edit.</param>
sealed class StackInstanceEdits(StackEditor editor) {
    /// <summary>
    ///     An instance of each layer a copy holds at its top, in order, over the chosen layer or at the top. The copy
    ///     names the stack it came from, and that stack's texture set is the one each instance reads.
    /// </summary>
    public void PasteInstances(StackClipContents clip, string? above, string set, Action<IAuthoringCommand> run) {
        var copied = LayerStackYaml.Read(clip.Text).Sets[0].Layers;
        var from = string.Equals(clip.Stack, editor.AssetPath, StringComparison.Ordinal) ? string.Empty : clip.Set;
        foreach (var layer in copied) {
            var (made, id) = AddInstance(new(layer.Id, from), above, set);
            run(made);
            above = id;
        }
    }

    /// <summary>An instance pointed at another source, which is refused when it would stand for itself.</summary>
    public SetLayerCommand Repointed(StackSetInstance repoint, string set) {
        var layer = editor.Require(repoint.Layer);
        if (layer.Kind != LayerKind.Instance) {
            throw new StackEditRefusedException($"'{layer.Name}' is not an instance.");
        }

        var (reference, _) = Source(repoint.Source, set);
        if (LayerStackInstances.Cycles(WithSiblings(reference.Set), set, layer.Id, reference)) {
            throw new StackEditRefusedException(
                $"'{layer.Name}' would stand, through instances, for itself.",
                Api.Refusals.InstanceCycle,
                new Dictionary<string, string> { ["layer"] = layer.Name }
            );
        }

        return new(editor, new(set, layer.Id), layer, layer with { Instance = reference }, "Change instance source");
    }

    /// <summary>A reference to a source as a file holds it, and the source.</summary>
    /// <exception cref="StackEditRefusedException">The source is not there.</exception>
    (InstanceReference Reference, LayerAsset Found) Source(StackInstanceRef source, string set) {
        var reference = new InstanceReference {
            Set = source.Set == set ? string.Empty : source.Set, Layer = source.Layer
        };
        var origin = reference.Set.Length > 0 ? reference.Set : set;
        return LayerStackEdit.Find(WithSiblings(reference.Set), new(origin, reference.Layer)) is { } found
            ? (reference, found)
            : throw new StackEditRefusedException(
                $"Texture set '{origin}' has no layer '{reference.Layer}' to stand for.",
                Api.Refusals.InstanceSourceMissing,
                new Dictionary<string, string> { ["layer"] = reference.Layer, ["set"] = origin }
            );
    }

    /// <summary>An instance of a layer, named after it, just above another or at the top.</summary>
    /// <exception cref="StackEditRefusedException">The source is not there, or it would stand for itself.</exception>
    public (AddLayerCommand Command, string Id) AddInstance(StackInstanceRef source, string? above, string set) {
        var (reference, found) = Source(source, set);
        var stack = WithSiblings(reference.Set);
        var id = LayerStackEdit.FreeId(editor.Set, found.Id.Length > 0 ? $"{found.Id}-instance" : "instance");
        if (LayerStackInstances.Cycles(stack, set, id, reference)) {
            throw new StackEditRefusedException(
                $"An instance of '{found.Name}' here would stand, through instances, for itself.",
                Api.Refusals.InstanceCycle,
                new Dictionary<string, string> { ["layer"] = found.Name }
            );
        }

        var slot = editor.Top(set, null);
        if (above is not null) {
            editor.Require(above);
            var at = LayerStackEdit.SlotOf(editor.Document, new(set, above))!.Value;
            slot = at with { Index = at.Index + 1 };
        }

        return (
            new(
                editor,
                slot,
                new() {
                    Id = id,
                    Name = editor.FreeName($"{(found.Name.Length > 0 ? found.Name : found.Id)} Instance"),
                    Kind = LayerKind.Instance,
                    Instance = reference
                },
                "Add instance"
            ), id);
    }

    /// <summary>
    ///     The document with another texture set beside its own, which is what an instance across sets reads.
    /// </summary>
    LayerStackAsset WithSiblings(string set) =>
        set.Length == 0 || editor.Document.SetNamed(set) is not null || editor.Sibling(set) is not { } sibling
            ? editor.Document
            : editor.Document with { Sets = [..editor.Document.Sets, sibling] };
}
