using Patina.Contracts;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Vixen.Authoring;
using Vixen.Core.Mathematics;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Server.Graphs;

/// <summary>
///     A command that cannot be applied, with the reason a client can show. The document has not changed.
/// </summary>
public sealed class GraphEditRefusedException : Exception {
    public GraphEditRefusedException() { }

    public GraphEditRefusedException(string message) : base(message) { }

    public GraphEditRefusedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
///     One graph document and its undo history. This is Vixen's model edited by Vixen's commands; what is Patina's is
///     turning a client's request into those commands and refusing what the model would take silently, such as a port
///     that does not exist. <c>CommandStack</c> is in Vixen's editor, with its interface dependencies, and it undoes
///     single commands, so the history here is its own short list of batches. Not thread safe: the session locks.
/// </summary>
public sealed class GraphEditor {
    const int Capacity = 256;

    static readonly HeadlessContext Context = new();

    readonly NodeGraphModel graph;
    readonly GraphInterfaceEdits ports;
    readonly GraphParameterEdits parameters;
    readonly List<Step> undoable = [];
    readonly List<Step> redoable = [];
    NodeTypeRegistry registry;
    long nextStep;
    long? savedStep;

    /// <summary>Takes a document as a file holds it.</summary>
    public GraphEditor(NodeGraphAsset asset, NodeTypeRegistry registry) {
        graph = NodeGraphDocument.Load(asset, out var repairs);
        ports = new(graph, TypeOf);
        parameters = new(graph);
        this.registry = registry;
        Repairs = repairs;
        Document = GraphDocuments.Canonical(NodeGraphDocument.Save(graph));
    }

    /// <summary>What reading the file had to drop or fix.</summary>
    public IReadOnlyList<NodeDiagnostic> Repairs { get; }

    /// <summary>The document as it stands, in canonical order.</summary>
    public NodeGraphAsset Document { get; private set; }

    public GraphHistoryState History =>
        new(
            undoable.Count > 0,
            redoable.Count > 0,
            undoable.Count > 0 ? undoable[^1].Label : null,
            redoable.Count > 0 ? redoable[^1].Label : null,
            (undoable.Count > 0 ? undoable[^1].Id : null) != savedStep
        );

    /// <summary>Applies a batch whole, or throws and leaves the document as it was.</summary>
    /// <returns>The nodes the batch created under a client's <c>ref</c>.</returns>
    /// <exception cref="GraphEditRefusedException">A command cannot be applied.</exception>
    public IReadOnlyList<GraphCreatedNode> Apply(GraphCommandBatch batch) {
        if (batch.Commands.Count == 0) {
            throw new GraphEditRefusedException("A batch needs at least one command.");
        }

        Dictionary<int, int> refs = [];
        List<IAuthoringCommand> applied = [];
        List<(int Ref, GraphGroup? Group, GraphComment? Note)> furniture = [];
        try {
            foreach (var command in batch.Commands) {
                var built = Build(command, refs);
                built.Do(Context);
                applied.Add(built);
                switch (command, built) {
                    case (GraphAddNode { Ref: { } reference }, AddNodeCommand added):
                        refs[reference] = added.Node.Id.Value;
                        break;
                    case (GraphAddGroup { Ref: { } reference }, AddGroupCommand group):
                        furniture.Add((reference, group.Group, null));
                        break;
                    case (GraphAddComment { Ref: { } reference }, AddNoteCommand note):
                        furniture.Add((reference, null, note.Note));
                        break;
                }
            }
        } catch (Exception e) when (e is GraphEditRefusedException or ArgumentException) {
            foreach (var command in Enumerable.Reverse(applied)) {
                command.Undo(Context);
            }

            throw e as GraphEditRefusedException ?? new GraphEditRefusedException(e.Message, e);
        }

        Remember(batch, applied);
        Document = GraphDocuments.Canonical(NodeGraphDocument.Save(graph));
        return [
            ..refs.Select(static r => new GraphCreatedNode(r.Key, r.Value)),
            ..furniture.Select(static f => new GraphCreatedNode(f.Ref, f.Group?.Id ?? f.Note!.Id))
        ];
    }

