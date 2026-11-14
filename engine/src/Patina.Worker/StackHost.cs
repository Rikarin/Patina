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
using Vixen.Core.Mathematics;
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

    /// <summary>
    ///     Hands the viewport what this evaluation changed, in the bytes an export writes. Only the rectangles a repair
    ///     rewrote cross, and a picture nothing touched costs nothing. No device is shared: the bytes are in this
    ///     process, which is also why a Worker with a dead viewport still paints.
    /// </summary>
    void Feed(Session stack, TextureBake bake) {
        var material = viewport.Live.MaterialOf(stack.SetName);
        foreach (var output in stack.Run.Outputs) {
            if (Slot(output.Usage) is not { } slot) {
                continue;
            }

            var key = bake.KeyOf(output.Image);
            var known = stack.Fed.TryGetValue(slot, out var fed);
            if (known && fed == key) {
                continue;
            }

            if (known && bake.RepairOf(output.Image) is { Length: > 0 } rects) {
                TextureRect[] list = [..stack.Narrowed(rects)];
                var patches = bake.Read(output.Image, list);
                viewport.Live.RepairLive(
                    slot,
                    [
                        ..list.Zip(patches)
                            .Select(static pair => new ViewportRepair(
                                    new(pair.First.X, pair.First.Y, pair.First.Width, pair.First.Height),
                                    pair.Second.Pixels
                                )
                            )
                    ],
                    material
                );
            } else {
                var map = bake.Read(output.Image);
                viewport.Live.SetLive(slot, map.Width, map.Height, map.Pixels, material);
            }

            stack.Fed[slot] = key;
        }

        // A map the stack no longer writes leaves the viewport with it, or the old picture would stay on the mesh.
        HashSet<ViewportSlot> written =
            [..stack.Run.Outputs.Select(static output => Slot(output.Usage)).OfType<ViewportSlot>()];
        foreach (var gone in stack.Fed.Keys.Where(slot => !written.Contains(slot)).ToList()) {
            viewport.Live.ClearLive(gone, material);
            stack.Fed.Remove(gone);
        }

        stack.Reported();
    }

    static ViewportSlot? Slot(string usage) =>
        usage switch {
            "baseColor" => ViewportSlot.BaseColor,
            "normal" => ViewportSlot.Normal,
            "roughness" => ViewportSlot.Roughness,
            "metalness" => ViewportSlot.Metallic,
            "occlusion" => ViewportSlot.Occlusion,
            "emissive" => ViewportSlot.Emissive,
            "opacity" => ViewportSlot.Opacity,
            _ => null
        };

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
        var problems = stack.Prepare(!ask.Viewport);
        if (problems.Count > 0 || stack.Run.Plan is null) {
            throw new IpcException(string.Join(' ', problems));
        }

        if (ask.Viewport) {
            return new() { Type = IpcTypes.Ok, StackPicture = Sampled(stack) };
        }

        if (ask.Layer is { } layer) {
            using var alone = stack.Alone(layer, ask.Mask, out var name);
            using var only = alone.EvaluateClean();
            return new() {
                Type = IpcTypes.Ok, StackPicture = Read(alone, only, $"{stack.Name}.{name}", ask.Folder, ask.File)
            };
        }

        using var bake = ask.Clean ? stack.Run.EvaluateClean() : stack.Run.Evaluate();
        return new() { Type = IpcTypes.Ok, StackPicture = Read(stack.Run, bake, stack.Name, ask.Folder, ask.File) };
    }

    /// <summary>What the viewport's draw call samples for each map, read back from the viewport's own device.</summary>
    List<StackPixels> Sampled(Session stack) {
        List<StackPixels> pixels = [];
        foreach (var output in stack.Run.Outputs) {
            if (Slot(output.Usage) is { } slot
                && viewport.Live.Sampled(slot, viewport.Live.MaterialOf(stack.SetName)) is { } bytes) {
                var size = stack.Run.Plan!.SizeOf(output.Image);
                pixels.Add(new(output.Usage, size.X, size.Y, Convert.ToHexStringLower(SHA256.HashData(bytes))));
            }
        }

        return pixels;
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
            var problems = Prepare(true);
            if (problems.Count > 0 || Run.Plan is null) {
                throw new IpcException(string.Join(' ', problems));
            }

            var x = Math.Clamp((int)(u * document.BaseWidth), 0, document.BaseWidth - 1);
            var y = Math.Clamp((int)(v * document.BaseHeight), 0, document.BaseHeight - 1);
            using var bake = Run.Evaluate();
            return [
                ..Run.Outputs.Select(output => new StackSampledChannel(
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

        public void Dispose() => Run.Dispose();

        /// <summary>Goes from the strokes that stand here to the strokes that should.</summary>
        public void Sync(StackSync sync, DeviceWatch watch) {
            if (sync.Document is { } text) {
                document = LayerStackYaml.Read(text);
                stale = true;
            }

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
            set => proxy = Math.Max(1, value);
        }

        /// <summary>
        ///     Brings the plan up to the document, which only a change of structure compiles, or a change of size: at
        ///     the proxy, or at full size for what a file would hold. Canvases stay at full size, and the plan samples
        ///     them down.
        /// </summary>
        public IReadOnlyList<string> Prepare(bool full = false) {
            var divided = full ? 1 : proxy;
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
            foreach (var file in touched) {
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

        Applied Begin(StackPaintPart part) {
            var file = LibraryLayout.Beside(folder, part.Paint);
            if (part.Capture is { } capture) {
                touched.Add(part.Paint);
                return known[part.Id] = new(part.Id, null, null) { Copy = new CanvasCapture(this, file, capture) };
            }

            if (part.From is { } from) {
                touched.Add(part.Paint);
                // At the size it was first made at, which a replay after the set's size changed keeps (#106).
                var made = part.Width > 0 && part.Height > 0
                    ? (part.Width, part.Height)
                    : (document.BaseWidth, document.BaseHeight);
                Made(file, made);
                CanvasCopy copy = new(canvases, file, Path.GetFullPath(from, folder), made.Item1, made.Item2);
                return known[part.Id] = new(part.Id, null, null) { Copy = copy };
            }

            // The stamps are texels of the set as it was when the stroke began, which a replay keeps (#19).
            var side = part.Width > 0 && part.Height > 0
                ? (part.Width, part.Height)
                : (document.BaseWidth, document.BaseHeight);
            var canvas = Made(file, side);
            if (canvas is null) {
                canvas = new(side.Item1, side.Item2);
                canvases.Adopt(file, canvas);
            } else if ((canvas.Width, canvas.Height) != side) {
                canvas = Resized(file, part.Paint, canvas, side);
            }

            // Before anything reads it. A file a stroke starts is new, or left by strokes that can never stand again.
            if (part.Start is { } start) {
                canvas.Channel(part.Usage).Fill(start);
            }

            touched.Add(part.Paint);
            stroked.Add(file);
            var surface = Surface(part.Model, canvas.Width, canvas.Height, part.Isolation);
            var image = canvas.Channel(part.Usage);
            var coverage = surface?.Coverage ?? PaintCoverage.Everywhere(canvas.Width, canvas.Height);
            var placement = PlacementOf(part, surface);
            var stroke = new Applied(
                part.Id,
                new(
                    image,
                    coverage,
                    Brush(part.Brush) with { Radius = StrokeShapes.Radius(part, surface, Bounds(part.Model)) },
                    part.Colour,
                    surface is null ? 0 : Gutter,
                    // A line or a path runs through its points, and a brush trailing them would stop short of the last.
                    part.Curve == "free" ? part.Brush.Lazy : 0f,
                    placement: placement,
                    source: StrokeSources.Source(part, image, coverage, Picture, Composite),
                    stencil: StrokeSources.Stencil(part.Stencil, Picture),
                    resume: StrokeShapes.Resume(part)
                ),
                part.Curve is "free" or "fill" ? null : new()
            ) { Fill = part.Curve == "fill", Outline = part.Outline };
            known[part.Id] = stroke;
            return stroke;
        }

        /// <summary>
        ///     The whole model's box, every texture set of it, or null with no model. ⚠ Not the set's own: a symmetry's
        ///     centre and a size in the model's units are the model's, and one set of a character is half of it.
        /// </summary>
        BoundingBox? Bounds(string model) =>
            model.Trim().Length == 0 ? null : meshes.Of(Path.GetFullPath(model, folder)).Projection.Bounds;

        /// <summary>
        ///     The mesh as a stroke reaches it, at the size of its canvas, in the set's layout: only the faces the
        ///     viewport drew when it began, with an isolation (#136). ⚠ Built again when the shelf reads the file
        ///     again: a mesh reloaded on the same layout keeps its documents open (#89), and its triangles may have
        ///     moved under the same coordinates.
        /// </summary>
        PaintMeshSurface? Surface(string model, int width, int height, ViewportIsolation? isolation) {
            if (model.Trim().Length == 0) {
                return null;
            }

            var file = Path.GetFullPath(model, folder);
            var (parts, materials) = (isolation?.Parts ?? [], isolation?.Materials ?? []);
            var shown = string.Join('\n', [..parts, string.Empty, ..materials]);
            var key = $"{file}\n{UvSet}\n{width}x{height}\n{shown}";
            var over = meshes.Of(file, set, UvSet, [..parts], [..materials]).Projection;
            if (!surfaces.TryGetValue(key, out var held) || !ReferenceEquals(held.Over, over)) {
                surfaces[key] = held = (over, new(over, width, height));
            }

            return held.Surface;
        }

        /// <summary>
        ///     A canvas made the size a stroke paints at (#19). Larger is exact and loses nothing, and is done only to
        ///     a canvas no stroke of this session has begun on, since a stroke's undo holds the pixels it painted over.
        ///     A canvas larger than its set keeps its size and is sampled at the set's, so it is never made smaller.
        /// </summary>
        /// <exception cref="IpcException">The canvas cannot be painted at that size here.</exception>
        PaintCanvas Resized(string file, string name, PaintCanvas canvas, (int Width, int Height) side) {
            var (width, height) = side;
            if (canvas.Width > width || canvas.Height > height) {
                throw new IpcException(
                    $"'{name}' holds {canvas.Width}×{canvas.Height} texels and the texture set is {width}×{height}. "
                    + "Painting it smaller would throw its detail away, so set the size back to paint this layer."
                );
            }

            if (stroked.Contains(file)) {
                throw new IpcException(
                    $"'{name}' was painted at {canvas.Width}×{canvas.Height} earlier in this session. Save, close and "
                    + $"open the document again to paint it at {width}×{height}."
                );
            }

            var enlarged = canvas.Enlarged(width, height);
            canvases.Adopt(file, enlarged);
            // The plan holds the smaller one's texture, which the larger one's tiles do not fit.
            stale = true;
            return enlarged;
        }

        /// <summary>
        ///     The canvas a journal entry paints or makes, as it was when the entry was first done. A blank this
        ///     Worker made at load for a canvas the journal makes is made again at the entry's size: it was made at
        ///     the set's size today, and a replay after the size changed would otherwise paint, copy or flatten a
        ///     canvas of another size than the first time (#106, #241). A blank holds nothing, so nothing is lost.
        /// </summary>
        /// <param name="file">The canvas.</param>
        /// <param name="side">The size the entry made it at.</param>
        /// <returns>The canvas, or null when there is none yet.</returns>
        PaintCanvas? Made(string file, (int Width, int Height) side) {
            var canvas = canvases.Open(file);
            if (canvas is null || !blanks.Remove(file) || (canvas.Width, canvas.Height) == side) {
                return canvas;
            }

            canvas = new(side.Width, side.Height);
            canvases.Adopt(file, canvas);
            stale = true;
            return canvas;
        }

        PaintBrush Brush(StackBrush brush) =>
            StrokeShapes.Brush(brush, StrokeShapes.Alpha(brush.Alpha, folder, alphas));

        /// <summary>
        ///     Makes a copy the journal holds. ⚠ As its turn comes and not as it begins: a replay begins every stroke
        ///     before it stamps any, and a copy made then would be of a canvas the strokes before it have not reached
        ///     yet.
        /// </summary>
        void Copy(Applied copy, IAuthoringCommand command) {
            if (copy.Done) {
                return;
            }

            command.Do(Context);
            copy.Command = command;
            copy.Done = true;
            unknown = true;
        }

        void Extend(Applied stroke, StackPaintPart part) {
            if (stroke.Copy is { } copy) {
                Copy(stroke, copy);
                return;
            }

            if (stroke.Stroke is null) {
                return;
            }

            // A canvas painted at its own larger size (#19) is dirtied in its own texels, which narrow no map's tiles.
            if (part.Width > 0 && (part.Width, part.Height) != (document.BaseWidth, document.BaseHeight)) {
                unknown = true;
            }

            if (stroke.Curve is { } curve) {
                Bend(stroke.Stroke, curve, part);
            } else if (stroke is { Fill: true, Outline: true }) {
                // One stamp for the whole outline, which the placement holds: where it lands does not matter.
                if (part.Stamps is [var first, ..]) {
                    stroke.Stroke.Dab(new(first.X, first.Y), dirtied, StrokeShapes.Pen(first));
                }
            } else if (stroke.Fill) {
                // Each click is a place of its own, and the stroke between two would fill whatever it passed over.
                foreach (var stamp in part.Stamps) {
                    stroke.Stroke.Dab(new(stamp.X, stamp.Y), dirtied, StrokeShapes.Pen(stamp));
                }
            } else {
                foreach (var stamp in part.Stamps) {
                    stroke.Stroke.MoveTo(new(stamp.X, stamp.Y), dirtied, StrokeShapes.Pen(stamp));
                }
            }

            if (!part.Open) {
                Close(stroke);
            }
        }

        static IAuthoringCommand Close(Applied stroke) {
            if (!stroke.Done) {
                stroke.Done = true;
                stroke.Command = stroke.Copy
                    ?? (stroke.Stroke is not { IsEmpty: false } painted
                        ? new Nothing()
                        : new PaintStrokeCommand([painted], "Stroke"));
            }

            return stroke.Command!;
        }

        sealed class NoContext : ICommandContext {
            public void Touch(IAuthoredDocument document) { }
        }
    }
}
