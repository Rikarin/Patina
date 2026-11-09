// SPDX-License-Identifier: GPL-3.0-or-later

using Patina.Export;
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

    /// <param name="mesh">The mesh.</param>
    /// <param name="png">Where the first frame goes.</param>
    /// <param name="width">The frame's width.</param>
    /// <param name="height">The frame's height.</param>
    /// <param name="maps">
    ///     A folder a bake was delivered to, to lay over the mesh, and a folder <c>patina bake</c> wrote a graph's
    ///     outputs to, as <c>baseColor.png</c>, <c>roughness.png</c>, <c>metalness.png</c> and <c>normal.png</c>, laid
    ///     over it in turn. Either may be null.
    /// </param>
    /// <param name="camera">The first frame's camera.</param>
    /// <param name="shading">How the frames are lit, shown and finished.</param>
    /// <param name="environment">A room or a panorama, or null for the studio.</param>
    public static int Run(
        string mesh,
        string png,
        int width,
        int height,
        (string? Bake, string? Material) maps,
        OrbitCamera camera,
        ViewportShading shading,
        string? environment
    ) {
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
        renderer.Shading = shading;
        if (environment is not null) {
            start.Restart();
            renderer.SetEnvironment(environment);
            Say($"environment baked in {start.Elapsed.TotalMilliseconds:F0} ms");
        }

        if (maps.Bake is not null || maps.Material is not null) {
            start.Restart();
            renderer.SetMaps(Maps(maps.Bake, maps.Material));
            Say($"maps on in {start.Elapsed.TotalMilliseconds:F0} ms");
        }

        var pixels = new byte[ViewportRenderer.FrameBytes(width, height)];
        start.Restart();
        renderer.Render(camera, width, height, pixels);
        Say($"first frame in {start.Elapsed.TotalMilliseconds:F0} ms");
        File.WriteAllBytes(png, PngCodec.Encode(new(width, height, pixels)));

        var times = new double[Frames];
        start.Restart();
        for (var i = 0; i < Frames; i++) {
            var frame = Stopwatch.GetTimestamp();
            renderer.Render(camera with { Yaw = i * 0.02f }, width, height, pixels);
            times[i] = Stopwatch.GetElapsedTime(frame).TotalMilliseconds;
        }

        Array.Sort(times);
        var fps = Frames / start.Elapsed.TotalSeconds;
        var median = times[Frames / 2];
        var p99 = times[(int)(Frames * 0.99)];
        Say($"{width}x{height}: {fps:F1} fps, median {median:F2} ms, p99 {p99:F2} ms");
        return 0;
    }

    static ViewportMaps Maps(string? folder, string? material) {
        return new(
            BaseColor: Painted("baseColor"),
            Normal: Painted("normal") ?? Of("normal"),
            Roughness: Painted("roughness"),
            Metallic: Painted("metalness"),
            Occlusion: Of("ao")
        );

        ViewportMapFile? Of(string map) =>
            folder is not null && BakedMapPicture.Find(folder, map) is { } found ? new(found.Path, found.Path) : null;

        ViewportMapFile? Painted(string usage) =>
            material is not null && Path.Combine(material, usage + ".png") is var path && File.Exists(path)
                ? new(path, path)
                : null;
    }

    static void Say(FormattableString line) => Console.WriteLine(line.ToString(CultureInfo.InvariantCulture));
}
