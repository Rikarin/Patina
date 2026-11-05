using Patina.Contracts;
using Vixen.Authoring;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Server.Graphs;

/// <summary>
///     Turns a client's port commands into edits of a graph's interface, and refuses what the model would take
///     silently. A boundary node shows the list as it stands, so nothing else has to change with it.
/// </summary>
/// <param name="graph">The graph.</param>
/// <param name="typeOf">A node's type, for the far end of a wire.</param>
sealed class GraphInterfaceEdits(NodeGraphModel graph, Func<GraphNode, NodeTypeDefinition> typeOf) {
    public SetInterfaceCommand Add(GraphAddPort add) {
        var port = CheckedPort(add.Port, null);
        return new(graph, [..graph.Interface, port], null, port.Name, $"Add {port.Name}");
    }

    public SetInterfaceCommand Set(GraphSetPort set) {
        var index = PortAt(set.Name, set.Direction);
        var port = CheckedPort(set.Port, graph.Interface[index]);
        foreach (var edge in BoundaryEdges(graph, graph.Interface[index])) {
            RefuseKindChange(edge, port);
        }

        List<PortDefinition> next = [..graph.Interface];
        next[index] = port;
        return new(graph, next, graph.Interface[index], port.Name, $"Set {set.Name}");
    }

    public SetInterfaceCommand Remove(GraphRemovePort remove) {
        var index = PortAt(remove.Name, remove.Direction);
        List<PortDefinition> next = [..graph.Interface];
        next.RemoveAt(index);
        return new(graph, next, graph.Interface[index], null, $"Remove {remove.Name}");
    }

    int PortAt(string name, string direction) {
        var index = graph.Interface.FindIndex(p => string.Equals(p.Name, name, StringComparison.Ordinal)
            && string.Equals(p.Direction.ToString(), direction, StringComparison.Ordinal)
        );
        return index >= 0 ? index : throw new GraphEditRefusedException($"There is no {direction} port '{name}'.");
    }

    /// <summary>A port as the model holds it, or a refusal a client can show.</summary>
    /// <param name="port">What a client sent.</param>
    /// <param name="replacing">The port it replaces, whose name it may keep and whose direction it must.</param>
    PortDefinition CheckedPort(GraphInterfacePort port, PortDefinition? replacing) {
        if (string.IsNullOrWhiteSpace(port.Name) || TextureGraphExpressions.IsExpression(port.Name, out _)) {
            throw new GraphEditRefusedException($"'{port.Name}' cannot name a port.");
        }

        if (!Enum.TryParse<PortDirection>(port.Direction, out var direction) || !Enum.IsDefined(direction)) {
            throw new GraphEditRefusedException($"'{port.Direction}' is not a direction.");
        }

        if (replacing is not null && direction != replacing.Direction) {
            throw new GraphEditRefusedException("A port keeps its direction. Remove it and add another.");
        }

        if (!Enum.TryParse<PortKind>(port.Kind, out var kind) || !Enum.IsDefined(kind) || kind == PortKind.None) {
            throw new GraphEditRefusedException($"'{port.Kind}' is not a kind of port.");
        }

        if (graph.Interface.Exists(p => p != replacing
                && p.Direction == direction
                && string.Equals(p.Name, port.Name, StringComparison.Ordinal)
            )) {
            throw new GraphEditRefusedException($"There is a {direction} port called '{port.Name}' already.");
        }

        if (port.Default.Count > 0
            && (port.Default.Count != PortKinds.Fields(kind) || !port.Default.All(float.IsFinite))) {
            throw new GraphEditRefusedException(
                $"{port.Name} ({kind}) takes {PortKinds.Fields(kind)} finite number(s)."
            );
        }

        return new(port.Name, direction, kind, [..port.Default], port.Summary);
    }

    /// <summary>Says no to a kind that a wire on the port cannot carry, which the model would take silently.</summary>
    void RefuseKindChange(GraphEdge edge, PortDefinition port) {
        var (other, seenAs) = port.Direction == PortDirection.Input
            ? (edge.To, PortDirection.Input)
            : (edge.From, PortDirection.Output);
        if (!graph.TryGet(other.Node, out var node) || typeOf(node).Port(other.Port, seenAs) is not { } end) {
            return;
        }

        var (source, target) = seenAs == PortDirection.Input
            ? (port.Kind, end)
            : (end.Kind, port with { Direction = PortDirection.Input });
        if (!new PortFilter(source, PortDirection.Input).Accepts(target)) {
            throw new GraphEditRefusedException($"A wire on {port.Name} cannot carry {port.Kind}. Take it off first.");
        }
    }

    /// <summary>The wires on the boundary side of an interface port.</summary>
    internal static List<GraphEdge> BoundaryEdges(NodeGraphModel graph, PortDefinition port) {
        var input = port.Direction == PortDirection.Input;
        var type = input ? SubGraphs.InputType : SubGraphs.OutputType;
        return [
            ..graph.Edges.Where(edge => {
                    var end = input ? edge.From : edge.To;
                    return string.Equals(end.Port, port.Name, StringComparison.Ordinal)
                        && graph.TryGet(end.Node, out var node)
                        && string.Equals(node.Type, type, StringComparison.Ordinal);
                }
            )
        ];
    }
}

/// <summary>
///     Changes the graph's interface, and the wires on its boundary nodes with it: a renamed port keeps them, and one
///     taken away loses them, until an undo. Vixen has no command for this: its editor writes the list through a form.
/// </summary>
/// <param name="graph">The graph.</param>
/// <param name="next">The whole list.</param>
/// <param name="port">The port changing, if one is.</param>
/// <param name="renamed">What that port's wires arrive at now, or null when they go.</param>
/// <param name="name">What the history calls it.</param>
sealed class SetInterfaceCommand(
    NodeGraphModel graph,
    IReadOnlyList<PortDefinition> next,
    PortDefinition? port,
    string? renamed,
    string name
) : IAuthoringCommand {
    readonly IReadOnlyList<PortDefinition> previous = [..graph.Interface];
    readonly List<GraphEdge> taken = [];
    readonly List<GraphEdge> put = [];

    public string Name => name;

    public void Do(ICommandContext context) {
        taken.Clear();
        put.Clear();
        if (port is not null) {
            taken.AddRange(GraphInterfaceEdits.BoundaryEdges(graph, port));
        }

        foreach (var edge in taken) {
            graph.Disconnect(edge.To);
        }

        Write(next);
        if (renamed is null || port is null) {
            return;
        }

        foreach (var edge in taken) {
            var moved = port.Direction == PortDirection.Input
                ? edge with { From = edge.From with { Port = renamed } }
                : edge with { To = edge.To with { Port = renamed } };
            graph.Connect(moved.From, moved.To);
            put.Add(moved);
        }
    }

    public void Undo(ICommandContext context) {
        foreach (var edge in put) {
            graph.Disconnect(edge.To);
        }

        Write(previous);
        foreach (var edge in taken) {
            graph.Connect(edge.From, edge.To);
        }
    }

    void Write(IReadOnlyList<PortDefinition> ports) {
        graph.Interface.Clear();
        graph.Interface.AddRange(ports);
    }
}
