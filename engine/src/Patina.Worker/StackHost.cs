using Patina.Contracts;
using Patina.Export;
using Patina.Graphs;
using Patina.Ipc;
using Patina.Library;
using Patina.Viewport;
using System.Diagnostics;
using System.Security.Cryptography;
using Vixen.Authoring;
using Vixen.Core.Imaging;
using Vixen.NodeGraph;
using Vixen.Terrain;
using Vixen.TextureGraph;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>
///     Layer stacks. Everything here is derived from what the Server holds: the document and the journal of strokes. A
///     stroke is stamped and composited here, in tiles, through the plan, and a Worker that dies costs the time to
///     stamp
///     again (ADR 0009). All the painting is Vixen's; what is here is holding sessions and answering the socket.
/// </summary>
sealed partial class StackHost(DeviceHost gpu, ViewportHost viewport, MeshShelf meshes, DeviceWatch watch)
    : SessionHost<StackHost.Session> {
    /// <summary>How a note about missing bakes starts, which a client reads the map names after.</summary>
    const string NeedsBake = "Needs a bake: ";

    /// <summary>How far a stamp on a mesh is dilated past an island's edge, in texels.</summary>
    const int Gutter = 4;

    /// <summary>How far beyond a stroke's rectangle a repair can move a texel: a bilinear read, and one over.</summary>
    const int Margin = 2;

    readonly PreviewWriter previews = new();
    readonly CompoundShelf compounds = new();
    readonly GraphPictures pictures = new();

    protected override string Kind => "stack";

    /// <summary>The model files strokes and checks read.</summary>
    MeshShelf Meshes => meshes;

    public IpcMessage Load(IpcMessage request) {
        var load = request.StackLoad ?? throw new IpcException("stack.load holds no stack.");
        var folder = load.Folder is null ? null : Path.Combine(load.Folder, LibraryLayout.CompoundsFolder);
        Hold(load.Session, new(load, gpu.Evaluator, compounds.Of(folder), pictures, meshes));
        return IpcMessage.Ok(request.Id);
    }

    /// <summary>The viewport starts wearing a stack, or stops. A stack already here sends everything it has.</summary>
    public IpcMessage Follow(IpcMessage request) {
        List<string> sessions = request.Following is { } front ? [front, ..request.FollowingAlso ?? []] : [];
        foreach (var session in viewport.Follow(sessions).Where(Holds)) {
            var stack = Find(session);
            stack.Fed.Clear();
            stack.QuickMaskFed = null;
            if (stack.Prepare().Count == 0 && stack.Run.Plan is not null) {
                using var bake = stack.Run.Evaluate();
                Feed(stack, bake);
            }
        }

        return IpcMessage.Ok(request.Id);
    }

    public IpcMessage Close(IpcMessage request) {
        Forget(request.StackClose ?? throw new IpcException("stack.close names no stack."));
        return IpcMessage.Ok(request.Id);
    }

    public IpcMessage FindLayer(IpcMessage request) {
        var ask = request.StackFind ?? throw new IpcException("stack.find names no stack.");
        return new() { Type = IpcTypes.Ok, FoundLayer = Find(ask.Session).LayerAt(ask.U, ask.V) };
    }

    /// <summary>What each channel holds at a place on the map, for a material picker: numbers, not a picture.</summary>
    public IpcMessage Sample(IpcMessage request) {
        var ask = request.StackSample ?? throw new IpcException("stack.sample names no stack.");
        return new() { Type = IpcTypes.Ok, Sampled = Find(ask.Session).SampleAt(ask.U, ask.V) };
    }

    public IpcMessage Sync(IpcMessage request) {
        var sync = request.StackSync ?? throw new IpcException("stack.sync says nothing.");
        Find(sync.Session).Sync(sync, watch);
        return IpcMessage.Ok(request.Id);
    }

    public IpcMessage Evaluate(IpcMessage request) {
        var ask = request.StackEvaluate ?? throw new IpcException("stack.evaluate says nothing to evaluate.");
        var start = Stopwatch.GetTimestamp();
        var stack = Find(ask.Session);
        stack.Proxy = ask.Proxy;
        var problems = stack.Prepare();
        if (problems.Count > 0 || stack.Run.Plan is not { } plan) {
            return Reply(new(stack.Compiled, false, 0, 0, 0, Milliseconds(start), [], problems, stack.Found(problems)));
        }

        var (ran, repaired, reused) = (0, 0, 0);

        // ⚠ The plan and not the extension: only the plan's outputs are sure to be readable afterwards, and
        // what the viewport needs is what this repair rewrote.
        if (viewport.Follows(ask.Session)) {
            using var bake = stack.Run.Evaluate();
            Feed(stack, bake);
            (ran, repaired, reused) = (bake.Ran.Length, bake.Repaired, bake.Reused);
        }

        if (ask.PreviewSide <= 0) {
            return Reply(
                new(
                    stack.Compiled,
                    true,
                    ran,
                    repaired,
                    reused,
                    Milliseconds(start),
                    [],
                    stack.Notes,
                    stack.Found([])
                )
            );
        }

        var outputs = stack.Run.Outputs;
        var extended = GraphPreviews.Extend(
            plan,
            [..outputs.Select(static (output, index) => new GraphPreviewSource(index, output.Usage, output.Image))],
            ask.PreviewSide,
            []
        );
        using var thumbnails = stack.Run.Evaluate(extended.Plan);
        var stored = previews.Write(
            thumbnails,
            [..extended.Images.Select(static image => image.Image)],
            ask.PreviewFolder
        );
        StackPreview[] written = [
            ..extended.Images.Zip(stored)
                .Select(static pair => new StackPreview(
                        pair.First.Port,
                        pair.Second.Hash,
                        pair.Second.Width,
                        pair.Second.Height
                    )
                )
        ];
        return Reply(
            new(
                stack.Compiled,
                true,
                ran + thumbnails.Ran.Length,
                repaired + thumbnails.Repaired,
                reused + thumbnails.Reused,
                Milliseconds(start),
                written,
                stack.Notes,
                stack.Found([])
            )
        );
    }

    /// <summary>Writes every canvas this session changed, beside the stack or into a checkpoint.</summary>
    public IpcMessage Save(IpcMessage request) {
        var save = request.StackSave ?? throw new IpcException("stack.save says nothing.");
        Find(save.Session).Save(save.Folder);
        return IpcMessage.Ok(request.Id);
    }

    /// <summary>Writes canvases as the journal has left them, for a paste to start from.</summary>
    public IpcMessage Copy(IpcMessage request) {
        var copy = request.StackCopy ?? throw new IpcException("stack.copy says nothing.");
        Find(copy.Session).Copy(copy.Paints, copy.Folder);
        return IpcMessage.Ok(request.Id);
    }

    /// <summary>
    ///     What each map holds, read from the strokes' repairs or evaluated from nothing, in the bytes an export
    ///     writes:
    ///     the hash that says the two are one picture without a pixel crossing the socket.
    /// </summary>
    public IpcMessage Pixels(IpcMessage request) {
        var ask = request.StackPixels ?? throw new IpcException("stack.pixels says nothing.");
        var stack = Find(ask.Session);

        // What the viewport holds is at the proxy; anything else is what a file would hold, at full size (#208).
        if (ask.Viewport) {
            var problems = stack.Prepare();
            if (problems.Count > 0 || stack.Run.Plan is null) {
                throw new IpcException(string.Join(' ', problems));
            }

            return new() { Type = IpcTypes.Ok, StackPicture = Sampled(stack) };
        }

        var run = stack.Whole();

        if (ask.Layer is { } layer) {
            using var alone = stack.Alone(layer, ask.Mask, out var name);
            using var only = alone.EvaluateClean();
            return new() {
                Type = IpcTypes.Ok, StackPicture = Read(alone, only, $"{stack.Name}.{name}", ask.Folder, ask.File)
            };
        }

        using var bake = ask.Clean ? run.EvaluateClean() : run.Evaluate();
        return new() { Type = IpcTypes.Ok, StackPicture = Read(run, bake, stack.Name, ask.Folder, ask.File) };
    }

    static string Digest(Bitmap map) => Convert.ToHexStringLower(SHA256.HashData(map.Pixels));

    static IpcMessage Reply(StackResult result) => new() { Type = IpcTypes.Ok, StackResult = result };

    static double Milliseconds(long start) => Stopwatch.GetElapsedTime(start).TotalMilliseconds;

    /// <summary>
    ///     One stroke as this Worker holds it: under way, or a command that can undo it. A copy has no stroke.
    /// </summary>
    sealed class Applied(long id, PaintStroke? stroke, PaintPath? curve) {
        public long Id { get; } = id;

        public PaintStroke? Stroke { get; } = stroke;

        /// <summary>The control points of a line or a path, which paints when it ends.</summary>
        public PaintPath? Curve { get; } = curve;

        public IAuthoringCommand? Command { get; set; }

        /// <summary>What a copy of a canvas does, which is not a stroke.</summary>
        public IAuthoringCommand? Copy { get; init; }

        /// <summary>Whether its stamps are the clicks of a polygon fill, each laid where it is.</summary>
        public bool Fill { get; init; }

        /// <summary>Whether that fill's stamps are one outline, filled from one stamp, rather than clicks.</summary>
        public bool Outline { get; init; }

        public bool Done { get; set; }
    }

    internal sealed partial class Session : IDisposable {
        static readonly NoContext Context = new();

        readonly string path;
        readonly string set;

        /// <summary>The coordinate set this texture set is laid out in, which every stroke lands in (#210).</summary>
        public int UvSet => LayerStackEdit.SetFor(document, set)?.UvSet ?? 0;

        readonly string folder;
        readonly PublishedCompounds compounds;
        readonly PaintCanvasStore canvases = new(long.MaxValue);
        readonly HashSet<string> touched = new(StringComparer.Ordinal);

        /// <summary>The canvases a stroke of this session began on, whose old pixels its undo holds.</summary>
        readonly HashSet<string> stroked = new(StringComparer.Ordinal);

        /// <summary>
        ///     Blanks made at load for canvases the journal makes, which nothing has written yet. See
        ///     <see cref="Made" />.
        /// </summary>
        readonly HashSet<string> blanks = new(StringComparer.Ordinal);

        readonly TexturePlanEvaluator evaluator;
        readonly LayerStackPictures fill;

        /// <summary>The box the bake's position map spans, which every decal and path is read through (#133).</summary>
        readonly IReadOnlyList<float>? positionBox;

        readonly List<Applied> applied = [];
        readonly Dictionary<long, Applied> known = [];

        readonly Dictionary<string, (PaintProjection Over, PaintMeshSurface Surface)> surfaces =
            new(StringComparer.Ordinal);

        readonly Dictionary<string, IBrushMask> alphas = new(StringComparer.Ordinal);
        readonly Dictionary<string, PaintImage> pictures = new(StringComparer.Ordinal);
        readonly Dictionary<string, Dictionary<string, PaintImage>> composites = new(StringComparer.Ordinal);
        readonly IReadOnlyList<string> imageRoots;
        readonly MeshShelf meshes;
        readonly List<PaintRect> dirtied = [];
        bool unknown = true;
        LayerStackAsset document;
        bool stale = true;
        int proxy = 1;

        /// <summary>The full-size stack beside a proxy's, for what a file would hold: <see cref="Whole" />.</summary>
        LayerStackEvaluator? whole;

        /// <summary>Whether <see cref="whole" /> is behind the document.</summary>
        bool wholeStale = true;

        /// <summary>What the plan was last prepared at, as the proxy's divisor.</summary>
        int prepared = 1;

        public Session(
            StackLoad load,
            TexturePlanEvaluator evaluator,
            PublishedCompounds compounds,
            GraphPictures pictures,
            MeshShelf meshes
        ) {
            this.meshes = meshes;
            imageRoots = load.ImageRoots;
            path = load.Path;
            set = load.Set;
            folder = Path.GetDirectoryName(Path.GetFullPath(load.Path))!;
            this.compounds = compounds;
            document = LayerStackYaml.Read(load.Document);
            this.evaluator = evaluator;
            fill = (uploads, plan, owed) => pictures.Fill(
                    uploads,
                    plan,
                    [..owed],
                    load.ImageRoots,
                    load.MeshMaps,
                    Unbaked
                )
                .Refusals.Select(static refusal => refusal.Message)
                .ToArray();
            // The bake's normal and occlusion shade the set beneath its layers (#86).
            positionBox = BakedMapPicture.PositionBox(load.MeshMaps);
            Run = new(evaluator, canvases, load.Path, fill) {
                Draft = true, Baked = BakedMapPicture.Shading(load.MeshMaps), PositionBox = positionBox
            };
            if (load.Checkpoint is { } checkpoint) {
                Restore(checkpoint);
            }

            foreach (var created in load.Created.Where(paint => !touched.Contains(paint))) {
                var file = LibraryLayout.Beside(folder, created);
                canvases.Adopt(file, new(document.BaseWidth, document.BaseHeight));
                blanks.Add(file);
            }
        }

        public LayerStackEvaluator Run { get; }

        /// <summary>The texture set this paints, which is a material's name on a model that has several.</summary>
        public string SetName => set;

        /// <summary>The size of its maps.</summary>
        public (int Width, int Height) Size => (document.BaseWidth, document.BaseHeight);

        /// <summary>The model it is painted on, or null for a stack on no mesh.</summary>
        public string? ModelFile => document.Model.Trim().Length == 0 ? null : Path.GetFullPath(document.Model, folder);

        /// <summary>
        ///     The topmost layer that covers a place on the map, inside its folders (#136). Each candidate, top first,
        ///     is weighed alone with the folders around it, so its own mask and theirs, its opacity and a paint layer's
        ///     strokes all count, and one texel of it is read back: a number, never the picture.
        /// </summary>
        public string? LayerAt(float u, float v) {
            var chosen = document.Sets.First(candidate => candidate.Name == set);
            var x = Math.Clamp((int)(u * document.BaseWidth), 0, document.BaseWidth - 1);
            var y = Math.Clamp((int)(v * document.BaseHeight), 0, document.BaseHeight - 1);
            foreach (var (layer, alone, inPicture) in StackFinding.Candidates(document, chosen)) {
                // A layer whose mask reads the picture is weighed in it, with the layers beneath it as they are (#113).
                // A draft, as the picture the click landed on is one: a layer half set up is weighed as that picture
                // draws it, rather than refusing the stack so that nothing above it is found.
                using LayerStackEvaluator run = new(evaluator, canvases, path, fill) {
                    Draft = true,
                    Weighing = !inPicture,
                    Asked = inPicture ? layer : string.Empty,
                    PositionBox = positionBox
                };
                var refused = inPicture
                    ? run.Prepare(document, chosen, compounds.Registry, compounds.Source)
                    : run.Prepare(document with { Sets = [alone] }, alone, compounds.Registry, compounds.Source);
                if (refused.Count > 0) {
                    continue;
                }

                // In any channel it writes: a layer that sets roughness alone is still on top where it does.
                using var bake = run.Evaluate();
                if (StackFinding.Covered(bake, run.Outputs, inPicture ? chosen : alone, x, y)) {
                    return layer;
                }
            }

            return null;
        }

        /// <summary>
        ///     What each channel of the composite holds at a place on the map, at full size: one texel of each read
        ///     back, as the plan stores it.
        /// </summary>
        /// <exception cref="IpcException">The stack does not evaluate.</exception>
        public IReadOnlyList<StackSampledChannel> SampleAt(float u, float v) {
            var run = Whole();
            var x = Math.Clamp((int)(u * document.BaseWidth), 0, document.BaseWidth - 1);
            var y = Math.Clamp((int)(v * document.BaseHeight), 0, document.BaseHeight - 1);
            using var bake = run.Evaluate();
            return [
                ..run.Outputs.Select(output => new StackSampledChannel(
                        output.Usage,
                        StackSampling.Value(
                            bake.ReadTexels(output.Image, [new(x, y, 1, 1)])[0],
                            bake.Plan.Images[output.Image].Format
                        )
                    )
                )
            ];
        }

        static IEnumerable<LayerAsset> Fills(IEnumerable<LayerAsset> layers) =>
            layers.SelectMany(static layer => layer.Kind == LayerKind.Group
                    ? Fills(layer.Children)
                    : layer is { Kind: LayerKind.Fill, Fill: not LayerFillSource.Constant }
                        ? [layer]
                        : (IEnumerable<LayerAsset>)[]
            );

        /// <summary>The mesh maps layers read that no bake has given, which read as grey until one does.</summary>
        public SortedSet<string> Unbaked { get; } = new(StringComparer.Ordinal);

        /// <summary>What a client is told after an evaluation that went ahead.</summary>
        public IReadOnlyList<string> Notes => Unbaked.Count == 0 ? [] : [$"{NeedsBake}{string.Join(", ", Unbaked)}"];

        /// <summary>
        ///     What the last compile said, against the layer it is about. A layer has one note per channel it reaches,
        ///     and says the same thing in each.
        /// </summary>
        /// <param name="refused">Why this evaluation could not go ahead, some of which may be about no layer.</param>
        public IReadOnlyList<StackProblem> Found(IReadOnlyList<string> refused) {
            List<StackProblem> found = [
                ..Run.Problems.Select(static problem => new StackProblem(
                        problem.Layer,
                        problem.Severity.ToString(),
                        problem.Message
                    )
                ),
                ..Run.Diagnostics.Select(diagnostic => new StackProblem(
                        Run.Layers.GetValueOrDefault(diagnostic.Node, string.Empty),
                        diagnostic.Severity.ToString(),
                        diagnostic.Message
                    )
                )
            ];
            found.AddRange(
                refused.Where(reason => !found.Exists(problem => problem.Message == reason))
                    .Select(static reason => new StackProblem(string.Empty, nameof(NodeSeverity.Error), reason))
            );
            if (Tiles() is { } tiles) {
                found.Add(new(string.Empty, nameof(NodeSeverity.Warning), tiles));
            }

            return [..found.Distinct()];
        }

        /// <summary>
        ///     Why this set's layout reaches past the one UV tile Patina paints, or null. The mesh is read once for
        ///     strokes too, and a mesh that does not read is said elsewhere.
        /// </summary>
        string? Tiles() {
            if (document.Model.Trim().Length == 0) {
                return null;
            }

            try {
                return meshes.Of(Path.GetFullPath(document.Model, folder), set, UvSet).Tiles;
            } catch (IpcException) {
                return null;
            }
        }

        /// <summary>
        ///     Which version of each map the viewport holds, by slot. ⚠ Not by image: an undone first stroke takes the
        ///     layer's paint file away, which compiles the old plan back with its old image numbers, and an entry under
        ///     such a number says the viewport holds a picture it stopped holding two compiles ago.
        /// </summary>
        public Dictionary<ViewportSlot, TextureResultKey> Fed { get; } = [];

        public string Name => Path.GetFileNameWithoutExtension(path);

        public void Dispose() {
            Run.Dispose();
            whole?.Dispose();
        }

        /// <summary>Goes from the strokes that stand here to the strokes that should.</summary>
        public void Sync(StackSync sync, DeviceWatch watch) {
            if (sync.Document is { } text) {
                document = LayerStackYaml.Read(text);
                Restale();
            }

            QuickMask = sync.QuickMask;

            foreach (var id in sync.Forget) {
                known.Remove(id);
            }

            var common = 0;
            while (common < applied.Count
                   && common < sync.Standing.Count
                   && applied[common].Id == sync.Standing[common]) {
                common++;
            }

            for (var index = applied.Count - 1; index >= common; index--) {
                Close(applied[index]).Undo(Context);
                applied.RemoveAt(index);
                unknown = true;
            }

            // Strokes this Worker holds that grew come first, since they stand before any it has not begun.
            var parts = sync.Parts.ToDictionary(static part => part.Id);
            HashSet<long> stamped = [];
            foreach (var part in sync.Parts.Where(part => applied.Take(common).Any(held => held.Id == part.Id))) {
                watch.Beat();
                Extend(known[part.Id], part);
                stamped.Add(part.Id);
            }

            for (var index = common; index < sync.Standing.Count; index++) {
                var id = sync.Standing[index];
                if (known.TryGetValue(id, out var stroke)) {
                    Close(stroke).Do(Context);
                    applied.Add(stroke);
                    unknown = true;
                } else if (parts.TryGetValue(id, out var part)) {
                    // ⚠ Begun and stamped before the next is begun: a stroke's undo puts back the layer as it was
                    // when the stroke began, so one begun before the strokes ahead of it had stamped took them away
                    // with it. A replay after a lost Worker, or two strokes that reached a busy Worker together, did.
                    applied.Add(Begin(part));
                    // A long replay is one request, and each stroke of it is progress.
                    watch.Beat();
                    Extend(known[id], part);
                    stamped.Add(id);
                } else {
                    throw new IpcException($"Stroke {id} stands and this Worker was never told it.");
                }
            }

            foreach (var part in sync.Parts.Where(part => !stamped.Contains(part.Id))) {
                watch.Beat();
                Extend(known[part.Id], part);
            }
        }

        /// <summary>
        ///     The rectangles of a repair that strokes reached since the viewport last heard. A repair is on the tile
        ///     cache's whole tiles, which is what a composite wants and far more than a stamp changes, and reading a
        ///     tile back is most of what a stamp costs on the way to the screen. Each op of a repair reads at most one
        ///     texel away, or it would not be a repair, so what the strokes touched and a margin is all that moved.
        ///     An undo says nothing about where, so that sends the whole of each rectangle.
        /// </summary>
        public IEnumerable<TextureRect> Narrowed(IEnumerable<TextureRect> rects) {
            foreach (var rect in rects) {
                // At a proxy a repair's rectangles are not the canvas's, which is all the strokes say anything about.
                if (unknown || prepared > 1) {
                    yield return rect;
                    continue;
                }

                PaintRect tile = new(rect.X, rect.Y, rect.Width, rect.Height);
                var reached = PaintRect.Empty;
                foreach (var region in dirtied) {
                    reached = reached.Union(region.Grow(Margin).Intersect(tile));
                }

                if (reached.IsEmpty) {
                    continue;
                }

                // A mip is made of whole pairs of texels, so the corner goes down to even numbers.
                var (x, y) = (reached.X & ~1, reached.Y & ~1);
                yield return new(x, y, reached.EndX - x, reached.EndY - y);
            }
        }

        /// <summary>The viewport has heard everything so far.</summary>
        public void Reported() {
            dirtied.Clear();
            unknown = false;
        }

        /// <summary>What the stack's sides are divided by while it is edited (#208).</summary>
        public int Proxy {
            set {
                proxy = Math.Max(1, value);
                if (proxy == 1) {
                    // At full size the viewport's plan is the file's too, and a second would only hold memory.
                    whole?.Dispose();
                    whole = null;
                }
            }
        }

        /// <summary>The plans are behind the document, or behind a canvas whose size changed.</summary>
        void Restale() {
            stale = true;
            wholeStale = true;
        }

        /// <summary>
        ///     The stack at full size, for whatever reads what a file would hold: a save, an export, a check, a sample
        ///     and a read of the pixels. With no proxy that is the viewport's own; under a proxy it is a second
        ///     evaluator kept beside the proxy's, so a full-size read compiles neither plan again and the next edit
        ///     goes on at the proxy where it left off (#288). It uploads the canvases a second time, which costs far
        ///     less than a compile each way per autosave on a big stack.
        /// </summary>
        /// <exception cref="IpcException">The stack does not evaluate.</exception>
        public LayerStackEvaluator Whole() {
            if (proxy == 1) {
                var problems = Prepare();
                return problems.Count > 0 || Run.Plan is null
                    ? throw new IpcException(string.Join(' ', problems))
                    : Run;
            }

            if (whole is null) {
                whole = new(evaluator, canvases, path, fill) {
                    Draft = true, Baked = Run.Baked, PositionBox = positionBox
                };
                wholeStale = true;
            }

            if (!wholeStale && whole.Plan is not null) {
                return whole;
            }

            if (LayerStackEdit.SetFor(document, set) is not { } chosen) {
                throw new IpcException($"This stack has no texture set '{set}'.");
            }

            var refused = whole.Prepare(document, chosen, compounds.Registry, compounds.Source);
            if (refused.Count > 0 || whole.Plan is null) {
                throw new IpcException(string.Join(' ', refused));
            }

            wholeStale = false;
            return whole;
        }

        /// <summary>
        ///     Brings the viewport's plan up to the document, which only a change of structure compiles, or a change of
        ///     the proxy. Canvases stay at full size, and the plan samples them down. What a file would hold is
        ///     <see cref="Whole" />.
        /// </summary>
        public IReadOnlyList<string> Prepare() {
            var divided = proxy;
            if (!stale && Run.Plan is not null && divided == prepared) {
                Compiled = false;
                return [];
            }

            if (LayerStackEdit.SetFor(document, set) is not { } chosen) {
                return [$"This stack has no texture set '{set}'."];
            }

            stale = false;
            prepared = divided;
            var sized = divided == 1
                ? document
                : document with {
                    BaseWidth = Math.Max(1, document.BaseWidth / divided),
                    BaseHeight = Math.Max(1, document.BaseHeight / divided)
                };
            var problems = Run.Prepare(sized, chosen, compounds.Registry, compounds.Source);
            Compiled = Run.Compiled;
            return problems;
        }

        /// <summary>
        ///     Whether the last <see cref="Prepare" /> compiled. Not the plan's own flag, which keeps saying so through
        ///     every evaluation that did not prepare it again.
        /// </summary>
        public bool Compiled { get; private set; }

        public void Save(string? to) {
            // A quick mask is the session's: a checkpoint keeps it, for a Worker that takes over, and a save does not.
            foreach (var file in touched.Where(file => to is not null || !IsQuickMask(file))) {
                if (canvases.Open(Path.Combine(folder, file)) is not { } canvas) {
                    continue;
                }

                var target = Path.Combine(to ?? folder, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var temporary = $"{target}.tmp";
                using (var stream = File.Create(temporary)) {
                    canvas.Write(stream);
                    stream.Flush(true);
                }

                SharedFile.MoveOver(temporary, target);
                if (to is null) {
                    canvases.Saved(Path.Combine(folder, file));
                }
            }
        }

        public void Copy(IReadOnlyList<string> paints, string to) {
            Directory.CreateDirectory(to);
            foreach (var paint in paints) {
                if (canvases.Open(LibraryLayout.Beside(folder, paint)) is not { } canvas) {
                    continue;
                }

                var target = Path.Combine(to, Path.GetFileName(paint));
                using var stream = File.Create(target);
                canvas.Write(stream);
            }
        }

        void Restore(string checkpoint) {
            foreach (var file in Directory.EnumerateFiles(checkpoint, $"*{LayerPaint.Extension}")) {
                using var stream = SharedFile.OpenRead(file);
                var name = Path.GetFileName(file);
                canvases.Adopt(Path.Combine(folder, name), PaintCanvas.Read(stream));
                touched.Add(name);
            }
        }

        sealed class NoContext : ICommandContext {
            public void Touch(IAuthoredDocument document) { }
        }
    }
}
