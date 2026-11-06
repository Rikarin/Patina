using Patina.Library;
using System.Globalization;
using System.Text;

namespace Patina.Bench;

/// <summary>The shape of a generated library.</summary>
/// <param name="Projects">How many projects.</param>
/// <param name="Seed">The seed: one seed and one shape give the same bytes, on any machine.</param>
/// <param name="AssetsPerThousandProjects">Shared assets per 1,000 projects, at least 50 in all.</param>
/// <param name="PinPercent">The share of references that are pinned.</param>
public sealed record GeneratorOptions(
    int Projects,
    int Seed = 42,
    int AssetsPerThousandProjects = 200,
    int PinPercent = 2
);

/// <summary>What the generator made that a measurement needs to find again.</summary>
/// <param name="Popular">The most referenced asset, which S6 uses for its fan-out.</param>
/// <param name="Projects">Every project's path below <c>projects/</c>.</param>
/// <param name="Assets">Every asset's path below <c>projects/</c>.</param>
public sealed record GeneratedLibrary(AssetId Popular, IReadOnlyList<string> Projects, IReadOnlyList<string> Assets);

/// <summary>
///     Writes a library that looks like a game's: a project reads a handful of materials, popularity follows a Zipf
///     curve so that one material is everywhere, smart materials read graphs, and a few references are pinned.
///     Payloads are small placeholders, because the index reads documents; the corpus says nothing about hashing a
///     real mesh.
/// </summary>
public static class LibraryGenerator {
    const uint GraphShare = 3; // every third asset is a graph that smart materials read
    const int PayloadBytes = 2048;

    static readonly string[] Subjects = [
        "crate", "barrel", "door", "wall", "fence", "lamp", "chair", "table", "rock", "tree", "pipe", "sign", "wheel",
        "shield", "sword", "helmet", "cart", "bridge", "statue", "column", "roof", "window", "stairs", "chest", "banner"
    ];

    static readonly string[] Finishes = [
        "rust", "oak", "pine", "steel", "brass", "copper", "stone", "marble", "moss", "mud", "leather", "cloth",
        "paint",
        "chrome", "ice", "sand", "clay", "brick", "tile", "bone"
    ];

    static readonly string[] Zones = ["props", "characters", "vehicles", "environment", "weapons", "architecture"];

    static readonly string[] Tags = [
        "hero", "background", "worn", "clean", "indoor", "outdoor", "medieval", "industrial", "sci-fi", "lod0", "wip",
        "approved", "metal", "wood", "organic", "kit"
    ];

    static readonly string[] Bakes = ["ambient-occlusion", "curvature", "normal", "thickness", "position"];

    public static GeneratedLibrary Generate(string root, GeneratorOptions options) {
        var random = new Random(options.Seed);
        LibraryFiles.CreateLibrary(
            root,
            new(NewId<LibraryId>(random), "Generated", 1024, ["baseColor", "normal", "roughness"], "pbr", ["default"])
        );

        foreach (var folder in new[] { "graphs", "smart-materials" }) {
            LibraryFiles.WriteProject(root, folder, new(NewId<ProjectId>(random), folder, [], [], []));
        }

        var assets = WriteAssets(root, options, random);
        var weights = Zipf(assets.Count);
        List<string> projects = [];
        // Documents first, one random stream, so that the seed decides everything; the writes then run in parallel.
        List<(string Path, ProjectDocument Document)> pending = [];
        for (var i = 0; i < options.Projects; i++) {
            var path = ProjectPath(i, random);
            projects.Add(path);
            pending.Add((path, Project(i, assets, weights, options, random)));
        }

        Parallel.ForEach(pending, project => WriteProject(root, project.Path, project.Document));
        return new(assets[0].Id, projects, [..assets.Select(static a => a.Path)]);
    }

