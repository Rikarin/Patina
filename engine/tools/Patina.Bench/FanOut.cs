using Patina.Contracts;
using Patina.Export;
using Patina.Export.Builds;
using Patina.Graphs;
using Patina.Library;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Vixen.Core.Imaging;
using Vixen.Core.Yaml;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Bench;

/// <summary>
///     A shared material read by every project, edited once, rebuilt. Two measurements, because no one machine runs the
///     real thing at the full size: the whole pipeline over <c>projects</c> projects with small stand-in maps, which is
///     everything a build does but the pixels, and a real one, GPU and encoder, on <c>real</c> projects at <c>side</c>.
/// </summary>
static class FanOut {
    const int NoiseNode = 1;

    public static async Task<int> RunAsync(string folder, int projects, int real, int side, string[] presets) {
        Console.WriteLine($"fan-out: {projects:N0} projects through the pipeline, {real:N0} on real pixels at {side}²");
        await Pipeline(Path.Combine(folder, "pipeline.ptlib"), projects, presets);
        return await Real(Path.Combine(folder, "real.ptlib"), real, side, presets);
    }

    /// <summary>A library with one project whose output is a bake, for what must run with no GPU.</summary>
    public static int WriteBakeLibrary(string root) {
        Directory.CreateDirectory(root);
        LibraryFiles.CreateLibrary(root, new(LibraryId.From(Guid.NewGuid()), "Bakes", 1024, [], "pbr", ["godot"]));
        LibraryFiles.WriteProject(
            root,
            "crate",
            new(
                ProjectId.From(Guid.NewGuid()),
                "Crate",
                [],
                [],
                [
                    new OutputDeclaration(
                        "bake",
                        BakeKeys.Tool,
                        [BakeFixture.LowFile, BakeFixture.HighFile],
                        [],
                        new Dictionary<string, string> {
                            ["low"] = BakeFixture.LowFile,
                            ["high"] = BakeFixture.HighFile,
                            ["resolution"] = "128",
                            ["rays"] = "16",
                            ["maps"] = "ao"
                        }
                    )
                ]
            )
        );
        return BakeFixture.Write(Path.Combine(root, "projects", "crate"), 0.05f);
    }

    static async Task Pipeline(string root, int projects, string[] presets) {
        Write(root, projects, 64, presets);
        using var library = OpenLibrary.Open(root);
        var popular = Popular(root);
        var timer = Stopwatch.StartNew();
        library.Rescan();
        Row("first rescan", timer, "");

        var executor = new StandIn();
        var build = new BuildPipeline(library, executor, ToolCatalog.Default);
        var first = await Build(build, root, "first", new() { Presets = presets });
        Report("first build (stand-in maps, real export of them)", first);

        Edit(popular);
        library.Rescan();

        timer.Restart();
        var plan = BuildPipeline.Plan(library, new() { Asset = popular.Id.Value, Presets = presets }, ToolCatalog.Default, default);
        var preview = plan.Preview(0, 50);
        Row("impact preview over the shared graph", timer, $"{preview.Projects:N0} projects, {preview.Rebuilds:N0} to build, {preview.Exports:N0} exports, {preview.Pinned:N0} pinned");

        executor.Reset();
        var second = await Build(build, root, "edit", new() { Presets = presets });
        Report("rebuild after one edit", second);
        Console.WriteLine($"  distinct keys run: {executor.Outputs:N0}");

        executor.Reset();
        var third = await Build(build, root, "again", new() { Presets = presets });
        Report("second build, nothing changed", third);
        Console.WriteLine($"  outputs run: {executor.Outputs:N0}, exports run: {executor.Exports:N0}");
    }

    static async Task<int> Real(string root, int projects, int side, string[] presets) {
        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Console.WriteLine($"No Vulkan device, so no real pixels: {refusal}");
            return 0;
        }

