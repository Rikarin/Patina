// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Vixen.Graphics.Vulkan;
using Vixen.TextureGraph;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Bench;

/// <summary>
///     Spike S4: a real stack, compiled to a real plan, with every stamp taken from the brush through to bytes a client
///     could show. The painted layer is near the bottom, so every layer above it has to composite again.
/// </summary>
static class PaintBench {
    const int Tile = 256;
    const int TilesEachWay = 3;

    public static int Run(
        int size,
        int layerCount,
        int channels,
        int stamps,
        int radius,
        int paintedAt,
        bool whole,
        bool everyChannel,
        bool blurAbove
    ) {
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
                Console.Error.WriteLine(string.Join('\n', [..compilation.Problems.Select(static p => p.Message), ..compilation.Diagnostics.Select(static d => d.Message)]));
                return 1;
            }

            Say($"compiled in {clock.Elapsed.TotalMilliseconds:F0} ms: {plan.Ops.Length} ops, {plan.Images.Length} images");
            foreach (var group in plan.Ops.GroupBy(op => (op.Kernel, plan.Images[op.Output].Format))) {
                Say($"  {group.Count()} × {group.Key.Kernel} → {group.Key.Format}");
            }

            using var evaluator = new TexturePlanEvaluator(device);
            if (Environment.GetEnvironmentVariable("S4_BUDGET_GB") is { } budget) {
                evaluator.Results.Budget = long.Parse(budget, CultureInfo.InvariantCulture) << 30;
            }

            using var uploads = new TextureUploads(device);
            var images = Fill(uploads, plan, compilation.Externals, size);
            TextureGraphExternal[] painted = [
                ..compilation.Externals.Where(e =>
                    (everyChannel || e.Asset.Contains("baseColor|", StringComparison.Ordinal))
                    && e.Asset.EndsWith($"layer{paintedAt}.vxpaint", StringComparison.Ordinal)
                )
            ];
            int[] outputs = [
                ..compilation.Outputs.Where(o => everyChannel || o.Usage == "baseColor").Select(static o => o.Image)
            ];

            clock.Restart();
            TextureBake bake = evaluator.EvaluateIncremental(plan, uploads.Externals);
            Say($"first evaluation {clock.Elapsed.TotalMilliseconds:F0} ms, cache {evaluator.Results.Bytes >> 20} MiB");
            var previous = bake.Keys;
            bake.Dispose();

            PaintStroke[] strokes = [
                ..painted.Select(external => new PaintStroke(
                        images[external.Image],
                        PaintCoverage.Everywhere(size, size),
                        new PaintBrush { Radius = radius, Spacing = 0.25f },
                        0xFF3060E0u + (uint)external.Image
                    )
                )
            ];
            var times = new Times();
            var repaired = 0;
            var dispatches = 0;
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
                TextureImageChange[] changes = [
                    ..painted.Select(external => new TextureImageChange(
                            external.Image,
                            [..tiles.Select(tile => new TextureTileWrite(tile, Texels(images[external.Image], tile)))],
                            new(new UInt128((ulong)external.Image + 1, (ulong)stamp + 1))
                        )
                    )
                ];
                uploads.Update(changes);
                var uploaded = Stopwatch.GetTimestamp();
                var regions = whole
                    ? null
                    : new TextureRegions(
                        previous,
                        painted.ToDictionary(static e => e.Image, _ => (ImmutableArray<TextureRect>)[..tiles])
                    );
                bake = evaluator.EvaluateIncremental(plan, uploads.Externals, regions);
                var composed = Stopwatch.GetTimestamp();
                var bytes = outputs.Sum(output => bake.ReadTexels(output, tiles).Sum(static r => r.Length));
                var readBack = Stopwatch.GetTimestamp();
                repaired = bake.Repaired;
                dispatches = bake.Dispatches;
                previous = bake.Keys;
                bake.Dispose();
                var end = Stopwatch.GetTimestamp();
                if (stamp >= 8 && tiles.Count > 0) { // the first few compile pipelines and are not a stroke
                    times.Add(start, stamped, uploaded, composed, readBack, end);
                }

