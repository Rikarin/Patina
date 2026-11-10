using Patina.Contracts;
using Patina.Graphs;
using Patina.Ipc;
using System.Globalization;
using Vixen.Core.Yaml;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Server.Graphs;

/// <summary>Between Vixen's document, the file and the contract, and what differs between two documents.</summary>
public static class GraphDocuments {
    /// <summary>
    ///     What every new graph is authored at, whatever size it starts at, so that a radius means one thing across a
    ///     library and in every compound.
    /// </summary>
    const string NewGraphBase = "1024";

    /// <summary>Reads a document from file text.</summary>
    /// <exception cref="GraphEditRefusedException">The text is not a graph.</exception>
    public static NodeGraphAsset Parse(string yaml) {
        try {
            return YamlSerializer.Parse<NodeGraphAsset>(yaml);
        } catch (Exception e) when (e is YamlParseException or YamlBindingException) {
            throw new GraphEditRefusedException($"This is not a graph file: {e.Message}", e);
        }
    }

    /// <summary>A new, empty graph, its settings written down so that it means the same on every machine.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="side">The side it evaluates at, or null for the usual one.</param>
    public static NodeGraphAsset New(string name, int? side = null) =>
        new() {
            Name = name.Trim(),
            Settings = new(StringComparer.Ordinal) {
                [TextureGraphSettings.BaseHeight] = NewGraphBase,
                [TextureGraphSettings.BaseWidth] = NewGraphBase,
                [TextureGraphSettings.Seed] = "0",
                [TextureSizes.GraphSetting] = (side ?? TextureSizes.Default).ToString(CultureInfo.InvariantCulture)
            }
        };

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
        new(node.Id, node.Type, node.X, node.Y, node.Values, node.Texts, node.Bypassed);

    public static GraphEdgeState State(GraphEdgeAsset edge) =>
        new(edge.FromNode, edge.FromPort, edge.ToNode, edge.ToPort);

    /// <summary>
    ///     A frame as clients see it. A colour no other reader could draw, such as a theme's token, is none.
    /// </summary>
    public static GraphGroupState State(GraphGroupAsset group) =>
        new(group.Id, group.Title, group.Nodes, FrameColour(group.Color) ?? string.Empty, group.Description);

    /// <summary>
    ///     A frame's colour as a file holds it: <c>#rrggbb</c> in lower case, or empty for the client's own choice.
    ///     Any client can draw that, where a CSS token, a name or <c>rgb()</c> would need a browser and a stylesheet
    ///     (#225).
    /// </summary>
    /// <returns>The colour, or null when <paramref name="text" /> is not one.</returns>
    public static string? FrameColour(string text) {
        var colour = text.Trim();
        return colour.Length == 0
            ? colour
            : colour.Length == 7 && colour[0] == '#' && colour[1..].All(char.IsAsciiHexDigit)
                ? colour.ToLowerInvariant()
                : null;
    }

    /// <summary>What a document says about itself, as clients see it.</summary>
    public static GraphInfoState Info(NodeGraphAsset document) => GraphFactsOf.Info(document);

    static bool SameInfo(NodeGraphAsset a, NodeGraphAsset b) =>
        string.Equals(a.Description, b.Description, StringComparison.Ordinal)
        && string.Equals(a.Author, b.Author, StringComparison.Ordinal)
        && a.Tags.AsSpan().SequenceEqual(b.Tags)
        && a.PhysicalSize.AsSpan().SequenceEqual(b.PhysicalSize);

    public static GraphPresetState State(GraphPresetAsset preset) => new(preset.Name, preset.Values);

    public static GraphPresetState State(GraphPreset preset) => new(preset.Name, preset.Values);

    public static GraphCommentState State(GraphCommentAsset comment) =>
        new(comment.Id, comment.Text, comment.X, comment.Y, comment.Width, comment.Height);

    public static GraphInterfacePort State(GraphPortAsset port) =>
        new(port.Name, port.Direction.ToString(), port.Kind.ToString(), port.Default, port.Summary);

    public static GraphSettingDefinition Definition(GraphParameterAsset parameter) =>
        new(
            parameter.Name,
            parameter.Default,
            parameter.Summary,
            parameter.Kind.ToString(),
            Bound(parameter.Minimum),
            Bound(parameter.Maximum),
            parameter.Group,
            parameter.Accepted,
            Terms.LabelsOf(parameter.Accepted),
            parameter.Label.Length > 0 ? parameter.Label : null,
            parameter.Widget == SettingWidget.Default ? null : parameter.Widget.ToString(),
            parameter.VisibleIf.Length > 0
                ? new(parameter.VisibleIf, parameter.VisibleValue, parameter.VisibleNegated)
                : null
        );

    public static GraphDiagnostic Diagnostic(NodeDiagnostic diagnostic) => GraphFactsOf.Diagnostic(diagnostic);

    /// <summary>A node type for a palette, its open-ended bounds as nulls since JSON has no infinity.</summary>
    public static GraphNodeType Definition(NodeTypeDefinition type) =>
        new(
            type.Path,
            type.Title,
            type.Category,
            type.Summary,
            type.Preview,
            [
                ..type.Ports.Select(Definition)
            ],
            [
                ..type.Settings.Select(Definition)
            ]
        );

