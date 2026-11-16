using Patina.Contracts;
using Vixen.Authoring;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>Copies, anchor points, flattening and carried paint.</summary>
public sealed partial class StackEditor {
    /// <summary>Each chosen layer once, less any inside another that is chosen: that one takes it along.</summary>
    List<string> Outermost(IReadOnlyList<string> layers, string set) {
        if (layers.Count == 0) {
            throw new StackEditRefusedException("Name at least one layer.");
        }

        HashSet<string> chosen = [..layers];
        List<string> outermost = [];
        foreach (var id in layers.Distinct(StringComparer.Ordinal)) {
            Require(id);
            var inside = false;
            for (var parent = LayerStackEdit.SlotOf(Document, new(set, id))?.Parent;
                 parent is not null;
                 parent = LayerStackEdit.SlotOf(Document, new(set, parent))?.Parent) {
                inside |= chosen.Contains(parent);
            }

            if (!inside) {
                outermost.Add(id);
            }
        }

        return outermost;
    }

    /// <summary>Layers as a file holds them, which is a deep copy: a record's lists are shared by a with.</summary>
    public static List<LayerAsset> Written(IEnumerable<LayerAsset> layers) =>
        LayerStackYaml.Read(
                LayerStackYaml.Write(new() { Name = "clip", Sets = [new() { Name = "clip", Layers = [..layers] }] })
            )
            .Sets[0]
            .Layers;

    /// <summary>
    ///     The layers a copy holds, read from text: the outermost of these, in the order they stand, bottom first. With
    ///     the canvases they name, for the Worker to write.
    /// </summary>
    /// <exception cref="StackEditRefusedException">A layer is not in this stack.</exception>
    public (string Text, int Count, IReadOnlyList<string> Canvases) Copied(IReadOnlyList<string> layers) {
        var set = Set.Name;
        var outermost = Outermost(layers, set);
        List<LayerAsset> standing = [
            ..Flattened(Set.Layers).Where(layer => outermost.Contains(layer.Id, StringComparer.Ordinal))
        ];
        var text = LayerStackYaml.Write(new() { Name = "clip", Sets = [new() { Name = "clip", Layers = standing }] });
        return (text, standing.Count, [..Canvases(standing).Distinct(StringComparer.Ordinal)]);
    }

    internal static IEnumerable<LayerAsset> Flattened(IEnumerable<LayerAsset> layers) =>
        layers.SelectMany(static layer => Flattened(layer.Children).Prepend(layer));

    /// <summary>Each layer duplicated just above itself, with canvases of its own copied from its own.</summary>
    void Duplicate(StackDuplicateLayers duplicate, UndoStep step, string set, Action<IAuthoringCommand> run) {
        var outermost = Outermost(duplicate.Layers, set);
        StackLayerCopies copies = new(this, set);
        List<(string Source, LayerAsset Copy)> made = [
            ..outermost.Select(id => (id, copies.Of(Written([Require(id)])[0], static paint => paint)))
        ];
        foreach (var (source, copy) in made.Select(pair => (pair.Source, copies.Reanchored(pair.Copy)))) {
            var slot = LayerStackEdit.SlotOf(Document, new(set, source))!.Value;
            run(new AddLayerCommand(this, slot with { Index = slot.Index + 1 }, copy, "Duplicate layers"));
        }

        Journal(copies, step);
    }

    /// <summary>
    ///     A copy's layers, new each time, over the chosen layer or at the top. Their canvases are the copy's, so a
    ///     cut layer and one painted since are what was copied. Limits to named meshes are another stack's meshes.
    /// </summary>
    void Paste(StackPasteLayers paste, UndoStep step, string set, Action<IAuthoringCommand> run) {
        var clip = Clip(paste.Clip)
            ?? throw new StackEditRefusedException("That copy has gone, with the Server that held it. Copy again.");
        if (paste.AsInstances) {
            StackLayerEdits.PasteInstances(this, clip, paste.Above, set, run);
            return;
        }

        var layers = LayerStackYaml.Read(clip.Text).Sets[0].Layers;
        // Paint pasted into a stack of another size is resampled to it as the Worker copies it (#106).
        var same = string.Equals(clip.Stack, AssetPath, StringComparison.Ordinal);

        if (paste.Into is { } into) {
            StackLayerEdits.PasteInto(this, Require(into), layers, paste.Part, set, step, clip.Folder, run);
            return;
        }

        StackLayerCopies copies = new(this, set);
        List<LayerAsset> made = [
            ..layers.Select(layer => copies.Of(
                    same ? layer : layer with { Mask = StackDocuments.MeshMasked(layer.Mask, new([], string.Empty)) },
                    paint => Path.Combine(clip.Folder, Path.GetFileName(paint))
                )
            )
        ];
        var slot = Top(set, null);
        if (paste.Above is { } above) {
            Require(above);
            var at = LayerStackEdit.SlotOf(Document, new(set, above))!.Value;
            slot = at with { Index = at.Index + 1 };
        }

        foreach (var layer in made.Select(copies.Reanchored)) {
            run(new AddLayerCommand(this, slot, layer, "Paste layers"));
            slot = slot with { Index = slot.Index + 1 };
        }

        Journal(copies, step);
    }

