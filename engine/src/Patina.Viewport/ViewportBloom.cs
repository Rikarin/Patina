// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     Light that spills past what is brighter than white, through Vixen's dual-filter bloom: what is over the
///     threshold is kept at half the frame's size, halved down a chain of levels, then added back up the chain and over
///     the frame, before the curve. The threshold is the light the curve takes to white at the exposure set, so what
///     blooms is what would clip. It runs only when it is asked for, and its pictures are the frame's size's.
/// </summary>
sealed class ViewportBloom : IDisposable {
    /// <summary>How many halvings the chain goes down, at most.</summary>
    const int MostLevels = 5;

    /// <summary>A level smaller than this on its shorter side ends the chain.</summary>
    const int SmallestSide = 8;

    readonly VulkanDevice device;
    readonly Func<int, (Effect Effect, PipelineHandle Pipeline)> variant;
    readonly SamplerHandle sampler;
    readonly Dictionary<int, (Effect Effect, PipelineHandle Pipeline)> modes = [];
    readonly List<Pass> passes = [];
    readonly List<(TextureHandle Texture, TextureViewHandle View)> pictures = [];
    (int Width, int Height) size;

    /// <param name="device">The device.</param>
    /// <param name="variant">Builds the pass for one of the shader's modes, writing the frame's light format.</param>
    public ViewportBloom(VulkanDevice device, Func<int, (Effect Effect, PipelineHandle Pipeline)> variant) {
        this.device = device;
        this.variant = variant;
        sampler = device.CreateSampler(SamplerDescription.LinearClamp);
    }

    /// <summary>
    ///     The frame's light with the bloom over it, which the curve reads, or null before the first frame.
    /// </summary>
    public (TextureHandle Texture, TextureViewHandle View)? Result { get; private set; }

    /// <summary>
    ///     Writes the numbers for a frame: how much is added back, and the light that counts as bright at this
    ///     exposure. Builds the chain again when the frame's size has changed.
    /// </summary>
    /// <param name="target">The frame.</param>
    /// <param name="strength">How much of the spilled light is laid over the frame, 0 to 1.</param>
    /// <param name="exposure">What the curve multiplies the light by.</param>
    public void Plan(ViewportTargets target, float strength, float exposure) {
        if (size != (target.Width, target.Height) || passes.Count == 0) {
            Build(target);
        }

        var threshold = 1f / Math.Max(exposure, 1e-4f);
        foreach (var pass in passes) {
            pass.Block.Set("threshold", threshold)
                .Set("knee", threshold * 0.5f)
                .Set("filterRadius", 1f)
                .Set("intensity", pass.Last ? Math.Clamp(strength, 0f, 1f) * 0.6f : 1f)
                .Set("texelSize", pass.Texel);
            device.Write(pass.Constants, 0, pass.Block.Bytes);
        }
    }

