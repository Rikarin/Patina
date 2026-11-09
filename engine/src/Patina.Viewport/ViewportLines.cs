// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     A pass of lines over the shaded model in the forward pass: tested against its depth and not written to it, and
///     blended over what is there, with one block of numbers of its own. The wireframe and the grid are each one, and
///     the overlay (<see cref="ViewportOverlay" />) is two, its see-through triangles being the same pass over a
///     triangle list. Segments rather than a fill mode, which needs a device feature, as Vixen's own wireframe.
/// </summary>
sealed class ViewportLines : IDisposable {
    readonly VulkanDevice device;
    readonly PipelineHandle pipeline;
    readonly BufferHandle constants;
    readonly DescriptorSetHandle set;

    public ViewportLines(
        VulkanDevice device,
        (Effect Effect, ShaderHandle Vertex, ShaderHandle Fragment) shader,
        PixelFormat colourFormat,
        PixelFormat depthFormat,
        int samples,
        VertexBufferLayout? layout = null,
        PrimitiveTopology topology = PrimitiveTopology.LineList
    ) {
        this.device = device;
        var (effect, vertex, fragment) = shader;
        pipeline = device.CreateGraphicsPipeline(
            new(
                vertex,
                fragment,
                effect.Layout,
                [new(colourFormat, BlendState.AlphaBlend)],
                [layout ?? ViewportMesh.Layout(effect)],
                topology,
                RasterizerState.TwoSided,
                DepthStencilState.TestOnly,
                depthFormat,
                samples,
                effect.Key.ShaderName
            )
        );

        var numbers = effect.BlockOf(DescriptorSetSlot.PerMaterial);
        Block = new(numbers);
        constants = device.CreateBuffer(
            new(Block.Bytes.Length, BufferUsage.Uniform, MemoryAccess.HostUpload, effect.Key.ShaderName)
        );
        set = device.CreateDescriptorSet(effect.SetLayouts[(int)DescriptorSetSlot.PerMaterial], effect.Key.ShaderName);
        device.UpdateDescriptorSet(set, [DescriptorWrite.Uniform(numbers.Binding, constants)]);
    }

    /// <summary>The pass's numbers, which <see cref="Commit" /> sends.</summary>
    public ConstantBlock Block { get; }

    /// <summary>Sends the numbers set on <see cref="Block" /> for the next frame.</summary>
    public void Commit() => device.Write(constants, 0, Block.Bytes);

    /// <summary>Binds the pass and its numbers inside the forward pass, after the model.</summary>
    public void Bind(ICommandList commands) {
        commands.BindPipeline(pipeline);
        commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, set);
    }

    public void Dispose() {
        device.Destroy(set);
        device.Destroy(constants);
        device.Destroy(pipeline);
    }
}
