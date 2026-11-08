using Patina.Contracts;
using Patina.Server.Graphs;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Vixen.Authoring;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Server.Stacks;

/// <summary>
///     A command that cannot be applied, with a neutral reason and a stable code a client says in its own words
///     (<see cref="Api.Refusals" />). Nothing has changed.
/// </summary>
public sealed class StackEditRefusedException : Exception {
    public StackEditRefusedException() { }

    public StackEditRefusedException(string message) : base(message) { }

    public StackEditRefusedException(string message, Exception innerException) : base(message, innerException) { }

    /// <param name="message">The reason, for a reader of the API.</param>
    /// <param name="code">What a client tells it apart by.</param>
    /// <param name="values">What the reason was written from, by name.</param>
    public StackEditRefusedException(string message, string code, IReadOnlyDictionary<string, string>? values = null)
        : base(message) {
        Code = code;
        Values = values ?? new Dictionary<string, string>();
    }

    /// <summary>What a client tells this refusal apart by.</summary>
    public string Code { get; } = Api.Refusals.StackRefused;

    /// <summary>The values the reason was written from.</summary>
    public IReadOnlyDictionary<string, string> Values { get; } = new Dictionary<string, string>();
}

/// <summary>
///     One layer stack and its undo history. The document is Vixen's and so are the commands. Strokes are not pixels
///     here: the Server keeps where each brush went, and that journal is what the pixels are a function of, so a Worker
///     that dies loses nothing that cannot be stamped again (ADR 0009). Not thread safe: the session locks.
/// </summary>
public sealed class StackEditor : ILayerStackDocument {
    static readonly HeadlessContext Context = new();

    readonly List<UndoStep> undoable = [];
    readonly List<UndoStep> redoable = [];
    long nextEntry;
    long nextStep;
    long? savedStep;
    bool extendedSinceSave;

    /// <summary>Takes a document as a file holds it.</summary>
    /// <param name="path">Its file.</param>
    /// <param name="document">The document.</param>
    public StackEditor(string path, LayerStackAsset document) {
        AssetPath = path;
        Document = document;
    }

    /// <summary>The library layered materials are read from, or null with none.</summary>
    public string? Library { get; set; }

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
    public string AssetPath { get; }

    /// <inheritdoc />
    public LayerStackAsset Document { get; set; }

    /// <inheritdoc />
    public string PaintSet => string.Empty;

    /// <summary>The texture set this session paints.</summary>
    public TextureSetAsset Set =>
        LayerStackEdit.SetFor(Document, PaintSet)
        ?? throw new StackEditRefusedException("This stack has no texture set.");

    public GraphHistoryState History =>
        new(
            undoable.Count > 0,
            redoable.Count > 0,
            undoable.Count > 0 ? undoable[^1].Label : null,
            redoable.Count > 0 ? redoable[^1].Label : null,
            extendedSinceSave || (undoable.Count > 0 ? undoable[^1].Id : null) != savedStep,
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
            extendedSinceSave = true;
            return extended;
        }

        EndDrag();
        var step = new UndoStep(nextStep + 1, batch.Label ?? Label(batch.Commands[0]));
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

    public void MarkSaved() {
        savedStep = undoable.Count > 0 ? undoable[^1].Id : null;
        extendedSinceSave = false;
    }

    /// <summary>What a colour in the API is in a paint file.</summary>
    /// <exception cref="StackEditRefusedException">It is not four numbers.</exception>
    public static uint Packed(IReadOnlyList<float> colour) =>
        colour.Count == 4
            ? PaintImage.Pack(colour[0], colour[1], colour[2], colour[3])
            : throw new StackEditRefusedException("A colour is red, green, blue and alpha.");

    static string Label(StackCommand command) =>
        command switch {
            StackAddLayer add => $"Add {add.Name}",
            StackRemoveLayer => "Remove layer",
            StackNestLayer => "Move layer",
            StackMoveLayer => "Move layer",
            StackSetLayer => "Change layer",
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
            entry.Stamps.AddRange(part.Stamps);
            entry.Open = part.Open;
        }