    /// <summary>Records the chain after the forward pass, which leaves its light as a colour target.</summary>
    /// <remarks>
    ///     A level is read twice, by the halving below it and on the way back up, so each picture's state is followed
    ///     and a barrier is only recorded where it changes. What it leaves is the result as a colour target, as the
    ///     stages after the curve expect of whatever they read first.
    /// </remarks>
    public void Record(ICommandList commands, ViewportTargets target) {
        Dictionary<TextureHandle, ResourceState> states = new() { [target.Hdr] = ResourceState.ColourTarget };
        foreach (var pass in passes) {
            List<TextureBarrier> barriers = [
                new(pass.Writes.Texture, ResourceState.Undefined, ResourceState.ColourTarget)
            ];
            states[pass.Writes.Texture] = ResourceState.ColourTarget;
            // A pass's reads are distinct, so marking one read as it goes changes nothing for the next.
            var unread = pass.Reads.Where(read =>
                states.GetValueOrDefault(read, ResourceState.ColourTarget) != ResourceState.ShaderRead
            );
            foreach (var read in unread) {
                barriers.Add(new(read, ResourceState.ColourTarget, ResourceState.ShaderRead));
                states[read] = ResourceState.ShaderRead;
            }

            commands.Barrier(new BarrierGroup([], [..barriers]));
            commands.BeginRenderPass(new([new(pass.Writes.View, LoadAction.DontCare)], name: "viewport bloom"));
            ViewportShadows.Cover(commands, pass.Writes.Width, pass.Writes.Height);
            commands.BindPipeline(pass.Pipeline);
            commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, pass.Set);
            commands.Draw(3);
            commands.EndRenderPass();
        }
    }

    /// <summary>Lets go of the chain, whose pictures are the old frame's size.</summary>
    public void Forget() {
        device.WaitIdle();
        foreach (var pass in passes) {
            device.Destroy(pass.Set);
            device.Destroy(pass.Constants);
        }

        passes.Clear();
        foreach (var (texture, view) in pictures) {
            device.Destroy(view);
            device.Destroy(texture);
        }

        pictures.Clear();
        Result = null;
        size = default;
    }

    /// <summary>Lets go of the chain and the sampler. The passes' pipelines are their builder's to destroy.</summary>
    public void Dispose() {
        Forget();
        device.Destroy(sampler);
    }

    /// <summary>
    ///     The chain for a frame of this size: the bright light at half size, the levels below it, a picture for each
    ///     level on the way back up, and the frame's light with all of it laid over.
    /// </summary>
    void Build(ViewportTargets target) {
        Forget();
        size = (target.Width, target.Height);
        List<(TextureHandle Texture, TextureViewHandle View, int Width, int Height)> down = [];
        var (width, height) = (Math.Max(1, target.Width / 2), Math.Max(1, target.Height / 2));
        while (down.Count < MostLevels && (down.Count < 2 || Math.Min(width, height) >= SmallestSide)) {
            down.Add(Picture(width, height, $"viewport bloom {down.Count}"));
            (width, height) = (Math.Max(1, width / 2), Math.Max(1, height / 2));
        }

        var frame = Picture(target.Width, target.Height, "viewport bloomed");
        Result = (frame.Texture, frame.View);

        Add(0, down[0], target.HdrView, null, [target.Hdr], target.Width, target.Height, false);
        for (var level = 1; level < down.Count; level++) {
            var from = down[level - 1];
            Add(level == 1 ? 1 : 2, down[level], from.View, null, [from.Texture], from.Width, from.Height, false);
        }

        var coming = down[^1];
        for (var level = down.Count - 2; level >= 0; level--) {
            var up = Picture(down[level].Width, down[level].Height, $"viewport bloom up {level}");
            Add(
                3,
                up,
                coming.View,
                down[level].View,
                [coming.Texture, down[level].Texture],
                coming.Width,
                coming.Height,
                false
            );
            coming = up;
        }

        Add(3, frame, coming.View, target.HdrView, [coming.Texture, target.Hdr], coming.Width, coming.Height, true);
    }

    (TextureHandle Texture, TextureViewHandle View, int Width, int Height) Picture(int width, int height, string name) {
        var texture = device.CreateTexture(
            new(
                ViewportTargets.HdrFormat,
                width,
                height,
                TextureUsage.ColourTarget | TextureUsage.Sampled,
                Name: name
            )
        );
        var view = device.CreateTextureView(texture);
        pictures.Add((texture, view));
        return (texture, view, width, height);
    }

    /// <summary>One draw of the chain: what it writes, what it reads, and the size of the picture it samples.</summary>
    void Add(
        int mode,
        (TextureHandle Texture, TextureViewHandle View, int Width, int Height) writes,
        TextureViewHandle source,
        TextureViewHandle? previous,
        TextureHandle[] reads,
        int sourceWidth,
        int sourceHeight,
        bool last
    ) {
        if (!modes.TryGetValue(mode, out var built)) {
            modes[mode] = built = variant(mode);
        }

        var (effect, pipeline) = built;
        var layout = effect.BlockOf(DescriptorSetSlot.PerMaterial);
        var block = new ConstantBlock(layout);
        var constants = device.CreateBuffer(
            new(block.Bytes.Length, BufferUsage.Uniform, MemoryAccess.HostUpload, "viewport bloom")
        );
        var set = device.CreateDescriptorSet(effect.SetLayouts[(int)DescriptorSetSlot.PerMaterial], "viewport bloom");
        List<DescriptorWrite> writesTo = [
            DescriptorWrite.Uniform(layout.Binding, constants),
            DescriptorWrite.Texture(effect.BindingOf("source")!.Value.Binding, source),
            DescriptorWrite.SamplerAt(effect.BindingOf("sourceSampler")!.Value.Binding, sampler)
        ];
        if (effect.BindingOf("previous") is { } slot) {
            writesTo.Add(DescriptorWrite.Texture(slot.Binding, previous ?? source));
        }

        device.UpdateDescriptorSet(set, [..writesTo]);
        passes.Add(
            new(
                pipeline,
                set,
                constants,
                block,
                writes,
                reads,
                new(1f / sourceWidth, 1f / sourceHeight),
                last
            )
        );
    }

    sealed record Pass(
        PipelineHandle Pipeline,
        DescriptorSetHandle Set,
        BufferHandle Constants,
        ConstantBlock Block,
        (TextureHandle Texture, TextureViewHandle View, int Width, int Height) Writes,
        TextureHandle[] Reads,
        Vector2 Texel,
        bool Last
    );
}