        using (device) {
            using var evaluator = new TexturePlanEvaluator(device) { Results = { Budget = 2L << 30 } };
            Write(root, projects, side, presets);
            using var library = OpenLibrary.Open(root);
            var popular = Popular(root);
            var executor = new Real_(new(() => evaluator, static work => work()));
            var build = new BuildPipeline(library, executor, ToolCatalog.Default);
                        Report("first build", await Build(build, root, "first", new() { Presets = presets }));

            Edit(popular);
            library.Rescan();
            var timer = Stopwatch.StartNew();
            var before = Directory.EnumerateFiles(Path.Combine(root, "exports"), "*.*", SearchOption.AllDirectories).Count();
            Report("rebuild after one edit", await Build(build, root, "edit", new() { Presets = presets }));
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {timer.Elapsed.TotalSeconds / Math.Max(1, projects) * 1000:F0} ms per project, {before:N0} files in exports"
                )
            );
            Report("second build, nothing changed", await Build(build, root, "again", new() { Presets = presets }));
        }

        return 0;
    }

    static async Task<LibraryReport> Build(BuildPipeline build, string root, string name, BuildRequest request) {
        var journal = new BuildJournal(Path.Combine(LibraryLayout.ReportsPath(root), name), name, DateTimeOffset.UtcNow);
        return await build.RunAsync(journal, request, null, null, TimeProvider.System, default);
    }

    static void Report(string what, LibraryReport report) =>
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{what}: {(report.Finished - report.Started)!.Value.TotalSeconds:F2} s; {report.Projects:N0} projects touched, {report.Built:N0} built, {report.Reused:N0} reused, {report.Fresh:N0} fresh, {report.Exported:N0} exported, {report.ExportsFresh:N0} exports fresh, {report.Files:N0} files, {report.Failed:N0} failed"
            )
        );

    static void Row(string what, Stopwatch timer, string note) =>
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{what}: {timer.Elapsed.TotalMilliseconds:F0} ms {note}"));

    static (AssetId Id, string Path) Popular(string root) {
        var sidecar = Directory.EnumerateFiles(Path.Combine(root, "shared", "graphs"), "*.asset.yaml").Single();
        var read = DocumentCodec.ReadAsset(File.ReadAllText(sidecar), sidecar).Document;
        return (read.Id, sidecar[..^LibraryLayout.SidecarSuffix.Length]);
    }

    /// <summary>A change of the graph's file, as an artist's save is: its seed, so every output moves.</summary>
    static void Edit((AssetId Id, string Path) popular) {
        var text = File.ReadAllText(popular.Path);
        var edited = text.Replace(": 7", ": 8", StringComparison.Ordinal).Replace(": '7'", ": '8'", StringComparison.Ordinal);
        File.WriteAllText(popular.Path, edited == text ? $"{text}\n" : edited);
    }

    /// <summary>A library where every project reads one graph, each with a noise scale of its own.</summary>
    static void Write(string root, int projects, int side, string[] presets) {
        Directory.CreateDirectory(root);
        LibraryFiles.CreateLibrary(root, new(LibraryId.From(Guid.NewGuid()), "Fan-out", 1024, [], "pbr", presets));
        var asset = AssetId.From(Guid.NewGuid());
        LibraryFiles.WriteAsset(
            root,
            "graphs/popular.vxtexgraph",
            Encoding.UTF8.GetBytes(YamlSerializer.ToYaml(Graph(side))),
            new(asset, "graph", "Popular", [], [])
        );

        Parallel.For(
            0,
            projects,
            index => {
                var scale = (2 + index % 5000 * 0.01).ToString("R", CultureInfo.InvariantCulture);
                LibraryFiles.WriteProject(
                    root,
                    $"props/{index / 100:D3}/prop_{index:D5}",
                    new(
                        ProjectId.From(Guid.NewGuid()),
                        $"Prop {index:D5}",
                        [],
                        [AssetReference.Floating(asset)],
                        [
                            new OutputDeclaration(
                                "maps",
                                "texture-graph",
                                [],
                                [asset],
                                new Dictionary<string, string> {
                                    [$"{OutputBuilders.ValuePrefix}{NoiseNode}:Scale"] = scale
                                }
                            )
                        ]
                    )
                );
            }
        );
    }

    static NodeGraphAsset Graph(int side) {
        GraphNodeAsset Node(int id, string type, string? usage = null) =>
            new() { Id = id, Type = type, X = id * 280, Texts = usage is null ? [] : new() { ["Usage"] = usage } };
        GraphEdgeAsset Edge(int from, int to, string port) => new() { FromNode = from, FromPort = "Out", ToNode = to, ToPort = port };

        return new() {
            Name = "Popular",
            Nodes = [
                Node(NoiseNode, "Source/Noise"),
                Node(2, "Colour/HSL"),
                Node(3, "Surface/Height to Normal"),
                Node(4, "Output/Output", "baseColor"),
                Node(5, "Output/Output", "normal"),
                Node(6, "Output/Output", "roughness"),
                Node(7, "Output/Output", "height")
            ],
            Edges = [
                Edge(NoiseNode, 2, "Input"),
                Edge(NoiseNode, 3, "Height"),
                Edge(2, 4, "Input"),
                Edge(3, 5, "Input"),
                Edge(NoiseNode, 6, "Input"),
                Edge(NoiseNode, 7, "Input")
            ],
            Settings = new() { ["seed"] = "7", ["baseWidth"] = $"{side}", ["baseHeight"] = $"{side}" }
        };
    }

    /// <summary>Everything but the GPU: four small maps, so that the keys, the objects and the exports are real.</summary>
    sealed class StandIn : IBuildExecutor {
        int outputs;
        int exports;

        public int Outputs => outputs;

        public int Exports => exports;

        public void Reset() {
            outputs = 0;
            exports = 0;
        }

        public Task<BuildManifest> OutputAsync(OutputStep work, CancellationToken cancellationToken) {
            outputs++;
            Directory.CreateDirectory(work.Folder);
            var image = new FloatImage(2, 2, 1, [0f, 0.25f, 0.5f, 1f]);
            BuiltMap[] maps = [
                MapPictures.Write(work.Folder, "roughness", image, true, null),
                MapPictures.Write(work.Folder, "metalness", image, true, null),
                MapPictures.Write(work.Folder, "occlusion", image, true, null),
                MapPictures.Write(work.Folder, "baseColor", image, true, null)
            ];
            return Task.FromResult<BuildManifest>(new(work.Tool, maps, [], []));
        }

        public Task<ExportManifest> ExportAsync(ExportStep work, CancellationToken cancellationToken) {
            exports++;
            return Task.FromResult(OutputBuilders.Export(work, cancellationToken));
        }
    }

    sealed class Real_(OutputBuilders builders) : IBuildExecutor {
        public Task<BuildManifest> OutputAsync(OutputStep work, CancellationToken cancellationToken) =>
            Task.FromResult(builders.Build(work, 0, cancellationToken));

        public Task<ExportManifest> ExportAsync(ExportStep work, CancellationToken cancellationToken) =>
            Task.FromResult(OutputBuilders.Export(work, cancellationToken));
    }
}
