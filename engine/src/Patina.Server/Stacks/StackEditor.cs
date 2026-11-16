using Patina.Contracts;
using Patina.Server.Graphs;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Vixen.Authoring;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Server.Stacks;

/// <summary>
///     One layer stack and its undo history. The document is Vixen's and so are the commands. Strokes are not pixels
///     here: the Server keeps where each brush went, and that journal is what the pixels are a function of, so a Worker
///     that dies loses nothing that cannot be stamped again (ADR 0009). Not thread safe: the session locks.
/// </summary>
/// <param name="path">Its file.</param>
/// <param name="document">The document, as a file holds it.</param>
public sealed partial class StackEditor(string path, LayerStackAsset document) : ILayerStackDocument {
    static readonly HeadlessContext Context = new();

    readonly List<UndoStep> undoable = [];
    readonly List<UndoStep> redoable = [];
    long nextEntry;
    long nextStep;
    (long? Step, int Parts) saved;

    /// <summary>
    ///     What size each canvas is as a Worker holds it, which a stroke is checked against before it begins.
    /// </summary>
    readonly StackCanvasSides canvasSides = new(Path.GetDirectoryName(path)!);

    /// <summary>The library layered materials are read from, or null with none.</summary>
    public string? Library { get; set; }

    /// <summary>What the viewport draws alone as the next batch arrives, which a stroke on the mesh keeps.</summary>
    public ViewportIsolation? Isolation { get; set; }

    /// <summary>This session's quick mask (#127), which strokes paint or are kept to.</summary>
    internal StackQuickMask QuickMask => field ??= new(Path.GetFileNameWithoutExtension(AssetPath), Set.Name);

    /// <summary>What a paste finds a copy by, or nothing when it has gone.</summary>
    public Func<string, StackClipContents?> Clip { get; set; } = static _ => null;

    /// <summary>
    ///     The texture set of that name among the model's others, as it stands, or null. What an instance of a layer in
    ///     another set reads. Never this session's own set.
    /// </summary>
    public Func<string, TextureSetAsset?> Sibling { get; set; } = static _ => null;

    /// <summary>Whether a material brought compounds with it since this was last cleared.</summary>
    public bool CompoundsArrived { get; set; }

    /// <inheritdoc />
    public string AssetPath { get; } = path;

    /// <inheritdoc />
    public LayerStackAsset Document { get; set; } = document;

    /// <inheritdoc />
    public string PaintSet => string.Empty;

    /// <summary>The texture set this session paints.</summary>
    public TextureSetAsset Set =>
        LayerStackEdit.SetFor(Document, PaintSet)
        ?? throw new StackEditRefusedException("This document has no texture set.");

    public GraphHistoryState History =>
        new(
            undoable.Count > 0,
            redoable.Count > 0,
            undoable.Count > 0 ? undoable[^1].Label : null,
            redoable.Count > 0 ? redoable[^1].Label : null,
            Mark != saved,
            undoable.Count > 0 ? undoable[^1].Id : null
        );

    /// <summary>The steps that stand, oldest first, which <see cref="UndoHistory.List" /> lists.</summary>
    public IReadOnlyList<UndoStep> DoneSteps => undoable;

    /// <summary>The steps undone, the next redo last.</summary>
    public IReadOnlyList<UndoStep> UndoneSteps => redoable;

    /// <summary>The strokes that stand, oldest first.</summary>
    public IReadOnlyList<StackJournalEntry> Standing => [..undoable.SelectMany(static step => step.Strokes)];

    /// <summary>Strokes that have been undone and could be redone.</summary>
    public IReadOnlyList<StackJournalEntry> Redoable => [..redoable.SelectMany(static step => step.Strokes)];

    /// <summary>Whether a drag is still going on.</summary>
    public bool HasOpenStroke => undoable.Count > 0 && undoable[^1].Strokes.Exists(static stroke => stroke.Open);

