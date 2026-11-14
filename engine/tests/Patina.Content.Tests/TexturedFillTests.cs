using Vixen.Core.Imaging;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Content.Tests;

/// <summary>
///     #273's texture-keeping hole fill: a large hole in a busy picture, filled by Photo/Fill Holes, is a smooth patch,
///     and filled by Photo/Textured Fill it keeps a grain like the picture's around it.
/// </summary>
public sealed class TexturedFillTests {
    const int Side = 256;

    [Fact]
    public void A_large_hole_in_a_busy_picture_keeps_its_grain() {
        FactoryCompounds.Register();

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Gpu.Skip($"No Vulkan device: {refusal}");

            return;
        }

        using (device) {
            var smooth = Filled(device, "Photo/Fill Holes");
            var textured = Filled(device, "Photo/Textured Fill");
            Pictures.Keep("Photo/Fill Holes", smooth, " hole");
            Pictures.Keep("Photo/Textured Fill", textured, " hole");

            var around = Spread(textured, false);
            var (plain, kept) = (Spread(smooth, true), Spread(textured, true));

            // The instrument: the picture is busy around the hole, and the smooth fill is smooth, or the grain
            // measured below proves nothing.
            Assert.True(around > 0.1, $"The picture around the hole spreads {around:0.000}.");
            Assert.True(plain < around * 0.3, $"Fill Holes left a grain of {plain:0.000} against {around:0.000}.");
            Assert.True(
                kept > around * 0.5,
                $"Textured Fill kept a grain of {kept:0.000} in the hole against {around:0.000} around it."
            );
        }
    }

    /// <summary>A white noise with a disc of a third of the picture cut out of its middle, filled by a compound.</summary>
    static Bitmap Filled(VulkanDevice device, string path) {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        var library = TextureCompoundLibrary.Publish(registry, null, new(Side, Side), out _);
        NodeGraphModel graph = new();
        var noise = graph.Add("Source/Noise");
        var hole = graph.Add("Source/Shape");
        var used = graph.Add(path);
        var output = graph.Add("Output/Output");

        noise.SetText("Basis", "White");
        noise.SetValue("Scale", 64f);
        hole.SetText("Kind", "Disc");
        hole.SetValue("Scale", 0.66f);
        hole.SetValue("Falloff", 0f);
        output.SetText("Usage", "baseColor");
        graph.Connect(new(noise.Id, "Out"), new(used.Id, "Input"));
        graph.Connect(new(hole.Id, "Out"), new(used.Id, "Mask"));
        graph.Connect(new(used.Id, "Out"), new(output.Id, "Input"));

        TextureGraphCompiler compiler = new(registry) {
            BaseWidth = Side, BaseHeight = Side, Seed = 1, SubGraphSource = library
        };
        var compilation = compiler.Compile(graph);

        Assert.True(
            compilation.Succeeded,
            string.Join("; ", compilation.Diagnostics.Select(static diagnostic => diagnostic.Message))
        );

        using TexturePlanEvaluator evaluator = new(device);
        using TextureUploads uploads = new(device);
        using var bake = evaluator.Evaluate(compilation.Value, uploads.Externals);

        return bake.Read(compiler.Outputs.Single().Image);
    }

    /// <summary>The standard deviation of red inside the middle of the hole, or in the corners well clear of it.</summary>
    static double Spread(Bitmap picture, bool inside) {
        List<double> values = [];

        for (var y = 0; y < Side; y++) {
            for (var x = 0; x < Side; x++) {
                var distance = Math.Sqrt((x - Side / 2.0) * (x - Side / 2.0) + (y - Side / 2.0) * (y - Side / 2.0));

                if (inside ? distance < Side * 0.2 : distance > Side * 0.45) {
                    values.Add(picture.Pixels[(y * Side + x) * 4] / 255.0);
                }
            }
        }

        var mean = values.Average();

        return Math.Sqrt(values.Average(value => (value - mean) * (value - mean)));
    }
}
