// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     What follows the forward pass: Vixen's curve to the screen, then its lens pass (vignette, lateral aberration
///     and grain) and its sharpening, each a full-screen draw from one picture into the next. A variant is compiled the
///     first time it is asked for, and the frame only pays for the effects that are on.
/// </summary>
sealed class ViewportPost : IDisposable {
    /// <summary>What the viewport opens with, and what the curve is scaled by: a touch under one.</summary>
    const float BaseExposure = 0.9f;

    /// <summary>The white point Reinhard and Hable are normalised to.</summary>
    const float WhitePoint = 4f;

    readonly VulkanDevice device;
    readonly ViewportShaders shaders;
    readonly Func<EffectData, (Effect Effect, PipelineHandle Pipeline)> build;
    readonly SamplerHandle linear;
    readonly SamplerHandle point;
    readonly BufferHandle meteredExposure;
    readonly Dictionary<string, Stage> stages = new(StringComparer.Ordinal);

    /// <summary>The stages the next frame runs, which <see cref="Plan" /> chose.</summary>
    List<Stage> planned = [];

    public ViewportPost(
        VulkanDevice device,
        ViewportShaders shaders,
        Func<EffectData, (Effect Effect, PipelineHandle Pipeline)> build
    ) {
        this.device = device;
        this.shaders = shaders;
        this.build = build;
        linear = device.CreateSampler(SamplerDescription.LinearClamp);
        point = device.CreateSampler(SamplerDescription.PointClamp);

        // Raven keeps the auto-exposure binding in these variants although the permutation that reads it is off.
        meteredExposure = device.CreateBuffer(
            new(sizeof(float), BufferUsage.Storage, MemoryAccess.HostUpload, "viewport metered exposure")
        );
        device.Write(meteredExposure, 0, BitConverter.GetBytes(1f));
    }

    /// <summary>Chooses the stages the next frame runs after the forward pass and writes their numbers.</summary>
    public void Plan(ViewportShading shading, int width, int height) {
        var data = shading.ShowsData;
        var tonemap = Get(
            $"tonemap {(data ? ViewportToneMapping.None : shading.ToneMapping)}",
            () => shaders.Tonemap(data ? ViewportToneMapping.None : shading.ToneMapping),
            "source",
            "sourceSampler",
            linear
        );
        var neutral = new Vector3(1f, 1f, 1f);
        tonemap.Block.Set("exposure", data ? 1f : BaseExposure * MathF.Pow(2f, shading.Exposure))
            .Set("whitePoint", WhitePoint)
            .Set("contrast", data ? 1f : shading.Contrast)
            .Set("saturation", data ? 1f : shading.Saturation)
            .Set("whiteBalance", neutral)
            .Set("colorFilter", neutral)
            .Set("shadowTint", neutral)
            .Set("highlightTint", neutral)
            // A block starts at zero and not at what the shader declares, and Hable's curve at zero is black: these
            // are the constants published with Uncharted 2, which Vixen declares.
            .Set("filmicShoulderStrength", 0.15f)
            .Set("filmicLinearStrength", 0.5f)
            .Set("filmicLinearAngle", 0.1f)
            .Set("filmicToeStrength", 0.2f)
            .Set("filmicToeNumerator", 0.02f)
            .Set("filmicToeDenominator", 0.3f);
        List<Stage> plan = [tonemap];
        if (data) {
            Written(plan);
            return;
        }

        var (vignette, aberration, grain) = (shading.Vignette > 0f, shading.Aberration > 0f, shading.Grain > 0f);
        if (vignette || aberration || grain) {
            var lens = Get(
                $"lens {vignette} {aberration} {grain}",
                () => shaders.Lens(vignette, aberration, grain),
                "source",
                "linearSampler",
                linear
            );
            lens.Block.Set("vignetteIntensity", Math.Clamp(shading.Vignette, 0f, 1f))
                .Set("vignetteSmoothness", 0.5f)
                .Set("vignetteRoundness", 0.5f)
                .Set("vignetteCenter", new Vector2(0.5f, 0.5f))
                .Set("vignetteColor", Vector3.Zero)
                .Set("aspectRatio", (float)width / height)
                .Set("aberrationStrength", Math.Clamp(shading.Aberration, 0f, 1f) * 0.06f)
                .Set("grainIntensity", Math.Clamp(shading.Grain, 0f, 1f) * 0.08f)
                .Set("grainScale", 1f)
                .Set("frameIndex", 0f)
                .Set("screenSize", new Vector2(width, height));
            plan.Add(lens);
        }

        if (shading.Sharpen > 0f) {
            var sharpen = Get("sharpen", shaders.Sharpen, "source", "pointSampler", point);
            sharpen.Block.Set("texelSize", new Vector2(1f / width, 1f / height))
                .Set("sharpness", Math.Clamp(shading.Sharpen, 0f, 1f));
            plan.Add(sharpen);
        }

        Written(plan);
    }

