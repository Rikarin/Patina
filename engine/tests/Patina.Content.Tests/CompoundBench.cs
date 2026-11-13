using Vixen.Core.Imaging;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Content.Tests;

/// <summary>
///     One factory compound in a graph, compiled with the library published and baked on a device over a small bake
///     of stand-in mesh maps whose answers a test can say in advance.
/// </summary>
static class CompoundBench {
    /// <summary>How wide and tall a bench graph is.</summary>
    public const int Side = 128;

    /// <summary><see cref="Side" /> as a graph setting spells it.</summary>
    const string SideText = "128";

    /// <summary>
    ///     Columns of <see cref="Curvature" /> that are a soft convex edge, as a real bake's rounded edge reads.
    /// </summary>
    public static readonly Range Convex = 40..56;

    /// <summary>Columns that are a soft concave crease.</summary>
    public static readonly Range Concave = 80..96;

    /// <summary>The rows whose world normal points up; the rest face sideways.</summary>
    public const int Upward = Side / 2;

    /// <summary>
    ///     A graph holding one compound, every image input fed a noise, and its first picture as the output.
    /// </summary>
    /// <param name="path">The compound.</param>
    /// <param name="used">The compound's node, to set its knobs on.</param>
    /// <param name="subject">The noise its first image input reads, or null when it reads none.</param>
    /// <returns>The compiler and the graph.</returns>
    public static (TextureGraphCompiler Compiler, NodeGraphModel Graph) Containing(
        string path,
        out GraphNode used,
        out GraphNode? subject
    ) {
        FactoryCompounds.Register();
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        var library = TextureCompoundLibrary.Publish(registry, null, out var problems);

        Assert.Empty(problems.Where(static problem => FactoryCompounds.Paths.Contains(problem.Path)));

        // A compound counts its radii in the base it was authored at, 1024 for every one that ships, and the bench
        // measures it in its own texels: so on the bench, every compound is authored at the bench's size.
        foreach (var shipped in TextureCompoundLibrary.Shipped) {
            if (library.TryGet(shipped, out var compound)) {
                compound.Settings[TextureGraphSettings.BaseWidth] = SideText;
                compound.Settings[TextureGraphSettings.BaseHeight] = SideText;
            }
        }

        var type = registry.Get(path);
        NodeGraphModel graph = new();
        used = graph.Add(path);
        subject = null;
        var output = graph.Add("Output/Output");
        var picture = type.Ports.First(static port => port is { Direction: PortDirection.Output, Kind: PortKind.Image }
        );

        output.SetText("Usage", "baseColor");
        graph.Connect(new(used.Id, picture.Name), new(output.Id, "Input"));

        foreach (var port in type.Ports.Where(static port => port is {
                         Direction: PortDirection.Input, Kind: PortKind.Image
                     }
                 )) {
            var noise = graph.Add("Source/Noise");
            noise.SetText("Basis", "Gradient");
            noise.SetValue("Octaves", 3f);
            graph.Connect(new(noise.Id, "Out"), new(used.Id, port.Name));
            subject ??= noise;
        }

        if (subject is not null) {
            // The picture the compound changes, kept so a test can say it changed it.
            var before = graph.Add("Output/Output");
            before.SetText("Usage", "roughness");
            graph.Connect(new(subject.Id, "Out"), new(before.Id, "Input"));
        }

        TextureGraphCompiler compiler = new(registry) {
            BaseWidth = Side, BaseHeight = Side, Seed = 1, SubGraphSource = library
        };

        return (compiler, graph);
    }