    /// <summary>
    ///     A name no other layer publishes, or the empty one. Taking one away that something reads is refused: the
    ///     reference would read nothing, and a rename is what keeps it.
    /// </summary>
    void Published(LayerAsset layer, string point) {
        if (point == layer.AnchorPoint) {
            return;
        }

        if (point.Length > 0 && LayerStackEdit.AnchorPoints(Set).ContainsKey(point)) {
            throw new StackEditRefusedException(
                $"Another layer publishes the anchor point '{point}'.",
                Api.Refusals.AnchorTaken,
                new Dictionary<string, string> { ["anchor"] = point }
            );
        }

        if (point.Length == 0
            && layer.AnchorPoint.Length > 0
            && LayerStackEdit.AnchorUses(Set)
                .FirstOrDefault(use => use.Reference.Anchor == layer.AnchorPoint) is { } use) {
            throw new StackEditRefusedException(
                $"'{use.Layer}' reads the anchor point '{layer.AnchorPoint}'.",
                Api.Refusals.AnchorInUse,
                new Dictionary<string, string> { ["anchor"] = layer.AnchorPoint, ["layer"] = Require(use.Layer).Name }
            );
        }
    }

    /// <summary>Every reference to one anchor point pointed at its new name, as one more command of the step.</summary>
    SetDocumentCommand Reanchored(string from, string to) {
        var set = Set;
        var moved = set with { Layers = LayerStackEdit.Reanchored(set.Layers, from, to) };
        return new(
            this,
            Document with { Sets = [..Document.Sets.Select(held => held == set ? moved : held)] },
            "Rename anchor point"
        );
    }

    /// <summary>
    ///     A group flattened into a canvas, or the whole stack gathered into a group and flattened. The canvas is a
    ///     journal entry the Worker writes in its turn, from the document as it stood, so a replay writes the same one.
    /// </summary>
    void Flatten(string? id, UndoStep step, string set, Action<IAuthoringCommand> run) {
        if (id is null) {
            var layers = Set.Layers;
            if (layers.Count == 0) {
                throw new StackEditRefusedException("This document has no layers to flatten.");
            }

            id = LayerStackEdit.FreeId(Set, "flattened");
            var whole = Set with {
                Layers = [
                    new() { Id = id, Name = FreeName("Flattened"), Kind = LayerKind.Group, Children = [..layers] }
                ]
            };
            run(
                new SetDocumentCommand(
                    this,
                    Document with { Sets = [..Document.Sets.Select(held => held.Name == set ? whole : held)] },
                    "Flatten all layers"
                )
            );
        }

        var group = Require(id);
        if (!LayerStackEdit.CanFlatten(Set, id, out var backdrop, out var reason)) {
            throw new StackEditRefusedException(
                reason,
                Api.Refusals.FlattenRefused,
                new Dictionary<string, string> { ["layer"] = group.Name, ["reason"] = reason }
            );
        }

        var capture = new StackCaptureEntry(StackDocuments.Text(Document), id, backdrop);
        var paint = FreeCanvas(Path.GetFileNameWithoutExtension(AssetPath), set, id, false, new HashSet<string>());
        run(new SetLayerCommand(this, new(set, id), group, group with { Paint = paint }, "Flatten folder"));
        step.Strokes.Add(StackJournalEntry.Captured(nextEntry++, paint, capture, Document.Model));
    }

    /// <summary>A flattened group back to its children. Its canvas stays in the history, for an undo.</summary>
    void Unflatten(string id, string set, Action<IAuthoringCommand> run) {
        var group = Require(id);
        if (!group.Flattened) {
            throw new StackEditRefusedException(
                $"'{group.Name}' is not flattened.",
                Api.Refusals.NotFlattened,
                new Dictionary<string, string> { ["layer"] = group.Name }
            );
        }

        run(new SetLayerCommand(this, new(set, id), group, group with { Paint = string.Empty }, "Unflatten folder"));
    }

    /// <summary>The set's paint carried from another UV set's layout onto its own (#210).</summary>
    void Carry(StackCarryPaint carry, UndoStep step, string set, Action<IAuthoringCommand> run) =>
        new StackPaintCarry(this, set).Run(carry, run, step.Strokes);

    /// <summary>The number the next entry of the journal takes.</summary>
    internal long TakeEntry() => nextEntry++;

    internal void Journal(StackLayerCopies copies, UndoStep step) {
        foreach (var (from, to) in copies.Canvases) {
            step.Strokes.Add(
                StackJournalEntry.Copy(nextEntry++, to, from, Document.Model, Document.BaseWidth, Document.BaseHeight)
            );
        }
    }
}