    /// <summary>Applies a batch whole, or throws and leaves everything as it was.</summary>
    /// <returns>What strokes in it touched.</returns>
    /// <exception cref="StackEditRefusedException">A command cannot be applied.</exception>
    public IReadOnlyList<StackPainted> Apply(StackCommandBatch batch) {
        if (batch.Commands.Count == 0) {
            throw new StackEditRefusedException("A batch needs at least one command.");
        }

        if (Extend(batch) is { } extended) {
            // More of a stroke is the same undo step, so the step alone cannot say the file is behind.
            undoable[^1].Parts++;
            return extended;
        }

        EndDrag();
        var step = new UndoStep(
            nextStep + 1,
            batch.Label
            ?? StackLayerEdits.Label(batch.Commands[0], id => LayerStackEdit.Find(Document, new(Set.Name, id)))
        );
        List<IAuthoringCommand> applied = [];
        try {
            foreach (var command in batch.Commands) {
                Run(command, step, applied);
            }
        } catch {
            for (var index = applied.Count - 1; index >= 0; index--) {
                applied[index].Undo(Context);
            }

            throw;
        }

        nextStep++;
        step.Commands.AddRange(applied);
        undoable.Add(step);
        canvasSides.Stood(step.Strokes, Document.BaseWidth, Document.BaseHeight);
        Forgotten.AddRange(redoable.SelectMany(static undone => undone.Strokes).Select(static entry => entry.Id));
        redoable.Clear();
        return [
            ..step.Strokes.Where(static stroke => stroke is { From: null, Capture: null })
                .Select(static stroke => new StackPainted(stroke.Layer, stroke.Usage))
        ];
    }

    /// <summary>Strokes taken out of history for good, for a Worker to let go of.</summary>
    public List<long> Forgotten { get; } = [];

    /// <returns>The label of the batch taken back, or null when there was none.</returns>
    public string? Undo() {
        EndDrag();
        if (undoable.Count == 0) {
            return null;
        }

        var step = undoable[^1];
        undoable.RemoveAt(undoable.Count - 1);
        for (var index = step.Commands.Count - 1; index >= 0; index--) {
            step.Commands[index].Undo(Context);
        }

        redoable.Add(step);
        return step.Label;
    }

    /// <returns>The label of the batch put back, or null when there was none.</returns>
    public string? Redo() {
        EndDrag();
        if (redoable.Count == 0) {
            return null;
        }

        var step = redoable[^1];
        redoable.RemoveAt(redoable.Count - 1);
        foreach (var command in step.Commands) {
            command.Do(Context);
        }

        undoable.Add(step);
        return step.Label;
    }

    /// <summary>
    ///     Where the history stands, which a save takes with its text (#306): the newest step that stands, or null, and
    ///     how far a drag had got with it. The revision is no measure, since it also moves for a bake chosen.
    /// </summary>
    public (long? Step, int Parts) Mark => undoable.Count > 0 ? (undoable[^1].Id, undoable[^1].Parts) : (null, 0);

    /// <summary>The file holds the document as it stood at a mark: back at it, the stack is saved.</summary>
    public void MarkSaved((long? Step, int Parts) at) => saved = at;

    /// <summary>What a colour in the API is in a paint file.</summary>
    /// <exception cref="StackEditRefusedException">It is not four numbers.</exception>
    public static uint Packed(IReadOnlyList<float> colour) =>
        colour.Count == 4
            ? PaintImage.Pack(colour[0], colour[1], colour[2], colour[3])
            : throw new StackEditRefusedException("A color is red, green, blue and alpha.");

    /// <summary>The next part of a drag, which adds to the step it started.</summary>
    IReadOnlyList<StackPainted>? Extend(StackCommandBatch batch) {
        if (undoable.Count == 0 || batch.Commands.Any(static command => command is not StackStroke)) {
            return null;
        }

        var open = undoable[^1].Strokes;
        List<(StackJournalEntry Entry, StackStroke Part)> parts = [];
        foreach (var part in batch.Commands.Cast<StackStroke>()) {
            if (Continued(open, part) is not { } entry) {
                return null;
            }

            parts.Add((entry, part));
        }

        foreach (var (entry, part) in parts) {
            entry.Extend(part.Stamps);
            entry.Open = part.Open;
        }

        return [..parts.Select(static part => new StackPainted(part.Entry.Layer, part.Entry.Usage))];
    }

    static StackJournalEntry? Continued(List<StackJournalEntry> open, StackStroke part) =>
        open.Find(entry =>
            entry.Open && entry.Stroke == part.Stroke && entry.Layer == part.Layer && Channel(part) == entry.Usage
        );

    static string Channel(StackStroke stroke) =>
        stroke.Mask || stroke.QuickMask == StackQuickMasks.Paint ? PaintReference.Mask : stroke.Channel;

