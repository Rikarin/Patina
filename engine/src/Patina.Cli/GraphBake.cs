using Patina.Contracts;
using Patina.Graphs;
using Patina.Library;
using System.Globalization;

// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Imaging;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Cli;

/// <summary>
///     Evaluates a texture graph or a package on a GPU and writes each output as a PNG, with no UI, no project and no
///     Server (#186). It reads a graph as the Worker does, through the same compound shelf and picture resolver, so a
///     graph that previews also bakes; a package is a graph with its compounds and pictures beside it (ADR 0027).
/// </summary>
internal static class GraphBake {
    public const string Usage = """
                                  patina bake <graph.vxtexgraph | package.ptpkg> <folder> [--library <path>]
                                              [--mesh-maps <bake folder>] [--size <side>] [--seed <n>]
                                              [--preset <name>] [--param <name>=<value>]...
                                """;

    /// <summary>What the options after the folder say.</summary>
    public sealed class Read {
        public string? Library { get; set; }
        public string? MeshMaps { get; set; }
        public string? Size { get; set; }
        public int? Seed { get; set; }
        public string? Preset { get; set; }
        public List<KeyValuePair<string, string>> Values { get; } = [];
    }

    /// <summary>The options after the folder, or null when they are not options this takes.</summary>
    public static Read? Options(string[] options) {
        Read found = new();
        for (var at = 0; at < options.Length; at += 2) {
            if (at + 1 >= options.Length) {
                return null;
            }

            var value = options[at + 1];
            switch (options[at]) {
                case "--library":
                    found.Library = value;
                    break;
                case "--mesh-maps":
                    found.MeshMaps = Path.GetFullPath(value);
                    break;
                case "--size":
                    found.Size = value;
                    break;
                case "--seed" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed):
                    found.Seed = seed;
                    break;
                case "--preset":
                    found.Preset = value;
                    break;
                case "--param" when value.IndexOf('=', StringComparison.Ordinal) is > 0 and var equals:
                    found.Values.Add(new(value[..equals], value[(equals + 1)..]));
                    break;
                default:
                    return null;
            }
        }

        return found;
    }

    /// <param name="graph">The graph file, or a package's folder.</param>
    /// <param name="folder">Where the PNGs go.</param>
    /// <param name="options">The rest: where its pictures and bake are, its size, seed and parameter values.</param>
    public static int Run(string graph, string folder, Read options) {
        NodeGraphAsset asset;
        string file;
        try {
            (file, asset, _) = GraphFactsOf.Read(graph);
            asset = GraphFactsOf.WithValues(asset, options.Preset, options.Values, options.Seed);
        } catch (Exception e) when (e is FileNotFoundException or LibraryFormatException or ArgumentException) {
            Console.Error.WriteLine($"Nothing baked: {e.Message}");
            return 2;
        }

        var model = NodeGraphDocument.Load(CompoundShelf.WithoutBoundaries(asset), out var repairs);

        foreach (var repair in repairs) {
            Console.Error.WriteLine(repair.Message);
        }

        var compounds = new CompoundShelf().Of(CompoundFolders.Of(file));
        var compiler = new TextureGraphCompiler(compounds.Registry) { SubGraphSource = compounds.Source };
        if (GraphSize.Apply(compiler, model, options.Size) is { } badSize) {
            Console.Error.WriteLine($"Nothing baked: {badSize}");
            return 2;
        }

        var compilation = compiler.Compile(model);

        foreach (var diagnostic in compilation.Diagnostics) {
            Console.Error.WriteLine($"{diagnostic.Id}: {diagnostic.Message}");
        }

        if (!compilation.Succeeded || compiler.Outputs.Length == 0) {
            Console.Error.WriteLine("Nothing baked: the graph did not compile, or it has no Output node.");
            return 1;
        }

        var plan = compilation.Value;

        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Console.Error.WriteLine($"Nothing baked: no Vulkan device. {refusal}");
            return 1;
        }

        using (device) {
            using TextureUploads uploads = new(device);

            var owed = TextureGraphExternals.Upload(uploads, plan, compiler.Externals);
            var roots = CompoundFolders.PictureRoots(file, options.Library);
            var filled = new GraphPictures().Fill(
                uploads,
                plan,
                owed,
                roots,
                options.MeshMaps ?? DocumentBakes.OfFile(file)
            );

            if (filled.Refusals.Count > 0) {
                foreach (var problem in filled.Refusals) {
                    Console.Error.WriteLine($"{problem.Id}: {problem.Message}");
                }

                Console.Error.WriteLine("Nothing baked: this graph reads something that cannot be read.");
                return 1;
            }

            using TexturePlanEvaluator evaluator = new(device);
            using var bake = evaluator.Evaluate(plan, uploads.Externals);

            foreach (var check in compiler.Checks.Where(check => check.Failed(bake))) {
                Console.Error.WriteLine($"{check.Problem.Id}: {check.Problem.Message}");
            }

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