        return [..parts.Select(static part => new StackPainted(part.Entry.Layer, part.Entry.Usage))];
    }

    static StackJournalEntry? Continued(List<StackJournalEntry> open, StackStroke part) =>
        open.Find(entry =>
            entry.Open && entry.Stroke == part.Stroke && entry.Layer == part.Layer && Channel(part) == entry.Usage
        );

    static string Channel(StackStroke stroke) => stroke.Mask ? PaintReference.Mask : stroke.Channel;

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
            case StackAddChannel add:
                Do(AddChannel(add.Usage));
                break;
            case StackSetSeed seed:
                Do(new SetDocumentCommand(this, Document with { Seed = seed.Seed }, "Change seed"));
                break;
            case StackSetHeightNormal height:
                Do(HeightNormal(height.Method));
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
            case StackAddInstance instance:
                Do(AddInstance(instance.Source, instance.Above, set).Command);
                break;
            case StackSetInstance repoint:
                Do(Repointed(repoint, set));
                break;
            case StackAddMaskLayer or StackRemoveMaskLayer or StackMoveMaskLayer or StackSetMaskLayer:
                Do(MaskLayers(command, set));
                break;
            default:
                throw new StackEditRefusedException($"Unknown command '{command.GetType().Name}'.");
        }
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
    string FreeName(string name) {
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
    LayerSlot Top(string set, string? parent) {
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

    static Dictionary<string, TValue> Merged<TValue>(
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
    static TEnum Parsed<TEnum>(string? name, TEnum held, string what) where TEnum : struct, Enum =>
        name is null
            ? held
            : Enum.TryParse<TEnum>(name, true, out var parsed)
                ? parsed
                : throw new StackEditRefusedException($"'{name}' is not a {what}.");

    /// <summary>
    ///     A mask as asked for, keeping the canvas it may already paint. Taking it away takes all of it, and what keeps
    ///     the layer to some meshes is not the mask's.
    /// </summary>
    static MaskAsset Masked(MaskAsset held, StackMask? mask) {
        if (mask is null) {
            return held;
        }

        var source = Parsed(mask.Source, held.Source, "mask source");
        return source == LayerMaskSource.None
            ? new() { Layers = [..held.Layers.Where(StackDocuments.KeepsToMeshes)] }
            : held with {
                Source = source,
                Value = mask.Value,
                Asset = mask.Asset,
                Anchor = StackDocuments.Reference(mask.Anchor),
                Generator = mask.Generator,
                GeneratorTexts = GeneratorKnobs(held, mask),
                GeneratorInputs = GeneratorFeeds(held, mask),
                Map = mask.Map,
                Effects = StackDocuments.WithSelection(held.Effects, mask)
            };
    }

    /// <summary>Another generator has other image inputs, so what fed the last one does not follow.</summary>
    static Dictionary<string, AnchorReference> GeneratorFeeds(MaskAsset held, StackMask mask) =>
        StackDocuments.References(mask.Inputs) is { } chosen ? chosen
        : mask.Generator != held.Generator ? []
        : held.GeneratorInputs;

    /// <summary>Another generator has other knobs, so what was set for the last one does not follow.</summary>
    static Dictionary<string, string> GeneratorKnobs(MaskAsset held, StackMask mask) =>
        mask.Generator != held.Generator ? []
        : mask.Parameters is { } chosen ? new(chosen)
        : held.GeneratorTexts;

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
                Inputs = StackDocuments.References(change.Inputs) ?? layer.Inputs,
                Projection = Parsed(change.Projection, layer.Projection, "projection"),
                PlanarAxis = Parsed(change.PlanarAxis, layer.PlanarAxis, "axis"),
                Placement = change.Placement is { } placement ? Placed(placement) : layer.Placement,
                Filter = filter,
                FilterNode = filterNode,
                Settings = Merged(sameFilter ? layer.Settings : [], Copied(change.Settings)),
                Texts = Merged(sameFilter ? layer.Texts : [], change.Parameters),
                Mask = StackDocuments.MeshMasked(Masked(layer.Mask, change.Mask), change.MeshMask)
            },
            "Change layer"
        );
    }

    static LayerPlacement Placed(StackPlacement placement) {
        if (placement.Scale <= 0f) {
            throw new StackEditRefusedException("A scale is above zero.");
        }

        return new() {
            Scale = placement.Scale,
            Rotation = placement.Rotation,
            OffsetX = placement.OffsetX,
            OffsetY = placement.OffsetY,
            Sharpness = placement.Sharpness
        };
    }

    /// <summary>What a channel holds before anything paints it, which is what leaves its material alone.</summary>
    /// <exception cref="StackEditRefusedException">It is not a channel a stack can have.</exception>
    public static float[] DefaultOf(string usage) =>
        usage switch {
            "normal" => [0.5f, 0.5f, 1f, 1f],
            "baseColor" or "roughness" or "height" => [0.5f, 0.5f, 0.5f, 1f],
            "opacity" or "occlusion" or "mask" => [1f, 1f, 1f, 1f],
            "metalness" or "emissive" => [0f, 0f, 0f, 1f],
            _ => throw new StackEditRefusedException($"'{usage}' is not a channel.")
        };

    SetDocumentCommand AddChannel(string usage) {
        if (!StackDocuments.Vocabulary.Usages.Any(term => term.Value == usage)) {
            throw new StackEditRefusedException($"'{usage}' is not a channel.");
        }

        var chosen = Set;
        if (chosen.Channels.Exists(channel => channel.Usage == usage)) {
            throw new StackEditRefusedException($"This stack has '{usage}' already.");
        }

        var added = chosen with { Channels = [..chosen.Channels, new() { Usage = usage, Default = DefaultOf(usage) }] };
        return new(
            this,
            Document with { Sets = [..Document.Sets.Select(held => held == chosen ? added : held)] },
            $"Add {usage}"
        );
    }

    SetDocumentCommand HeightNormal(string method) {
        if (!Enum.TryParse<LayerHeightNormal>(method, false, out var chosen) || !Enum.IsDefined(chosen)) {
            throw new StackEditRefusedException(
                $"'{method}' is no way to turn height into a normal. "
                + $"Use {string.Join(" or ", Enum.GetNames<LayerHeightNormal>())}."
            );
        }

        var set = Set;
        var changed = set with { HeightNormal = chosen };
        return new(
            this,
            Document with { Sets = [..Document.Sets.Select(held => held == set ? changed : held)] },
            "Height to normal"
        );
    }

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

    void Stroke(StackStroke stroke, UndoStep step, string set, Action<IAuthoringCommand> run) {
        var layer = Require(stroke.Layer);
        if (stroke.Brush.Problem() is { } problem) {
            throw new StackEditRefusedException(problem, Api.Refusals.BrushRefused, Api.Refusals.Reason(problem));
        }

        if (!stroke.Mask) {
            Writable(stroke, layer);
        }

        Aimed(stroke);

        var (paint, start) = stroke switch {
            { Mask: true, MaskLayer: { } entry } => (MaskLayerCanvas(layer, entry, set, run), null),
            { Mask: true } => MaskCanvas(layer, set, run),
            _ => (LayerCanvas(layer, set, run), (uint?)null)
        };

        // ⚠ Live, two strokes of one batch take turns; replayed, they run one after the other. Where they meet in one
        // channel that is two pictures, and a journal has to be one.
        if (step.Strokes.Exists(other => other.Paint == paint && other.Usage == Channel(stroke))) {
            throw new StackEditRefusedException(
                "A batch paints each channel of a layer once.",
                Api.Refusals.RepeatedChannel
            );
        }

        step.Strokes.Add(
            new(nextEntry++, stroke, paint, Channel(stroke), Packed(stroke.Colour), Document.Model, start)
        );
    }

    /// <summary>Refuses a stroke on a layer's own pixels that would land nowhere, or nowhere that shows.</summary>
    void Writable(StackStroke stroke, LayerAsset layer) {
        // A mask is any layer's, and only a paint layer has pixels of its own besides — or a flattened group, whose
        // canvas is its picture until it is unflattened.
        if (layer.Kind != LayerKind.Paint && !layer.Flattened) {
            var kind = layer.Kind.ToString().ToLowerInvariant();
            throw new StackEditRefusedException(
                $"Only a paint layer can be painted, and '{layer.Name}' is a {kind} layer.",
                Api.Refusals.NotPaintLayer,
                new Dictionary<string, string> { ["layer"] = layer.Name, ["kind"] = layer.Kind.ToString() }
            );
        }

        if (Set.Channels.TrueForAll(channel => channel.Usage != stroke.Channel)) {
            throw new StackEditRefusedException($"This stack has no channel '{stroke.Channel}'.");
        }

        if (!layer.Writes(stroke.Channel)) {
            throw new StackEditRefusedException(
                $"'{layer.Name}' does not write '{stroke.Channel}', so a stroke there would never show."
            );
        }
    }

    /// <summary>
    ///     The canvas of a paint layer. The first stroke names it, and undoing that takes the name back. ⚠ Never a file
    ///     that is there already: a layer removed and saved left its pixels under the id a new layer is given.
    /// </summary>
    string LayerCanvas(LayerAsset layer, string set, Action<IAuthoringCommand> run) {
        var paint = layer.Paint.Trim();
        if (paint.Length == 0) {
            paint = FreeCanvas(
                Path.GetFileNameWithoutExtension(AssetPath),
                set,
                layer.Id,
                false,
                new HashSet<string>()
            );
            run(Named(layer, layer with { Paint = paint }, set));
        }

        return paint;
    }

    /// <summary>The canvas of a mask, and what it starts as if this is the stroke that makes one.</summary>
    (string Paint, uint? Start) MaskCanvas(LayerAsset layer, string set, Action<IAuthoringCommand> run) {
        var paint = layer.Mask.Paint.Trim();
        if (layer.Mask.Source == LayerMaskSource.Paint && paint.Length > 0) {
            return (paint, null);
        }

        var showed = Showed(layer);
        paint = FreeCanvas(Path.GetFileNameWithoutExtension(AssetPath), set, layer.Id, true, new HashSet<string>());
        var painted = layer.Mask with { Source = LayerMaskSource.Paint, Paint = paint };
        run(Named(layer, layer with { Mask = StackDocuments.MeshMasked(painted, null) }, set));
        return (paint, PaintImage.Pack(showed, showed, showed, 1f));
    }

    /// <summary>
    ///     The canvas of a paint entry of a mask's stack, named by its first stroke. It starts transparent, so that what
    ///     is beneath it shows until it is painted.
    /// </summary>
    /// <exception cref="StackEditRefusedException">The entry is not a paint entry.</exception>
    string MaskLayerCanvas(LayerAsset layer, int index, string set, Action<IAuthoringCommand> run) {
        var entry = StackMaskLayers.At(layer, index);
        if (entry.Source != LayerMaskSource.Paint) {
            throw new StackEditRefusedException(
                $"Mask layer {index} of '{layer.Name}' is a {entry.Source} layer, and only a paint layer takes a brush.",
                Api.Refusals.MaskLayerNotPaint,
                new Dictionary<string, string> { ["layer"] = layer.Name, ["source"] = entry.Source.ToString() }
            );
        }

        var paint = entry.Paint.Trim();
        if (paint.Length > 0) {
            return paint;
        }

        paint = FreeCanvas(
            Path.GetFileNameWithoutExtension(AssetPath),
            set,
            string.Create(CultureInfo.InvariantCulture, $"{layer.Id}-{index + 1}"),
            true,
            new HashSet<string>()
        );
        var named = StackMaskLayers.Changed(layer, index, held => held with { Paint = paint });
        run(Named(layer, layer with { Mask = named }, set));
        return paint;
    }

    /// <summary>An edit of a layer's mask stack, as one change of the layer, so that undo puts the whole mask back.</summary>
    SetLayerCommand MaskLayers(StackCommand command, string set) {
        var (id, mask, name) = command switch {
            StackAddMaskLayer add => (add.Layer,
                (Func<LayerAsset, MaskAsset>)(layer => StackMaskLayers.Added(layer, NewMaskLayer(add), add.Index)),
                "Add mask layer"),
            StackRemoveMaskLayer remove => (remove.Layer, layer => StackMaskLayers.Removed(layer, remove.Index),
                "Remove mask layer"),
            StackMoveMaskLayer move => (move.Layer, layer => StackMaskLayers.Moved(layer, move.Index, move.Delta),
                "Move mask layer"),
            StackSetMaskLayer change => (change.Layer,
                layer => StackMaskLayers.Changed(layer, change.Index, held => ChangedMaskLayer(held, change)),
                "Change mask layer"),
            _ => throw new StackEditRefusedException($"Unknown command '{command.GetType().Name}'.")
        };
        var layer = Require(id);
        return new(this, new(set, id), layer, layer with { Mask = mask(layer) }, name);
    }

    static MaskLayerAsset NewMaskLayer(StackAddMaskLayer add) {
        var source = MaskLayerSource(add.Source);
        return new() {
            Source = source,
            Generator = source == LayerMaskSource.Generator
                ? add.Generator ?? (StackDocuments.Vocabulary.Generators is [var first, ..] ? first.Value : string.Empty)
                : string.Empty
        };
    }

    static LayerMaskSource MaskLayerSource(string name) {
        var source = Parsed(name, LayerMaskSource.None, "mask source");
        return source == LayerMaskSource.None
            ? throw new StackEditRefusedException("A mask layer has a source; remove it to have none.")
            : source;
    }

    static MaskLayerAsset ChangedMaskLayer(MaskLayerAsset held, StackSetMaskLayer change) {
        if (change.Opacity is < 0f or > 1f || change.Value is < 0f or > 1f) {
            throw new StackEditRefusedException("A mask layer's value and opacity are from 0 to 1.");
        }

        var generator = change.Generator ?? held.Generator;
        var sameGenerator = generator == held.Generator;
        return held with {
            Source = change.Source is { } source ? MaskLayerSource(source) : held.Source,
            Value = change.Value ?? held.Value,
            Asset = change.Asset ?? held.Asset,
            Anchor = change.Anchor is { } anchor ? StackDocuments.Reference(anchor) : held.Anchor,
            Generator = generator,
            GeneratorTexts = Merged(sameGenerator ? held.GeneratorTexts : [], change.Parameters),
            GeneratorValues = sameGenerator ? held.GeneratorValues : [],
            GeneratorInputs = sameGenerator ? held.GeneratorInputs : [],
            Map = change.Map ?? held.Map,
            Blend = Parsed(change.Blend, held.Blend, "blend mode"),
            Opacity = change.Opacity ?? held.Opacity,
            Enabled = change.Enabled ?? held.Enabled
        };
    }

    /// <summary>
    ///     The one number a mask stood at, which is what a canvas taking its place starts as, so that a stroke changes
    ///     only what it touches. A paint mask nobody has painted reads nothing, and so hides nothing.
    /// </summary>
    /// <exception cref="StackEditRefusedException">
    ///     The mask is a picture, which one number cannot stand for.
    /// </exception>
    static float Showed(LayerAsset layer) =>
        layer.Mask.Source switch {
            LayerMaskSource.None or LayerMaskSource.Paint => 1f,
            LayerMaskSource.Constant => layer.Mask.Value,
            var source => throw new StackEditRefusedException(
                $"A brush would replace the {source} mask of '{layer.Name}'. "
                + "Set the mask's source to Paint to paint it."
            )
        };

    /// <summary>
    ///     A file for a new canvas. ⚠ Never one anything can reach: a removed layer's strokes can still be undone back
    ///     onto its canvas, a file beside the stack holds what a save kept, and the document may name it already.
    /// </summary>
    /// <param name="stem">The stack's file name without its extension.</param>
    /// <param name="set">The texture set.</param>
    /// <param name="layer">The layer it is for.</param>
    /// <param name="mask">Whether it is the layer's mask.</param>
    /// <param name="alsoTaken">Names given out in this batch, which nothing holds yet.</param>
    internal string FreeCanvas(string stem, string set, string layer, bool mask, IReadOnlySet<string> alsoTaken) {
        var folder = Path.GetDirectoryName(AssetPath)!;
        HashSet<string> taken = [
            ..Standing.Concat(Redoable).Select(static stroke => stroke.Paint),
            ..Canvases(Set.Layers),
            ..alsoTaken
        ];
        for (var count = 1;; count++) {
            var id = count == 1 ? layer : string.Create(CultureInfo.InvariantCulture, $"{layer}-{count}");
            var name = LayerPaint.NameFor(stem, set, id, mask);
            if (!taken.Contains(name) && !File.Exists(Path.Combine(folder, name))) {
                return name;
            }
        }
    }

    /// <summary>Every canvas some layers name: their own, their mask's and those of their mask's entries.</summary>
    static IEnumerable<string> Canvases(IEnumerable<LayerAsset> layers) =>
        layers.SelectMany(static layer => Canvases(layer.Children)
            .Concat(
                [
                    layer.Paint.Trim(), layer.Mask.Paint.Trim(),
                    ..layer.Mask.Layers.Select(static entry => entry.Paint.Trim())
                ]
            )
            .Where(static paint => paint.Length > 0)
        );

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
            PasteInstances(clip, paste.Above, set, run);
            return;
        }

        var layers = LayerStackYaml.Read(clip.Text).Sets[0].Layers;
        var same = string.Equals(clip.Stack, AssetPath, StringComparison.Ordinal);
        if (Canvases(layers).Any() && (clip.Width, clip.Height) != (Document.BaseWidth, Document.BaseHeight)) {
            throw new StackEditRefusedException(
                $"These layers are painted at {clip.Width}×{clip.Height} and this stack at "
                + $"{Document.BaseWidth}×{Document.BaseHeight}. Paint moves between stacks of one size."
            );
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
    ///     An instance of each layer a copy holds at its top, in order, over the chosen layer or at the top. The copy
    ///     names the stack it came from, and that stack's texture set is the one each instance reads.
    /// </summary>
    void PasteInstances(StackClipContents clip, string? above, string set, Action<IAuthoringCommand> run) {
        var copied = LayerStackYaml.Read(clip.Text).Sets[0].Layers;
        var from = string.Equals(clip.Stack, AssetPath, StringComparison.Ordinal) ? string.Empty : clip.Set;
        foreach (var layer in copied) {
            var (made, id) = AddInstance(new(layer.Id, from), above, set);
            run(made);
            above = id;
        }
    }

    /// <summary>An instance pointed at another source, which is refused when it would stand for itself.</summary>
    SetLayerCommand Repointed(StackSetInstance repoint, string set) {
        var layer = Require(repoint.Layer);
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

        return new(this, new(set, layer.Id), layer, layer with { Instance = reference }, "Change instance source");
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
    (AddLayerCommand Command, string Id) AddInstance(StackInstanceRef source, string? above, string set) {
        var (reference, found) = Source(source, set);
        var stack = WithSiblings(reference.Set);
        var id = LayerStackEdit.FreeId(Set, found.Id.Length > 0 ? $"{found.Id}-instance" : "instance");
        if (LayerStackInstances.Cycles(stack, set, id, reference)) {
            throw new StackEditRefusedException(
                $"An instance of '{found.Name}' here would stand, through instances, for itself.",
                Api.Refusals.InstanceCycle,
                new Dictionary<string, string> { ["layer"] = found.Name }
            );
        }

        var slot = Top(set, null);
        if (above is not null) {
            Require(above);
            var at = LayerStackEdit.SlotOf(Document, new(set, above))!.Value;
            slot = at with { Index = at.Index + 1 };
        }

        return (
            new(
                this,
                slot,
                new() {
                    Id = id,
                    Name = FreeName($"{(found.Name.Length > 0 ? found.Name : found.Id)} Instance"),
                    Kind = LayerKind.Instance,
                    Instance = reference
                },
                "Add instance"
            ), id);
    }

    /// <summary>The document with another texture set beside its own, which is what an instance across sets reads.</summary>
    LayerStackAsset WithSiblings(string set) =>
        set.Length == 0 || Document.SetNamed(set) is not null || Sibling(set) is not { } sibling
            ? Document
            : Document with { Sets = [..Document.Sets, sibling] };

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
                throw new StackEditRefusedException("This stack has no layers to flatten.");
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
                    "Flatten stack"
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

    void Journal(StackLayerCopies copies, UndoStep step) {
        foreach (var (from, to) in copies.Canvases) {
            step.Strokes.Add(StackJournalEntry.Copy(nextEntry++, to, from, Document.Model));
        }
    }

    /// <summary>Refuses what a Worker could not do with a stroke, while nothing has happened yet.</summary>
    void Aimed(StackStroke stroke) {
        if (stroke.Curve is not ("free" or "line" or "path")) {
            throw new StackEditRefusedException($"'{stroke.Curve}' is not a kind of stroke.");
        }

        if (stroke.Symmetry?.Problem() is { } problem) {
            throw new StackEditRefusedException(problem, Api.Refusals.BrushRefused, Api.Refusals.Reason(problem));
        }

        // A symmetry, a view and a size in the model's units are all on the mesh.
        var onMesh = stroke.View is not null || stroke.Symmetry is not null || stroke.Brush.SizeSpace == "model";
        if (onMesh && Document.Model.Trim().Length == 0) {
            throw new StackEditRefusedException("This stack has no mesh to paint on.", Api.Refusals.NoMesh);
        }

        if (stroke.View is not { } view) {
            return;
        }

        if (view is not { Eye.Count: 3, Right.Count: 3, Up.Count: 3, Forward.Count: 3, Width: > 0, Height: > 0 }) {
            throw new StackEditRefusedException("A view is three vectors of three numbers and a picture with a size.");
        }
    }

    SetLayerCommand Named(LayerAsset before, LayerAsset after, string set) =>
        new(this, new(set, before.Id), before, after, "Name paint file");

    LayerAsset Require(string id) =>
        LayerStackEdit.Find(Document, new(Set.Name, id))
        ?? throw new StackEditRefusedException(
            string.Create(CultureInfo.InvariantCulture, $"This stack has no layer '{id}'.")
        );

    /// <summary>One undo step: what it is called, what a jump names it by, and what it did.</summary>
    public sealed record UndoStep(long Id, string Label) {
        public List<IAuthoringCommand> Commands { get; } = [];

        public List<StackJournalEntry> Strokes { get; } = [];
    }

    /// <summary>Nothing listens: a session reads the document after every edit.</summary>
    sealed class HeadlessContext : ICommandContext {
        public void Touch(IAuthoredDocument document) { }
    }
}