    /// <summary>Compiles and bakes a graph, feeding every mesh map it reads from the stand-in bake.</summary>
    /// <param name="device">The device.</param>
    /// <param name="compiler">The compiler.</param>
    /// <param name="graph">The graph.</param>
    /// <returns>Each output's picture, by usage.</returns>
    public static Dictionary<string, Bitmap> Bake(
        VulkanDevice device,
        TextureGraphCompiler compiler,
        NodeGraphModel graph
    ) {
        var compilation = compiler.Compile(graph);

        Assert.True(
            compilation.Succeeded,
            string.Join("; ", compilation.Diagnostics.Select(static diagnostic => diagnostic.Message))
        );

        var plan = compilation.Value;
        using TextureUploads uploads = new(device);

        foreach (var owed in TextureGraphExternals.Upload(uploads, plan, compiler.Externals)) {
            Assert.StartsWith("meshmap:", owed.Asset, StringComparison.Ordinal);
            uploads.AddPicture(plan, owed.Image, Side, Side, Map(owed.Asset["meshmap:".Length..]), TextureFormat.Rgba8);
        }

        using TexturePlanEvaluator evaluator = new(device);
        using var bake = evaluator.Evaluate(plan, uploads.Externals);

        return compiler.Outputs.ToDictionary(static output => output.Usage, output => bake.Read(output.Image));
    }

    /// <summary>The red of a picture at a texel, from nought to one.</summary>
    public static double At(Bitmap picture, int x, int y) => picture.Pixels[(y * picture.Width + x) * 4] / 255.0;

    /// <summary>The mean red over a block of texels.</summary>
    public static double Mean(Bitmap picture, Range columns, Range rows) {
        var (x0, width) = columns.GetOffsetAndLength(picture.Width);
        var (y0, height) = rows.GetOffsetAndLength(picture.Height);
        var sum = 0.0;

        for (var y = y0; y < y0 + height; y++) {
            for (var x = x0; x < x0 + width; x++) {
                sum += At(picture, x, y);
            }
        }

        return sum / (width * height);
    }

    /// <summary>The share of texels whose red differs by more than a step between two pictures.</summary>
    public static double Moved(Bitmap first, Bitmap second) {
        var moved = 0;

        for (var at = 0; at < first.Width * first.Height; at++) {
            if (Math.Abs(first.Pixels[at * 4] - second.Pixels[at * 4]) > 3) {
                moved++;
            }
        }

        return moved / (double)(first.Width * first.Height);
    }

    /// <summary>A stand-in for one baked map, as eight-bit RGBA.</summary>
    /// <param name="usage">What the map measures.</param>
    /// <returns>The texels.</returns>
    /// <remarks>
    ///     Curvature is flat at a half, with a soft convex band and a soft concave one at 0.53 and 0.47 and one texel
    ///     at the top of the range, as a bake scaled by its sharpest texel reads. The top half faces up and the rest
    ///     sideways; the bottom quarter is sheltered; position climbs up the picture; the id map is two parts.
    /// </remarks>
    static byte[] Map(string usage) {
        var texels = new byte[Side * Side * 4];

        for (var y = 0; y < Side; y++) {
            for (var x = 0; x < Side; x++) {
                var (r, g, b) = usage switch {
                    "curvature" => Grey(Curvature(x, y)),
                    "ao" => Grey(y >= Side * 3 / 4 ? 0.4 : 1.0),
                    "world" => y < Upward ? (0.5, 1.0, 0.5) : (1.0, 0.5, 0.5),
                    "position" => (x / (double)Side, 1.0 - y / (double)Side, 0.5),
                    "id" => x < Side / 2 ? (1.0, 0.0, 0.0) : (0.0, 0.0, 1.0),
                    "normal" or "bent" => (0.5, 0.5, 1.0),
                    _ => Grey(0.5)
                };
                var at = (y * Side + x) * 4;

                texels[at] = Byte(r);
                texels[at + 1] = Byte(g);
                texels[at + 2] = Byte(b);
                texels[at + 3] = byte.MaxValue;
            }
        }

        return texels;
    }

    static double Curvature(int x, int y) =>
        x == 0 && y == 0 ? 1.0
        : x >= Convex.Start.Value && x < Convex.End.Value ? 0.53
        : x >= Concave.Start.Value && x < Concave.End.Value ? 0.47
        : 0.5;

    static (double R, double G, double B) Grey(double value) => (value, value, value);

    static byte Byte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * byte.MaxValue);
}
