using Patina.Server.Graphs;
using Patina.Server.Stacks;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

/// <summary>
///     A graph beside a texture set that reads the set's paint is evaluated again as the set's strokes reach the
///     Worker and as its painting ends, so a painted mask feeds the graph as it is painted (#205). A graph that reads
///     no paint, or reads paint elsewhere, is left alone.
/// </summary>
public sealed class StackGraphPaintTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-graph-paint-");

    public void Dispose() => folder.Delete(true);

    [Fact]
    public void Painting_a_set_evaluates_the_graphs_beside_it_that_read_its_paint() {
        var stackFile = Path.Combine(folder.FullName, "Body.vxlayers");
        File.WriteAllText(
            stackFile,
            LayerStackYaml.Write(
                new() {
                    Name = "Body",
                    BaseWidth = 32,
                    BaseHeight = 32,
                    Sets = [new() { Name = "S", Channels = [new() { Usage = "baseColor" }] }]
                }
            )
        );
        var elsewhere = Directory.CreateDirectory(Path.Combine(folder.FullName, "elsewhere")).FullName;
        var reads = Graph(folder.FullName, "Reads", "vxpaint:mask|Body.mask.vxpaint");
        var plain = Graph(folder.FullName, "Plain", "noise.png");
        var away = Graph(elsewhere, "Away", "vxpaint:mask|Body.mask.vxpaint");

        ServerIdentity identity = new(null, null);
        using GraphSessions graphs = new(identity);
        using StackSessions stacks = new(identity, new(identity), null, graphs);
        GraphSession[] open = [graphs.Open(reads), graphs.Open(plain), graphs.Open(away)];
        foreach (var graph in open) {
            graph.Settle(graph.Pending()!);
            Assert.Null(graph.Pending());
        }

        var stack = stacks.Open(stackFile);
        stacks.CatchUp(stack);

        Assert.True(open[0].ReadsPaint);
        Assert.True(open[0].Pending() is { Forced: true });
        Assert.Null(open[1].Pending());
        Assert.Null(open[2].Pending());

        open[0].Settle(open[0].Pending()!);
        stacks.Close(stack);
        Assert.True(open[0].Pending() is { Forced: true });
    }

    static string Graph(string folder, string name, string source) {
        var file = Path.Combine(folder, name + ".vxtexgraph");
        File.WriteAllText(
            file,
            $$"""
              name: {{name}}
              nodes:
                - id: 1
                  type: Source/Bitmap
                  texts: { Source: '{{source}}' }
                - id: 2
                  type: Output/Output
              edges:
                - { fromNode: 1, fromPort: Out, toNode: 2, toPort: Input }
              """
        );
        return file;
    }
}
