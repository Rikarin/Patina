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
            var value = 0.5 + x / (double)(Width - 1) * 4.0 / 255.0;
            samples[x * 3] = samples[x * 3 + 1] = samples[x * 3 + 2] = (ushort)Math.Round(value * ushort.MaxValue);
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
        levels.SetValue("Input White", 0.5f + 4f / 255f);
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

    /// <summary>
    ///     A bake's colour map reaches a graph as the picture it is: written in sRGB, it is decoded, so mid grey in
    ///     linear light (0.735 as written) reads as a position map holding 0.5 does. Every other map is a measurement
    ///     and is read as written.
    /// </summary>
    [Fact]
    public void A_graph_reads_the_bakes_colour_map_decoded_and_deep() {
        var written = (ushort)Math.Round(0.7353569 * ushort.MaxValue);
        var half = (ushort)Math.Round(0.5 * ushort.MaxValue);
        File.WriteAllBytes(
            Path.Combine(folder, "colour.png"),
            PngCodec.Encode16(4, 1, 3, Enumerable.Repeat(written, 12).ToArray())
        );
        File.WriteAllBytes(
            Path.Combine(folder, "position.png"),
            PngCodec.Encode16(4, 1, 3, Enumerable.Repeat(half, 12).ToArray())
        );
        BakeManifest manifest = new(
            "stand",
            BakeInputs.Png16,
            new([new("colour", "colour.png", 4, 1), new("position", "position.png", 4, 1)], 4, 0, [], false)
        );
        File.WriteAllBytes(
            Path.Combine(folder, "stand." + BakeCache.ManifestFile),
            JsonSerializer.SerializeToUtf8Bytes(manifest, ContractsJsonContext.Default.BakeManifest)
        );

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
            return;
        }

        using (device) {
            var colour = Through(device, "colour");
            var position = Through(device, "position");

            Assert.InRange(colour, position - 1, position + 1);
            // Read raw, the colour map would draw as 0.735 does: a grey well clear of the half the position map draws.
            Assert.InRange(position, (byte)100, (byte)200);
        }
    }

    /// <summary>
    ///     A mesh map with no bake stands in as mid grey when the caller asks for that, as a layer stack does. The
    ///     grey is eight bits and the node declares half-floats (Rikarin/Vixen#1534), so it goes up at its own
    ///     depth: through the plan's declaration it was refused.
    /// </summary>
    [Fact]
    public void A_mesh_map_with_no_bake_stands_in_as_mid_grey() {
        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
            return;
        }

        using (device) {
            NodeTypeRegistry registry = new();
            NodeTypes.Register(registry);
            NodeGraphModel graph = new();
            var map = graph.Add("Source/Mesh Map");
            var output = graph.Add("Output/Output");
            map.SetText("Map", "curvature");
            output.SetText("Usage", "baseColor");
            graph.Connect(new(map.Id, "Out"), new(output.Id, "Input"));
            TextureGraphCompiler compiler = new(registry) { BaseWidth = 4, BaseHeight = 4 };
            var plan = compiler.Compile(graph).Value;
            using TextureUploads uploads = new(device);
            var owed = TextureGraphExternals.Upload(uploads, plan, compiler.Externals);
            List<string> unbaked = [];
            var filled = new GraphPictures().Fill(uploads, plan, owed, [folder], null, unbaked);

            Assert.Empty(filled.Refusals);
            Assert.Equal(["curvature"], unbaked);

            using TexturePlanEvaluator evaluator = new(device);
            using var bake = evaluator.Evaluate(plan, uploads.Externals);
            Assert.InRange(bake.Read(compiler.Outputs.Single().Image).Pixels[0], (byte)126, (byte)130);
        }
    }

    /// <summary>The first texel's red, as a graph reading one mesh map straight to its base colour draws it.</summary>
    byte Through(VulkanDevice device, string usage) {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        NodeGraphModel graph = new();
        var map = graph.Add("Source/Mesh Map");
        var output = graph.Add("Output/Output");
        map.SetText("Map", usage);
        output.SetText("Usage", "baseColor");
        graph.Connect(new(map.Id, "Out"), new(output.Id, "Input"));
        TextureGraphCompiler compiler = new(registry) { BaseWidth = 4, BaseHeight = 4 };
        var plan = compiler.Compile(graph).Value;
        using TextureUploads uploads = new(device);
        var owed = TextureGraphExternals.Upload(uploads, plan, compiler.Externals);
        var filled = new GraphPictures().Fill(uploads, plan, owed, [folder], folder);
        Assert.Empty(filled.Refusals);
        using TexturePlanEvaluator evaluator = new(device);
        using var bake = evaluator.Evaluate(plan, uploads.Externals);
        return bake.Read(compiler.Outputs.Single().Image).Pixels[0];
    }
}
