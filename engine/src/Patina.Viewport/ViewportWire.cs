// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     The triangles' edges over the shaded model: lines tested against its depth and not written to it, and blended
///     over what is there. Segments rather than a fill mode, which needs a device feature, as Vixen's own wireframe.
/// </summary>
sealed class ViewportWire : IDisposable {
    /// <summary>How far the edges are lifted towards the eye, in bounding radii.</summary>
    const float Lift = 0.002f;

    readonly VulkanDevice device;
    readonly PipelineHandle pipeline;
    readonly ConstantBlock block;
    readonly BufferHandle constants;
    readonly DescriptorSetHandle set;
    readonly PixelFormat colourFormat;
    readonly PixelFormat depthFormat;
    readonly int samples;
    ViewportOverlay? overlay;

    public ViewportWire(
        VulkanDevice device,
        Effect effect,
        ShaderHandle vertex,
        ShaderHandle fragment,
        PixelFormat colourFormat,
        PixelFormat depthFormat,
        int samples
    ) {
        this.device = device;
        (this.colourFormat, this.depthFormat, this.samples) = (colourFormat, depthFormat, samples);
        pipeline = device.CreateGraphicsPipeline(
            new(
                vertex,
                fragment,
                effect.Layout,
                [new(colourFormat, BlendState.AlphaBlend)],
                [ViewportMesh.Layout(effect)],
                PrimitiveTopology.LineList,
                RasterizerState.TwoSided,
                DepthStencilState.TestOnly,
                depthFormat,
                samples,
                effect.Key.ShaderName
            )
        );

        var layout = effect.BlockOf(DescriptorSetSlot.PerMaterial);
        block = new(layout);
        constants = device.CreateBuffer(
            new(block.Bytes.Length, BufferUsage.Uniform, MemoryAccess.HostUpload, "viewport wire")
        );
        set = device.CreateDescriptorSet(effect.SetLayouts[(int)DescriptorSetSlot.PerMaterial], "viewport wire");
        device.UpdateDescriptorSet(set, [DescriptorWrite.Uniform(layout.Binding, constants)]);
    }

    /// <summary>The frame's camera, and how far a line stands off its surface for a model of this size.</summary>
    public void Write(Matrix4x4 viewProjection, Vector3 eye, float radius) {
        block.Set("viewProjection", viewProjection)
            .Set("cameraPosition", eye)
            .Set("lift", radius * Lift)
            .Set("wireColor", new Vector4(0f, 0f, 0f, 0.55f));
        device.Write(constants, 0, block.Bytes);
        overlay?.Write(viewProjection, eye, radius);
    }

    /// <summary>
    ///     Draws coloured shapes over the model as well, from <see cref="SetOverlay" />, in a pass of their own: the
    ///     wireframe's lift and depth rule, each vertex with its own colour.
    /// </summary>
    public void UseOverlay(Effect effect, ShaderHandle vertex, ShaderHandle fragment) {
        overlay?.Dispose();
        overlay = new(device, effect, vertex, fragment, colourFormat, depthFormat, samples);
    }

    /// <summary>Takes these shapes in place of the last, or none for empty arrays.</summary>
    public void SetOverlay(float[] triangles, float[] lines) =>
        (overlay ?? throw new InvalidOperationException("No overlay is in use.")).Set(new(triangles, lines));

    /// <summary>Draws the overlay's shapes inside the forward pass, after the model and its edges.</summary>
    public void RecordOverlay(ICommandList commands) => overlay?.Record(commands);

    /// <summary>Draws the edges inside the forward pass, after the model.</summary>
    public void Record(ICommandList commands, BufferHandle vertices, BufferHandle edges, int count) {
        commands.BindPipeline(pipeline);
        commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, set);
        commands.BindVertexBuffer(0, vertices);
        commands.BindIndexBuffer(edges, IndexFormat.UInt32);
        commands.DrawIndexed(count);
    }

    public void Dispose() {
        overlay?.Dispose();
        device.Destroy(set);
        device.Destroy(constants);
        device.Destroy(pipeline);
    }
}
