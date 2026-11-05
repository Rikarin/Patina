using Patina.Ipc;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using Vixen.Core.Imaging;
using Vixen.Core.Yaml;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Worker;

/// <summary>
///     Texture graph evaluation. Everything here is derived: a copy of a document, its compiled plan, GPU results. The
///     Server has the document and sends it to a new Worker, and the result cache starts cold, so a restart costs time
///     and nothing else. The device opens on the first evaluation, so a Worker with no Vulkan still answers health.
/// </summary>
sealed class GraphHost : IDisposable {
    /// <summary>How many previews are remembered, so that a long drag cannot grow this for ever.</summary>
    const int PreviewMemory = 8192;

    readonly Dictionary<string, Loaded> graphs = new(StringComparer.Ordinal);
    readonly Dictionary<TextureResultKey, Written> written = [];
    readonly GraphPictures pictures = new();
    readonly CompoundShelf compounds = new();
    VulkanDevice? device;
    TexturePlanEvaluator? evaluator;

    public void Dispose() {
        foreach (var graph in graphs.Values) {
            graph.Dispose();
        }

        graphs.Clear();
        evaluator?.Dispose();
        device?.Dispose();
    }

    public IpcMessage Load(IpcMessage request) {
        var load = request.GraphLoad ?? throw new IpcException("graph.load holds no graph.");
        Forget(load.Session);
        graphs[load.Session] = new(load, compounds.Of(load.Folder), pictures);
        return IpcMessage.Ok(request.Id);
    }

    public IpcMessage Close(IpcMessage request) {
        Forget(request.GraphClose ?? throw new IpcException("graph.close names no graph."));
        return IpcMessage.Ok(request.Id);
    }

    public IpcMessage Evaluate(IpcMessage request) {
        var evaluate = request.GraphEvaluate ?? throw new IpcException("graph.evaluate says nothing to evaluate.");
        if (!graphs.TryGetValue(evaluate.Session, out var graph)) {
            throw new IpcException($"This Worker has not been told about graph '{evaluate.Session}'.");
        }

        var start = Stopwatch.GetTimestamp();
        graph.Apply(evaluate.Values);
        var runner = Evaluator();
        var problems = graph.Prepare(runner, out var compiled);
        if (graph.Plan is not { } plan) {
            return Reply(new(compiled, false, 0, 0, Milliseconds(start), [], problems));
        }

        var previews = GraphPreviews.Extend(plan, graph.Images, evaluate.PreviewSide);
        using var bake = runner.EvaluateIncremental(previews.Plan, graph.Uploads!.Externals);
        var stored = Write(bake, previews.Images, evaluate.PreviewFolder);
        return Reply(new(compiled, true, bake.Ran.Length, bake.Reused, Milliseconds(start), stored, problems));
    }

    /// <summary>
    ///     Writes one picture at full size, from the plan the last evaluation ran. The result cache usually has it; if
    ///     not, it runs again, at the cost of holding up the next evaluation.
    /// </summary>
    public IpcMessage Image(IpcMessage request) {
        var ask = request.GraphImage ?? throw new IpcException("graph.image says nothing to write.");
        if (!graphs.TryGetValue(ask.Session, out var graph)) {
            throw new IpcException($"This Worker has not been told about graph '{ask.Session}'.");
        }

        if (graph.Plan is not { } plan) {
            throw new IpcException("This graph has not evaluated, so it has no picture to write.");
        }

        var image = graph.Images.FirstOrDefault(i => i.Node == ask.Node && i.Port == ask.Port);
        if (image.Port is null || plan.Ops.All(op => op.Output != image.Image)) {
            throw new IpcException($"Node {ask.Node} has no picture on '{ask.Port}'.");
        }

        using var bake = Evaluator()
            .EvaluateIncremental(GraphPreviews.Full(plan, image.Image), graph.Uploads!.Externals);
        var key = bake.KeyOf(image.Image);
        if (!written.TryGetValue(key, out var picture)
            || !File.Exists(GraphPreviewFiles.PathOf(ask.Folder, picture.Hash))) {
            picture = Encode(bake.Read(image.Image), ask.Folder);
            written[key] = picture;
        }

        return new() { Type = IpcTypes.Ok, GraphFullImage = new(picture.Hash, picture.Width, picture.Height) };
    }

    static IpcMessage Reply(GraphResult result) => new() { Type = IpcTypes.Ok, GraphResult = result };

    static double Milliseconds(long start) => Stopwatch.GetElapsedTime(start).TotalMilliseconds;

