// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Vixen.Graphics.Vulkan;
using Vixen.TextureGraph;
using Vixen.Texturing;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Bench;

/// <summary>
///     Spike S4: a real stack, compiled to a real plan, with every stamp taken from the brush through to bytes a client
///     could show. The painted layer is near the bottom, so every layer above it has to composite again.
/// </summary>
static class PaintBench {
    const int Tile = 256;

    public static int Run(Scenario scenario, int layerCount, int channels, bool blurAbove) {
        var (size, _, radius, paintedAt, _, _) = scenario;
        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            Console.Error.WriteLine($"No Vulkan device: {refusal}");
            return 1;
        }

        using (device) {
            Say($"device {device.Adapter.Name}, {size}², {layerCount} layers, {channels} channels, radius {radius}");
            var stack = Stack(size, layerCount, channels, paintedAt, blurAbove);
            var clock = Stopwatch.StartNew();
            var compilation = LayerStackCompiler.Compile(stack, stack.Sets[0]);
            if (compilation.Plan is not { } plan) {
                Console.Error.WriteLine(
                    string.Join(
                        '\n',
                        [
                            ..compilation.Problems.Select(static p => p.Message),
                            ..compilation.Diagnostics.Select(static d => d.Message)
                        ]
                    )
                );
                return 1;
            }

            Say($"compiled in {clock.Elapsed.TotalMilliseconds:F0} ms");
            Say($"{plan.Ops.Length} ops, {plan.Images.Length} images");
            foreach (var group in plan.Ops.GroupBy(op => (op.Kernel, plan.Images[op.Output].Format))) {
                Say($"  {group.Count()} × {group.Key.Kernel} → {group.Key.Format}");
            }

            using var evaluator = new TexturePlanEvaluator(device);
            evaluator.Results.Budget = (Environment.GetEnvironmentVariable("S4_BUDGET_GB") is { } budget
                    ? long.Parse(budget, CultureInfo.InvariantCulture)
                    : 6)
                << 30;
            var folder = Path.Combine(Path.GetTempPath(), $"patina-s4-{Environment.ProcessId}");
            var canvases = new PaintCanvasStore(long.MaxValue);
            foreach (var layer in stack.Sets[0].Layers.Where(static l => l.Kind == LayerKind.Paint)) {
                canvases.Adopt(Path.Combine(folder, layer.Paint), new(size, size));
                if (layer.Mask.Paint.Length > 0) {
                    canvases.Adopt(Path.Combine(folder, layer.Mask.Paint), new(size, size));
                }
            }

            var library = TextureNodeLibrary.Publish();
            using var run = new LayerStackEvaluator(evaluator, canvases, Path.Combine(folder, "s4.vxlayers"));
            clock.Restart();
            var refusals = run.Prepare(stack, stack.Sets[0], library.Registry, library.SubGraphs);
            if (refusals.Count > 0) {
                Console.Error.WriteLine(string.Join('\n', refusals));
                return 1;
            }

            Say($"uploaded in {clock.Elapsed.TotalMilliseconds:F0} ms");
            clock.Restart();
            run.Evaluate().Dispose();
            Say($"first evaluation {clock.Elapsed.TotalMilliseconds:F0} ms, cache {evaluator.Results.Bytes >> 20} MiB");

            Stroke(run, canvases, evaluator, folder, stack, scenario);
        }

