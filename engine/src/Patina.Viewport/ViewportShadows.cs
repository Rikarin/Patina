// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>What the light can see: the model's depth along it, which the forward pass compares against.</summary>
sealed class ViewportShadows : IDisposable {
    /// <summary>The side of the map, in texels.</summary>
    public const int Size = 1024;

    const PixelFormat Format = PixelFormat.R32Float;

    readonly VulkanDevice device;
    readonly PipelineHandle pipeline;
    readonly TextureHandle map;
    readonly TextureHandle depth;
    readonly TextureViewHandle depthView;
    readonly ConstantBlock block;
    readonly BufferHandle constants;
    readonly DescriptorSetHandle set;

    public ViewportShadows(
        VulkanDevice device,
        Effect effect,
        ShaderHandle vertex,
        ShaderHandle fragment,
        PixelFormat depthFormat
    ) {
        this.device = device;
        var location = (uint)effect.VertexInputs
            .First(static input => input.Name.EndsWith("position", StringComparison.Ordinal))
            .Location;
        pipeline = device.CreateGraphicsPipeline(
            new(
                vertex,
                fragment,
                effect.Layout,
                [new(Format, BlendState.Opaque)],
                [new(ViewportMesh.FloatsPerVertex * sizeof(float), [new(location, VertexFormat.Float32X3, 0)])],
                Rasterizer: RasterizerState.TwoSided,
                DepthStencil: DepthStencilState.Default,
                DepthFormat: depthFormat,
                SampleCount: 1,
                Name: effect.Key.ShaderName
            )
        );
        Sampler = device.CreateSampler(SamplerDescription.PointClamp);
        map = device.CreateTexture(
            new(Format, Size, Size, TextureUsage.ColourTarget | TextureUsage.Sampled, Name: "viewport shadow")
        );
        depth = device.CreateTexture(
            new(depthFormat, Size, Size, TextureUsage.DepthStencilTarget, Name: "viewport shadow depth")
        );
        MapView = device.CreateTextureView(map);
        depthView = device.CreateTextureView(depth);

        var layout = effect.BlockOf(DescriptorSetSlot.PerFrame);
        block = new(layout);
        constants = device.CreateBuffer(
            new(block.Bytes.Length, BufferUsage.Uniform, MemoryAccess.HostUpload, "viewport shadow")
        );
        set = device.CreateDescriptorSet(effect.SetLayouts[(int)DescriptorSetSlot.PerFrame], effect.Key.ShaderName);
        device.UpdateDescriptorSet(set, [DescriptorWrite.Uniform(layout.Binding, constants)]);
    }

    /// <summary>The map, as the forward pass reads it.</summary>
    public TextureViewHandle MapView { get; }

    public SamplerHandle Sampler { get; }

    /// <summary>The whole model, seen along the light from outside its bounds.</summary>
    public static Matrix4x4 LightViewProjection(BoundingBox bounds, Vector3 light) {
        var radius = Math.Max(bounds.Extent.Length(), 1e-4f);
        var view = Matrix4x4.LookAt(bounds.Center + light * (radius * 2f), bounds.Center, new(0f, 1f, 0f));
        return view * Matrix4x4.Orthographic(radius * 2.2f, radius * 2.2f, radius * 0.5f, radius * 3.5f);
    }

    /// <summary>Draws to the whole of a target.</summary>
    public static void Cover(ICommandList commands, int width, int height) {
        commands.SetViewport(new(0f, 0f, width, height));
        commands.SetScissor(new(0, 0, width, height));
    }

    public void Write(Matrix4x4 lightViewProjection) {
        block.Set("lightViewProjection", lightViewProjection);
        device.Write(constants, 0, block.Bytes);
    }

    public void Record(
        ICommandList commands,
        BufferHandle vertices,
        BufferHandle indices,
        IReadOnlyList<ViewportPart> parts
    ) {
        commands.Barrier(
            new BarrierGroup(
                [],
                [
                    new(map, ResourceState.Undefined, ResourceState.ColourTarget),
                    new(depth, ResourceState.Undefined, ResourceState.DepthStencilWrite)
                ]
            )
        );
        commands.BeginRenderPass(
            new(
                [new(MapView, LoadAction.Clear)],
                new DepthStencilAttachment(depthView, LoadAction.Clear, StoreAction.DontCare),
                "viewport shadow"
            )
        );
        commands.SetViewport(new(0f, 0f, Size, Size));
        commands.SetScissor(new(0, 0, Size, Size));
        commands.BindPipeline(pipeline);
        commands.BindDescriptorSet(DescriptorSetSlot.PerFrame, set);
        commands.BindVertexBuffer(0, vertices);
        commands.BindIndexBuffer(indices, IndexFormat.UInt32);
        foreach (var part in parts) {
            commands.DrawIndexed(part.IndexCount, firstIndex: part.FirstIndex);
        }

        commands.EndRenderPass();
        commands.Barrier(new BarrierGroup([], [new(map, ResourceState.ColourTarget, ResourceState.ShaderRead)]));
    }

    public void Dispose() {
        device.Destroy(set);
        device.Destroy(constants);
        device.Destroy(depthView);
        device.Destroy(MapView);
        device.Destroy(depth);
        device.Destroy(map);
        device.Destroy(Sampler);
        device.Destroy(pipeline);
    }
}
