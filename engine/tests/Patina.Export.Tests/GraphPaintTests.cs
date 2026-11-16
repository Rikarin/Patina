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
            Assert.Contains(
                filled.Stamps,
                static stamp => stamp.Path.EndsWith("Paint.vxpaint", StringComparison.Ordinal)
            );

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

    /// <summary>
    ///     A paint layer nobody has painted has no file yet, which the stack reads as clear, so the graph does too, and
    ///     stamps the missing file so that its first save is noticed. A file outside the graph's folder is refused.
    /// </summary>
    [Fact]
    public void A_paint_file_not_yet_written_reads_clear_and_one_elsewhere_is_refused() {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        NodeGraphModel graph = new();
        Output(graph, Bitmap(graph, PaintReference.Reference("paint/Unpainted.vxpaint", "baseColor")), "baseColor");
        Output(graph, Bitmap(graph, PaintReference.Reference("../Elsewhere.vxpaint", "roughness")), "roughness");
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

            Assert.Contains("../Elsewhere.vxpaint", Assert.Single(filled.Refusals).Message);
            var unpainted = Assert.Single(filled.Stamps);
            Assert.Equal(-1, unpainted.Length);
            Assert.False(GraphPictures.Moved(unpainted));
            Directory.CreateDirectory(Path.Combine(folder, "paint"));
            File.WriteAllBytes(Path.Combine(folder, "paint", "Unpainted.vxpaint"), [1]);
            Assert.True(GraphPictures.Moved(unpainted));
        }
    }

    /// <summary>
    ///     A canvas a texture set is painting is read in place of its file, so a painted mask feeds a graph as it is
    ///     painted (#205). What was read goes stale on the next stroke and when the painting ends, and a file read
    ///     before somebody began to paint it goes stale then too.
    /// </summary>
    [Fact]
    public void A_canvas_being_painted_is_read_in_place_of_its_file_and_each_stroke_is_noticed() {
        PaintCanvas saved = new(Side, Side);
        saved.Channel(PaintReference.Mask).Fill(new Vector4(0.2f, 0.2f, 0.2f, 1f));
        Directory.CreateDirectory(Path.Combine(folder, "paint"));
        var file = Path.Combine(folder, "paint", "Mask.vxpaint");
        using (var stream = File.Create(file)) {
            saved.Write(stream);
        }

        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        NodeGraphModel graph = new();
        Output(graph, Bitmap(graph, PaintReference.Reference("paint/Mask.vxpaint", PaintReference.Mask)), "baseColor");
        TextureGraphCompiler compiler = new(registry) { BaseWidth = Side, BaseHeight = Side };
        var plan = compiler.Compile(graph).Value;

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
            return;
        }

        using (device) {
            PaintCanvas? painting = null;
            GraphPictures pictures = new() { Painting = path => path == Path.GetFullPath(file) ? painting : null };
            using TextureUploads before = new(device);
            var fromFile = Assert.Single(
                pictures.Fill(
                        before,
                        plan,
                        TextureGraphExternals.Upload(before, plan, compiler.Externals),
                        [folder],
                        null
                    )
                    .Stamps
            );
            Assert.False(pictures.Stale(fromFile));

            using (var stream = File.OpenRead(file)) {
                painting = PaintCanvas.Read(stream);
            }

            painting.Channel(PaintReference.Mask).Fill(new Vector4(0.9f, 0.9f, 0.9f, 1f));
            Assert.True(pictures.Stale(fromFile));

            using TextureUploads uploads = new(device);
            var filled = pictures.Fill(
                uploads,
                plan,
                TextureGraphExternals.Upload(uploads, plan, compiler.Externals),
                [folder],
                null
            );
            var live = Assert.Single(filled.Stamps);
            Assert.False(pictures.Stale(live));
            using (TexturePlanEvaluator evaluator = new(device)) {
                using var bake = evaluator.Evaluate(plan, uploads.Externals);
                Assert.InRange(bake.Read(compiler.Outputs.Single().Image).Pixels[0], 226, 234);
            }

            painting.Channel(PaintReference.Mask).Fill(new Vector4(0.5f, 0.5f, 0.5f, 1f));
            Assert.True(pictures.Stale(live));
            painting = null;
            Assert.True(pictures.Stale(live));
        }
    }

    /// <summary>
    ///     A stroke on a canvas a graph reads as it is painted carries the tiles it touched onto what was uploaded, and
    ///     no more: reading the channel again uploaded all of it on every stroke (#311). A painting that ends is read
    ///     from its file again, so it is not carried.
    /// </summary>
    [Fact]
    public void A_stroke_on_a_canvas_being_painted_carries_the_tiles_it_touched() {
        const int side = 2 * PaintImage.TileSize;
        var file = Path.Combine(folder, "paint", "Mask.vxpaint");
        PaintCanvas? painting = new(side, side);
        painting.Channel(PaintReference.Mask).Fill(new Vector4(0.2f, 0.2f, 0.2f, 1f));

        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        NodeGraphModel graph = new();
        Output(graph, Bitmap(graph, PaintReference.Reference("paint/Mask.vxpaint", PaintReference.Mask)), "baseColor");
        TextureGraphCompiler compiler = new(registry) { BaseWidth = side, BaseHeight = side };
        var plan = compiler.Compile(graph).Value;

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
            return;
        }

        using (device) {
            GraphPictures pictures = new() { Painting = path => path == Path.GetFullPath(file) ? painting : null };
            using TextureUploads uploads = new(device);
            var filled = pictures.Fill(
                uploads,
                plan,
                TextureGraphExternals.Upload(uploads, plan, compiler.Externals),
                [folder],
                null
            );
            Assert.Single(filled.Live);
            GraphPaintTiles tiles = new();
            tiles.Watch(pictures, filled);

            painting.Channel(PaintReference.Mask).Write(side - 1, side - 1, new Vector4(0.9f, 0.9f, 0.9f, 1f));
            Assert.True(pictures.Stale(Assert.Single(filled.Stamps)));
            var carried = tiles.Carry(pictures, filled.Stamps, uploads);

            Assert.NotNull(carried);
            Assert.False(pictures.Stale(Assert.Single(carried)));
            Assert.Equal(1, tiles.TilesUploaded);
            using (TexturePlanEvaluator evaluator = new(device)) {
                using var bake = evaluator.Evaluate(plan, uploads.Externals);
                var pixels = bake.Read(compiler.Outputs.Single().Image).Pixels;
                Assert.InRange(pixels[0], 49, 53);
                Assert.InRange(pixels[(side * side - 1) * 4], 226, 234);
            }

            painting.Channel(PaintReference.Mask).Write(0, 0, Vector4.One);
            painting = null;
            Assert.Null(tiles.Carry(pictures, carried, uploads));
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
