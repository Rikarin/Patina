using Patina.Graphs;
using Vixen.Graphics.Vulkan;
using Vixen.Interchange;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Export.Tests;

/// <summary>
///     Patina #139: a graph's Source/Text and Source/Vector are drawn by Vixen from a font or an SVG Patina finds the
///     way it finds a bitmap, read sharing write, and stamped so that saving the file is noticed.
/// </summary>
public sealed class GraphDrawingTests : IDisposable {
    const int Side = 64;

    const string HalfRed = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                             <rect width="50" height="100" fill="#f00"/>
                           </svg>
                           """;

    readonly string folder = Directory.CreateTempSubdirectory("patina-drawing-").FullName;

    public void Dispose() => Directory.Delete(folder, true);

    (TexturePlan Plan, TextureGraphCompiler Compiler) Compile(string path, Action<GraphNode> configure) {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        NodeGraphModel graph = new();
        var node = graph.Add(path);
        var output = graph.Add("Output/Output");
        configure(node);
        output.SetText("Usage", "baseColor");
        graph.Connect(new(node.Id, "Out"), new(output.Id, "Input"));
        TextureGraphCompiler compiler = new(registry) { BaseWidth = Side, BaseHeight = Side };
        var compilation = compiler.Compile(graph);
        Assert.True(compilation.Succeeded, string.Join(' ', compilation.Diagnostics.Select(static d => d.Message)));
        return (compilation.Value, compiler);
    }

    [Fact]
    public void A_vector_beside_the_graph_is_drawn_and_stamped() {
        var svg = Path.Combine(folder, "logo.svg");
        File.WriteAllText(svg, HalfRed);
        var (plan, compiler) = Compile("Source/Vector", static node => node.SetText("File", "logo.svg"));

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
            return;
        }

        using (device) {
            using TextureUploads uploads = new(device);
            var owed = TextureGraphExternals.Upload(uploads, plan, compiler.Externals);
            var filled = new GraphPictures().Fill(uploads, plan, owed, [folder], null);

            Assert.Empty(filled.Refusals);
            Assert.Equal([svg], filled.Stamps.Select(static stamp => stamp.Path));
            Assert.False(GraphPictures.Moved(filled.Stamps[0]));

            using TexturePlanEvaluator evaluator = new(device);
            using var bake = evaluator.Evaluate(plan, uploads.Externals);
            var picture = bake.Read(compiler.Outputs.Single().Image);

            // Red and opaque on the left, nothing on the right.
            Assert.Equal(255, picture.Pixels[(32 * Side + 8) * 4]);
            Assert.Equal(255, picture.Pixels[(32 * Side + 8) * 4 + 3]);
            Assert.Equal(0, picture.Pixels[(32 * Side + 56) * 4 + 3]);

            // Saving the file is a change the graph hears about, as it is for a bitmap.
            File.WriteAllText(svg, HalfRed.Replace("#f00", "#00f", StringComparison.Ordinal) + "\n");
            Assert.True(GraphPictures.Moved(filled.Stamps[0]));
        }
    }

    [Fact]
    public void Text_in_the_bundled_face_needs_no_file_and_a_missing_font_is_a_refusal_naming_it() {
        var (plan, compiler) = Compile("Source/Text", static node => node.SetText("Text", "Patina"));
        var (missing, missingCompiler) = Compile(
            "Source/Text",
            static node => node.SetText("Font", "fonts/Nowhere.ttf")
        );

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
            return;
        }

        using (device) {
            using TextureUploads uploads = new(device);
            var filled = new GraphPictures().Fill(
                uploads,
                plan,
                TextureGraphExternals.Upload(uploads, plan, compiler.Externals),
                [folder],
                null
            );

            Assert.Empty(filled.Refusals);
            Assert.Empty(filled.Stamps);

            using TexturePlanEvaluator evaluator = new(device);
            using var bake = evaluator.Evaluate(plan, uploads.Externals);
            Assert.Contains(bake.Read(compiler.Outputs.Single().Image).Pixels, static value => value > 200);

            using TextureUploads others = new(device);
            var refused = new GraphPictures().Fill(
                others,
                missing,
                TextureGraphExternals.Upload(others, missing, missingCompiler.Externals),
                [folder],
                null
            );

            var problem = Assert.Single(refused.Refusals);
            Assert.Equal(GraphPictures.Problem, problem.Id);
            Assert.Contains("fonts/Nowhere.ttf", problem.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     A TIFF is a picture a Bitmap reads (#251): an 8-bit one as bytes, and a 16-bit one at its own depth.
    /// </summary>
    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void A_bitmap_reads_a_tiff(int bits) {
        var values = new float[Side * Side];
        for (var at = 0; at < values.Length; at++) {
            values[at] = at % Side < Side / 2 ? 0.25f : 0.75f;
        }

        var file = Path.Combine(folder, "picture.tif");
        File.WriteAllBytes(
            file,
            bits == 8
                ? TiffWriter.Encode8(Side, Side, 1, [..values.Select(static v => (byte)Math.Round(v * 255))])
                : TiffWriter.Encode16(Side, Side, 1, [..values.Select(static v => (ushort)Math.Round(v * 65535))])
        );
        Assert.Equal(bits > 8, MapPictures.IsDeep(file));
        var (plan, compiler) = Compile("Source/Bitmap", static node => node.SetText("Source", "picture.tif"));

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
            return;
        }

        using (device) {
            using TextureUploads uploads = new(device);
            var owed = TextureGraphExternals.Upload(uploads, plan, compiler.Externals);
            var filled = new GraphPictures().Fill(uploads, plan, owed, [folder], null);

            Assert.Empty(filled.Refusals);
            using TexturePlanEvaluator evaluator = new(device);
            using var bake = evaluator.Evaluate(plan, uploads.Externals);
            var picture = bake.Read(compiler.Outputs.Single().Image);
            Assert.InRange(picture.Pixels[(32 * Side + 8) * 4], 60, 68);
            Assert.InRange(picture.Pixels[(32 * Side + 56) * 4], 187, 195);
        }
    }
}