    static List<GeneratedAsset> WriteAssets(string root, GeneratorOptions options, Random random) {
        var count = Math.Max(50, options.Projects * options.AssetsPerThousandProjects / 1000);
        List<GeneratedAsset> assets = [];
        List<GeneratedAsset> graphs = [];
        for (var i = 0u; i < count; i++) {
            var name = $"{Pick(Finishes, random)} {Pick(Subjects, random)} {i}";
            var graph = i % GraphShare == 1;
            var folder = graph ? "graphs" : "smart-materials";
            const string extension = "vxtexgraph";
            var path = $"{folder}/{name.Replace(' ', '_')}.{extension}";

            var content = Encoding.UTF8.GetBytes($"version: 1\nname: {name}\nseed: {random.Next()}\n");
            List<AssetReference> dependencies = [];
            if (!graph) {
                dependencies.AddRange(
                    graphs.OrderBy(_ => random.Next())
                        .Take(random.Next(1, 4))
                        .Select(static g => AssetReference.Floating(g.Id))
                );
            }

            var id = NewId<AssetId>(random);
            LibraryFiles.WriteAsset(
                root,
                path,
                content,
                new(id, graph ? "graph" : "smart-material", name, SomeTags(random), dependencies)
            );
            var asset = new GeneratedAsset(id, path, Revision.Of(content));
            assets.Add(asset);
            if (graph) {
                graphs.Add(asset);
            }
        }

        return assets;
    }

    static ProjectDocument Project(
        int index,
        List<GeneratedAsset> assets,
        double[] weights,
        GeneratorOptions options,
        Random random
    ) {
        var used = new Dictionary<AssetId, AssetReference>();
        var wanted = random.Next(3, 13);
        for (var i = 0; i < wanted; i++) {
            var asset = assets[Sample(weights, random)];
            used[asset.Id] = random.Next(100) < options.PinPercent
                ? AssetReference.Pinned(asset.Id, asset.Revision)
                : AssetReference.Floating(asset.Id);
        }

        var references = used.Values.ToList();
        var outputs = Enumerable.Range(0, random.Next(2, 5))
            .Select(i => new OutputDeclaration(
                    Bakes[i],
                    $"bake.{Bakes[i]}",
                    ["meshes/low.glb", "meshes/high.glb"],
                    [..references.Take(4).Select(static r => r.Asset)],
                    new Dictionary<string, string> {
                        ["resolution"] = (1024 << random.Next(3)).ToString(CultureInfo.InvariantCulture)
                    }
                )
            )
            .ToList();
        var name = $"{Pick(Finishes, random)} {Pick(Subjects, random)} {index:D5}";
        return new(NewId<ProjectId>(random), name, SomeTags(random), references, outputs);
    }

    /// <summary>The non-document files a real project has, which a scan must not be slowed down by.</summary>
    static void WriteProject(string root, string path, ProjectDocument document) {
        LibraryFiles.WriteProject(root, path, document);
        var folder = Path.GetDirectoryName(LibraryFiles.ProjectFile(root, path))!;
        Directory.CreateDirectory(Path.Combine(folder, "meshes"));
        var payload = Encoding.UTF8.GetBytes(document.Id + new string('.', PayloadBytes));
        File.WriteAllBytes(Path.Combine(folder, "meshes", "low.glb"), payload);
        File.WriteAllBytes(Path.Combine(folder, "meshes", "high.glb"), payload);
        File.WriteAllText(Path.Combine(folder, "crate.vxlayers"), "version: 1\nlayers: []\n");
        File.WriteAllText(Path.Combine(folder, "bakes.yaml"), "version: 1\nresolution: 2048\n");
    }

    static string ProjectPath(int index, Random random) =>
        $"{Pick(Zones, random)}/{index / 100:D3}/{Pick(Subjects, random)}_{index:D5}";

    static string[] SomeTags(Random random) =>
        [..Enumerable.Range(0, random.Next(1, 5)).Select(_ => Pick(Tags, random)).Distinct()];

    static T Pick<T>(T[] items, Random random) => items[random.Next(items.Length)];

    static T NewId<T>(Random random) where T : IGuidId<T> {
        Span<byte> bytes = stackalloc byte[16];
        random.NextBytes(bytes);
        return T.From(new(bytes));
    }

    /// <summary>The running total of a Zipf curve, s = 1: the first asset is the popular one.</summary>
    static double[] Zipf(int count) {
        var weights = new double[count];
        var total = 0d;
        for (var i = 0; i < count; i++) {
            total += 1d / (i + 1);
            weights[i] = total;
        }

        return weights;
    }

    static int Sample(double[] weights, Random random) {
        var found = Array.BinarySearch(weights, random.NextDouble() * weights[^1]);
        return Math.Min(found < 0 ? ~found : found, weights.Length - 1);
    }

    sealed record GeneratedAsset(AssetId Id, string Path, Revision Revision);
}
