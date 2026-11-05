using Patina.Contracts;
using Patina.Graphs;
using Patina.Ipc;
using System.Diagnostics;
using System.Security.Cryptography;
using Patina.Viewport;
using Vixen.Authoring;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
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
sealed class StackHost(DeviceHost gpu, ViewportHost viewport, MeshShelf meshes) : SessionHost<StackHost.Session> {
    /// <summary>How far a stamp on a mesh is dilated past an island's edge, in texels.</summary>
    const int Gutter = 4;

    readonly PreviewWriter previews = new();
    readonly CompoundShelf compounds = new();
    readonly GraphPictures pictures = new();

    protected override string Kind => "stack";

    public IpcMessage Load(IpcMessage request) {
        var load = request.StackLoad ?? throw new IpcException("stack.load holds no stack.");
        Hold(load.Session, new(load, gpu.Evaluator, compounds.Of(load.Folder), pictures, meshes));
        return IpcMessage.Ok(request.Id);
    }

    /// <summary>The viewport starts wearing a stack, or stops. A stack already here sends everything it has.</summary>
    public IpcMessage Follow(IpcMessage request) {
        viewport.Follow(request.Following);
        if (request.Following is { } session && Holds(session)) {
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

    public IpcMessage Sync(IpcMessage request) {
        var sync = request.StackSync ?? throw new IpcException("stack.sync says nothing.");
        Find(sync.Session).Sync(sync);
        return IpcMessage.Ok(request.Id);
    }

    public IpcMessage Evaluate(IpcMessage request) {
        var ask = request.StackEvaluate ?? throw new IpcException("stack.evaluate says nothing to evaluate.");
        var start = Stopwatch.GetTimestamp();
        var stack = Find(ask.Session);
        var problems = stack.Prepare();
        if (problems.Count > 0 || stack.Run.Plan is not { } plan) {
            return Reply(new(stack.Run.Compiled, false, 0, 0, 0, Milliseconds(start), [], problems));
        }

        var (ran, repaired, reused) = (0, 0, 0);

        // ⚠ The plan and not the extension: only the plan's outputs are sure to be readable afterwards, and
        // what the viewport needs is what this repair rewrote.
        if (viewport.Following == ask.Session) {
            using var bake = stack.Run.Evaluate();
            Feed(stack, bake);
            (ran, repaired, reused) = (bake.Ran.Length, bake.Repaired, bake.Reused);
        }

        if (ask.PreviewSide <= 0) {
            return Reply(new(stack.Run.Compiled, true, ran, repaired, reused, Milliseconds(start), [], []));
        }

        var outputs = stack.Run.Outputs;
        var extended = GraphPreviews.Extend(
            plan,
            [..outputs.Select(static (output, index) => new GraphPreviewSource(index, output.Usage, output.Image))],
            ask.PreviewSide
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
                stack.Run.Compiled,
                true,
                ran + thumbnails.Ran.Length,
                repaired + thumbnails.Repaired,
                reused + thumbnails.Reused,
                Milliseconds(start),
                written,
                []
            )
        );
    }

    /// <summary>
    ///     Hands the viewport what this evaluation changed, in the bytes an export writes. Only the rectangles a repair
    ///     rewrote cross, and a picture nothing touched costs nothing. No device is shared: the bytes are in this
    ///     process, which is also why a Worker with a dead viewport still paints.
    /// </summary>
    void Feed(Session stack, TextureBake bake) {
        foreach (var output in stack.Run.Outputs) {
            if (Slot(output.Usage) is not { } slot) {
                continue;
            }

            var key = bake.KeyOf(output.Image);
            var known = stack.Fed.TryGetValue(output.Image, out var fed);
            if (known && fed == key) {
                continue;
            }

            if (known && bake.RepairOf(output.Image) is { Length: > 0 } rects) {
                TextureRect[] list = [..rects];
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
                    ]
                );
            } else {
                var map = bake.Read(output.Image);
                viewport.Live.SetLive(slot, map.Width, map.Height, map.Pixels);
            }

            stack.Fed[output.Image] = key;
        }
    }

    static ViewportSlot? Slot(string usage) =>
        usage switch {
            "baseColor" => ViewportSlot.BaseColor,
            "normal" => ViewportSlot.Normal,
            "roughness" => ViewportSlot.Roughness,
            "metalness" => ViewportSlot.Metallic,
            "occlusion" => ViewportSlot.Occlusion,
            _ => null
        };

    /// <summary>Writes every canvas this session changed, beside the stack or into a checkpoint.</summary>
    public IpcMessage Save(IpcMessage request) {
        var save = request.StackSave ?? throw new IpcException("stack.save says nothing.");
        Find(save.Session).Save(save.Folder);
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
        var problems = stack.Prepare();
        if (problems.Count > 0 || stack.Run.Plan is null) {
            throw new IpcException(string.Join(' ', problems));
        }

        if (ask.Viewport) {
            return new() { Type = IpcTypes.Ok, StackPicture = Sampled(stack) };
        }

        using var bake = ask.Clean ? stack.Run.EvaluateClean() : stack.Run.Evaluate();
        var outputs = stack.Run.Outputs;
        var maps = bake.Read([..outputs.Select(static output => output.Image)]);
        List<StackPixels> pixels = [];
        foreach (var (output, map) in outputs.Zip(maps)) {
            pixels.Add(
                new(output.Usage, map.Width, map.Height, Digest(map), Export(stack, output.Usage, map, ask.Folder))
            );
        }

        return new() { Type = IpcTypes.Ok, StackPicture = pixels };
    }

    /// <summary>What the viewport's draw call samples for each map, read back from the viewport's own device.</summary>
    List<StackPixels> Sampled(Session stack) {
        List<StackPixels> pixels = [];
        foreach (var output in stack.Run.Outputs) {
            if (Slot(output.Usage) is { } slot && viewport.Live.Sampled(slot) is { } bytes) {
                var size = stack.Run.Plan!.SizeOf(output.Image);
                pixels.Add(new(output.Usage, size.X, size.Y, Convert.ToHexStringLower(SHA256.HashData(bytes))));
            }
        }

        return pixels;
    }

    static string? Export(Session stack, string usage, Bitmap map, string? folder) {
        if (folder is null) {
            return null;
        }

        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"{stack.Name}.{usage}.png");
        File.WriteAllBytes(file, PngCodec.Encode(map));
        return file;
    }

    static string Digest(Bitmap map) => Convert.ToHexStringLower(SHA256.HashData(map.Pixels));

    static IpcMessage Reply(StackResult result) => new() { Type = IpcTypes.Ok, StackResult = result };

    static double Milliseconds(long start) => Stopwatch.GetElapsedTime(start).TotalMilliseconds;

    /// <summary>One stroke as this Worker holds it: under way, or a command that can undo it.</summary>
    sealed class Applied(long id, PaintStroke stroke, PaintPath? curve) {
        public long Id { get; } = id;

        public PaintStroke Stroke { get; } = stroke;

        /// <summary>The control points of a line or a path, which paints when it ends.</summary>
        public PaintPath? Curve { get; } = curve;

        public IAuthoringCommand? Command { get; set; }

        public bool Done { get; set; }
    }

    internal sealed class Session : IDisposable {
        static readonly NoContext Context = new();

        readonly string path;
        readonly string set;
        readonly string folder;
        readonly PublishedCompounds compounds;
        readonly PaintCanvasStore canvases = new(long.MaxValue);
        readonly HashSet<string> touched = new(StringComparer.Ordinal);
        readonly List<Applied> applied = [];
        readonly Dictionary<long, Applied> known = [];
        readonly Dictionary<string, PaintMeshSurface> surfaces = new(StringComparer.Ordinal);
        readonly Dictionary<string, IBrushMask> alphas = new(StringComparer.Ordinal);
        readonly MeshShelf meshes;
        LayerStackAsset document;
        bool stale = true;

        public Session(
            StackLoad load,
            TexturePlanEvaluator evaluator,
            PublishedCompounds compounds,
            GraphPictures pictures,
            MeshShelf meshes
        ) {
            this.meshes = meshes;
            path = load.Path;
            set = load.Set;
            folder = Path.GetDirectoryName(Path.GetFullPath(load.Path))!;
            this.compounds = compounds;
            document = LayerStackYaml.Read(load.Document);
            Run = new(
                evaluator,
                canvases,
                load.Path,
                (uploads, plan, owed) => pictures.Fill(
                        uploads,
                        plan,
                        [..owed],
                        load.ImageRoots,
                        load.MeshMaps
                    )
                    .Refusals.Select(static refusal => refusal.Message)
                    .ToArray()
            );
            if (load.Checkpoint is { } checkpoint) {
                Restore(checkpoint);
            }

            foreach (var created in load.Created.Where(paint => !touched.Contains(paint))) {
                canvases.Adopt(Path.Combine(folder, created), new(document.BaseWidth, document.BaseHeight));
            }
        }

        public LayerStackEvaluator Run { get; }

        /// <summary>Which version of each output the viewport holds.</summary>
        public Dictionary<int, TextureResultKey> Fed { get; } = [];

        public string Name => Path.GetFileNameWithoutExtension(path);

        public void Dispose() => Run.Dispose();

        /// <summary>Goes from the strokes that stand here to the strokes that should.</summary>
        public void Sync(StackSync sync) {
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
            }

            var parts = sync.Parts.ToDictionary(static part => part.Id);
            for (var index = common; index < sync.Standing.Count; index++) {
                var id = sync.Standing[index];
                if (known.TryGetValue(id, out var stroke)) {
                    Close(stroke).Do(Context);
                    applied.Add(stroke);
                } else if (parts.TryGetValue(id, out var part)) {
                    applied.Add(Begin(part));
                } else {
                    throw new IpcException($"Stroke {id} stands and this Worker was never told it.");
                }
            }

            foreach (var part in sync.Parts) {
                Extend(known[part.Id], part);
            }
        }

        /// <summary>Brings the plan up to the document, which only a change of structure compiles.</summary>
        public IReadOnlyList<string> Prepare() {
            if (!stale && Run.Plan is not null) {
                return [];
            }

            if (LayerStackEdit.SetFor(document, set) is not { } chosen) {
                return [$"This stack has no texture set '{set}'."];
            }

            stale = false;
            return Run.Prepare(document, chosen, compounds.Registry, compounds.Source);
        }

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
                }

                File.Move(temporary, target, true);
                if (to is null) {
                    canvases.Saved(Path.Combine(folder, file));
                }
            }
        }

        void Restore(string checkpoint) {
            foreach (var file in Directory.EnumerateFiles(checkpoint, $"*{LayerPaint.Extension}")) {
                using var stream = File.OpenRead(file);
                var name = Path.GetFileName(file);
                canvases.Adopt(Path.Combine(folder, name), PaintCanvas.Read(stream));
                touched.Add(name);
            }
        }

        Applied Begin(StackPaintPart part) {
            var file = Path.Combine(folder, part.Paint);
            var canvas = canvases.Open(file);
            if (canvas is null) {
                canvas = new(document.BaseWidth, document.BaseHeight);
                canvases.Adopt(file, canvas);
            }

            touched.Add(part.Paint);
            var surface = Surface(part.Model);
            var stroke = new Applied(
                part.Id,
                new(
                    canvas.Channel(part.Usage),
                    surface?.Coverage ?? PaintCoverage.Everywhere(canvas.Width, canvas.Height),
                    Brush(part.Brush),
                    part.Colour,
                    surface is null ? 0 : Gutter,
                    placement: Placement(part, surface)
                ),
                part.Curve == "free" ? null : new()
            );
            known[part.Id] = stroke;
            return stroke;
        }

        /// <summary>Where a stroke's stamps land besides under themselves, which only a mesh can say.</summary>
        static IPaintPlacement? Placement(StackPaintPart part, PaintMeshSurface? surface) {
            if (part.View is not { } view) {
                return surface?.AcrossSeams();
            }

            return (surface ?? throw new IpcException("A stroke with a view needs a mesh."))
                .Through(
                    new(
                        Point(view.Eye),
                        Point(view.Right),
                        Point(view.Up),
                        Point(view.Forward),
                        view.VerticalFieldOfView,
                        view.Width,
                        view.Height
                    ),
                    part.Mirror switch {
                        "x" => PaintSymmetry.X,
                        "y" => PaintSymmetry.Y,
                        "z" => PaintSymmetry.Z,
                        _ => null
                    }
                );
        }

        static Vector3 Point(IReadOnlyList<float> numbers) => new(numbers[0], numbers[1], numbers[2]);

        PaintMeshSurface? Surface(string model) {
            if (model.Trim().Length == 0) {
                return null;
            }

            var file = Path.GetFullPath(model, folder);
            if (!surfaces.TryGetValue(file, out var surface)) {
                surfaces[file] = surface = new(meshes.Of(file).Projection, document.BaseWidth, document.BaseHeight);
            }

            return surface;
        }

        PaintBrush Brush(StackBrush brush) =>
            PaintBrush.Default with {
                Radius = brush.Radius,
                Flow = brush.Flow,
                Opacity = brush.Opacity,
                Spacing = brush.Spacing,
                Falloff = brush.Falloff,
                Alpha = Alpha(brush.Alpha),
                Angle = brush.Angle,
                Rotation = brush.Rotation switch {
                    "along" => BrushRotation.AlongStroke,
                    "random" => BrushRotation.Random,
                    _ => BrushRotation.Fixed
                },
                PositionJitter = brush.PositionJitter,
                AngleJitter = brush.AngleJitter,
                SizeJitter = brush.SizeJitter,
                PressureSize = brush.PressureSize,
                PressureFlow = brush.PressureFlow
            };

        /// <summary>A named alpha, or a picture beside the stack read through its red channel.</summary>
        IBrushMask? Alpha(string? name) {
            if (name is null || name == PaintAlphas.Round) {
                return null;
            }

            if (PaintAlphas.Find(name) is { } shelved) {
                return shelved;
            }

            var file = Path.GetFullPath(name, folder);
            if (alphas.TryGetValue(file, out var held)) {
                return held;
            }

            try {
                var picture = PngCodec.Decode(File.ReadAllBytes(file));
                PaintImage image = new(picture.Width, picture.Height);
                for (var at = 0; at < picture.Width * picture.Height; at++) {
                    image[at] = PaintImage.Pack(picture.Pixels[at * 4] / 255f, 0f, 0f, 1f);
                }

                return alphas[file] = new PaintImageMask(image, 0);
            } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) {
                throw new IpcException($"'{name}' would not read as an alpha: {e.Message}");
            }
        }

        static void Extend(Applied stroke, StackPaintPart part) {
            if (stroke.Curve is { } curve) {
                foreach (var stamp in part.Stamps) {
                    curve.Add(new(stamp.X, stamp.Y));
                }

                if (!part.Open) {
                    List<Vector2> points = [];
                    curve.Sample(points);
                    foreach (var point in points) {
                        stroke.Stroke.MoveTo(point);
                    }
                }
            } else {
                foreach (var stamp in part.Stamps) {
                    stroke.Stroke.MoveTo(new(stamp.X, stamp.Y), pressure: stamp.Pressure);
                }
            }

            if (!part.Open) {
                Close(stroke);
            }
        }

        static IAuthoringCommand Close(Applied stroke) {
            if (!stroke.Done) {
                stroke.Done = true;
                stroke.Command = stroke.Stroke.IsEmpty
                    ? new Nothing()
                    : new PaintStrokeCommand([stroke.Stroke], "Stroke");
            }

            return stroke.Command!;
        }

        sealed class NoContext : ICommandContext {
            public void Touch(IAuthoredDocument document) { }
        }

        /// <summary>A stroke that stamped nothing, which still stands in the journal.</summary>
        sealed class Nothing : IAuthoringCommand {
            public string Name => "Stroke";

            public void Do(ICommandContext context) { }

            public void Undo(ICommandContext context) { }

            public bool TryMergeWith(
                IAuthoringCommand previous,
                [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IAuthoringCommand? merged
            ) {
                merged = null;
                return false;
            }
        }
    }
}