                if (stamp == stamps / 2) {
                    Say($"mid-stroke: {tiles.Count} tiles, {repaired} repaired, {dispatches} dispatches, {bytes >> 10} KiB read, cache {evaluator.Results.Bytes >> 20} MiB");
                }
            }

            times.Report();
        }

        return 0;
    }

    static List<TextureRect> Tiles(PaintRect dirty, int size) {
        List<TextureRect> tiles = [];
        var box = dirty.Clip(size, size);
        for (var y = box.Y / Tile; !box.IsEmpty && y <= (box.EndY - 1) / Tile; y++) {
            for (var x = box.X / Tile; x <= (box.EndX - 1) / Tile; x++) {
                tiles.Add(new(x * Tile, y * Tile, Math.Min(Tile, size - (x * Tile)), Math.Min(Tile, size - (y * Tile))));
            }
        }

        return tiles;
    }

    static byte[] Texels(PaintImage image, TextureRect tile) {
        var bytes = new byte[tile.Width * tile.Height * 4];
        for (var row = 0; row < tile.Height; row++) {
            Buffer.BlockCopy(
                image.Texels,
                (((tile.Y + row) * image.Width) + tile.X) * 4,
                bytes,
                row * tile.Width * 4,
                tile.Width * 4
            );
        }

        return bytes;
    }

    static Dictionary<int, PaintImage> Fill(
        TextureUploads uploads,
        TexturePlan plan,
        ImmutableArray<TextureGraphExternal> externals,
        int size
    ) {
        Dictionary<int, PaintImage> images = [];
        foreach (var external in TextureGraphExternals.Upload(uploads, plan, externals)) {
            var image = new PaintImage(size, size);
            images[external.Image] = image;
            uploads.Add(plan, external.Image, size, size, image.Texels);
        }

        return images;
    }

    /// <summary>
    ///     What an artist's stack looks like: fills, paint layers, some with a painted mask, and filters. Every layer
    ///     writes every channel, so nothing folds away.
    /// </summary>
    static LayerStackAsset Stack(int size, int layerCount, int channels, int paintedAt, bool blurAbove) {
        string[] usages = ["baseColor", "roughness", "metalness", "normal", "height"];
        List<ChannelAsset> wanted = [
            ..usages.Take(channels).Select(static usage => new ChannelAsset { Usage = usage, Default = [0.5f, 0.5f, 0.5f, 1f] })
        ];
        List<LayerAsset> layers = [];
        LayerBlendMode[] modes = [LayerBlendMode.Copy, LayerBlendMode.Multiply, LayerBlendMode.Overlay, LayerBlendMode.Screen];
        for (var index = 0; index < layerCount; index++) {
            var id = $"layer{index}";
            var opacity = 0.5f + (0.05f * (index % 8));
            layers.Add((index, index % 5) switch {
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
                    Values = wanted.ToDictionary(static c => c.Usage, c => (float[])[0.6f, 0.5f, 0.4f, Array.IndexOf(usages, c.Usage) * 0.1f])
                },
                (_, 3) => new() {
                    Id = id,
                    Kind = LayerKind.Filter,
                    Filter = LayerFilterKind.Hsl,
                    Opacity = opacity
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
                    Values = wanted.ToDictionary(static c => c.Usage, c => (float[])[0.3f, 0.5f, 0.7f, 1f - (Array.IndexOf(usages, c.Usage) * 0.1f)])
                }
            });
        }

        return new() {
            Name = "S4",
            BaseWidth = size,
            BaseHeight = size,
            Sets = [new() { Name = "Body", Channels = wanted, Layers = layers }]
        };
    }

    static void Say(FormattableString line) => Console.WriteLine(line.ToString(CultureInfo.InvariantCulture));

    sealed class Times {
        readonly List<double> stamp = [];
        readonly List<double> upload = [];
        readonly List<double> compose = [];
        readonly List<double> read = [];
        readonly List<double> total = [];

        public void Add(long start, long stamped, long uploaded, long composed, long readBack, long end) {
            static double Ms(long a, long b) => (b - a) * 1000.0 / Stopwatch.Frequency;
            stamp.Add(Ms(start, stamped));
            upload.Add(Ms(stamped, uploaded));
            compose.Add(Ms(uploaded, composed));
            read.Add(Ms(composed, readBack));
            total.Add(Ms(start, end));
        }

        public void Report() {
            Line("stamp", stamp);
            Line("upload", upload);
            Line("composite", compose);
            Line("read-back", read);
            Line("total", total);
        }

        static void Line(string name, List<double> values) {
            var sorted = values.Order().ToArray();
            Say($"{name,-10} p50 {sorted[sorted.Length / 2]:F2}  p95 {sorted[sorted.Length * 95 / 100]:F2}  max {sorted[^1]:F2} ms ({sorted.Length} samples)");
        }
    }
}
