using Patina.Contracts;
using Patina.Graphs;
using Patina.Jobs;
using System.Text.Json;
using Vixen.Core.Imaging;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Export.Tests;

/// <summary>
///     ⚠ A graph reads a baked map at the depth it was baked. It read every map at eight bits, so a 16-bit position map
///     held 256 places across the model and a volume noise finer than that drew stairs (Patina wave 6).
/// </summary>
public sealed class GraphMeshMapDepthTests : IDisposable {
    const int Width = 256;
    readonly string folder = Directory.CreateTempSubdirectory("patina-depth-").FullName;

    public void Dispose() => Directory.Delete(folder, true);

    [Fact]
    public void A_graph_reads_a_sixteen_bit_map_at_its_own_depth() {
        // A ramp across four eight-bit levels, stored in sixteen bits: read at eight it is four steps.
        var samples = new ushort[Width * 3];
        for (var x = 0; x < Width; x++) {
            var value = 0.5 + (x / (double)(Width - 1) * 4.0 / 255.0);
            samples[x * 3] = samples[(x * 3) + 1] = samples[(x * 3) + 2] = (ushort)Math.Round(value * ushort.MaxValue);
        }

        File.WriteAllBytes(Path.Combine(folder, "position.png"), PngCodec.Encode16(Width, 1, 3, samples));
        BakeManifest manifest = new(
            "stand",
            BakeInputs.Png16,
            new([new("position", "position.png", Width, 1)], Width, 0, [], false)
        );
        File.WriteAllBytes(
            Path.Combine(folder, "stand." + BakeCache.ManifestFile),
            JsonSerializer.SerializeToUtf8Bytes(manifest, ContractsJsonContext.Default.BakeManifest)
        );

        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        NodeGraphModel graph = new();
        var map = graph.Add("Source/Mesh Map");
        var levels = graph.Add("Colour/Levels");
        var output = graph.Add("Output/Output");
        map.SetText("Map", "position");
        levels.SetValue("Input Black", 0.5f);
        levels.SetValue("Input White", 0.5f + (4f / 255f));
        output.SetText("Usage", "baseColor");
        graph.Connect(new(map.Id, "Out"), new(levels.Id, "Input"));
        graph.Connect(new(levels.Id, "Out"), new(output.Id, "Input"));
        TextureGraphCompiler compiler = new(registry) { BaseWidth = Width, BaseHeight = 4 };
        var plan = compiler.Compile(graph).Value;

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
            return;
        }

        using (device) {
            using TextureUploads uploads = new(device);
            var owed = TextureGraphExternals.Upload(uploads, plan, compiler.Externals);
            var filled = new GraphPictures().Fill(uploads, plan, owed, [folder], folder);

            Assert.Empty(filled.Refusals);

            using TexturePlanEvaluator evaluator = new(device);
            using var bake = evaluator.Evaluate(plan, uploads.Externals);
            var picture = bake.Read(compiler.Outputs.Single().Image);
            var levelsSeen = Enumerable.Range(0, Width).Select(x => picture.Pixels[x * 4]).Distinct().Count();

            // Eight bits are five steps here. A half-float keeps eleven bits, a step of 1/2048 about a half, so the
            // ramp is some thirty-two: a texture a device filters, which a full float is not on every device.
            Assert.True(levelsSeen > 16, $"The ramp came through in {levelsSeen} steps.");
        }
    }
}