    /// <summary>
    ///     A step that begins, or an undo, ends the drag before it: a client that vanished cannot hold
    ///     history.
    /// </summary>
    void EndDrag() {
        if (undoable.Count > 0) {
            foreach (var stroke in undoable[^1].Strokes) {
                stroke.Open = false;
            }
        }
    }

    void Run(StackCommand command, UndoStep step, List<IAuthoringCommand> applied) {
        void Do(IAuthoringCommand edit) {
            edit.Do(Context);
            applied.Add(edit);
        }

        var set = Set.Name;
        switch (command) {
            case StackAddLayer { Material: { } material }:
                Apply(material, set, Do);
                break;
            case StackAddLayer add:
                Do(Add(add, set));
                break;
            case StackRemoveLayer remove:
                Require(remove.Layer);
                Do(new RemoveLayerCommand(this, new(set, remove.Layer), "Remove layer"));
                break;
            case StackMoveLayer move:
                Require(move.Layer);
                if (!MoveLayerCommand.CanMove(Document, new(set, move.Layer), move.Delta)) {
                    throw new StackEditRefusedException($"'{move.Layer}' cannot move {move.Delta} places.");
                }

                Do(new MoveLayerCommand(this, new(set, move.Layer), move.Delta, "Move layer"));
                break;
            case StackSetLayer change:
                var renamed = Require(change.Layer).AnchorPoint;
                Do(Change(change, set));
                if (change.AnchorPoint is { } point && point != renamed && renamed.Length > 0 && point.Length > 0) {
                    Do(Reanchored(renamed, point));
                }

                break;
            case StackNestLayer nest:
                Nest(nest, set, Do);
                break;
            case StackSetModel model:
                Do(new SetModelCommand(this, model.Model, "Bind mesh"));
                break;
            case StackSetSeed seed:
                Do(new SetDocumentCommand(this, Document with { Seed = seed.Seed }, "Change seed"));
                break;
            case StackStroke stroke:
                Stroke(stroke, step, set, Do);
                break;
            case StackDuplicateLayers duplicate:
                Duplicate(duplicate, step, set, Do);
                break;
            case StackPasteLayers paste:
                Paste(paste, step, set, Do);
                break;
            case StackFlattenLayer flatten:
                Flatten(flatten.Layer, step, set, Do);
                break;
            case StackUnflattenLayer unflatten:
                Unflatten(unflatten.Layer, set, Do);
                break;
            case StackCarryPaint carry:
                Carry(carry, step, set, Do);
                break;
            default:
                if (!StackLayerEdits.Run(this, command, (set, step.Strokes), Do)
                    && !StackSetEdits.Run(this, command, Do)) {
                    throw new StackEditRefusedException($"Unknown command '{command.GetType().Name}'.");
                }

                break;
        }
    }

    /// <summary>
    ///     A layered material a stroke lays (#125), made ready for the set as applying it would make it, with the
    ///     graphs it needs brought into the project; default for any other source.
    /// </summary>
    /// <exception cref="StackEditRefusedException">There is no such material, or it has none for this set.</exception>
    ImmutableArray<LayerAsset> Layered(StackStrokeSource? source) {
        if (source is not { Kind: "material", Layered: { } named }) {
            return default;
        }

        var material = MaterialShelf.Read(Library, named);
        CompoundsArrived |= MaterialShelf.BringCompounds(Library, named, AssetPath);
        var applied = SmartMaterial.Prepare(Set, material);
        return applied.Layers.IsDefaultOrEmpty ? throw new StackEditRefusedException(applied.Status) : applied.Layers;
    }

    void Apply(string source, string set, Action<IAuthoringCommand> run) {
        var material = MaterialShelf.Read(Library, source);
        CompoundsArrived |= MaterialShelf.BringCompounds(Library, source, AssetPath);
        var applied = SmartMaterial.Prepare(Set, material);
        if (applied.Layers.IsDefaultOrEmpty) {
            throw new StackEditRefusedException(applied.Status);
        }

        run(new ApplySmartMaterialCommand(this, set, applied.Layers, $"Apply {material.Name}"));
    }

