using Patina.Contracts;
using Vixen.Authoring;
using Vixen.Core.Mathematics;
using Vixen.NodeGraph;

namespace Patina.Server.Graphs;

/// <summary>
///     The edits that shape how a graph is authored rather than what it computes: switching nodes off, removing them
///     with their chain kept joined, laying them out, and its presets. Apart from <see cref="GraphEditor" /> so that
///     the editor's dispatch stays one table of the document's own commands.
/// </summary>
/// <param name="graph">The document.</param>
/// <param name="parameters">Its parameter edits, which presets set.</param>
/// <param name="editor">The editor, for finding nodes by a client's numbers and for their types.</param>
sealed class GraphAuthoringEdits(NodeGraphModel graph, GraphParameterEdits parameters, GraphEditor editor) {
    /// <summary>Whether a node type is a reroute dot, which no registry holds and a graph may still add.</summary>
    public static bool IsDot(string type) => Reroutes.IsReroute(type);

    /// <summary>A reroute dot's ports, carrying whatever is wired to it, or null for any other node.</summary>
    public NodeTypeDefinition? DotOf(GraphNode node) =>
        Reroutes.IsReroute(node.Type) ? Reroutes.Definition(Reroutes.KindOf(graph, node.Id, editor.TypeOrNull)) : null;

    /// <exception cref="GraphEditRefusedException">The command is none of these, or cannot be applied.</exception>
    public IAuthoringCommand Build(GraphCommand command, Dictionary<int, int> refs) =>
        command switch {
            GraphRemoveNodes { Relink: true } remove => BuildRelink(remove, refs),
            GraphSetBypass bypass => BuildBypass(bypass, refs),
            GraphArrange arrange => BuildArrange(arrange, refs),
            GraphSavePreset save => parameters.SavePreset(save),
            GraphRemovePreset remove => parameters.RemovePreset(remove),
            GraphApplyPreset apply => parameters.ApplyPreset(apply),
            GraphSetInfo info => BuildInfo(info),
            _ => throw new GraphEditRefusedException($"Unknown command '{command.GetType().Name}'.")
        };

    /// <summary>What the graph says about itself, with what a client left out kept as it is.</summary>
    /// <exception cref="GraphEditRefusedException">The physical size is not a width and a height above zero.</exception>
    SetInfoCommand BuildInfo(GraphSetInfo set) {
        var held = graph.Info;
        var size = held.PhysicalSize;
        if (set.PhysicalWidth is not null || set.PhysicalHeight is not null) {
            var (width, height) = (set.PhysicalWidth ?? 0f, set.PhysicalHeight ?? 0f);
            size = (width, height) switch {
                (0f, 0f) => default,
                _ when float.IsFinite(width) && float.IsFinite(height) && width > 0f && height > 0f => new(width, height),
                _ => throw new GraphEditRefusedException(
                    "A physical size is a width and a height above zero, in centimetres, or both zero for none."
                )
            };
        }

        List<string> tags = [];
        foreach (var tag in set.Tags ?? held.Tags) {
            var word = tag.Trim();
            if (word.Length > 0 && !tags.Contains(word, StringComparer.OrdinalIgnoreCase)) {
                tags.Add(word);
            }
        }

        return new(
            graph,
            new(set.Description?.Trim() ?? held.Description, set.Author?.Trim() ?? held.Author, tags, size)
        );
    }

    /// <summary>Removes nodes and wires what fed them to what they fed, as one command so it undoes as one.</summary>
    CompositeCommand BuildRelink(GraphRemoveNodes remove, Dictionary<int, int> refs) {
        if (remove.Nodes.Count == 0) {
            throw new GraphEditRefusedException("Removing nothing is not an edit.");
        }

        RemoveNodesCommand removal = new(graph, [..remove.Nodes.Select(node => editor.Find(node, refs).Id)]);
        HashSet<NodeId> removing = [..remove.Nodes.Select(node => editor.Find(node, refs).Id)];
        var bridges = NodeBypass.Bridges(graph, removing, editor.TypeOrNull);
        return new(
            remove.Nodes.Count == 1 ? "Delete and Relink" : $"Delete and Relink {remove.Nodes.Count} Nodes",
            [removal, ..bridges.Select(bridge => new ConnectCommand(graph, bridge.From, bridge.To))]
        );
    }

