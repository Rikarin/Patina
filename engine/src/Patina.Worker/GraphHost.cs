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
            return Reply(new(compiled, false, 0, 0, Milliseconds(start), [], problems));
        }

        var extended = GraphPreviews.Extend(plan, graph.Images, evaluate.PreviewSide);
        using var bake = runner.EvaluateIncremental(extended.Plan, graph.Uploads!.Externals);
        var stored = Write(bake, extended.Images, evaluate.PreviewFolder);
        return Reply(new(compiled, true, bake.Ran.Length, bake.Reused, Milliseconds(start), stored, problems));
    }

    /// <summary>
    ///     Writes one picture at full size, from the plan the last evaluation ran. The result cache usually has it; if
    ///     not, it runs again, at the cost of holding up the next evaluation.
    /// </summary>
    public IpcMessage Image(IpcMessage request) {
        var ask = request.GraphImage ?? throw new IpcException("graph.image says nothing to write.");
        var graph = Find(ask.Session);
        if (graph.Plan is not { } plan) {
            throw new IpcException("This graph has not evaluated, so it has no picture to write.");
        }

        var image = graph.Images.FirstOrDefault(i => i.Node == ask.Node && i.Port == ask.Port);
        if (image.Port is null || plan.Ops.All(op => op.Output != image.Image)) {
            throw new IpcException($"Node {ask.Node} has no picture on '{ask.Port}'.");
        }

        using var bake = gpu.Evaluator
            .EvaluateIncremental(GraphPreviews.Full(plan, image.Image), graph.Uploads!.Externals);
        var picture = previews.WriteOne(bake, image.Image, ask.Folder);
        return new() { Type = IpcTypes.Ok, GraphFullImage = new(picture.Hash, picture.Width, picture.Height) };
    }

    static IpcMessage Reply(GraphResult result) => new() { Type = IpcTypes.Ok, GraphResult = result };

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
            compiler = new(compounds.Registry) { SubGraphSource = compounds.Source, PreviewEveryNode = true };
            badSize = GraphSize.Apply(compiler, graph);
            this.pictures = pictures;
            imageRoots = load.ImageRoots;
            meshMaps = load.MeshMaps;
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
            ),
            ..badSize is { } problem
                ? [new GraphResultProblem(GraphSize.ProblemId, problem, "Warning", 0, string.Empty)]
                : Array.Empty<GraphResultProblem>()
        ];
    }
}
