using Patina.Bench;
using Patina.Content;
using Patina.Jobs;
using Vixen.Core.Imaging;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Export.Tests;

/// <summary>
///     Wave 12's weathering by material and Rust's grain, judged on a real bake rather than a stand-in (#273): the
///     test housing's high mesh baked onto its low one, and each look drawn over a flat paint from those maps. A look
///     that reads its bake wears the housing's edges differently from the middle of its faces; one whose wiring to a
///     mesh map broke draws the same everywhere, which the stand-in benches cannot tell from a quiet look.
/// </summary>
/// <remarks>
///     <c>PATINA_CONTENT_PICTURES</c> names a folder each look's base color is written to, for a person to judge.
/// </remarks>
public sealed class WeatheringOnABakeTests : IDisposable {
    const int Side = 256;

    /// <summary>The looks, each a material filter over the four channels.</summary>
    public static TheoryData<string> Looks { get; } = [
        "Weathering/Rust",
        "Weathering/Worn Metal",
        "Weathering/Worn Leather",
        "Weathering/Worn Fabric",
        "Weathering/Weathered Rock"
    ];

    /// <summary>The flat material under each look, by channel.</summary>
    static readonly Dictionary<string, (float R, float G, float B)> Under = new(StringComparer.Ordinal) {
        ["baseColor"] = (0.55f, 0.2f, 0.15f),
        ["roughness"] = (0.4f, 0.4f, 0.4f),
        ["metalness"] = (0f, 0f, 0f),
        ["height"] = (0.5f, 0.5f, 0.5f)
    };

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-weathering-");

    public void Dispose() => folder.Delete(true);

    [Theory]
    [MemberData(nameof(Looks))]
    public void A_look_wears_the_housings_edges_apart_from_the_middle_of_its_faces(string look) {
        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PATINA_REQUIRE_VULKAN"))) {
                Assert.Fail($"Vulkan is required here: {refusal}");
            }

            Assert.Skip($"No Vulkan device: {refusal}");
            return;
        }

        using (device) {
            var maps = Bake();
            var drawn = Draw(device, look, maps);
            var (edges, faces) = Places(maps);
            var (red, _, _) = Under["baseColor"];
            var onEdges = edges.Average(at => Math.Abs(drawn.Pixels[at * 4] / 255.0 - red));
            var onFaces = faces.Average(at => Math.Abs(drawn.Pixels[at * 4] / 255.0 - red));

            Keep(look, drawn);

            // The instrument: the bake has both places, or the comparison below holds of nothing.
            Assert.True(edges.Count > 100 && faces.Count > 100, $"{edges.Count} edge and {faces.Count} face texels.");
            Assert.True(
                Math.Abs(onEdges - onFaces) > 0.02,
                $"{look} changed the edges by {onEdges:0.000} and the faces by {onFaces:0.000}: it ignores the bake."
            );
        }
    }

    /// <summary>The housing's high baked onto its low, each map read back by its usage.</summary>
    Dictionary<string, Bitmap> Bake() {
        BakeFixture.Write(folder.FullName, 0.02f);
        var into = Path.Combine(folder.FullName, "bake");
        string[] usages = ["curvature", "ao", "position", "world", "id", "thickness", "normal", "islands"];

        MeshMapBakeRun.Run(
            new() {
                High = [Path.Combine(folder.FullName, BakeFixture.HighFile)],
                Low = Path.Combine(folder.FullName, BakeFixture.LowFile),
                Output = into,
                Options = new() { Resolution = Side, Format = BakeInputs.Png8, Maps = usages }
            },
            into,
            0,
            null,
            TestContext.Current.CancellationToken
        );

        return usages.ToDictionary(
            static usage => usage,
            usage => PngCodec.Decode(File.ReadAllBytes(Path.Combine(into, $"{usage}.png"))),
            StringComparer.Ordinal
        );
    }

    /// <summary>The texels a chart covers on a sharp convex edge, and those in the flat middle of a face.</summary>
    static (List<int> Edges, List<int> Faces) Places(Dictionary<string, Bitmap> maps) {
        var (curvature, islands) = (maps["curvature"], maps["islands"]);
        List<int> edges = [];
        List<int> faces = [];

        for (var at = 0; at < Side * Side; at++) {
            if (islands.Pixels[at * 4] + islands.Pixels[at * 4 + 1] + islands.Pixels[at * 4 + 2] == 0) {
                continue;
            }

            var bend = curvature.Pixels[at * 4] / 255.0 - 0.5;
            (bend > 0.1 ? edges : Math.Abs(bend) < 0.01 ? faces : null)?.Add(at);
        }

        return (edges, faces);
    }

    /// <summary>One look over the flat material, its base color drawn from the bake.</summary>
    static Bitmap Draw(VulkanDevice device, string look, Dictionary<string, Bitmap> maps) {
        FactoryCompounds.Register();
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        var library = TextureCompoundLibrary.Publish(registry, null, new(Side, Side), out _);
        NodeGraphModel graph = new();
        var used = graph.Add(look);

        foreach (var (channel, (r, g, b)) in Under) {
            var flat = graph.Add("Source/Uniform");
            flat.SetValue("Colour", r, g, b, 1f);
            graph.Connect(new(flat.Id, "Out"), new(used.Id, channel));
        }

        var output = graph.Add("Output/Output");
        output.SetText("Usage", "baseColor");
        graph.Connect(new(used.Id, "baseColor"), new(output.Id, "Input"));

        TextureGraphCompiler compiler = new(registry) {
            BaseWidth = Side, BaseHeight = Side, Seed = 1, SubGraphSource = library
        };
        var compilation = compiler.Compile(graph);
        Assert.True(compilation.Succeeded, string.Join("; ", compilation.Diagnostics.Select(static d => d.Message)));

        var plan = compilation.Value;
        using TextureUploads uploads = new(device);
        foreach (var owed in TextureGraphExternals.Upload(uploads, plan, compiler.Externals)) {
            var map = maps[owed.Asset["meshmap:".Length..]];
            uploads.AddPicture(plan, owed.Image, map.Width, map.Height, map.Pixels, TextureFormat.Rgba8);
        }

        using TexturePlanEvaluator evaluator = new(device);
        using var bake = evaluator.Evaluate(plan, uploads.Externals);
        return bake.Read(compiler.Outputs.Single().Image);
    }

    static void Keep(string look, Bitmap picture) {
        if (Environment.GetEnvironmentVariable("PATINA_CONTENT_PICTURES") is { Length: > 0 } pictures) {
            Directory.CreateDirectory(pictures);
            PngCodec.Save(Path.Combine(pictures, $"bake {look.Replace('/', ' ')}.png"), picture);
        }
    }
}
