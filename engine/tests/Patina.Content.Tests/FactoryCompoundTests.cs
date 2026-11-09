using System.Globalization;
using System.Runtime.CompilerServices;
using Vixen.Core.Imaging;
using Vixen.Core.Yaml;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Content.Tests;

/// <summary>
///     Patina's factory compounds (#169): each is shipped once registered, is a graph of nodes Patina already has, and
///     draws a picture that tiles.
/// </summary>
public sealed class FactoryCompoundTests {
    /// <summary>The grunges #169 asks for by name, each of which has to ship.</summary>
    static readonly string[] Named = [
        "Grunges/Dirt Patches",
        "Grunges/Drip Paint",
        "Grunges/Dust Splash",
        "Grunges/Fibre Tangle",
        "Grunges/Galvanised Spangle",
        "Grunges/Pitted Steel",
        "Grunges/Scuffs",
        "Grunges/Shavings",
        "Grunges/Speckle",
        "Grunges/Water Stains"
    ];

    [Fact]
    public void Every_factory_compound_is_shipped_once_registered_and_none_replaces_one_of_Vixens() {
        FactoryCompounds.Register();

        // The instrument: the folder was embedded at all, or every assertion below holds of nothing.
        Assert.Subset(
            FactoryCompounds.Paths.ToHashSet(StringComparer.Ordinal),
            Named.ToHashSet(StringComparer.Ordinal)
        );
        Assert.Subset(
            TextureCompoundLibrary.Shipped.ToHashSet(StringComparer.Ordinal),
            FactoryCompounds.Paths.ToHashSet(StringComparer.Ordinal)
        );

        foreach (var path in FactoryCompounds.Paths) {
            var text = TextureCompoundLibrary.Source(path);

            Assert.NotNull(text);

            // Shipped under its own name, so the file read back is this assembly's and not a Vixen compound it
            // collided with.
            var document = YamlSerializer.Parse<NodeGraphAsset>(text);

            Assert.Equal(path[(path.LastIndexOf('/') + 1)..], document.Name);
            Assert.Contains(
                document.Interface,
                static port => port is { Direction: PortDirection.Output, Kind: PortKind.Image }
            );
            Assert.All(document.Interface, static port => Assert.False(string.IsNullOrWhiteSpace(port.Summary)));
        }
    }

    /// <summary>
    ///     Every knob a factory compound shows says what it is in the product's words: a label, a sentence, a group,
    ///     and a range its default sits in.
    /// </summary>
    [Fact]
    public void Every_knob_has_a_label_a_summary_and_a_range_its_default_is_in() {
        FactoryCompounds.Register();
        List<string> problems = [];

        foreach (var path in FactoryCompounds.Paths) {
            var document = YamlSerializer.Parse<NodeGraphAsset>(TextureCompoundLibrary.Source(path)!);

            foreach (var knob in document.Parameters) {
                var value = float.Parse(knob.Default, CultureInfo.InvariantCulture);

                if (string.IsNullOrWhiteSpace(knob.Label)
                    || string.IsNullOrWhiteSpace(knob.Summary)
                    || string.IsNullOrWhiteSpace(knob.Group)) {
                    problems.Add($"{path}: '{knob.Name}' has no label, summary or group");
                }

                if (!(knob.Minimum < knob.Maximum) || value < knob.Minimum || value > knob.Maximum) {
                    problems.Add(
                        $"{path}: '{knob.Name}' defaults to {knob.Default}, outside {knob.Minimum}..{knob.Maximum}"
                    );
                }
            }
        }

        Assert.Empty(problems);
    }

