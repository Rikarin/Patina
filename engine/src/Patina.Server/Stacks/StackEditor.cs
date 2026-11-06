using Patina.Contracts;
using Patina.Server.Graphs;
using System.Globalization;
using Vixen.Authoring;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Server.Stacks;

/// <summary>A command that cannot be applied, with the reason a client can show. Nothing has changed.</summary>
public sealed class StackEditRefusedException : Exception {
    public StackEditRefusedException() { }

    public StackEditRefusedException(string message) : base(message) { }

    public StackEditRefusedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>A stroke as the journal holds it: where the brush went, in order, which is all a Worker needs.</summary>
public sealed class StackJournalEntry(
    long id,
    StackStroke first,
    string paint,
    string usage,
    uint colour,
    string model
) {
    public long Id { get; } = id;

    public string Stroke { get; } = first.Stroke;

    public string Layer { get; } = first.Layer;

    public string Paint { get; } = paint;

    public string Usage { get; } = usage;

    public StackBrush Brush { get; } = first.Brush;

    public uint Colour { get; } = colour;

    /// <summary>The mesh that stood when this was painted: a later binding must not move it.</summary>
    public string Model { get; } = model;

    public StackView? View { get; } = first.View;

    public string Curve { get; } = first.Curve;

    public string? Mirror { get; } = first.Mirror;

    public List<StackStamp> Stamps { get; } = [..first.Stamps];

    public bool Open { get; set; } = first.Open;
}

/// <summary>
///     One layer stack and its undo history. The document is Vixen's and so are the commands. Strokes are not pixels
///     here: the Server keeps where each brush went, and that journal is what the pixels are a function of, so a Worker
///     that dies loses nothing that cannot be stamped again (ADR 0009). Not thread safe: the session locks.
/// </summary>
public sealed class StackEditor : ILayerStackDocument {
    static readonly HeadlessContext Context = new();

    readonly List<Step> undoable = [];
    readonly List<Step> redoable = [];
    long nextEntry;
    long nextStep;
    long? savedStep;

    /// <summary>Takes a document as a file holds it.</summary>
    /// <param name="path">Its file.</param>
    /// <param name="document">The document.</param>
    public StackEditor(string path, LayerStackAsset document) {
        AssetPath = path;
        Document = document;
    }

