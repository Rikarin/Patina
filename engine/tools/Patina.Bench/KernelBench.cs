// SPDX-License-Identifier: GPL-3.0-or-later

using Patina.Viewport;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Shaders;
using Vixen.TextureGraph;

namespace Patina.Bench;

/// <summary>
///     Spike S2: every texture kernel from source and from the disk cache. Run it twice with the same folder: the first
///     run is cold, the second finds everything on disk. Each run is its own process, so JIT is in the numbers.
/// </summary>
static class KernelBench {
    /// <summary>A number unique to the run, or null to measure the kernels as they ship.</summary>
    public static int? Salt { get; set; }

    public static int Run(
        string folder,
        TextureFormat format,
        bool parallel,
        bool pipelines,
        string? pipelineCache,
        bool threads
    ) {
        var cache = new ShaderCache(folder);
        var names = TextureKernels.Names;
        var times = new double[names.Count];
        var compiled = new EffectData[names.Count];
        var clock = Stopwatch.StartNew();

        void One(int index) {
            var start = Stopwatch.GetTimestamp();
            compiled[index] = Compile(cache, names[index], format);
            times[index] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        if (parallel) {
            Parallel.For(0, names.Count, One);
        } else {
            for (var i = 0; i < names.Count; i++) {
                One(i);
            }
        }

        var total = clock.Elapsed.TotalMilliseconds;
        var ordered = times.Order().ToArray();
        var median = ordered[ordered.Length / 2];
        Say($"{names.Count} kernels, {(parallel ? "parallel" : "serial")}, hits {cache.Hits}, misses {cache.Misses}");
        Say($"total {total:F0} ms; first {times[0]:F0} ms; median {median:F1} ms; max {ordered[^1]:F0} ms");
        Say($"spir-v bytes {compiled.Sum(Size)}");
        return pipelines ? Pipelines(compiled, pipelineCache, threads) : 0;
    }

    static void Say(FormattableString line) => Console.WriteLine(line.ToString(CultureInfo.InvariantCulture));

    static EffectData Compile(ShaderCache cache, string kernel, TextureFormat format) {
        var variant = Salted(TextureKernels.Variant(kernel, format));
        var name = TextureKernels.VariantName(kernel, format);
        ImmutableArray<(string Name, string Text)> sources = [..TextureKernelPrelude.Sources, (name, variant)];
        return cache.Get(EffectKey.Of(kernel), sources, () => TextureKernelPrelude.Compile(name, variant));
    }

    /// <summary>
    ///     Makes each run's modules ones Metal has never seen: its on-disk cache otherwise hides the first compile, and
    ///     does so across processes. A variable the kernel does not read, placed at the top of its entry point.
    /// </summary>
    static string Salted(string source) {
        if (Salt is not { } salt) {
            return source;
        }

        var entry = source.IndexOf("[ComputeShader", StringComparison.Ordinal);
        var body = source.IndexOf("{\n", entry, StringComparison.Ordinal) + 2;
        return source.Insert(body, $"var patinaSalt = {salt}f\npatinaSalt = patinaSalt + 1f\n");
    }

    static long Size(EffectData effect) => effect.Stages.Sum(static stage => (long)stage.Bytecode.Length);

    /// <summary>
    ///     What a Worker pays after the SPIR-V, on the real device: every kernel into a compute pipeline.
    /// </summary>
    static int Pipelines(EffectData[] compiled, string? pipelineCache, bool threads) {
        var open = Stopwatch.StartNew();
        if (!VulkanDevice.TryCreate(new() { PipelineCachePath = pipelineCache }, out var device, out var refusal)) {
            Console.Error.WriteLine($"No Vulkan device: {refusal}");
            return 1;
        }

        using (device) {
            Say($"device {device.Adapter.Name} opened in {open.Elapsed.TotalMilliseconds:F0} ms");
            var loader = new EffectLoader(device);
            var clock = Stopwatch.StartNew();
            var made = new (ShaderHandle Module, PipelineHandle Pipeline)[compiled.Length];
            var prepared = compiled.Select(data => {
                        var effect = loader.Load(data);
                        var stage = effect.Stages.First(static s => s.Stage == ShaderStage.Compute);
                        return (effect.Layout, Module: device.CreateShader(
                            ShaderStage.Compute,
                            stage.Bytecode.AsSpan(),
                            data.ShaderName
                        ));
                    }
                )
                .ToArray();

            void Build(int index) {
                var (layout, module) = prepared[index];
                made[index] = (module, device.CreateComputePipeline(new(module, layout, compiled[index].ShaderName)));
            }

            if (threads) {
                Parallel.For(0, prepared.Length, Build);
            } else {
                for (var i = 0; i < prepared.Length; i++) {
                    Build(i);
                }
            }

            Say($"{made.Length} pipelines in {clock.Elapsed.TotalMilliseconds:F0} ms");
            foreach (var (module, pipeline) in made) {
                device.Destroy(pipeline);
                device.Destroy(module);
            }

            loader.Release();
        }

        return 0;
    }
}