    AddLayerCommand Add(StackAddLayer add, string set) {
        if (!Enum.TryParse<LayerKind>(add.Kind, true, out var kind) || kind == LayerKind.Instance) {
            throw new StackEditRefusedException(
                kind == LayerKind.Instance
                    ? "An instance names the layer it stands for: use addInstance."
                    : $"'{add.Kind}' is not a kind of layer."
            );
        }

        var id = LayerStackEdit.FreeId(
            Set,
            add.Name.Length > 0 ? add.Name.ToLowerInvariant().Replace(' ', '-') : "layer"
        );
        var filter = LayerFilterKind.Levels;
        if (add.Filter is { } named && !Enum.TryParse(named, true, out filter)) {
            throw new StackEditRefusedException($"'{named}' is not a filter.");
        }

        return new(
            this,
            Top(set, add.Parent),
            new() {
                Id = id,
                Name = FreeName(add.Name),
                Kind = kind,
                Filter = filter,
                FilterNode = add.FilterNode ?? string.Empty,
                Values = Copied(add.Values) ?? (kind == LayerKind.Fill ? ChannelDefaults() : [])
            },
            $"Add {add.Name}"
        );
    }

    /// <summary>What a fill nobody has set yet writes, which is what Properties shows for it.</summary>
    Dictionary<string, float[]> ChannelDefaults() =>
        Set.Channels.ToDictionary(static channel => channel.Usage, static channel => channel.Default.ToArray());

    /// <summary>A name no layer has, so that two rows can be told apart.</summary>
    internal string FreeName(string name) {
        HashSet<string> taken = new(Names(Set.Layers), StringComparer.Ordinal);
        if (!taken.Contains(name)) {
            return name;
        }

        for (var count = 2;; count++) {
            var numbered = string.Create(CultureInfo.InvariantCulture, $"{name} {count}");
            if (!taken.Contains(numbered)) {
                return numbered;
            }
        }
    }

    static IEnumerable<string> Names(IEnumerable<LayerAsset> layers) =>
        layers.SelectMany(static layer => Names(layer.Children).Prepend(layer.Name));

    /// <summary>The slot at the top of a group, or of the stack.</summary>
    internal LayerSlot Top(string set, string? parent) {
        if (parent is null) {
            return new(set, null, Set.Layers.Count);
        }

        // A group holds layers, and any other layer holds its effects: both are children.
        return new(set, parent, Require(parent).Children.Count);
    }

    void Nest(StackNestLayer nest, string set, Action<IAuthoringCommand> run) {
        var layer = Require(nest.Layer);
        if (nest.Parent is { } parent && Inside(layer, parent)) {
            throw new StackEditRefusedException("A group cannot go inside itself.");
        }

        run(new RemoveLayerCommand(this, new(set, layer.Id), "Move layer"));
        run(new AddLayerCommand(this, Top(set, nest.Parent), layer, "Move layer"));
    }

    static bool Inside(LayerAsset layer, string id) =>
        layer.Id == id || layer.Children.Exists(child => Inside(child, id));

    static Dictionary<string, float[]>? Copied(IReadOnlyDictionary<string, IReadOnlyList<float>>? numbers) =>
        numbers?.ToDictionary(static value => value.Key, static value => value.Value.ToArray());

    internal static Dictionary<string, TValue> Merged<TValue>(
        Dictionary<string, TValue> held,
        IEnumerable<KeyValuePair<string, TValue>>? changed
    ) {
        Dictionary<string, TValue> merged = new(held, StringComparer.Ordinal);
        foreach (var (key, value) in changed ?? []) {
            merged[key] = value;
        }

        return merged;
    }

    /// <exception cref="StackEditRefusedException">It does not name a value of that enumeration.</exception>
    internal static TEnum Parsed<TEnum>(string? name, TEnum held, string what) where TEnum : struct, Enum =>
        name is null
            ? held
            : Enum.TryParse<TEnum>(name, true, out var parsed)
                ? parsed
                : throw new StackEditRefusedException($"'{name}' is not a {what}.");

