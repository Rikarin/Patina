using Patina.Contracts;
using Patina.Export;
using Patina.Export.Builds;
using Patina.Export.Presets;
using Patina.Jobs;
using Patina.Library;
using System.Globalization;
using Vixen.Core.Imaging;
using Vixen.Core.Yaml;
using Vixen.Graphics;
using Vixen.NodeGraph;
using Vixen.TextureGraph;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Graphs;

/// <summary>
///     What runs a step of a build, in a Worker or in <c>patina build</c>. Each step reads files, evaluates, and leaves
///     a folder that appears whole. The GPU work goes through <paramref name="gpu" />, which a Worker uses to take
///     turns with the graphs and stacks it is also answering for.
/// </summary>
/// <param name="evaluator">The evaluator, opened on first use: a step that needs no GPU needs no device.</param>
/// <param name="gpu">Runs the part of a step that holds the device.</param>
public sealed class OutputBuilders(
    Func<TexturePlanEvaluator> evaluator,
    Func<Func<TextureRawImage[]>, TextureRawImage[]> gpu) {
    /// <summary>
    ///     The prefix of a setting that writes a value into a graph: <c>value:node:port</c>, then
    ///     numbers.
    /// </summary>
    public const string ValuePrefix = "value:";

    readonly CompoundShelf compounds = new();
    readonly GraphPictures pictures = new();

    /// <summary>Builds an output.</summary>
    /// <exception cref="BuildStepFailedException">It cannot be built, however often it is tried.</exception>
    public BuildManifest Build(OutputStep step, int threads, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(step);
        var part = $"{step.Folder}.{Guid.NewGuid():N}.part";
        Directory.CreateDirectory(part);
        try {
            var manifest = step.Tool switch {
                BuildTools.TextureGraph => Graph(step, part),
                BuildTools.LayerStack => Stack(step, part),
                BakeKeys.Tool => Bake(step, part, threads, cancellationToken),
                BuildTools.MeshLod => MeshLods.Build(step, part, cancellationToken),
                BuildTools.BakedMaps => Baked(step, part),
                _ => throw new BuildStepFailedException($"Nothing builds the tool '{step.Tool}'.")
            };
            BuildObjects.Publish(part, step.Folder);
            return manifest;
        } catch (Exception e) when (e is InvalidDataException or ArgumentException or FileNotFoundException) {
            Directory.Delete(part, true);
            throw new BuildStepFailedException(e.Message, e);
        } catch {
            Directory.Delete(part, true);
            throw;
        }
    }

    /// <summary>Runs a preset over a project's maps.</summary>
    /// <exception cref="BuildStepFailedException">A preset or a map cannot be used.</exception>
    public static ExportManifest Export(ExportStep step, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(step);
        try {
            var preset = ExportPresetReader.Read(step.Preset, "the preset");
            return ExportRun.Run(preset, SetsOf(step.Sources), step.Folder, null, cancellationToken);
        } catch (Exception e) when (e is InvalidDataException or ExportPresetException or ArgumentException) {
            throw new BuildStepFailedException(e.Message, e);
        }
    }

    /// <summary>
    ///     Each texture set's maps, merged by set so that one set's maps cannot replace another's. A source with no
    ///     set is every set's, under what each makes for itself.
    /// </summary>
    static List<ExportSet> SetsOf(IReadOnlyList<ExportSource> sources) {
        var named = sources.Select(static s => s.Set).Where(static s => s.Length > 0).Distinct(StringComparer.Ordinal);
        List<string> sets = [..named.DefaultIfEmpty(string.Empty)];
        return [
            ..sets.Select(set => {
                    Dictionary<string, MapPicture> maps = new(StringComparer.Ordinal);
                    foreach (var source in sources.Where(s => s.Set.Length == 0 || s.Set == set)) {
                        foreach (var map in MapPictures.Read(source.Folder, source.Manifest)) {
                            maps[map.Name] = map;
                        }
                    }

                    return new ExportSet(set, maps);
                }
            )
        ];
    }

    /// <summary>The bake a document's own output names, or the one chosen for its file.</summary>
    static string? BakeOf(OutputStep step, string file) =>
        step.Settings.GetValueOrDefault(ProjectOutputs.BakeSetting) is { } bake
            ? Path.GetFullPath(bake, Path.GetDirectoryName(file)!)
            : GraphMeshMaps.Read(file);

    BuildManifest Graph(OutputStep step, string folder) {
        var graph = step.Settings.ContainsKey(ProjectOutputs.GraphSetting)
            ? MeshMapSettings.Role(step, ProjectOutputs.GraphSetting)
            : step.Assets.Concat(step.Files).FirstOrDefault()
            ?? throw new InvalidDataException("A texture-graph output names no graph.");
        var model = NodeGraphDocument.Load(
            CompoundShelf.WithoutBoundaries(YamlSerializer.Parse<NodeGraphAsset>(File.ReadAllText(graph))),
            out _
        );
        Apply(model, step.Settings);

        var shelf = compounds.Of(CompoundFolders.Of(graph));
        var compiler = new TextureGraphCompiler(shelf.Registry) { SubGraphSource = shelf.Source };
        var compilation = compiler.Compile(model);
        if (!compilation.Succeeded || compiler.Outputs.Length == 0) {
            var problems = string.Join(' ', compilation.Diagnostics.Select(static d => $"{d.Id}: {d.Message}"));
            throw new InvalidDataException($"'{Path.GetFileName(graph)}' has no output to build. {problems}");
        }

        var plan = compilation.Value;
        var outputs = compiler.Outputs;
        var raw = gpu(() => {
                var runner = evaluator();
                using var uploads = new TextureUploads(runner.Device);
                var owed = TextureGraphExternals.Upload(uploads, plan, compiler.Externals);
                var roots = CompoundFolders.PictureRoots(graph, step.Library);
                var filled = pictures.Fill(uploads, plan, owed, roots, BakeOf(step, graph));
                if (filled.Refusals.Count > 0) {
                    throw new InvalidDataException(string.Join(' ', filled.Refusals.Select(static r => r.Message)));
                }

                using var bake = runner.Evaluate(plan, uploads.Externals);
                return [..bake.ReadRaw([..outputs.Select(static output => output.Image)])];
            }
        );

        return Pictures(step, folder, [..outputs.Select(static o => o.Usage)], raw, GreenOf(step, GreenAxis.Down));
    }

    BuildManifest Stack(OutputStep step, string folder) {
        var file = step.Files.FirstOrDefault(static file => file.EndsWith(
                    StackInputs.Extension,
                    StringComparison.Ordinal
                )
            )
            ?? throw new InvalidDataException("A layer-stack output names no stack.");
        var meshMaps = BakeOf(step, file);
        var text = File.ReadAllText(file);
        StackInputs.CheckPaint(file, text);
        var document = LayerStackYaml.Read(text);
        var set = LayerStackEdit.SetFor(document, step.Settings.GetValueOrDefault("set", string.Empty))
            ?? throw new InvalidDataException($"'{Path.GetFileName(file)}' has no texture set.");
        var shelf = compounds.Of(CompoundFolders.Of(file));
        var roots = CompoundFolders.PictureRoots(file, step.Library);
        string[] usages = [];
        var raw = gpu(() => {
                var store = new PaintCanvasStore(long.MaxValue);
                using var run = new LayerStackEvaluator(
                    evaluator(),
                    store,
                    file,
                    (uploads, plan, owed) => pictures.Fill(uploads, plan, [..owed], roots, meshMaps)
                        .Refusals.Select(static refusal => refusal.Message)
                        .ToArray()
                );
                var problems = run.Prepare(document, set, shelf.Registry, shelf.Source);
                if (problems.Count > 0 || run.Plan is null) {
                    throw new InvalidDataException(string.Join(' ', problems));
                }

                usages = [..run.Outputs.Select(static o => o.Usage)];
                using var bake = run.EvaluateClean();
                return [..bake.ReadRaw([..run.Outputs.Select(static o => o.Image)])];
            }
        );

        return Pictures(step, folder, usages, raw, GreenOf(step, GreenAxis.Down));
    }

    static BuildManifest Bake(OutputStep step, string folder, int threads, CancellationToken cancellationToken) {
        var inner = Path.Combine(folder, "bake");
        var request = MeshMapSettings.Request(step, folder);
        var manifest = MeshMapBakeRun.Run(request, inner, threads, null, cancellationToken);
        return new(step.Tool, Written(inner, manifest, folder), [], manifest.Result.Warnings);
    }

    /// <summary>The maps of a bake that was made before this build, which is read and not made again.</summary>
    static BuildManifest Baked(OutputStep step, string folder) {
        var file = step.Files.Where(static f => f.EndsWith($".{BakeCache.ManifestFile}", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .FirstOrDefault()
            ?? throw new InvalidDataException("A baked-maps output names no bake.");
        var manifest = BakeCache.ReadManifestFile(file)
            ?? throw new InvalidDataException($"'{Path.GetFileName(file)}' is not a finished bake.");
        return new(step.Tool, Written(Path.GetDirectoryName(file)!, manifest, folder), [], manifest.Result.Warnings);
    }

    /// <summary>A bake's maps in an object's folder, under the names a preset reads.</summary>
    static List<BuiltMap> Written(string bake, BakeManifest manifest, string folder) {
        var maps = Path.Combine(folder, "maps");
        Directory.CreateDirectory(maps);

        List<BuiltMap> built = [];
        foreach (var map in MapPictures.OfBake(bake, manifest)) {
            var written = MapPictures.Write(
                maps,
                map.Name,
                map.Image,
                false,
                map.Name == ExportMaps.Normal ? map.Green : null
            );
            built.Add(written with { File = $"maps/{written.File}" });
        }

        return built;
    }

    /// <summary>
    ///     The way a map's green runs. A bake's runs up. A graph's <c>Height to Normal</c> runs down (Vixen's README,
    ///     and a test there), and a stack carries what its graph fills make without touching it, so that is the
    ///     default of both. An output that composes a baked normal says
    ///     <c>
    ///         green:
    ///         up
    ///     </c>
    ///     .
    /// </summary>
    static GreenAxis GreenOf(OutputStep step, GreenAxis fallback) =>
        step.Settings.GetValueOrDefault("green") switch {
            "up" => GreenAxis.Up,
            "down" => GreenAxis.Down,
            null => fallback,
            var other => throw new InvalidDataException($"'green' is up or down, not '{other}'.")
        };

    static BuildManifest Pictures(
        OutputStep step,
        string folder,
        string[] usages,
        TextureRawImage[] raw,
        GreenAxis green
    ) {
        List<BuiltMap> maps = [];
        for (var at = 0; at < usages.Length; at++) {
            var (image, eightBit) = RawPictures.ToFloat(raw[at]);
            maps.Add(
                MapPictures.Write(folder, usages[at], image, eightBit, usages[at] == ExportMaps.Normal ? green : null)
            );
        }

        return new(step.Tool, maps, [], []);
    }

    static void Apply(NodeGraphModel graph, IReadOnlyDictionary<string, string> settings) {
        foreach (var (name, value) in settings.Where(static s => s.Key.StartsWith(ValuePrefix, StringComparison.Ordinal)
                 )) {
            var parts = name.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var id)) {
                throw new InvalidDataException($"'{name}' is not value:<node>:<port>.");
            }

            if (graph.TryGet(new(id), out var node) && node is not null) {
                node.SetValue(
                    parts[2],
                    [..value.Split(',').Select(static lane => float.Parse(lane, CultureInfo.InvariantCulture))]
                );
            }
        }
    }
}

