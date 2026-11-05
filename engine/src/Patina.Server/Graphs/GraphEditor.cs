using Patina.Contracts;
using System.Globalization;
using Vixen.Authoring;
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
    readonly NodeTypeRegistry registry;
    readonly List<Step> undoable = [];
    readonly List<Step> redoable = [];
    long nextStep;
    long? savedStep;

    /// <summary>Takes a document as a file holds it.</summary>
    public GraphEditor(NodeGraphAsset asset, NodeTypeRegistry registry) {
        graph = NodeGraphDocument.Load(asset, out var repairs);
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
        try {
            foreach (var command in batch.Commands) {
                var built = Build(command, refs);
                built.Do(Context);
                applied.Add(built);
                if (command is GraphAddNode { Ref: { } reference } && built is AddNodeCommand added) {
                    refs[reference] = added.Node.Id.Value;
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
        return [..refs.Select(static r => new GraphCreatedNode(r.Key, r.Value))];
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
            _ => throw new GraphEditRefusedException($"Unknown command '{command.GetType().Name}'.")
        };

    AddNodeCommand BuildAdd(GraphAddNode add, Dictionary<int, int> refs) {
        if (!registry.TryGet(add.Type, out _)) {
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

        Dictionary<NodeId, Vixen.Core.Mathematics.Vector2> positions = [];
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
            _ => throw new GraphEditRefusedException($"There is no graph setting '{setting.Key}'.")
        };
        return valid
            ? new(graph, setting.Key, setting.Value)
            : throw new GraphEditRefusedException($"'{setting.Value}' is not a value for {setting.Key}.");
    }

    GraphNode Find(int id, Dictionary<int, int> refs) {
        var actual = id < 0 && refs.TryGetValue(id, out var created) ? created : id;
        return graph.TryGet(new(actual), out var node)
            ? node
            : throw new GraphEditRefusedException($"There is no node {id}.");
    }

    NodeTypeDefinition TypeOf(GraphNode node) =>
        registry.TryGet(node.Type, out var type)
            ? type
            : throw new GraphEditRefusedException($"There is no node type '{node.Type}'.");

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