    SetLayerCommand Change(StackSetLayer change, string set) {
        var layer = Require(change.Layer);
        if (change.Opacity is < 0f or > 1f) {
            throw new StackEditRefusedException("An opacity is from 0 to 1.");
        }

        var filter = Parsed(change.Filter, layer.Filter, "filter");
        var point = change.AnchorPoint?.Trim() ?? layer.AnchorPoint;
        Published(layer, point);
        var filterNode = change.FilterNode ?? layer.FilterNode;

        // Another filter has other numbers, so what was set for the last one does not follow. It would only be refused.
        var sameFilter = StackDocuments.FilterNodeOf(layer)
            == StackDocuments.FilterNodeOf(layer with { Filter = filter, FilterNode = filterNode });
        return new(
            this,
            new(set, change.Layer),
            layer,
            layer with {
                Name = change.Name ?? layer.Name,
                Tag = Parsed(change.Tag, layer.Tag, "tag"),
                Enabled = change.Enabled ?? layer.Enabled,
                Opacity = change.Opacity ?? layer.Opacity,
                Blend = Parsed(change.Blend, layer.Blend, "blend mode"),
                Channels = change.Channels is { } channels ? [..channels] : layer.Channels,
                Fill = Parsed(change.Fill, layer.Fill, "kind of fill"),
                Values = Merged(layer.Values, Copied(change.Values)),
                Textures = Merged(layer.Textures, change.Textures),
                Graph = change.Graph ?? layer.Graph,
                AnchorPoint = point,
                Anchor = change.Anchor is { } anchor ? StackDocuments.Reference(anchor) : layer.Anchor,
                Inputs = StackDocuments.Sources(change.Inputs)
                    ?? (sameFilter && (change.Graph ?? layer.Graph) == layer.Graph ? layer.Inputs : []),
                Projection = Parsed(change.Projection, layer.Projection, "projection"),
                PlanarAxis = Parsed(change.PlanarAxis, layer.PlanarAxis, "axis"),
                Placement = change.Placement is { } placement ? StackDocuments.Placed(placement) : layer.Placement,
                Decal = StackLayerEdits.Decal(change, layer),
                SurfacePath = StackLayerEdits.SurfacePath(change, layer),
                Filter = filter,
                FilterNode = filterNode,
                Settings = Merged(sameFilter ? layer.Settings : [], Copied(change.Settings)),
                Texts = Merged(sameFilter ? layer.Texts : [], change.Parameters),
                Mask = StackDocuments.MeshMasked(StackLayerEdits.Masked(layer.Mask, change.Mask), change.MeshMask)
            },
            "Change layer"
        );
    }

    /// <summary>What a channel holds before anything paints it, which is what leaves its material alone.</summary>
    /// <exception cref="StackEditRefusedException">It is not a channel a stack can have.</exception>
    /// <remarks>Vixen's own answer, which a filter reading a channel the set lacks reads too (#254).</remarks>
    public static float[] DefaultOf(string usage) =>
        ChannelAsset.Untouched(usage) ?? throw new StackEditRefusedException($"'{usage}' is not a channel.");

    /// <summary>A whole-document change as one undoable step, which is how a set's own settings change.</summary>
    internal IAuthoringCommand Replaced(LayerStackAsset after, string name) =>
        new SetDocumentCommand(this, after, name);

    /// <summary>A whole-document change, which is how a texture set's own fields are undone.</summary>
    sealed class SetDocumentCommand(ILayerStackDocument document, LayerStackAsset after, string name)
        : IAuthoringCommand {
        readonly LayerStackAsset before = document.Document;

        public string Name { get; } = name;

        public void Do(ICommandContext context) => document.Document = after;

        public void Undo(ICommandContext context) => document.Document = before;

        public bool TryMergeWith(IAuthoringCommand previous, [NotNullWhen(true)] out IAuthoringCommand? merged) {
            merged = null;
            return false;
        }
    }


    internal SetLayerCommand Named(LayerAsset before, LayerAsset after, string set) =>
        new(this, new(set, before.Id), before, after, "Name paint file");

    internal LayerAsset Require(string id) =>
        LayerStackEdit.Find(Document, new(Set.Name, id))
        ?? throw new StackEditRefusedException("That layer is not in this document any more.");

    /// <summary>One undo step: what it is called, what a jump names it by, and what it did.</summary>
    public sealed record UndoStep(long Id, string Label) {
        public List<IAuthoringCommand> Commands { get; } = [];

        /// <summary>How many times a drag has added to the step since it began.</summary>
        public int Parts { get; set; }

        public List<StackJournalEntry> Strokes { get; } = [];
    }

    /// <summary>Nothing listens: a session reads the document after every edit.</summary>
    sealed class HeadlessContext : ICommandContext {
        public void Touch(IAuthoredDocument document) { }
    }
}