    /// <summary>What moving some nodes into a compound would make. Nothing changes until it is applied.</summary>
    /// <param name="name">What the compound is called.</param>
    /// <param name="nodes">The nodes.</param>
    /// <exception cref="GraphEditRefusedException">They cannot be moved.</exception>
    public SubGraphExtraction PlanExtraction(string name, IReadOnlyCollection<int> nodes) {
        var ids = nodes.Select(static node => new NodeId(node)).ToArray();
        if (nodes.Any(node => graph.TryGet(new(node), out var held) && SubGraphs.IsBoundary(held.Type))) {
            throw new GraphEditRefusedException("A boundary node belongs to this graph and cannot leave it.");
        }

        try {
            return SubGraphs.Extract(graph, ids, name, registry);
        } catch (ArgumentException e) {
            throw new GraphEditRefusedException(e.Message, e);
        }
    }

    /// <summary>Replaces the nodes with one that holds the compound, as one undo step.</summary>
    /// <param name="extraction">What <see cref="PlanExtraction" /> worked out.</param>
    /// <param name="type">The node type it was published under.</param>
    /// <param name="x">Where the node goes.</param>
    /// <param name="y">Where the node goes.</param>
    /// <returns>The node.</returns>
    public int ApplyExtraction(SubGraphExtraction extraction, string type, float x, float y) {
        ExtractSubGraphCommand command = new(graph, extraction, type, Finite(x, y));
        command.Do(Context);
        Remember(new([], $"Save as {type}"), [command]);
        Document = GraphDocuments.Canonical(NodeGraphDocument.Save(graph));
        return command.StandIn.Id.Value;
    }

    /// <summary>Takes the newest edit back.</summary>
    /// <returns>What it was called, or null when there is nothing to undo.</returns>
    public string? Undo() {
        if (undoable.Count == 0) {
            return null;
        }

        var step = undoable[^1];
        undoable.RemoveAt(undoable.Count - 1);
        foreach (var command in Enumerable.Reverse(step.Commands)) {
            command.Undo(Context);
        }

        redoable.Add(step);
        Document = GraphDocuments.Canonical(NodeGraphDocument.Save(graph));
        return step.Label;
    }

    /// <summary>Puts the newest undone edit back.</summary>
    /// <returns>What it was called, or null when there is nothing to redo.</returns>
    public string? Redo() {
        if (redoable.Count == 0) {
            return null;
        }

        var step = redoable[^1];
        redoable.RemoveAt(redoable.Count - 1);
        foreach (var command in step.Commands) {
            command.Do(Context);
        }

        undoable.Add(step);
        Document = GraphDocuments.Canonical(NodeGraphDocument.Save(graph));
        return step.Label;
    }

    /// <summary>Takes a new list of node types, after a compound changed.</summary>
    public void UseRegistry(NodeTypeRegistry types) => registry = types;

    /// <summary>
    ///     Says the document now matches the file. A later drag starts a new undo step rather than merging.
    /// </summary>
    public void MarkSaved() => savedStep = undoable.Count > 0 ? undoable[^1].Id : null;

    void Remember(GraphCommandBatch batch, List<IAuthoringCommand> applied) {
        redoable.Clear();
        if (batch.Merge && TryMerge(applied)) {
            return;
        }

        var label = batch.Label ?? (applied.Count == 1 ? applied[0].Name : $"{applied.Count} Edits");
        undoable.Add(new(nextStep++, label, applied));
        if (undoable.Count > Capacity) {
            undoable.RemoveAt(0);
        }
    }

    /// <summary>Folds a batch into the last step, keeping that step's "before" (plan: a drag is one undo).</summary>
    bool TryMerge(List<IAuthoringCommand> applied) {
        if (undoable.Count == 0 || undoable[^1].Id == savedStep || undoable[^1].Commands.Count != applied.Count) {
            return false;
        }

        var last = undoable[^1];
        List<IAuthoringCommand> merged = new(applied.Count);
        for (var i = 0; i < applied.Count; i++) {
            if (!applied[i].TryMergeWith(last.Commands[i], out var both)) {
                return false;
            }

            merged.Add(both);
        }

        undoable[^1] = last with { Id = nextStep++, Commands = merged };
        return true;
    }