/// <summary>A bake's settings, which an output declares as text.</summary>
static class MeshMapSettings {
    public static BakeRequest Request(OutputStep step, string folder) {
        var s = step.Settings;
        return new() {
            Low = Role(step, "low"),
            High = Role(step, "high"),
            Cage = s.ContainsKey("cage") ? Role(step, "cage") : null,
            Output = folder,
            Resolution = Int(s, "resolution", 2048),
            Format = s.GetValueOrDefault("format", BakeInputs.Png16),
            OcclusionRays = Int(s, "rays", 64),
            Supersampling = Int(s, "supersampling", 1),
            Maps = s.TryGetValue("maps", out var maps) ? maps.Split(',') : ["ao"]
        };
    }

    /// <summary>
    ///     A file by its part in the bake. A project's lists are sorted, so a position would mean
    ///     nothing.
    /// </summary>
    public static string Role(OutputStep step, string role) {
        if (step.Settings.GetValueOrDefault(role) is not { } named) {
            throw new InvalidDataException($"A mesh-maps output says which of its files is the {role} mesh.");
        }

        var ending = Path.DirectorySeparatorChar + named.Replace('/', Path.DirectorySeparatorChar);
        return step.Files.FirstOrDefault(file => file.EndsWith(ending, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"The {role} mesh, '{named}', is not among this output's files.");
    }

    static int Int(IReadOnlyDictionary<string, string> settings, string name, int fallback) =>
        !settings.TryGetValue(name, out var text)
            ? fallback
            : int.TryParse(text, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw new InvalidDataException($"The setting '{name}' has to be a whole number, and it is '{text}'.");
}

/// <summary>A plan image's bytes as numbers, keeping how many bits they had.</summary>
static class RawPictures {
    public static (FloatImage Image, bool EightBit) ToFloat(TextureRawImage raw) {
        var texels = raw.Width * raw.Height;
        var (lanes, width) = raw.Format switch {
            PixelFormat.R8UNorm => (1, 1),
            PixelFormat.Rg8UNorm => (2, 1),
            PixelFormat.Rgba8UNorm => (4, 1),
            PixelFormat.R16Float => (1, 2),
            PixelFormat.Rgba16Float => (4, 2),
            PixelFormat.R32Float => (1, 4),
            PixelFormat.Rgba32Float => (4, 4),
            _ => throw new InvalidDataException($"A plan image is never {raw.Format}.")
        };
        var values = new float[texels * lanes];
        var bytes = raw.Texels;
        for (var at = 0; at < values.Length; at++) {
            values[at] = width switch {
                1 => bytes[at] / 255f,
                2 => (float)BitConverter.ToHalf(bytes, at * 2),
                _ => BitConverter.ToSingle(bytes, at * 4)
            };
        }

        return (new(raw.Width, raw.Height, lanes, values), width == 1);
    }
}
