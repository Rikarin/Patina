// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     Lays the tints the forward pass wrote (<see cref="ViewportTargets.Tint" />) over the finished picture, after the
///     curve and every effect (#286). A quick mask's red and a mark's wash laid on the light before the curve faded as
///     the light grew, so on a brightly lit model the mask went pink and the wash went pale; laid on here they are the
///     colours a person sees, however the model is lit or exposed. One full-screen draw, blended as premultiplied alpha
///     over what is there, which leaves an untinted pixel as it was.
/// </summary>
sealed class ViewportTint : IDisposable {
    readonly VulkanDevice device;
    readonly Effect effect;
    readonly PipelineHandle pipeline;
    readonly SamplerHandle point;
    readonly Dictionary<TextureViewHandle, DescriptorSetHandle> sets = [];

    public ViewportTint(VulkanDevice device, (Effect Effect, PipelineHandle Pipeline) pass) {
        this.device = device;
        (effect, pipeline) = pass;
        point = device.CreateSampler(SamplerDescription.PointClamp);
    }

    /// <summary>The blend the pipeline is built with: the tint over the picture, premultiplied.</summary>
    public static ColourTargetState[] Targets => [new(ViewportTargets.OutputFormat, BlendState.PremultipliedAlpha)];

    /// <summary>
    ///     Draws the tints over <paramref name="picture" />, which the post's last stage wrote and is still a colour
    ///     target.
    /// </summary>
    public void Record(ICommandList commands, ViewportTargets target, TextureViewHandle picture) {
        commands.Barrier(
            new BarrierGroup([], [new(target.Tint, ResourceState.ColourTarget, ResourceState.ShaderRead)])
        );
        commands.BeginRenderPass(new([new(picture, LoadAction.Load)], name: "viewport tint"));
        ViewportShadows.Cover(commands, target.Width, target.Height);
        commands.BindPipeline(pipeline);
        commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, Reading(target.TintView));
        commands.Draw(3);
        commands.EndRenderPass();
    }

    /// <summary>Lets go of the sets that read the old frame's tints.</summary>
    public void Forget() {
        foreach (var set in sets.Values) {
            device.Destroy(set);
        }

        sets.Clear();
    }

    public void Dispose() {
        Forget();
        device.Destroy(point);
    }

    DescriptorSetHandle Reading(TextureViewHandle tint) {
        if (sets.TryGetValue(tint, out var set)) {
            return set;
        }

        set = device.CreateDescriptorSet(effect.SetLayouts[(int)DescriptorSetSlot.PerMaterial], "viewport tint");
        device.UpdateDescriptorSet(
            set,
            [
                DescriptorWrite.Texture(effect.BindingOf("tint")!.Value.Binding, tint),
                DescriptorWrite.SamplerAt(effect.BindingOf("tintSampler")!.Value.Binding, point)
            ]
        );
        sets[tint] = set;
        return set;
    }
}
