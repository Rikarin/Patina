using Patina.Contracts;
using Patina.Graphs;
using Patina.Ipc;
using System.Diagnostics;
using System.Security.Cryptography;
using Vixen.Authoring;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
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
sealed class StackHost(DeviceHost gpu) : SessionHost<StackHost.Session> {
    readonly PreviewWriter previews = new();
    readonly CompoundShelf compounds = new();
    readonly GraphPictures pictures = new();

    protected override string Kind => "stack";

    public IpcMessage Load(IpcMessage request) {
        var load = request.StackLoad ?? throw new IpcException("stack.load holds no stack.");
        Hold(load.Session, new(load, gpu.Evaluator, compounds.Of(load.Folder), pictures));
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

        var outputs = stack.Run.Outputs;
        var extended = GraphPreviews.Extend(
            plan,
            [..outputs.Select(static (output, index) => new GraphPreviewSource(index, output.Usage, output.Image))],
            ask.PreviewSide
        );
        using var bake = stack.Run.Evaluate(extended.Plan);
        var stored = previews.Write(bake, [..extended.Images.Select(static image => image.Image)], ask.PreviewFolder);
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
                bake.Ran.Length,
                bake.Repaired,
                bake.Reused,
                Milliseconds(start),
                written,
                []
            )
        );
    }

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
    sealed class Applied(long id, PaintStroke stroke) {
        public long Id { get; } = id;

        public PaintStroke Stroke { get; } = stroke;

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
        LayerStackAsset document;
        bool stale = true;

        public Session(
            StackLoad load,
            TexturePlanEvaluator evaluator,
            PublishedCompounds compounds,
            GraphPictures pictures
        ) {
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
            var stroke = new Applied(
                part.Id,
                new(
                    canvas.Channel(part.Usage),
                    PaintCoverage.Everywhere(canvas.Width, canvas.Height),
                    PaintBrush.Default with {
                        Radius = part.Brush.Radius,
                        Flow = part.Brush.Flow,
                        Opacity = part.Brush.Opacity,
                        Spacing = part.Brush.Spacing,
                        Falloff = part.Brush.Falloff
                    },
                    part.Colour,
                    gutter: 0
                )
            );
            known[part.Id] = stroke;
            return stroke;
        }

        static void Extend(Applied stroke, StackPaintPart part) {
            foreach (var stamp in part.Stamps) {
                stroke.Stroke.MoveTo(new Vector2(stamp.X, stamp.Y));
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
