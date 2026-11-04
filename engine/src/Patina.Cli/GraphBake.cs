// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Imaging;
using Vixen.Core.Yaml;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Cli;

/// <summary>Evaluates a texture graph on a GPU and writes each output as a PNG, with no UI and no project.</summary>
internal static class GraphBake {
    public static int Run(string graph, string folder) {
        if (!File.Exists(graph)) {
            Console.Error.WriteLine($"There is no file at '{graph}'.");
            return 2;
        }

        var model = NodeGraphDocument.Load(
            YamlSerializer.Parse<NodeGraphAsset>(File.ReadAllText(graph)),
            out var repairs
        );

        foreach (var repair in repairs) {
            Console.Error.WriteLine(repair.Message);
        }

        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);

        var compounds = TextureCompoundLibrary.Publish(
            registry,
            Path.GetDirectoryName(Path.GetFullPath(graph))!,
            out _
        );
        var compiler = new TextureGraphCompiler(registry) { SubGraphSource = compounds };
        var compilation = compiler.Compile(model);

        foreach (var diagnostic in compilation.Diagnostics) {
            Console.Error.WriteLine($"{diagnostic.Id}: {diagnostic.Message}");
        }

        if (compilation.Value is not { } plan || compiler.Outputs.Length == 0) {
            Console.Error.WriteLine("Nothing baked: the graph did not compile, or it has no Output node.");
            return 1;
        }

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Console.Error.WriteLine($"Nothing baked: no Vulkan device. {refusal}");
            return 1;
        }

        using (device) {
            using TextureUploads uploads = new(device);

            var owed = TextureGraphExternals.Upload(uploads, plan, compiler.Externals);

            if (owed.Length > 0) {
                Console.Error.WriteLine(
                    $"Nothing baked: this graph reads {owed.Length} external image(s), which this command cannot supply yet."
                );
                return 1;
            }

            using TexturePlanEvaluator evaluator = new(device);
            using var bake = evaluator.Evaluate(plan, uploads.Externals);

            Directory.CreateDirectory(folder);

            foreach (var written in compiler.Outputs) {
                var path = Path.Combine(folder, written.Usage + ".png");

                File.WriteAllBytes(path, PngCodec.Encode(bake.Read(written.Image)));
                Console.WriteLine(path);
            }
        }

        return 0;
    }
}
