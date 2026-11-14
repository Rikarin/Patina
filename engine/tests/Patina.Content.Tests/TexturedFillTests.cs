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

    /// <summary>A photo that does not tile lends the hole its grain and not its edge (#290).</summary>
    /// <remarks>
    ///     A ramp from black at the left to white at the right is smooth everywhere but over its own edge, where it
    ///     drops from white to black. The grain is borrowed from the photo moved half way across, which puts that drop
    ///     down the middle of the hole, and a high pass of a drop is a bright and a dark line. So the hole's columns,
    ///     which are smooth in the ramp, must stay smooth in the fill.
    /// </remarks>
    [Fact]
    public void A_photo_that_does_not_tile_lends_its_grain_and_not_its_edge() {
        FactoryCompounds.Register();

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Gpu.Skip($"No Vulkan device: {refusal}");

            return;
        }

        using (device) {
            var filled = Filled(
                device,
                "Photo/Textured Fill",
                static graph => {
                    var ramp = graph.Add("Filters/Pixel Processor");

                    ramp.SetText("Expression", "float4(uv.x, uv.x, uv.x, 1f)");

                    return ramp;
                }
            );
            Pictures.Keep("Photo/Textured Fill", filled, " edge");

            var worst = 0.0;
            var at = 0;

            for (var x = (int)(Side * 0.35); x < (int)(Side * 0.65); x++) {
                var step = Math.Abs(Column(filled, x + 1) - Column(filled, x));

                if (step > worst) {
                    (worst, at) = (step, x);
                }
            }

            Assert.True(worst < 0.05, $"The fill steps by {worst:0.000} between columns {at} and {at + 1}.");
        }
    }

    /// <summary>The mean of red down one column of the middle of the hole.</summary>
    static double Column(Bitmap picture, int x) {
        var rows = Enumerable.Range((int)(Side * 0.45), (int)(Side * 0.1));

        return rows.Average(y => picture.Pixels[(y * Side + x) * 4] / 255.0);
    }

    /// <summary>A white noise with a disc a third of the picture across cut out of it, filled by a compound.</summary>
    static Bitmap Filled(VulkanDevice device, string path) =>
        Filled(
            device,
            path,
            static graph => {
                var noise = graph.Add("Source/Noise");

                noise.SetText("Basis", "White");
                noise.SetValue("Scale", 64f);

                return noise;
            }
        );

    /// <summary>A picture with a disc a third of the picture across cut out of it, filled by a compound.</summary>
    static Bitmap Filled(VulkanDevice device, string path, Func<NodeGraphModel, GraphNode> picture) {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        var library = TextureCompoundLibrary.Publish(registry, null, new(Side, Side), out _);
        NodeGraphModel graph = new();
        var noise = picture(graph);
        var hole = graph.Add("Source/Shape");
        var used = graph.Add(path);
        var output = graph.Add("Output/Output");

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

    /// <summary>The standard deviation of red in the middle of the hole, or in the corners clear of it.</summary>
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