    IAuthoringCommand Build(GraphCommand command, Dictionary<int, int> refs) =>
        command switch {
            GraphAddNode add => BuildAdd(add, refs),
            GraphRemoveNodes remove => BuildRemove(remove, refs),
            GraphMoveNodes move => BuildMove(move, refs),
            GraphConnect connect => BuildConnect(connect, refs),
            GraphDisconnect disconnect => BuildDisconnect(disconnect, refs),
            GraphSetValue value => BuildValue(value, refs),
            GraphSetText text => BuildText(text, refs),
            GraphSetSetting setting => BuildSetting(setting),
            GraphAddParameter add => parameters.Add(add),
            GraphSetParameter set => parameters.Set(set),
            GraphRemoveParameter remove => parameters.Remove(remove),
            GraphAddPort add => ports.Add(add),
            GraphSetPort set => ports.Set(set),
            GraphRemovePort remove => ports.Remove(remove),
            GraphAddGroup add => new AddGroupCommand(graph, add.Title, Members(add.Nodes, refs)),
            GraphSetGroup set => BuildSetGroup(set, refs),
            GraphRemoveGroup remove => new RemoveGroupCommand(graph, GroupOf(remove.Id)),
            GraphAddComment add => BuildAddComment(add),
            GraphSetComment set => BuildSetComment(set),
            GraphRemoveComment remove => new RemoveCommentCommand(graph, CommentOf(remove.Id)),
            _ => throw new GraphEditRefusedException($"Unknown command '{command.GetType().Name}'.")
        };

    AddNodeCommand BuildAdd(GraphAddNode add, Dictionary<int, int> refs) {
        if (!SubGraphs.IsBoundary(add.Type) && !registry.TryGet(add.Type, out _)) {
            throw new GraphEditRefusedException($"There is no node type '{add.Type}'.");
        }

        if (add.Ref is { } reference && (reference >= 0 || refs.ContainsKey(reference))) {
            throw new GraphEditRefusedException($"Ref {reference} is not a new negative number.");
        }

        return new(graph, add.Type, new(add.X, add.Y));
    }

    RemoveNodesCommand BuildRemove(GraphRemoveNodes remove, Dictionary<int, int> refs) {
        if (remove.Nodes.Count == 0) {
            throw new GraphEditRefusedException("Removing nothing is not an edit.");
        }

        return new(graph, [..remove.Nodes.Select(node => Find(node, refs).Id)]);
    }

    MoveNodesCommand BuildMove(GraphMoveNodes move, Dictionary<int, int> refs) {
        if (move.Positions.Count == 0) {
            throw new GraphEditRefusedException("Moving nothing is not an edit.");
        }

        Dictionary<NodeId, Vector2> positions = [];
        foreach (var position in move.Positions) {
            positions[Find(position.Node, refs).Id] = new(position.X, position.Y);
        }

        return new(graph, positions);
    }

    ConnectCommand BuildConnect(GraphConnect connect, Dictionary<int, int> refs) {
        var from = Find(connect.FromNode, refs);
        var to = Find(connect.ToNode, refs);
        var source = PortOf(from, connect.FromPort, PortDirection.Output);
        var target = PortOf(to, connect.ToPort, PortDirection.Input);
        var filter = new PortFilter(source.Kind, PortDirection.Input);
        if (!filter.Accepts(target)) {
            throw new GraphEditRefusedException(
                $"{connect.FromPort} ({source.Kind}) cannot drive {connect.ToPort} ({target.Kind})."
            );
        }

        return new(graph, new(from.Id, connect.FromPort), new(to.Id, connect.ToPort));
    }

    DisconnectCommand BuildDisconnect(GraphDisconnect disconnect, Dictionary<int, int> refs) {
        var to = new PortRef(Find(disconnect.ToNode, refs).Id, disconnect.ToPort);
        return graph.Source(to) is null
            ? throw new GraphEditRefusedException($"No wire arrives at {to}.")
            : new(graph, to);
    }

    SetPortValueCommand BuildValue(GraphSetValue value, Dictionary<int, int> refs) {
        var node = Find(value.Node, refs);
        var port = PortOf(node, value.Port, PortDirection.Input);
        var (fewest, most) = port.Kind == PortKind.Dynamic
            ? (1, 4)
            : (PortKinds.Fields(port.Kind), PortKinds.Fields(port.Kind));
        if (most == 0 || value.Value.Count < fewest || value.Value.Count > most) {
            throw new GraphEditRefusedException(
                most == 0
                    ? $"{value.Port} ({port.Kind}) takes no number."
                    : $"{value.Port} ({port.Kind}) takes {fewest} number(s), not {value.Value.Count}."
            );
        }

        return value.Value.All(float.IsFinite)
            ? new(graph, node.Id, value.Port, [..value.Value])
            : throw new GraphEditRefusedException($"{value.Port} takes finite numbers.");
    }

