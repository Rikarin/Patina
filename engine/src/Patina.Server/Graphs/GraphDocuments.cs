using Patina.Contracts;
using Patina.Ipc;
using Vixen.Core.Yaml;
using Vixen.NodeGraph;

namespace Patina.Server.Graphs;

/// <summary>Between Vixen's document, the file and the contract, and what differs between two documents.</summary>
public static class GraphDocuments {
    /// <summary>Reads a document from file text.</summary>
    /// <exception cref="GraphEditRefusedException">The text is not a graph.</exception>
    public static NodeGraphAsset Parse(string yaml) {
        try {
            return YamlSerializer.Parse<NodeGraphAsset>(yaml);
        } catch (Exception e) when (e is YamlParseException or YamlBindingException) {
            throw new GraphEditRefusedException($"This is not a graph file: {e.Message}", e);
        }
    }

    /// <summary>The file's text. The same document always gives the same bytes.</summary>
    public static string Text(NodeGraphAsset document) => YamlSerializer.ToYaml(document);

    /// <summary>
    ///     A document in the order the plan's file rule asks for, so that an edit never reorders a file and undoing an
    ///     edit gives the bytes that were there before: nodes by identity, wires by the input they arrive at. Vixen's
    ///     model puts a restored wire last and a restored node wherever its dictionary has room, and neither order
    ///     means anything.
    /// </summary>
    public static NodeGraphAsset Canonical(NodeGraphAsset document) =>
        document with {
            Nodes = [
                ..document.Nodes.OrderBy(static node => node.Id)
                    .Select(static node => node with {
                            Values = new(
                                node.Values.OrderBy(static value => value.Key, StringComparer.Ordinal),
                                StringComparer.Ordinal
                            ),
                            Texts = new(
                                node.Texts.OrderBy(static text => text.Key, StringComparer.Ordinal),
                                StringComparer.Ordinal
                            )
                        }
                    )
            ],
            Edges = [
                ..document.Edges.OrderBy(static edge => edge.ToNode)
                    .ThenBy(static edge => edge.ToPort, StringComparer.Ordinal)
            ],
            Settings = new(
                document.Settings.OrderBy(static setting => setting.Key, StringComparer.Ordinal),
                StringComparer.Ordinal
            )
        };

    public static GraphNodeState State(GraphNodeAsset node) =>
        new(node.Id, node.Type, node.X, node.Y, node.Values, node.Texts);

    public static GraphEdgeState State(GraphEdgeAsset edge) =>
        new(edge.FromNode, edge.FromPort, edge.ToNode, edge.ToPort);

    public static GraphSettingDefinition Definition(GraphParameterAsset parameter) =>
        new(
            parameter.Name,
            parameter.Default,
            parameter.Summary,
            parameter.Kind.ToString(),
            Bound(parameter.Minimum),
            Bound(parameter.Maximum),
            parameter.Group,
            parameter.Accepted
        );

    public static GraphDiagnostic Diagnostic(NodeDiagnostic diagnostic) =>
        new(
            diagnostic.Id,
            diagnostic.Message,
            diagnostic.Severity.ToString(),
            diagnostic.Node.IsValid ? diagnostic.Node.Value : null,
            diagnostic.Port.Length > 0 ? diagnostic.Port : null
        );

    /// <summary>A node type for a palette, its open-ended bounds as nulls since JSON has no infinity.</summary>
    public static GraphNodeType Definition(NodeTypeDefinition type) =>
        new(
            type.Path,
            type.Title,
            type.Category,
            type.Summary,
            type.Preview,
            [
                ..type.Ports.Select(static port => new GraphPortDefinition(
                        port.Name,
                        port.Direction.ToString(),
                        port.Kind.ToString(),
                        port.Default,
                        port.Summary
                    )
                )
            ],
            [
                ..type.Settings.Select(static setting => new GraphSettingDefinition(
                        setting.Name,
                        setting.Default,
                        setting.Summary,
                        setting.Kind.ToString(),
                        Bound(setting.Minimum),
                        Bound(setting.Maximum),
                        setting.Group,
                        setting.Accepted
                    )
                )
            ]
        );

    /// <summary>What turns one document into another, whichever way the history moved.</summary>
    public static GraphDelta Delta(NodeGraphAsset before, NodeGraphAsset after) {
        var old = before.Nodes.ToDictionary(static node => node.Id);
        HashSet<int> kept = [..after.Nodes.Select(static node => node.Id)];
        HashSet<GraphEdgeAsset> oldEdges = [..before.Edges];
        HashSet<GraphEdgeAsset> newEdges = [..after.Edges];
        return new(
            [
                ..after.Nodes.Where(node => !old.TryGetValue(node.Id, out var was) || !SameNode(was, node))
                    .Select(static node => State(node))
            ],
            [..before.Nodes.Where(node => !kept.Contains(node.Id)).Select(static node => node.Id)],
            [..after.Edges.Where(edge => !oldEdges.Contains(edge)).Select(static edge => State(edge))],
            [..before.Edges.Where(edge => !newEdges.Contains(edge)).Select(static edge => State(edge))],
            after.Settings.Where(setting => before.Settings.GetValueOrDefault(setting.Key) != setting.Value)
                .ToDictionary(StringComparer.Ordinal),
            [..before.Settings.Keys.Where(key => !after.Settings.ContainsKey(key))]
        );
    }

    /// <summary>
    ///     What a Worker holding <paramref name="told" /> needs to reach <paramref name="now" />, if numbers on inputs
    ///     will do it. Null means anything else changed and it needs the whole document; empty means nothing it
    ///     computes from changed, as after a move.
    /// </summary>
    public static IReadOnlyList<GraphValue>? Values(NodeGraphAsset told, NodeGraphAsset now) {
        if (told.Nodes.Length != now.Nodes.Length
            || !told.Edges.AsSpan().SequenceEqual(now.Edges)
            || !SameTexts(told.Settings, now.Settings)) {
            return null;
        }

        List<GraphValue> values = [];
        for (var i = 0; i < now.Nodes.Length; i++) {
            var (was, node) = (told.Nodes[i], now.Nodes[i]);
            if (was.Id != node.Id
                || !string.Equals(was.Type, node.Type, StringComparison.Ordinal)
                || !SameTexts(was.Texts, node.Texts)) {
                return null;
            }

            AddChanges(values, was, node);
        }

        return values;
    }

    static void AddChanges(List<GraphValue> values, GraphNodeAsset was, GraphNodeAsset node) {
        foreach (var (port, lanes) in node.Values) {
            if (!was.Values.TryGetValue(port, out var held) || !held.AsSpan().SequenceEqual(lanes)) {
                values.Add(new(node.Id, port, lanes));
            }
        }

        values.AddRange(
            was.Values.Keys.Where(port => !node.Values.ContainsKey(port))
                .Select(port => new GraphValue(node.Id, port, null))
        );
    }

    static float? Bound(float value) => float.IsFinite(value) ? value : null;

    static bool SameNode(GraphNodeAsset a, GraphNodeAsset b) =>
        string.Equals(a.Type, b.Type, StringComparison.Ordinal)
        && a.X.Equals(b.X)
        && a.Y.Equals(b.Y)
        && SameTexts(a.Texts, b.Texts)
        && a.Values.Count == b.Values.Count
        && a.Values.All(value => b.Values.TryGetValue(value.Key, out var other)
            && value.Value.AsSpan().SequenceEqual(other)
        );

    static bool SameTexts(Dictionary<string, string> a, Dictionary<string, string> b) =>
        a.Count == b.Count && a.All(text => b.GetValueOrDefault(text.Key) == text.Value);
}