        return 0;
    }

    static void Stroke(
        LayerStackEvaluator run,
        PaintCanvasStore canvases,
        TexturePlanEvaluator evaluator,
        string folder,
        LayerStackAsset stack,
        Scenario scenario
    ) {
        var (size, stamps, radius, paintedAt, whole, everyChannel) = scenario;
        string[] usages = everyChannel ? [..stack.Sets[0].Channels.Select(static c => c.Usage)] : ["baseColor"];
        var canvas = canvases.Open(Path.Combine(folder, $"layer{paintedAt}.vxpaint"))!;
        int[] outputs = [..run.Outputs.Where(o => usages.Contains(o.Usage)).Select(static o => o.Image)];
        PaintStroke[] strokes = [
            ..usages.Select((string usage, int index) => new PaintStroke(
                    canvas.Channel(usage),
                    PaintCoverage.Everywhere(size, size),
                    PaintBrush.Default with { Radius = radius, Flow = 1f, Spacing = 0.25f },
                    0xFF3060E0u + (uint)index,
                    gutter: 0
                )
            )
        ];
        var times = new Times();
        for (var stamp = 0; stamp < stamps; stamp++) {
            var angle = stamp * 0.05f;
            var at = new Vixen.Core.Mathematics.Vector2(
                (size / 2f) + (MathF.Cos(angle) * size * 0.3f),
                (size / 2f) + (MathF.Sin(angle * 0.7f) * size * 0.3f)
            );

            var start = Stopwatch.GetTimestamp();
            var dirty = PaintRect.Empty;
            foreach (var stroke in strokes) {
                dirty = stroke.MoveTo(at);
            }

            var stamped = Stopwatch.GetTimestamp();
            var tiles = Tiles(dirty, size);
            using var bake = whole ? run.EvaluateClean() : run.Evaluate();
            var composed = Stopwatch.GetTimestamp();
            var bytes = 0;
            foreach (var output in outputs) {
                bytes += bake.ReadTexels(output, tiles).Sum(static r => r.Length);
            }

            var readBack = Stopwatch.GetTimestamp();
            if (stamp >= 8 && tiles.Count > 0) { // the first few compile pipelines and are not a stroke
                times.Add(start, stamped, composed, readBack);
            }

            if (stamp == stamps / 2) {
                Say($"mid-stroke: {tiles.Count} tiles, {bake.Repaired} repaired of {bake.Ran.Length} run");
                Say($"{bake.Dispatches} dispatches, {bytes >> 10} KiB read, cache {evaluator.Results.Bytes >> 20} MiB");
            }
        }

        times.Report();
    }

    static List<TextureRect> Tiles(PaintRect dirty, int size) {
        List<TextureRect> tiles = [];
        var box = dirty.Clip(size, size);
        for (var y = box.Y / Tile; !box.IsEmpty && y <= (box.EndY - 1) / Tile; y++) {
            for (var x = box.X / Tile; x <= (box.EndX - 1) / Tile; x++) {
                tiles.Add(
                    new(x * Tile, y * Tile, Math.Min(Tile, size - (x * Tile)), Math.Min(Tile, size - (y * Tile)))
                );
            }
        }

        return tiles;
    }

    /// <summary>
    ///     What an artist's stack looks like: fills, paint layers, some with a painted mask, and filters. Every layer
    ///     writes every channel, so nothing folds away.
    /// </summary>
    static LayerStackAsset Stack(int size, int layerCount, int channels, int paintedAt, bool blurAbove) {
        string[] usages = ["baseColor", "roughness", "metalness", "normal", "height"];
        List<ChannelAsset> wanted = [
            ..usages.Take(channels)
                .Select(static usage => new ChannelAsset { Usage = usage, Default = [0.5f, 0.5f, 0.5f, 1f] })
        ];
        List<LayerAsset> layers = [];
        LayerBlendMode[] modes =
            [LayerBlendMode.Copy, LayerBlendMode.Multiply, LayerBlendMode.Overlay, LayerBlendMode.Screen];
        for (var index = 0; index < layerCount; index++) {
            var id = $"layer{index}";
            var opacity = 0.5f + (0.05f * (index % 8));
            layers.Add(
                (index, index % 5) switch {
                    _ when index == paintedAt => new() { Id = id, Kind = LayerKind.Paint, Paint = $"{id}.vxpaint" },
                    _ when blurAbove && index == layerCount - 2 => new() {
                        Id = id,
                        Kind = LayerKind.Filter,
                        Filter = LayerFilterKind.Blur,
                        Opacity = 0.8f,
                        Settings = { ["Radius"] = (float[])[3f] }
                    },
                    (0, _) => new() {
                        Id = id,
                        Kind = LayerKind.Fill,
                        Values = wanted.ToDictionary(
                            static c => c.Usage,
                            c => (float[])[0.6f, 0.5f, 0.4f, Array.IndexOf(usages, c.Usage) * 0.1f]
                        )
                    },
                    (_, 3) => new() {
                        Id = id, Kind = LayerKind.Filter, Filter = LayerFilterKind.Hsl, Opacity = opacity
                    },
                    (_, 1) => new() {
                        Id = id,
                        Kind = LayerKind.Paint,
                        Paint = $"{id}.vxpaint",
                        Blend = modes[index % modes.Length],
                        Opacity = opacity,
                        Mask = new() { Source = LayerMaskSource.Paint, Paint = $"{id}.mask.vxpaint" }
                    },
                    (_, 2) => new() { Id = id, Kind = LayerKind.Paint, Paint = $"{id}.vxpaint", Opacity = opacity },
                    _ => new() {
                        Id = id,
                        Kind = LayerKind.Fill,
                        Blend = modes[index % modes.Length],
                        Opacity = opacity,
                        Values = wanted.ToDictionary(
                            static c => c.Usage,
                            c => (float[])[0.3f, 0.5f, 0.7f, 1f - (Array.IndexOf(usages, c.Usage) * 0.1f)]
                        )
                    }
                }
            );
        }

        return new() {
            Name = "S4",
            BaseWidth = size,
            BaseHeight = size,
            Sets = [new() { Name = "Body", Channels = wanted, Layers = layers }]
        };
    }

    public readonly record struct Scenario(
        int Size,
        int Stamps,
        int Radius,
        int PaintedAt,
        bool Whole,
        bool EveryChannel);

    static void Say(FormattableString line) => Console.WriteLine(line.ToString(CultureInfo.InvariantCulture));

    sealed class Times {
        readonly List<double> stamp = [];
        readonly List<double> compose = [];
        readonly List<double> read = [];
        readonly List<double> total = [];

        public void Add(long start, long stamped, long composed, long readBack) {
            static double Ms(long a, long b) => (b - a) * 1000.0 / Stopwatch.Frequency;
            stamp.Add(Ms(start, stamped));
            compose.Add(Ms(stamped, composed));
            read.Add(Ms(composed, readBack));
            total.Add(Ms(start, readBack));
        }

        public void Report() {
            Line("stamp", stamp);
            Line("upload and composite", compose);
            Line("read-back", read);
            Line("total", total);
        }

        static void Line(string name, List<double> values) {
            var sorted = values.Order().ToArray();
            var (median, high) = (sorted[sorted.Length / 2], sorted[sorted.Length * 95 / 100]);
            Say($"{name,-22} p50 {median:F2}  p95 {high:F2}  max {sorted[^1]:F2} ms ({sorted.Length} samples)");
        }
    }
}