    SetPortTextCommand BuildText(GraphSetText text, Dictionary<int, int> refs) {
        var node = Find(text.Node, refs);
        if (TextureGraphExpressions.IsExpression(text.Port, out var input)) {
            var port = PortOf(node, input, PortDirection.Input);
            return port.Kind is PortKind.Float or PortKind.Int or PortKind.Bool
                ? new(graph, node.Id, text.Port, text.Value)
                : throw new GraphEditRefusedException($"{input} ({port.Kind}) cannot hold an expression.");
        }

        var setting = TypeOf(node).Setting(text.Port)
            ?? throw new GraphEditRefusedException($"{node.Type} has no setting '{text.Port}'.");
        return setting.Accepts(text.Value)
            ? new(graph, node.Id, text.Port, setting.Canonical(text.Value))
            : throw new GraphEditRefusedException(
                $"{text.Port} takes {string.Join(", ", setting.Accepted)}, not '{text.Value}'."
            );
    }

    SetGraphSettingCommand BuildSetting(GraphSetSetting setting) {
        var valid = setting.Key switch {
            TextureGraphSettings.BaseWidth or TextureGraphSettings.BaseHeight =>
                int.TryParse(setting.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var extent)
                && extent is > 0 and <= TexturePlan.MaxExtent,
            TextureGraphSettings.Seed =>
                uint.TryParse(setting.Value, NumberStyles.None, CultureInfo.InvariantCulture, out _),
            TextureGraphSettings.BitDepth => setting.Value is "16" or "32",
            TextureSizes.GraphSetting =>
                int.TryParse(setting.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var side)
                && TextureSizes.Sides.Contains(side),
            _ => throw new GraphEditRefusedException($"There is no graph setting '{setting.Key}'.")
        };
        return valid
            ? new(graph, setting.Key, setting.Value)
            : throw new GraphEditRefusedException($"'{setting.Value}' is not a value for {setting.Key}.");
    }

    IEnumerable<NodeId> Members(IReadOnlyList<int> nodes, Dictionary<int, int> refs) =>
        [..nodes.Distinct().Select(node => Find(node, refs).Id)];

    GraphGroup GroupOf(int id) =>
        graph.Groups.Find(group => group.Id == id) ?? throw new GraphEditRefusedException($"There is no group {id}.");

    GraphComment CommentOf(int id) =>
        graph.Comments.Find(note => note.Id == id) ?? throw new GraphEditRefusedException($"There is no note {id}.");

    SetGroupCommand BuildSetGroup(GraphSetGroup set, Dictionary<int, int> refs) {
        var group = GroupOf(set.Id);
        return new(group, set.Title ?? group.Title, [..set.Nodes is { } nodes ? Members(nodes, refs) : group.Nodes]);
    }

    AddNoteCommand BuildAddComment(GraphAddComment add) {
        GraphComment standard = new();
        return new(
            graph,
            new() {
                Text = add.Text,
                Position = Finite(add.X, add.Y),
                Size = Size(add.Width ?? standard.Size.X, add.Height ?? standard.Size.Y)
            }
        );
    }

    SetNoteCommand BuildSetComment(GraphSetComment set) {
        var comment = CommentOf(set.Id);
        return new(
            comment,
            new(
                set.Text ?? comment.Text,
                Finite(set.X ?? comment.Position.X, set.Y ?? comment.Position.Y),
                Size(set.Width ?? comment.Size.X, set.Height ?? comment.Size.Y)
            )
        );
    }

    static Vector2 Finite(float x, float y) =>
        float.IsFinite(x) && float.IsFinite(y)
            ? new(x, y)
            : throw new GraphEditRefusedException("A note's position takes finite numbers.");

    static Vector2 Size(float width, float height) =>
        float.IsFinite(width) && float.IsFinite(height) && width > 0f && height > 0f
            ? new(width, height)
            : throw new GraphEditRefusedException("A note's size takes positive numbers.");

