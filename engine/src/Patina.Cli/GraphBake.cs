using Patina.Graphs;
using Patina.Library;

// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Imaging;
using Vixen.Core.Yaml;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Cli;

/// <summary>
///     Evaluates a texture graph on a GPU and writes each output as a PNG, with no UI and no project. It reads a graph
///     as the Worker does, through the same compound shelf and picture resolver, so a graph that previews also bakes.
/// </summary>
internal static class GraphBake {
    /// <param name="graph">The graph file.</param>
    /// <param name="folder">Where the PNGs go.</param>
    /// <param name="library">The library its pictures may be under, or null for the one around it, if any.</param>
    public static int Run(string graph, string folder, string? library = null) {
        if (!File.Exists(graph)) {
            Console.Error.WriteLine($"There is no file at '{graph}'.");
            return 2;
        }

        var model = NodeGraphDocument.Load(
            CompoundShelf.WithoutBoundaries(YamlSerializer.Parse<NodeGraphAsset>(File.ReadAllText(graph))),
            out var repairs
        );

        foreach (var repair in repairs) {
            Console.Error.WriteLine(repair.Message);
        }

        var compounds = new CompoundShelf().Of(CompoundFolders.Of(graph));
        var compiler = new TextureGraphCompiler(compounds.Registry) { SubGraphSource = compounds.Source };
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
            var filled = new GraphPictures().Fill(uploads, plan, owed, CompoundFolders.PictureRoots(graph, library));

            if (filled.Refusals.Count > 0) {
                foreach (var problem in filled.Refusals) {
                    Console.Error.WriteLine($"{problem.Id}: {problem.Message}");
                }

                Console.Error.WriteLine("Nothing baked: this graph reads something that cannot be read.");
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
