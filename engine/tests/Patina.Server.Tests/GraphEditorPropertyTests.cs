using CsCheck;
using Patina.Contracts;
using Patina.Server.Graphs;
using System.Globalization;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Server.Tests;

public sealed class GraphEditorPropertyTests {
    static readonly NodeTypeRegistry Registry = Catalogue();

    static readonly string[] Types =
        ["Source/Noise", "Colour/HSL", "Colour/Invert", "Filters/Blur", "Colour/Blend", "Output/Output"];

    /// <summary>
    ///     Random batches, some of which are refused, then undoing all of them, gives the original bytes; redoing all
    ///     of them gives the edited bytes. A refused batch changes nothing at any point on the way.
    /// </summary>
    [Fact]
    [Trait("Category", "Property")]
    public void Random_batches_undone_return_the_original_document() =>
        Gen.Int.Array[1, 30]
            .Sample(static batches => {
                    var editor = new GraphEditor(GraphTesting.Sample(), Registry);
                    var original = GraphDocuments.Text(editor.Document);
                    foreach (var seed in batches) {
                        var before = GraphDocuments.Text(editor.Document);
                        var random = new Random(seed);
                        try {
                            editor.Apply(RandomBatch(random, editor.Document));
                        } catch (GraphEditRefusedException) {
                            Assert.Equal(before, GraphDocuments.Text(editor.Document));
                        }
                    }

                    var edited = GraphDocuments.Text(editor.Document);
                    while (editor.Undo() is not null) { }

                    Assert.Equal(original, GraphDocuments.Text(editor.Document));
                    Assert.False(editor.History.Dirty);

                    while (editor.Redo() is not null) { }

                    Assert.Equal(edited, GraphDocuments.Text(editor.Document));
                }
            );

    /// <summary>Whatever the history did, deltas take a client from one document to the next.</summary>
    [Fact]
    [Trait("Category", "Property")]
    public void Deltas_rebuild_the_document_through_edits_and_undo() =>
        Gen.Int.Array[1, 20]
            .Sample(static batches => {
                    var editor = new GraphEditor(GraphTesting.Sample(), Registry);
                    var nodes = editor.Document.Nodes.Select(GraphDocuments.State).ToDictionary(static node => node.Id);
                    var edges = editor.Document.Edges.Select(GraphDocuments.State).ToHashSet();
                    IReadOnlyList<GraphSettingDefinition> parameters =
                        [..editor.Document.Parameters.Select(GraphDocuments.Definition)];
                    IReadOnlyList<GraphGroupState> groups = [..editor.Document.Groups.Select(GraphDocuments.State)];
                    IReadOnlyList<GraphCommentState> comments =
                        [..editor.Document.Comments.Select(GraphDocuments.State)];
                    foreach (var seed in batches) {
                        var random = new Random(seed);
                        var before = editor.Document;
                        try {
                            if (random.Next(4) == 0) {
                                editor.Undo();
                            } else {
                                editor.Apply(RandomBatch(random, before));
                            }
                        } catch (GraphEditRefusedException) {
                            continue;
                        }

                        var delta = GraphDocuments.Delta(before, editor.Document);
                        foreach (var id in delta.RemovedNodes) {
                            nodes.Remove(id);
                        }

                        foreach (var node in delta.Nodes) {
                            nodes[node.Id] = node;
                        }

                        edges.ExceptWith(delta.RemovedEdges);
                        edges.UnionWith(delta.Edges);
                        Assert.Equal(
                            editor.Document.Nodes.Select(static node => (node.Id, node.Type, node.X, node.Y)),
                            nodes.Values.OrderBy(static node => node.Id)
                                .Select(static node => (node.Id, node.Type, node.X, node.Y))
                        );
                        Assert.True(edges.SetEquals(editor.Document.Edges.Select(GraphDocuments.State)));
                        parameters = delta.Parameters ?? parameters;
                        groups = delta.Groups ?? groups;
                        comments = delta.Comments ?? comments;
                        Assert.Equal(
                            editor.Document.Parameters.Select(static p => (p.Name, p.Default, p.Kind.ToString())),
                            parameters.Select(static p => (p.Name, p.Default, p.Kind))
                        );
                        Assert.Equal(
                            editor.Document.Groups.Select(static g => $"{g.Title}:{string.Join(',', g.Nodes)}"),
                            groups.Select(static g => $"{g.Title}:{string.Join(',', g.Nodes)}")
                        );
                        Assert.Equal(editor.Document.Comments.Select(GraphDocuments.State), comments);
                    }
                }
            );

    static NodeTypeRegistry Catalogue() {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        return registry;
    }

    static GraphCommandBatch RandomBatch(Random random, NodeGraphAsset document) =>
        new(
            [..Enumerable.Range(0, random.Next(1, 4)).Select(_ => RandomCommand(random, document))],
            Merge: random.Next(3) == 0
        );

    static GraphSettingDefinition Parameter(string name, float value) =>
        new(name, value.ToString(CultureInfo.InvariantCulture), string.Empty, "Float", 0f, 1f, string.Empty, []);

    static GraphCommand RandomCommand(Random random, NodeGraphAsset document) {
        return random.Next(19) switch {
            0 => new GraphAddNode(Types[random.Next(Types.Length)], random.Next(100), random.Next(100)),
            1 => new GraphRemoveNodes([Node()]),
            2 => new GraphMoveNodes([new(Node(), random.Next(1000), random.Next(1000))]),
            3 => new GraphConnect(Node(), "Out", Node(), random.Next(2) == 0 ? "Input" : "Background"),
            4 => document.Edges.Length == 0
                ? new GraphDisconnect(Node(), "Input")
                : new GraphDisconnect(
                    document.Edges[random.Next(document.Edges.Length)].ToNode,
                    document.Edges[random.Next(document.Edges.Length)].ToPort
                ),
            5 => new GraphSetValue(Node(), random.Next(2) == 0 ? "Radius" : "Hue", [random.NextSingle()]),
            6 => new GraphSetValue(Node(), "Scale", [1f + random.Next(16)]),
            7 => new GraphSetText(Node(), "Usage", random.Next(2) == 0 ? "normal" : "roughness"),
            8 => new GraphSetSetting("seed", $"{random.Next(1000)}"),
            9 => new GraphAddParameter(Parameter($"P{random.Next(4)}", random.NextSingle())),
            10 => new GraphSetParameter(ParameterName(), Parameter($"P{random.Next(4)}", random.NextSingle())),
            11 => new GraphRemoveParameter(ParameterName()),
            12 => new GraphAddGroup("Group", [Node(), Node()]),
            13 => new GraphSetGroup(random.Next(3), "Renamed", random.Next(2) == 0 ? [Node()] : null),
            14 => new GraphRemoveGroup(random.Next(3)),
            15 => new GraphAddComment("Note", random.Next(100), random.Next(100)),
            16 => new GraphSetComment(random.Next(3), Width: random.Next(2) == 0 ? 300f : -1f, X: random.Next(100)),
            17 => new GraphRemoveComment(random.Next(3)),
            _ => new GraphSetText(Node(), "=Hue", "Wear * 2")
        };
        int Node() => document.Nodes.Length == 0 ? 1 : document.Nodes[random.Next(document.Nodes.Length)].Id;

        string ParameterName() =>
            document.Parameters.Length == 0
                ? "Wear"
                : document.Parameters[random.Next(document.Parameters.Length)].Name;
    }
}