    GraphNode Find(int id, Dictionary<int, int> refs) {
        var actual = id < 0 && refs.TryGetValue(id, out var created) ? created : id;
        return graph.TryGet(new(actual), out var node)
            ? node
            : throw new GraphEditRefusedException($"There is no node {id}.");
    }

    /// <summary>A node's type. A boundary node's ports are this graph's interface, as it stands now.</summary>
    NodeTypeDefinition TypeOf(GraphNode node) {
        if (SubGraphs.IsBoundary(node.Type)) {
            return SubGraphs.Boundary(graph, node.Type);
        }

        return registry.TryGet(node.Type, out var type)
            ? type
            : throw new GraphEditRefusedException($"There is no node type '{node.Type}'.");
    }

    PortDefinition PortOf(GraphNode node, string name, PortDirection direction) =>
        TypeOf(node).Port(name, direction)
        ?? throw new GraphEditRefusedException($"{node.Type} has no {direction} port '{name}'.");

    sealed record Step(long Id, string Label, List<IAuthoringCommand> Commands);

    /// <summary>Nothing listens: a session reads the document after every edit.</summary>
    sealed class HeadlessContext : ICommandContext {
        public void Touch(IAuthoredDocument document) { }
    }
}

/// <summary>Sets a graph setting. Vixen has no command for this: its editor writes settings through a form.</summary>
sealed class SetGraphSettingCommand(NodeGraphModel graph, string key, string value) : IAuthoringCommand {
    readonly string? previous = graph.Settings.GetValueOrDefault(key);

    public string Name => $"Set {key}";

    public void Do(ICommandContext context) => graph.Settings[key] = value;

    public void Undo(ICommandContext context) {
        if (previous is null) {
            graph.Settings.Remove(key);
        } else {
            graph.Settings[key] = previous;
        }
    }
}

/// <summary>Retitles a group and changes its members. Vixen's own command only retitles.</summary>
sealed class SetGroupCommand(GraphGroup group, string title, NodeId[] nodes) : IAuthoringCommand {
    readonly string previousTitle = group.Title;
    readonly NodeId[] previousNodes = [..group.Nodes];

    public string Name => "Edit Group";

    public void Do(ICommandContext context) => Write(title, nodes);

    public void Undo(ICommandContext context) => Write(previousTitle, previousNodes);

    void Write(string text, NodeId[] members) {
        group.Title = text;
        group.Nodes.Clear();
        group.Nodes.AddRange(members);
    }
}

/// <summary>Adds a note with a size. Vixen's <see cref="AddCommentCommand" /> takes text and a position.</summary>
sealed class AddNoteCommand(NodeGraphModel graph, GraphComment comment) : IAuthoringCommand {
    public string Name => "Add Note";

    public void Do(ICommandContext context) => graph.Comments.Add(comment);

    /// <summary>The note, whose identity the document gives it when it is next written down.</summary>
    public GraphComment Note => comment;

    public void Undo(ICommandContext context) => graph.Comments.Remove(comment);
}

/// <summary>What a note says and where. Vixen's <see cref="SetCommentCommand" /> only retypes it.</summary>
readonly record struct NoteState(string Text, Vector2 Position, Vector2 Size);

/// <summary>Changes a note, and folds a drag into one step.</summary>
sealed class SetNoteCommand(GraphComment comment, NoteState next, NoteState? before = null) : IAuthoringCommand {
    readonly NoteState previous = before ?? new(comment.Text, comment.Position, comment.Size);

    public string Name => "Edit Note";

    public void Do(ICommandContext context) => Write(next);

    public void Undo(ICommandContext context) => Write(previous);

    public bool TryMergeWith(IAuthoringCommand earlierCommand, [NotNullWhen(true)] out IAuthoringCommand? merged) {
        merged = earlierCommand is SetNoteCommand earlier && earlier.IsFor(comment)
            ? new SetNoteCommand(comment, next, earlier.previous)
            : null;
        return merged is not null;
    }

    bool IsFor(GraphComment note) => ReferenceEquals(note, comment);

    void Write(NoteState state) {
        comment.Text = state.Text;
        comment.Position = state.Position;
        comment.Size = state.Size;
    }
}
