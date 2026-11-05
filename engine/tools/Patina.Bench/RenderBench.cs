// SPDX-License-Identifier: GPL-3.0-or-later

using Patina.Viewport;
using System.Diagnostics;
using System.Globalization;
using Vixen.Core.Imaging;

namespace Patina.Bench;

/// <summary>
///     Renders a mesh once to a PNG, then times a spinning camera: the renderer with no transport in the way.
/// </summary>
static class RenderBench {
    const int Frames = 240;

    public static int Run(string mesh, string png, int width, int height) {
        var start = Stopwatch.StartNew();
        var cache = new ShaderCache(ShaderCache.DefaultDirectory);
        if (!ViewportRenderer.TryCreate(cache, out var created, out var refusal)) {
            Console.Error.WriteLine($"No Vulkan device: {refusal}");
            return 1;
        }

        using var renderer = created!;
        var ready = start.Elapsed.TotalMilliseconds;
        Say($"{renderer.Adapter}: renderer ready in {ready:F0} ms, hits {cache.Hits}, misses {cache.Misses}");
        start.Restart();
        renderer.OpenMesh(mesh);
        Say($"mesh open in {start.Elapsed.TotalMilliseconds:F0} ms");

        var pixels = new byte[ViewportRenderer.FrameBytes(width, height)];
        start.Restart();
        renderer.Render(OrbitCamera.Default, width, height, pixels);
        Say($"first frame in {start.Elapsed.TotalMilliseconds:F0} ms");
        File.WriteAllBytes(png, PngCodec.Encode(new(width, height, pixels)));

        var times = new double[Frames];
        start.Restart();
        for (var i = 0; i < Frames; i++) {
            var frame = Stopwatch.GetTimestamp();
            renderer.Render(OrbitCamera.Default with { Yaw = i * 0.02f }, width, height, pixels);
            times[i] = Stopwatch.GetElapsedTime(frame).TotalMilliseconds;
        }

        Array.Sort(times);
        var fps = Frames / start.Elapsed.TotalSeconds;
        var median = times[Frames / 2];
        var p99 = times[(int)(Frames * 0.99)];
        Say($"{width}x{height}: {fps:F1} fps, median {median:F2} ms, p99 {p99:F2} ms");
        return 0;
    }

    static void Say(FormattableString line) => Console.WriteLine(line.ToString(CultureInfo.InvariantCulture));
}