    /// <summary>
    ///     The folder and the shipped set agree file for file. A dot in a file name would publish under a phantom
    ///     folder, because a resource name's dots are its folders, so one is refused here rather than found in the
    ///     palette.
    /// </summary>
    [Fact]
    public void Every_file_in_the_content_folder_ships_under_its_own_path() {
        var folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, "../../../content/compounds"));
        string[] files = [
            ..Directory.EnumerateFiles(folder, "*" + TextureCompoundLibrary.Extension, SearchOption.AllDirectories)
                .Select(file => Path.ChangeExtension(Path.GetRelativePath(folder, file), null)!.Replace(
                        Path.DirectorySeparatorChar,
                        '/'
                    )
                )
                .Order(StringComparer.Ordinal)
        ];

        Assert.NotEmpty(files);
        Assert.All(files, static file => Assert.DoesNotContain('.', file));
        Assert.Equal(files, FactoryCompounds.Paths);
    }

    static string Here([CallerFilePath] string path = "") => path;

    /// <summary>
    ///     Every factory compound compiles with nothing to say, with each image input it has fed. Nothing to say
    ///     includes <c>TG0025</c>, so a number or a setting under a name its node has not got is a typo found here.
    /// </summary>
    [Fact]
    public void Every_factory_compound_is_a_graph_of_nodes_Patina_has() {
        FactoryCompounds.Register();

        foreach (var path in FactoryCompounds.Paths) {
            var (compiler, graph) = CompoundBench.Containing(path, out _, out _);
            var compilation = compiler.Compile(graph);

            Assert.True(
                compilation.Diagnostics.IsEmpty,
                $"{path}: {string.Join("; ", compilation.Diagnostics.Select(static diagnostic => diagnostic.Message))}"
            );
            Assert.NotEmpty(compilation.Value.Ops);
        }
    }

    /// <summary>
    ///     Every starter material compiles with nothing to say, since a bake refuses a key nothing reads.
    /// </summary>
    [Fact]
    public void Every_starter_material_compiles_with_nothing_to_say() {
        FactoryCompounds.Register();
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        var library = TextureCompoundLibrary.Publish(registry, null, out _);
        var folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, "../../../content/materials"));
        var files = Directory.GetFiles(folder, "*" + TextureCompoundLibrary.Extension);

        Assert.NotEmpty(files);

        foreach (var file in files) {
            var asset = YamlSerializer.Parse<NodeGraphAsset>(File.ReadAllText(file));
            var model = NodeGraphDocument.Load(asset, out var repairs);
            TextureGraphCompiler compiler = new(registry) { SubGraphSource = library };
            var diagnostics = compiler.Compile(model).Diagnostics;

            Assert.Empty(repairs);
            var said = string.Join("; ", diagnostics.Select(static diagnostic => diagnostic.Message));

            Assert.True(diagnostics.IsEmpty, $"{Path.GetFileName(file)}: {said}");
        }
    }

    /// <summary>
    ///     Not flat, and no seam: across each edge a texel differs from the texel on the far side about as much as two
    ///     neighbours differ inside the picture.
    /// </summary>
    [Fact]
    public void Every_factory_compound_draws_a_picture_that_tiles() {
        FactoryCompounds.Register();

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Gpu.Skip($"No Vulkan device: {refusal}");

            return;
        }

        using (device) {
            using TexturePlanEvaluator evaluator = new(device);
            List<string> problems = [];

            // A compound that reads the mesh or changes a picture is no tile of its own; MeshCompoundTests holds those.
            foreach (var path in FactoryCompounds.Paths.Except(MeshCompoundTests.Named, StringComparer.Ordinal)) {
                const int Side = 128;
                var (compiler, graph) = Containing(path, Side);
                var plan = compiler.Compile(graph).Value;

                using TextureUploads uploads = new(device);
                Assert.Empty(TextureGraphExternals.Upload(uploads, plan, compiler.Externals));

                using var bake = evaluator.Evaluate(plan, uploads.Externals);
                var picture = bake.Read(compiler.Outputs.Single().Image);
                var (seam, inside, spread) = Measure(picture);

                if (spread < 0.05) {
                    problems.Add($"{path} is flat: its values span {spread:0.000}");
                }

                if (seam > 2.5 * inside + 0.01) {
                    problems.Add($"{path} has a seam: {seam:0.0000} across the edges against {inside:0.0000} inside");
                }
            }

            Assert.Empty(problems);
        }
    }

    /// <summary>The mean step across the two seams, the mean step between neighbours, and the value range.</summary>
    static (double Seam, double Inside, double Spread) Measure(Bitmap picture) {
        double At(int x, int y) => picture.Pixels[(y * picture.Width + x) * 4] / 255.0;

        var (seam, inside, lowest, highest) = (0.0, 0.0, 1.0, 0.0);
        var (w, h) = (picture.Width, picture.Height);

        for (var y = 0; y < h; y++) {
            seam += Math.Abs(At(w - 1, y) - At(0, y));

            for (var x = 1; x < w; x++) {
                inside += Math.Abs(At(x, y) - At(x - 1, y));
                lowest = Math.Min(lowest, At(x, y));
                highest = Math.Max(highest, At(x, y));
            }
        }

        for (var x = 0; x < w; x++) {
            seam += Math.Abs(At(x, h - 1) - At(x, 0));

            for (var y = 1; y < h; y++) {
                inside += Math.Abs(At(x, y) - At(x, y - 1));
            }
        }

        return (seam / (w + h), inside / (w * (h - 1) + h * (w - 1)), highest - lowest);
    }

    /// <summary>A graph holding one compound and an output, at a size, tiling, with the library published.</summary>
    static (TextureGraphCompiler Compiler, NodeGraphModel Graph) Containing(string path, int side) {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        var library = TextureCompoundLibrary.Publish(registry, null, out var problems);

        Assert.Empty(problems.Where(static problem => FactoryCompounds.Paths.Contains(problem.Path)));

        NodeGraphModel graph = new();
        var used = graph.Add(path);
        var output = graph.Add("Output/Output");

        output.SetText("Usage", "mask");
        graph.Connect(new(used.Id, "Out"), new(output.Id, "Input"));

        TextureGraphCompiler compiler = new(registry) {
            BaseWidth = side, BaseHeight = side, Seed = 1, SubGraphSource = library
        };

        return (compiler, graph);
    }
}

/// <summary>Patina.Server.Tests' rule: a machine with no GPU skips, and nothing else may.</summary>
static class Gpu {
    public static void Skip(string reason) {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PATINA_REQUIRE_VULKAN"))) {
            Assert.Fail($"Vulkan is required here: {reason}");
        }

        Assert.Skip(reason);
    }
}
