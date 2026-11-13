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
/// <param name="path">Its file.</param>
/// <param name="document">The document, as a file holds it.</param>
public sealed class StackEditor(string path, LayerStackAsset document) : ILayerStackDocument {
    static readonly HeadlessContext Context = new();

    readonly List<UndoStep> undoable = [];
    readonly List<UndoStep> redoable = [];
    long nextEntry;
    long nextStep;
    long? savedStep;
    bool extendedSinceSave;

    /// <summary>
    ///     What size each canvas is as a Worker holds it, which a stroke is checked against before it begins.
    /// </summary>
    readonly StackCanvasSides canvasSides = new(Path.GetDirectoryName(path)!);

    /// <summary>The library layered materials are read from, or null with none.</summary>
    public string? Library { get; set; }

    /// <summary>What the viewport draws alone as the next batch arrives, which a stroke on the mesh keeps.</summary>
    public ViewportIsolation? Isolation { get; set; }

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

    public void MarkSaved() {
        savedStep = undoable.Count > 0 ? undoable[^1].Id : null;
        extendedSinceSave = false;
    }

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
            default:
                if (!StackLayerEdits.Run(this, command, set, Do) && !StackSetEdits.Run(this, command, Do)) {
                    throw new StackEditRefusedException($"Unknown command '{command.GetType().Name}'.");
                }

                break;
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
            { Mask: true, MaskLayer: { } entry } => (StackLayerEdits.MaskLayerCanvas(this, layer, entry, set, run),
                null),
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

        // A Worker refuses to begin one its canvas cannot take at this size, and the stack would fail until it is
        // undone.
        var (width, height) = canvasSides.Check(
            layer.Name,
            paint,
            Document.BaseWidth,
            Document.BaseHeight,
            stroke.View is not null
        );
        // A brush sized in texels keeps its size on the model when it paints a larger canvas at its own (#19).
        var painted = width == Document.BaseWidth || stroke.Brush.SizeSpace != "texture"
            ? stroke
            : stroke with { Brush = stroke.Brush with { Radius = stroke.Brush.Radius * width / Document.BaseWidth } };
        step.Strokes.Add(
            new(nextEntry++, painted, paint, Channel(stroke), Packed(stroke.Colour), Document.Model, start) {
                SourceDocument = StackDocuments.SourceText(stroke.Source, Document, Set),
                Width = width,
                Height = height,
                // Only a stroke on the mesh has faces to keep to; one in the 2D view reaches the whole layout.
                Isolation = stroke.View is null ? null : Isolation
            }
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

        var channel = StackDocuments.ChannelName(stroke.Channel);
        // A layer keeps what it held for a channel taken off the set (#141), so it may still write it (#261).
        if (Set.Channels.TrueForAll(held => held.Usage != stroke.Channel)) {
            throw new StackEditRefusedException(
                $"This document has no {channel} channel.",
                Api.Refusals.ChannelMissing,
                new Dictionary<string, string> { ["channel"] = channel }
            );
        }

        if (!layer.Writes(stroke.Channel)) {
            throw new StackEditRefusedException(
                $"'{layer.Name}' does not write {channel}, so a stroke there would never show."
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

    /// <summary>
    ///     The canvas of a mask, and what it starts as if this is the stroke that makes one. A mask that is a picture
    ///     stays one, live, and the brush paints a paint layer of the mask's own stack over it (#72, #109).
    /// </summary>
    (string Paint, uint? Start) MaskCanvas(LayerAsset layer, string set, Action<IAuthoringCommand> run) {
        if (StackLayerEdits.CanvasOverMask(this, layer, set, run) is { } over) {
            return (over, null);
        }

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
    ///     The one number a flat mask stood at, which is what a canvas taking its place starts as, so that a stroke
    ///     changes only what it touches. A paint mask nobody has painted reads nothing, and so hides nothing.
    /// </summary>
    static float Showed(LayerAsset layer) => layer.Mask.Source == LayerMaskSource.Constant ? layer.Mask.Value : 1f;

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

    internal void Journal(StackLayerCopies copies, UndoStep step) {
        foreach (var (from, to) in copies.Canvases) {
            step.Strokes.Add(
                StackJournalEntry.Copy(nextEntry++, to, from, Document.Model, Document.BaseWidth, Document.BaseHeight)
            );
        }
    }

    /// <summary>Refuses what a Worker could not do with a stroke, while nothing has happened yet.</summary>
    void Aimed(StackStroke stroke) {
        if (stroke.Curve is not ("free" or "line" or "path" or "fill")) {
            throw new StackEditRefusedException($"'{stroke.Curve}' is not a kind of stroke.");
        }

        if ((stroke.Symmetry?.Problem() ?? stroke.PaintProblem()) is { } problem) {
            throw new StackEditRefusedException(problem, Api.Refusals.BrushRefused, Api.Refusals.Reason(problem));
        }

        // A symmetry, a view, a size in the model's units, a polygon fill and a material laid in the model's space are
        // all on the mesh.
        var onMesh = stroke.View is not null
            || stroke.Symmetry is not null
            || stroke.Brush.SizeSpace == "model"
            || stroke.Curve == "fill"
            || stroke.Source is { Kind: "material", Projection: not (null or "Uv") };
        if (onMesh && Document.Model.Trim().Length == 0) {
            throw new StackEditRefusedException("This document has no mesh to paint on.", Api.Refusals.NoMesh);
        }

        if (stroke.View is not { } view) {
            return;
        }

        if (view is not { Eye.Count: 3, Right.Count: 3, Up.Count: 3, Forward.Count: 3, Width: > 0, Height: > 0 }) {
            throw new StackEditRefusedException("A view is three vectors of three numbers and a picture with a size.");
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

        public List<StackJournalEntry> Strokes { get; } = [];
    }

    /// <summary>Nothing listens: a session reads the document after every edit.</summary>
    sealed class HeadlessContext : ICommandContext {
        public void Touch(IAuthoredDocument document) { }
    }
}