    /// <summary>The library layered materials are read from, or null with none.</summary>
    public string? Library { get; set; }

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
            (undoable.Count > 0 ? undoable[^1].Id : null) != savedStep
        );

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
            return extended;
        }

        EndDrag();
        var step = new Step(nextStep + 1, batch.Label ?? Label(batch.Commands[0]));
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
        return [..step.Strokes.Select(static stroke => new StackPainted(stroke.Layer, stroke.Usage))];
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

    public void MarkSaved() => savedStep = undoable.Count > 0 ? undoable[^1].Id : null;

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
            StackStroke => "Stroke",
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

    void Run(StackCommand command, Step step, List<IAuthoringCommand> applied) {
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
                Do(Change(change, set));
                break;
            case StackNestLayer nest:
                Nest(nest, set, Do);
                break;
            case StackSetModel model:
                Do(new SetModelCommand(this, model.Model, "Bind mesh"));
                break;
            case StackStroke stroke:
                Stroke(stroke, step, set, Do);
                break;
            default:
                throw new StackEditRefusedException($"Unknown command '{command.GetType().Name}'.");
        }
    }

    void Apply(string source, string set, Action<IAuthoringCommand> run) {
        var material = MaterialShelf.Read(Library, source);
        var applied = SmartMaterial.Prepare(Set, material);
        if (applied.Layers.IsDefaultOrEmpty) {
            throw new StackEditRefusedException(applied.Status);
        }

        run(new ApplySmartMaterialCommand(this, set, applied.Layers, $"Apply {material.Name}"));
    }

    AddLayerCommand Add(StackAddLayer add, string set) {
        if (!Enum.TryParse<LayerKind>(add.Kind, true, out var kind)) {
            throw new StackEditRefusedException($"'{add.Kind}' is not a kind of layer.");
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
                Name = add.Name,
                Kind = kind,
                Filter = filter,
                Values = Copied(add.Values) ?? []
            },
            $"Add {add.Name}"
        );
    }

    /// <summary>The slot at the top of a group, or of the stack.</summary>
    LayerSlot Top(string set, string? parent) {
        if (parent is null) {
            return new(set, null, Set.Layers.Count);
        }

        var group = Require(parent);
        return group.Kind == LayerKind.Group
            ? new(set, parent, group.Children.Count)
            : throw new StackEditRefusedException($"'{group.Name}' is not a group.");
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

    /// <summary>A mask as asked for, keeping the canvas it may already paint.</summary>
    static MaskAsset Masked(MaskAsset held, StackMask? mask) =>
        mask is null
            ? held
            : held with {
                Source = Parsed(mask.Source, held.Source, "mask source"),
                Value = mask.Value,
                Asset = mask.Asset,
                Anchor = mask.Anchor,
                Generator = mask.Generator,
                Map = mask.Map,
                Effects = StackDocuments.WithSelection(held.Effects, mask)
            };

    SetLayerCommand Change(StackSetLayer change, string set) {
        var layer = Require(change.Layer);
        if (change.Opacity is < 0f or > 1f) {
            throw new StackEditRefusedException("An opacity is from 0 to 1.");
        }

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
                Projection = Parsed(change.Projection, layer.Projection, "projection"),
                PlanarAxis = Parsed(change.PlanarAxis, layer.PlanarAxis, "axis"),
                Filter = Parsed(change.Filter, layer.Filter, "filter"),
                Settings = Merged(layer.Settings, Copied(change.Settings)),
                Mask = Masked(layer.Mask, change.Mask)
            },
            "Change layer"
        );
    }

    void Stroke(StackStroke stroke, Step step, string set, Action<IAuthoringCommand> run) {
        var layer = Require(stroke.Layer);
        if (layer.Kind != LayerKind.Paint) {
            throw new StackEditRefusedException(
                $"'{layer.Name}' is a {layer.Kind} layer, and only a paint layer takes a brush."
            );
        }

        if (stroke.Brush.Radius <= 0f) {
            throw new StackEditRefusedException("A brush has a radius.");
        }

        Aimed(stroke);

        if (!stroke.Mask && Set.Channels.TrueForAll(channel => channel.Usage != stroke.Channel)) {
            throw new StackEditRefusedException($"This stack has no channel '{stroke.Channel}'.");
        }

        var stem = Path.GetFileNameWithoutExtension(AssetPath);
        string paint;
        if (stroke.Mask) {
            paint = layer.Mask.Paint.Trim();
            if (paint.Length == 0) {
                paint = LayerPaint.NameFor(stem, set, layer.Id, true);
                run(
                    Named(
                        layer,
                        layer with { Mask = layer.Mask with { Source = LayerMaskSource.Paint, Paint = paint } },
                        set
                    )
                );
            }
        } else {
            paint = layer.Paint.Trim();
            if (paint.Length == 0) {
                // The first stroke writes the name down, and undoing it takes the name back with the pixels.
                paint = LayerPaint.NameFor(stem, set, layer.Id);
                run(Named(layer, layer with { Paint = paint }, set));
            }
        }

        // ⚠ Live, two strokes of one batch take turns; replayed, they run one after the other. Where they meet in one
        // channel that is two pictures, and a journal has to be one.
        if (step.Strokes.Exists(other => other.Paint == paint && other.Usage == Channel(stroke))) {
            throw new StackEditRefusedException("One batch paints one channel of a layer once.");
        }

        step.Strokes.Add(new(nextEntry++, stroke, paint, Channel(stroke), Packed(stroke.Colour), Document.Model));
    }

    /// <summary>Refuses what a Worker could not do with a stroke, while nothing has happened yet.</summary>
    void Aimed(StackStroke stroke) {
        if (stroke.Curve is not ("free" or "line" or "path")) {
            throw new StackEditRefusedException($"'{stroke.Curve}' is not a kind of stroke.");
        }

        if (stroke.Mirror is not (null or "x" or "y" or "z")) {
            throw new StackEditRefusedException($"'{stroke.Mirror}' is not a plane to mirror through.");
        }

        if (stroke.Brush.Rotation is not ("fixed" or "along" or "random")) {
            throw new StackEditRefusedException($"'{stroke.Brush.Rotation}' is not a way for a brush to turn.");
        }

        if (stroke.View is not { } view) {
            if (stroke.Mirror is not null) {
                throw new StackEditRefusedException(
                    "Only a stroke with a view can be mirrored: a mirror is on the mesh."
                );
            }

            return;
        }

        if (Document.Model.Trim().Length == 0) {
            throw new StackEditRefusedException("This stack has no mesh to paint on. Bind one first.");
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

    sealed record Step(long Id, string Label) {
        public List<IAuthoringCommand> Commands { get; } = [];

        public List<StackJournalEntry> Strokes { get; } = [];
    }

    /// <summary>Nothing listens: a session reads the document after every edit.</summary>
    sealed class HeadlessContext : ICommandContext {
        public void Touch(IAuthoredDocument document) { }
    }
}
