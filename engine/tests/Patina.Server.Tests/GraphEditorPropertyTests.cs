using CsCheck;
using Patina.Contracts;
using Patina.Server.Graphs;
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

    static GraphCommand RandomCommand(Random random, NodeGraphAsset document) {
        return random.Next(9) switch {
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
            _ => new GraphSetSetting("seed", $"{random.Next(1000)}")
        };
        int Node() => document.Nodes.Length == 0 ? 1 : document.Nodes[random.Next(document.Nodes.Length)].Id;
    }
}
