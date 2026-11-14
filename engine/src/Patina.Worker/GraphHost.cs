using Patina.Graphs;
using Patina.Ipc;
using System.Collections.Immutable;
using System.Diagnostics;
using Vixen.Core.Yaml;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Worker;

/// <summary>
///     Texture graph evaluation. Everything here is derived: a copy of a document, its compiled plan, GPU results. The
///     Server has the document and sends it to a new Worker, and the result cache starts cold, so a restart costs time
///     and nothing else. The device opens on the first evaluation, so a Worker with no Vulkan still answers health.
/// </summary>
sealed class GraphHost(DeviceHost gpu) : SessionHost<GraphHost.Loaded> {
    readonly PreviewWriter previews = new();
    readonly GraphPictures pictures = new();
    readonly CompoundShelf compounds = new();

    /// <summary>What a histogram counts, in the order <see cref="GraphLevels" /> holds them.</summary>
    static readonly TextureHistogramLane[] Lanes = [
        TextureHistogramLane.Red,
        TextureHistogramLane.Green,
        TextureHistogramLane.Blue,
        TextureHistogramLane.Alpha,
        TextureHistogramLane.Luma
    ];

    /// <summary>What a thumbnail's copy is called. It is never held, so it never meets a session.</summary>
    const string ThumbnailSession = "thumbnail";

    protected override string Kind => "graph";

    public IpcMessage Load(IpcMessage request) {
        var load = request.GraphLoad ?? throw new IpcException("graph.load holds no graph.");
        Hold(load.Session, new(load, compounds.Of(load.Folder), pictures));
        return IpcMessage.Ok(request.Id);
    }

    public IpcMessage Close(IpcMessage request) {
        Forget(request.GraphClose ?? throw new IpcException("graph.close names no graph."));
        return IpcMessage.Ok(request.Id);
    }

    public IpcMessage Evaluate(IpcMessage request) {
        var evaluate = request.GraphEvaluate ?? throw new IpcException("graph.evaluate says nothing to evaluate.");
        var graph = Find(evaluate.Session);
        var start = Stopwatch.GetTimestamp();
        graph.Apply(evaluate.Values);
        var runner = gpu.Evaluator;
        var problems = graph.Prepare(runner, out var compiled);
        if (graph.Plan is not { } plan) {
            return Reply(new(compiled, false, 0, 0, Milliseconds(start), [], problems, []));
        }

        var extended = GraphPreviews.Extend(plan, graph.Images, evaluate.PreviewSide, graph.Checks);
        using var bake = Timed(runner, extended.Plan, graph.Uploads!.Externals);
        var stored = Write(bake, extended.Images, evaluate.PreviewFolder);
        problems = [
            ..problems, ..graph.Checks.Where(check => check.Failed(bake)).Select(static check => Problem(check.Problem))
        ];
        var times = GraphTimes.Of(bake.OpMilliseconds, graph.OpNodes);
        return Reply(new(compiled, true, bake.Ran.Length, bake.Reused, Milliseconds(start), stored, problems, times));
    }

    /// <summary>
    ///     An evaluation that times its operations, for the nodes' timing badges (#182). The evaluator is the device's
    ///     and shared, so the switch goes back as it was.
    /// </summary>
    static TextureBake Timed(
        TexturePlanEvaluator runner,
        TexturePlan plan,
        IReadOnlyDictionary<int, TextureExternal> externals
    ) {
        runner.TimeOps = true;
        try {
            return runner.EvaluateIncremental(plan, externals);
        } finally {
            runner.TimeOps = false;
        }
    }

    /// <summary>
    ///     Counts one picture into a histogram per channel at the size the graph computes it, on the GPU, from the plan
    ///     the last evaluation ran. The result cache usually holds the picture, so only the counting runs (#172).
    /// </summary>
    public IpcMessage Histogram(IpcMessage request) {
        var ask = request.GraphHistogram ?? throw new IpcException("graph.histogram says nothing to count.");
        var graph = Find(ask.Session);
        var (plan, image) = Picture(graph, ask.Node, ask.Port);
        var counted = TextureHistograms.Append(plan, image, Lanes);
        using var bake = gpu.Evaluator.EvaluateIncremental(counted.Plan, graph.Uploads!.Externals);
        var shares = counted.Tables.Select(table => TextureHistograms.Read(bake, table)).ToArray();
        var size = plan.SizeOf(image);
        return new() {
            Type = IpcTypes.Ok, GraphLevels = new(size.X, size.Y, shares[0], shares[1], shares[2], shares[3], shares[4])
        };
    }

    /// <summary>The plan the last evaluation ran and the image one node's port wrote in it.</summary>
    static (TexturePlan Plan, int Image) Picture(Loaded graph, int node, string port) {
        if (graph.Plan is not { } plan) {
            throw new IpcException("This graph has not evaluated, so it has no picture to write.");
        }

        var image = graph.Images.FirstOrDefault(i => i.Node == node && i.Port == port);
        if (image.Port is null || plan.Ops.All(op => op.Output != image.Image)) {
            throw new IpcException($"Node {node} has no picture on '{port}'.");
        }

        return (plan, image.Image);
    }

    /// <summary>
    ///     Writes one picture at full size, from the plan the last evaluation ran. The result cache usually has it; if
    ///     not, it runs again, at the cost of holding up the next evaluation.
    /// </summary>
    public IpcMessage Image(IpcMessage request) {
        var ask = request.GraphImage ?? throw new IpcException("graph.image says nothing to write.");
        var graph = Find(ask.Session);
        var (plan, image) = Picture(graph, ask.Node, ask.Port);
        using var bake = gpu.Evaluator.EvaluateIncremental(GraphPreviews.Full(plan, image), graph.Uploads!.Externals);
        var picture = previews.WriteOne(bake, image, ask.Folder);
        return new() { Type = IpcTypes.Ok, GraphFullImage = new(picture.Hash, picture.Width, picture.Height) };
    }