    /// <summary>
    ///     Puts every picture the cache lacks into it. An unchanged result keeps its file, so only what ran costs
    ///     anything. Reads share the device and take turns; encoding is most of the cost and has no such limit.
    /// </summary>
    GraphResultPreview[] Write(TextureBake bake, IReadOnlyList<GraphPreviewImage> images, string folder) {
        Dictionary<TextureResultKey, Bitmap> fresh = [];
        foreach (var image in images) {
            var key = bake.KeyOf(image.Image);
            if (!fresh.ContainsKey(key)
                && (!written.TryGetValue(key, out var known)
                    || !File.Exists(GraphPreviewFiles.PathOf(folder, known.Hash)))) {
                fresh[key] = bake.Read(image.Image);
            }
        }

        var encoded = new ConcurrentDictionary<TextureResultKey, Written>();
        Parallel.ForEach(fresh, picture => encoded[picture.Key] = Encode(picture.Value, folder));
        foreach (var (key, picture) in encoded) {
            written[key] = picture;
        }

        GraphResultPreview[] stored = [
            ..images.Select(image => {
                    var picture = written[bake.KeyOf(image.Image)];
                    return new GraphResultPreview(image.Node, image.Port, picture.Hash, picture.Width, picture.Height);
                }
            )
        ];
        if (written.Count > PreviewMemory) {
            written.Clear();
        }

        return stored;
    }

    static Written Encode(Bitmap bitmap, string folder) {
        var png = PngCodec.Encode(bitmap);
        var hash = Convert.ToHexStringLower(SHA256.HashData(png));
        GraphPreviews.Store(folder, hash, png);
        return new(hash, bitmap.Width, bitmap.Height);
    }

    void Forget(string session) {
        if (graphs.Remove(session, out var graph)) {
            graph.Dispose();
        }
    }

    TexturePlanEvaluator Evaluator() {
        if (evaluator is null) {
            if (!VulkanDevice.TryCreate(new(), out device, out var refusal)) {
                throw new IpcException($"No Vulkan device: {refusal}");
            }

            evaluator = new(device);
        }

        return evaluator;
    }

    readonly record struct Written(string Hash, int Width, int Height);

    /// <summary>One graph: the document, the compiler that remembers it, and what the last compile produced.</summary>
    sealed class Loaded : IDisposable {
        readonly NodeGraphModel graph;
        readonly TextureGraphCompiler compiler;
        readonly GraphPictures pictures;
        readonly IReadOnlyList<string> imageRoots;
        ImmutableArray<NodeDiagnostic> diagnostics = [];
        IReadOnlyList<PictureStamp> stamps = [];

        public Loaded(GraphLoad load, PublishedCompounds compounds, GraphPictures pictures) {
            graph = NodeGraphDocument.Load(
                CompoundShelf.WithoutBoundaries(YamlSerializer.Parse<NodeGraphAsset>(load.Document)),
                out _
            );
            compiler = new(compounds.Registry) { SubGraphSource = compounds.Source, PreviewEveryNode = true };
            this.pictures = pictures;
            imageRoots = load.ImageRoots;
        }

        public TexturePlan? Plan { get; private set; }

        public TextureUploads? Uploads { get; private set; }

        public IReadOnlyList<GraphPreviewSource> Images { get; private set; } = [];

        public void Dispose() => Uploads?.Dispose();

        public void Apply(IReadOnlyList<GraphValue> values) {
            foreach (var value in values) {
                if (!graph.TryGet(new(value.Node), out var node)) {
                    continue; // a compound's boundary node, which this copy leaves out: it computes nothing
                }

                if (value.Lanes is { } lanes) {
                    node.SetValue(value.Port, [..lanes]);
                } else {
                    node.ClearValue(value.Port);
                }
            }
        }

        /// <summary>Brings the plan up to date, compiling only when the numbers alone could not (plan §3.2).</summary>
        public GraphResultProblem[] Prepare(TexturePlanEvaluator evaluator, out bool compiled) {
            compiled = false;
            if (!stamps.Any(GraphPictures.Moved) && compiler.Refresh(graph) is { } refreshed) {
                Plan = refreshed;
                return Problems();
            }

            compiled = true;
            Uploads?.Dispose();
            Uploads = null;
            Plan = null;
            var compilation = compiler.Compile(graph);
            diagnostics = compilation.Diagnostics;
            if (!compilation.Succeeded) {
                return Problems();
            }

            var uploads = new TextureUploads(evaluator.Device);
            var owed = TextureGraphExternals.Upload(uploads, compilation.Value, compiler.Externals);
            var filled = pictures.Fill(uploads, compilation.Value, owed, imageRoots);
            stamps = filled.Stamps;
            if (filled.Refusals.Count > 0) {
                uploads.Dispose();
                diagnostics = diagnostics.AddRange(filled.Refusals);
                return Problems();
            }

            Uploads = uploads;
            Plan = compilation.Value;
            Images = GraphPreviews.Sources(compiler);
            return Problems();
        }

        GraphResultProblem[] Problems() => [
            ..diagnostics.Select(static d => new GraphResultProblem(
                    d.Id,
                    d.Message,
                    d.Severity.ToString(),
                    d.Node.Value,
                    d.Port
                )
            )
        ];
    }
}