    void Written(List<Stage> plan) {
        foreach (var stage in plan) {
            device.Write(stage.Constants, 0, stage.Block.Bytes);
        }

        planned = plan;
    }

    /// <summary>
    ///     Draws the stages after the forward pass. Each reads the last picture and writes the other of the two the
    ///     frame has, however many run.
    /// </summary>
    /// <returns>The picture the last stage wrote, as a colour target.</returns>
    public TextureHandle Record(ICommandList commands, ViewportTargets target) {
        var (reading, readingView) = (target.Hdr, target.HdrView);
        for (var at = 0; at < planned.Count; at++) {
            var stage = planned[at];
            var (writing, writingView) =
                at % 2 == 0 ? (target.Output, target.OutputView) : (target.Spare, target.SpareView);
            commands.Barrier(
                new BarrierGroup(
                    [],
                    [
                        new(reading, ResourceState.ColourTarget, ResourceState.ShaderRead),
                        new(writing, ResourceState.Undefined, ResourceState.ColourTarget)
                    ]
                )
            );
            commands.BeginRenderPass(new([new(writingView, LoadAction.DontCare)], name: "viewport post"));
            ViewportShadows.Cover(commands, target.Width, target.Height);
            commands.BindPipeline(stage.Pipeline);
            commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, stage.Reading(readingView));
            commands.Draw(3);
            commands.EndRenderPass();
            (reading, readingView) = (writing, writingView);
        }

        return reading;
    }

    /// <summary>Lets go of every set that reads a picture of the old size.</summary>
    public void Forget() {
        foreach (var stage in stages.Values) {
            stage.Forget();
        }
    }

    public void Dispose() {
        foreach (var stage in stages.Values) {
            stage.Dispose();
        }

        device.Destroy(meteredExposure);
        device.Destroy(point);
        device.Destroy(linear);
    }

    Stage Get(string key, Func<EffectData> data, string source, string sampler, SamplerHandle filter) {
        if (!stages.TryGetValue(key, out var stage)) {
            var (effect, pipeline) = build(data());
            stages[key] = stage = new(device, effect, pipeline, source, sampler, filter, meteredExposure);
        }

        return stage;
    }

    /// <summary>One full-screen draw: its pipeline, its numbers, and a set for each picture it has read.</summary>
    internal sealed class Stage : IDisposable {
        readonly VulkanDevice device;
        readonly Effect effect;
        readonly string source;
        readonly string sampler;
        readonly SamplerHandle filter;
        readonly BufferHandle exposure;
        readonly Dictionary<TextureViewHandle, DescriptorSetHandle> sets = [];

        public Stage(
            VulkanDevice device,
            Effect effect,
            PipelineHandle pipeline,
            string source,
            string sampler,
            SamplerHandle filter,
            BufferHandle exposure
        ) {
            this.device = device;
            this.effect = effect;
            this.source = source;
            this.sampler = sampler;
            this.filter = filter;
            this.exposure = exposure;
            Pipeline = pipeline;
            Block = new(effect.BlockOf(DescriptorSetSlot.PerMaterial));
            Constants = device.CreateBuffer(
                new(Block.Bytes.Length, BufferUsage.Uniform, MemoryAccess.HostUpload, effect.Key.ShaderName)
            );
        }

        public PipelineHandle Pipeline { get; }

        public ConstantBlock Block { get; }

        public BufferHandle Constants { get; }

        /// <summary>The set that reads this picture.</summary>
        public DescriptorSetHandle Reading(TextureViewHandle picture) {
            if (sets.TryGetValue(picture, out var set)) {
                return set;
            }

            set = device.CreateDescriptorSet(
                effect.SetLayouts[(int)DescriptorSetSlot.PerMaterial],
                effect.Key.ShaderName
            );
            List<DescriptorWrite> writes = [
                DescriptorWrite.Uniform(effect.BlockOf(DescriptorSetSlot.PerMaterial).Binding, Constants),
                DescriptorWrite.Texture(effect.BindingOf(source)!.Value.Binding, picture),
                DescriptorWrite.SamplerAt(effect.BindingOf(sampler)!.Value.Binding, filter)
            ];
            if (effect.BindingOf("exposureBuffer") is { } metered) {
                writes.Add(DescriptorWrite.Storage(metered.Binding, exposure));
            }

            device.UpdateDescriptorSet(set, [..writes]);
            sets[picture] = set;
            return set;
        }

        public void Forget() {
            foreach (var set in sets.Values) {
                device.Destroy(set);
            }

            sets.Clear();
        }

        public void Dispose() {
            Forget();
            device.Destroy(Constants);
        }
    }
}