    /// <summary>
    ///     One Output of a graph, drawn for the shelf from a copy that goes as soon as it has. The Server has already
    ///     set the graph's size to the picture's, so nothing is computed larger than it is shown.
    /// </summary>
    public IpcMessage Thumbnail(MaterialThumbnailAsk ask) {
        var load = new GraphLoad(ThumbnailSession, ask.Document, ask.Folder, ask.ImageRoots);
        using Loaded graph = new(load, compounds.Of(ask.Folder), pictures);
        var runner = gpu.Evaluator;
        graph.Prepare(runner, out _);
        var source = graph.Images.FirstOrDefault(image => image.Node == ask.Node && image.Port == ask.Usage);
        if (graph.Plan is not { } plan || source.Port is null) {
            return Drawn(null);
        }

        var extended = GraphPreviews.Extend(plan, [source], int.MaxValue, []);
        if (extended.Images.Count == 0) {
            return Drawn(null);
        }

        using var bake = runner.EvaluateIncremental(extended.Plan, graph.Uploads!.Externals);
        return Drawn(previews.Write(bake, [extended.Images[0].Image], ask.PreviewFolder)[0].Hash);
    }

    internal static IpcMessage Drawn(string? hash) => new() { Type = IpcTypes.Ok, ThumbnailHash = hash };

    static IpcMessage Reply(GraphResult result) => new() { Type = IpcTypes.Ok, GraphResult = result };

    static GraphResultProblem Problem(NodeDiagnostic problem) =>
        new(problem.Id, problem.Message, problem.Severity.ToString(), problem.Node.Value, problem.Port);

    static double Milliseconds(long start) => Stopwatch.GetElapsedTime(start).TotalMilliseconds;

    /// <summary>
    ///     Puts every picture the cache lacks into it. An unchanged result keeps its file, so only what ran costs
    ///     anything. The reads are one submission, so a preview costs a copy rather than a wait for the device, and
    ///     encoding has no such limit.
    /// </summary>
    GraphResultPreview[] Write(TextureBake bake, IReadOnlyList<GraphPreviewImage> images, string folder) {
        var stored = previews.Write(bake, [..images.Select(static image => image.Image)], folder);
        return [
            ..images.Zip(stored)
                .Select(static pair => new GraphResultPreview(
                        pair.First.Node,
                        pair.First.Port,
                        pair.Second.Hash,
                        pair.Second.Width,
                        pair.Second.Height
                    )
                )
        ];
    }

    /// <summary>One graph: the document, the compiler that remembers it, and what the last compile produced.</summary>
    internal sealed class Loaded : IDisposable {
        readonly NodeGraphModel graph;
        readonly TextureGraphCompiler compiler;
        readonly GraphPictures pictures;
        readonly IReadOnlyList<string> imageRoots;
        readonly string? meshMaps;
        readonly string? badSize;
        ImmutableArray<NodeDiagnostic> diagnostics = [];
        IReadOnlyList<PictureStamp> stamps = [];

        public Loaded(GraphLoad load, PublishedCompounds compounds, GraphPictures pictures) {
            graph = NodeGraphDocument.Load(
                CompoundShelf.WithoutBoundaries(YamlSerializer.Parse<NodeGraphAsset>(load.Document)),
                out _
            );
            compiler = new(compounds.Registry) {
                SubGraphSource = compounds.Source, PreviewEveryNode = true, Drafting = true
            };
            badSize = GraphSize.Apply(compiler, graph);
            this.pictures = pictures;
            imageRoots = load.ImageRoots;
            meshMaps = load.MeshMaps;
        }

        public TexturePlan? Plan { get; private set; }

        public TextureUploads? Uploads { get; private set; }

        public IReadOnlyList<GraphPreviewSource> Images { get; private set; } = [];

        /// <summary>Whose each op of <see cref="Plan" /> is, by the node's number; 0 for no node's.</summary>
        public IReadOnlyList<int> OpNodes { get; private set; } = [];

        public IReadOnlyList<TextureGraphCheck> Checks => compiler.Checks;

        public void Dispose() => Uploads?.Dispose();

        public void Apply(IReadOnlyList<GraphValue> values) {
            foreach (var value in values) {
                if (!graph.TryGet(new(value.Node), out var node)) {
                    continue; // a compound's boundary node, which this copy leaves out: it computes nothing
                }

                if (value.Text is { } text) {
                    node.SetText(value.Port, text);
                } else if (value.Lanes is { } lanes) {
                    node.SetValue(value.Port, [..lanes]);
                } else {
                    node.ClearValue(value.Port);
                }
            }
        }

        /// <summary>Brings the plan up to date, compiling only for more than a value (plan §3.2).</summary>
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
            var filled = pictures.Fill(uploads, compilation.Value, owed, imageRoots, meshMaps);
            stamps = filled.Stamps;
            if (filled.Refusals.Count > 0) {
                uploads.Dispose();
                diagnostics = diagnostics.AddRange(filled.Refusals);
                return Problems();
            }

            Uploads = uploads;
            Plan = compilation.Value;
            Images = GraphPreviews.Sources(compiler);
            OpNodes = [..compiler.OpNodes.Select(static node => node.Value)];
            return Problems();
        }

        GraphResultProblem[] Problems() => [
            ..diagnostics.Select(Problem),
            ..badSize is { } problem
                ? [new GraphResultProblem(GraphSize.ProblemId, problem, "Warning", 0, string.Empty)]
                : Array.Empty<GraphResultProblem>()
        ];
    }
}
