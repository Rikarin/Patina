using Patina.Graphs;
using System.Numerics;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;
using Vixen.Texturing.Painting;

namespace Patina.Export.Tests;

/// <summary>
///     A graph reads a painted canvas the way a layer stack names one (#205), so the graph a stack stands for shows its
///     paint layers. A graph refused every <c>vxpaint:</c> reference as a file that was not there.
/// </summary>
public sealed class GraphPaintTests : IDisposable {
    const int Side = 8;
    readonly string folder = Directory.CreateTempSubdirectory("patina-paint-").FullName;

    public void Dispose() => Directory.Delete(folder, true);

    [Fact]
    public void A_graph_reads_a_painted_channel_and_a_channel_never_painted_reads_clear() {
        PaintCanvas canvas = new(Side, Side);
        canvas.Channel("baseColor").Fill(new Vector4(0.8f, 0.2f, 0.4f, 1f));
        Directory.CreateDirectory(Path.Combine(folder, "paint"));
        using (var stream = File.Create(Path.Combine(folder, "paint", "Paint.vxpaint"))) {
            canvas.Write(stream);
        }

        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        NodeGraphModel graph = new();
        var painted = Bitmap(graph, PaintReference.Reference("paint/Paint.vxpaint", "baseColor"));
        var unpainted = Bitmap(graph, PaintReference.Reference("paint/Paint.vxpaint", "roughness"));
        Output(graph, painted, "baseColor");
        Output(graph, unpainted, "roughness");
        TextureGraphCompiler compiler = new(registry) { BaseWidth = Side, BaseHeight = Side };
        var plan = compiler.Compile(graph).Value;

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
            return;
        }

        using (device) {
            using TextureUploads uploads = new(device);
            var owed = TextureGraphExternals.Upload(uploads, plan, compiler.Externals);
            var filled = new GraphPictures().Fill(uploads, plan, owed, [folder], null);

            Assert.Empty(filled.Refusals);
            Assert.Contains(filled.Stamps, static stamp => stamp.Path.EndsWith("Paint.vxpaint", StringComparison.Ordinal));

            using TexturePlanEvaluator evaluator = new(device);
            using var bake = evaluator.Evaluate(plan, uploads.Externals);
            var colour = bake.Read(compiler.Outputs.Single(static o => o.Usage == "baseColor").Image);
            var rough = bake.Read(compiler.Outputs.Single(static o => o.Usage == "roughness").Image);
            Assert.InRange(colour.Pixels[0], 200, 208);
            Assert.InRange(colour.Pixels[1], 49, 53);
            Assert.InRange(colour.Pixels[2], 100, 104);
            Assert.Equal(0, rough.Pixels[0]);
        }
    }

    [Fact]
    public void A_painted_file_that_is_not_there_is_refused_by_name() {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        NodeGraphModel graph = new();
        Output(graph, Bitmap(graph, PaintReference.Reference("paint/Gone.vxpaint", "baseColor")), "baseColor");
        TextureGraphCompiler compiler = new(registry) { BaseWidth = Side, BaseHeight = Side };
        var plan = compiler.Compile(graph).Value;

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
            return;
        }

        using (device) {
            using TextureUploads uploads = new(device);
            var owed = TextureGraphExternals.Upload(uploads, plan, compiler.Externals);
            var filled = new GraphPictures().Fill(uploads, plan, owed, [folder], null);

            Assert.Contains("vxpaint:baseColor|paint/Gone.vxpaint", Assert.Single(filled.Refusals).Message);
        }
    }

    static NodeId Bitmap(NodeGraphModel graph, string reference) {
        var node = graph.Add("Source/Bitmap");
        node.SetText("Source", reference);
        node.SetText("Space", "Linear");
        return node.Id;
    }

    static void Output(NodeGraphModel graph, NodeId from, string usage) {
        var output = graph.Add("Output/Output");
        output.SetText("Usage", usage);
        graph.Connect(new(from, "Out"), new(output.Id, "Input"));
    }
}