    /// <summary>What a client is told about a port of a node.</summary>
    public static GraphPortDefinition Definition(PortDefinition port) =>
        new(port.Name, port.Direction.ToString(), port.Kind.ToString(), port.Default, port.Summary, RangeOf(port));

    /// <summary>What a client is told about a setting of a node, a compound or a generator.</summary>
    public static GraphSettingDefinition Definition(SettingDefinition setting) =>
        GraphFactsOf.Definition(setting, Terms.LabelsOf);

    static GraphPortRange? RangeOf(PortDefinition port) {
        GraphPortRange range = new(
            Bound(port.Minimum),
            Bound(port.Maximum),
            Bound(port.SoftMinimum),
            Bound(port.SoftMaximum)
        );
        return range == new GraphPortRange(null, null, null, null) ? null : range;
    }

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
            [..before.Settings.Keys.Where(key => !after.Settings.ContainsKey(key))],
            SameParameters(before, after) ? null : [..after.Parameters.Select(Definition)],
            SameGroups(before, after) ? null : [..after.Groups.Select(static group => State(group))],
            before.Comments.AsSpan().SequenceEqual(after.Comments)
                ? null
                : [..after.Comments.Select(static comment => State(comment))],
            SameInterface(before, after) ? null : [..after.Interface.Select(static port => State(port))],
            SamePresets(before, after) ? null : [..after.Presets.Select(static preset => State(preset))],
            SameInfo(before, after) ? null : Info(after)
        );
    }

    /// <summary>
    ///     What a Worker holding <paramref name="told" /> needs to reach <paramref name="now" />, if numbers and texts
    ///     on nodes will do it. Null means anything else changed and it needs the whole document; empty means nothing
    ///     it computes from changed, as after a move. A text its compiler cannot refresh from, it compiles.
    /// </summary>
    public static IReadOnlyList<GraphValue>? Values(NodeGraphAsset told, NodeGraphAsset now) {
        if (told.Nodes.Length != now.Nodes.Length
            || !told.Edges.AsSpan().SequenceEqual(now.Edges)
            || !SameTexts(told.Settings, now.Settings)
            || !SameParameters(told, now)) {
            return null;
        }

        List<GraphValue> values = [];
        for (var i = 0; i < now.Nodes.Length; i++) {
            var (was, node) = (told.Nodes[i], now.Nodes[i]);
            // Switching a node off changes the graph the compiler walks, so it is never a number.
            if (was.Id != node.Id
                || !string.Equals(was.Type, node.Type, StringComparison.Ordinal)
                || was.Bypassed != node.Bypassed
                || LostAText(was, node)) {
                return null;
            }

            AddChanges(values, was, node);
        }

        return values;
    }

    static bool LostAText(GraphNodeAsset was, GraphNodeAsset node) =>
        was.Texts.Keys.Any(key => !node.Texts.ContainsKey(key));

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
        values.AddRange(
            node.Texts.Where(text => was.Texts.GetValueOrDefault(text.Key) != text.Value)
                .Select(text => new GraphValue(node.Id, text.Key, null, text.Value))
        );
    }

    static float? Bound(float value) => float.IsFinite(value) ? value : null;

    static bool SameParameters(NodeGraphAsset a, NodeGraphAsset b) =>
        a.Parameters.Length == b.Parameters.Length
        && a.Parameters.Zip(b.Parameters)
            .All(static pair => pair.First with { Accepted = [] } == pair.Second with { Accepted = [] }
                && pair.First.Accepted.AsSpan().SequenceEqual(pair.Second.Accepted)
            );

    static bool SameInterface(NodeGraphAsset a, NodeGraphAsset b) =>
        a.Interface.Length == b.Interface.Length
        && a.Interface.Zip(b.Interface)
            .All(static pair => pair.First with { Default = [] } == pair.Second with { Default = [] }
                && pair.First.Default.AsSpan().SequenceEqual(pair.Second.Default)
            );

    static bool SameGroups(NodeGraphAsset a, NodeGraphAsset b) =>
        a.Groups.Length == b.Groups.Length
        && a.Groups.Zip(b.Groups)
            .All(static pair => pair.First.Id == pair.Second.Id
                && string.Equals(pair.First.Title, pair.Second.Title, StringComparison.Ordinal)
                && string.Equals(pair.First.Color, pair.Second.Color, StringComparison.Ordinal)
                && string.Equals(pair.First.Description, pair.Second.Description, StringComparison.Ordinal)
                && pair.First.Nodes.AsSpan().SequenceEqual(pair.Second.Nodes)
            );

    static bool SamePresets(NodeGraphAsset a, NodeGraphAsset b) =>
        a.Presets.Length == b.Presets.Length
        && a.Presets.Zip(b.Presets)
            .All(static pair => string.Equals(pair.First.Name, pair.Second.Name, StringComparison.Ordinal)
                && SameTexts(pair.First.Values, pair.Second.Values)
            );

    static bool SameNode(GraphNodeAsset a, GraphNodeAsset b) =>
        string.Equals(a.Type, b.Type, StringComparison.Ordinal)
        && a.Bypassed == b.Bypassed
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