    SetBypassCommand BuildBypass(GraphSetBypass bypass, Dictionary<int, int> refs) {
        if (bypass.Nodes.Count == 0) {
            throw new GraphEditRefusedException("Switching nothing off is not an edit.");
        }

        GraphNode[] nodes = [..bypass.Nodes.Distinct().Select(node => editor.Find(node, refs))];
        if (nodes.FirstOrDefault(static node => SubGraphs.IsBoundary(node.Type) || Reroutes.IsReroute(node.Type)
            ) is { } fixture) {
            throw new GraphEditRefusedException($"{fixture.Type} has nothing to switch off.");
        }

        return new(nodes, bypass.Bypassed);
    }

    /// <summary>
    ///     Vixen's layered layout, over the whole graph or over a block of nodes that keeps its top-left corner. The
    ///     sizes are Patina's cards: wider than Vixen's, and a preview square under the title.
    /// </summary>
    MoveNodesCommand BuildArrange(GraphArrange arrange, Dictionary<int, int> refs) {
        var block = graph;
        if (arrange.Nodes.Count > 0) {
            HashSet<NodeId> chosen = [..arrange.Nodes.Select(node => editor.Find(node, refs).Id)];
            block = new();
            foreach (var node in graph.Nodes.Where(node => chosen.Contains(node.Id))) {
                block.Add(node.Id, node.Type, node.Position);
            }

            foreach (var edge in graph.Edges.Where(edge => chosen.Contains(edge.From.Node)
                         && chosen.Contains(edge.To.Node)
                     )) {
                block.Connect(edge.From, edge.To);
            }
        }

        if (block.Nodes.Count == 0) {
            throw new GraphEditRefusedException("There is nothing to lay out.");
        }

        var corner = new Vector2(
            block.Nodes.Min(static node => node.Position.X),
            block.Nodes.Min(static node => node.Position.Y)
        );
        var placed = NodeGraphLayout.Arrange(block, editor.Registry, Layout with { Origin = corner });
        // Named as Vixen's LayoutCommand names it, and never folded into a drag that came before it.
        return new(graph, new Dictionary<NodeId, Vector2>(placed), null, "Auto-Layout", false);
    }

    /// <summary>
    ///     A Patina node card: 176 wide, a 30-high title, a preview as tall as the card is wide, 24 per port row.
    /// </summary>
    static readonly NodeLayoutOptions Layout = new(default, 96f, 32f, 176f, 30f + 176f, 24f, 12f);
}

/// <summary>Switches nodes off or on, remembering what each was.</summary>
sealed class SetBypassCommand(GraphNode[] nodes, bool bypassed) : IAuthoringCommand {
    readonly bool[] previous = [..nodes.Select(static node => node.Bypassed)];

    public string Name => bypassed ? "Bypass" : "Stop Bypassing";

    public void Do(ICommandContext context) {
        foreach (var node in nodes) {
            node.Bypassed = bypassed;
        }
    }

    public void Undo(ICommandContext context) {
        for (var i = 0; i < nodes.Length; i++) {
            nodes[i].Bypassed = previous[i];
        }
    }
}

/// <summary>Several commands that are one edit, done in order and undone the other way.</summary>
sealed class CompositeCommand(string name, IReadOnlyList<IAuthoringCommand> commands) : IAuthoringCommand {
    public string Name => name;

    public void Do(ICommandContext context) {
        foreach (var command in commands) {
            command.Do(context);
        }
    }

    public void Undo(ICommandContext context) {
        foreach (var command in commands.Reverse()) {
            command.Undo(context);
        }
    }
}

/// <summary>Replaces what a graph says about itself. Nothing it holds is computed from, so it never evaluates.</summary>
sealed class SetInfoCommand(NodeGraphModel graph, GraphInfo info) : IAuthoringCommand {
    readonly GraphInfo previous = graph.Info;

    public string Name => "Describe Graph";

    public void Do(ICommandContext context) => graph.Info = info;

    public void Undo(ICommandContext context) => graph.Info = previous;
}
