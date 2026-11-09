// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     Coloured shapes over the model that are not the model, such as a bake's reach, its high mesh and which parts
///     match (#149): see-through triangles and lines, each vertex with its own colour, tested against the model's
///     depth and not written to it.
/// </summary>
/// <param name="Triangles">
///     Three vertices a triangle, each <see cref="ViewportOverlay.FloatsPerVertex" />
///     floats.
/// </param>
/// <param name="Lines">Two vertices a line, laid out the same.</param>
sealed record ViewportShapes(float[] Triangles, float[] Lines) {
    /// <summary>Nothing to draw.</summary>
    public static ViewportShapes None { get; } = new([], []);

    /// <summary>Whether there is anything to draw.</summary>
    public bool IsEmpty => Triangles.Length == 0 && Lines.Length == 0;
}

/// <summary>
///     Draws <see cref="ViewportShapes" /> in the forward pass after the model and its wireframe. Its own pass,
///     pipelines and buffers, so nothing about how the model is shaded reaches it or is reached by it.
/// </summary>
sealed class ViewportOverlay : IDisposable {
    /// <summary>Position 3, then red, green, blue and opacity.</summary>
    public const int FloatsPerVertex = 7;

    /// <summary>
    ///     How far shapes are lifted towards the eye, in bounding radii: more than the wireframe, so they sit over
    ///     it.
    /// </summary>
    const float Lift = 0.003f;

    readonly VulkanDevice device;
    readonly PipelineHandle fill;
    readonly PipelineHandle lines;
    readonly ConstantBlock block;
    readonly BufferHandle constants;
    readonly DescriptorSetHandle set;
    BufferHandle? triangleBuffer;
    BufferHandle? lineBuffer;
    int triangleVertices;
    int lineVertices;

    public ViewportOverlay(
        VulkanDevice device,
        Effect effect,
        ShaderHandle vertex,
        ShaderHandle fragment,
        PixelFormat colourFormat,
        PixelFormat depthFormat,
        int samples
    ) {
        this.device = device;
        VertexElement[] elements = [
            new((uint)Input(effect, "position"), VertexFormat.Float32X3, 0),
            new((uint)Input(effect, "color"), VertexFormat.Float32X4, 3 * sizeof(float))
        ];
        var layout = new VertexBufferLayout(FloatsPerVertex * sizeof(float), elements);

        PipelineHandle Pipeline(PrimitiveTopology topology) =>
            device.CreateGraphicsPipeline(
                new(
                    vertex,
                    fragment,
                    effect.Layout,
                    [new(colourFormat, BlendState.AlphaBlend)],
                    [layout],
                    topology,
                    RasterizerState.TwoSided,
                    DepthStencilState.TestOnly,
                    depthFormat,
                    samples,
                    effect.Key.ShaderName
                )
            );

        fill = Pipeline(PrimitiveTopology.TriangleList);
        lines = Pipeline(PrimitiveTopology.LineList);

        var constantLayout = effect.BlockOf(DescriptorSetSlot.PerMaterial);
        block = new(constantLayout);
        constants = device.CreateBuffer(
            new(block.Bytes.Length, BufferUsage.Uniform, MemoryAccess.HostUpload, "viewport overlay")
        );
        set = device.CreateDescriptorSet(effect.SetLayouts[(int)DescriptorSetSlot.PerMaterial], "viewport overlay");
        device.UpdateDescriptorSet(set, [DescriptorWrite.Uniform(constantLayout.Binding, constants)]);
    }

    /// <summary>Whether there is anything to draw.</summary>
    public bool IsEmpty => triangleVertices == 0 && lineVertices == 0;

    /// <summary>Takes these shapes in place of the last, or none.</summary>
    public void Set(ViewportShapes shapes) {
        ArgumentNullException.ThrowIfNull(shapes);
        device.WaitIdle();
        Release();
        triangleVertices = shapes.Triangles.Length / FloatsPerVertex;
        lineVertices = shapes.Lines.Length / FloatsPerVertex;
        triangleBuffer = triangleVertices > 0 ? Upload(shapes.Triangles, "viewport overlay triangles") : null;
        lineBuffer = lineVertices > 0 ? Upload(shapes.Lines, "viewport overlay lines") : null;
    }

    /// <summary>The frame's camera, and how far shapes stand off the model for a model of this size.</summary>
    public void Write(Matrix4x4 viewProjection, Vector3 eye, float radius) {
        block.Set("viewProjection", viewProjection).Set("cameraPosition", eye).Set("lift", radius * Lift);
        device.Write(constants, 0, block.Bytes);
    }

    /// <summary>Draws the shapes inside the forward pass: the see-through fill first, then the lines over it.</summary>
    public void Record(ICommandList commands) {
        if (IsEmpty) {
            return;
        }

        if (triangleBuffer is { } triangles) {
            commands.BindPipeline(fill);
            commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, set);
            commands.BindVertexBuffer(0, triangles);
            commands.Draw(triangleVertices);
        }

        if (lineBuffer is { } edges) {
            commands.BindPipeline(lines);
            commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, set);
            commands.BindVertexBuffer(0, edges);
            commands.Draw(lineVertices);
        }
    }

    public void Dispose() {
        Release();
        device.Destroy(set);
        device.Destroy(constants);
        device.Destroy(lines);
        device.Destroy(fill);
    }

    void Release() {
        if (triangleBuffer is { } triangles) {
            device.Destroy(triangles);
        }

        if (lineBuffer is { } edges) {
            device.Destroy(edges);
        }

        (triangleBuffer, lineBuffer, triangleVertices, lineVertices) = (null, null, 0, 0);
    }

    BufferHandle Upload(float[] data, string name) {
        var bytes = MemoryMarshal.AsBytes(data.AsSpan());
        var buffer = device.CreateBuffer(new(bytes.Length, BufferUsage.Vertex, MemoryAccess.HostUpload, name));
        device.Write(buffer, 0, bytes);
        return buffer;
    }

    static int Input(Effect effect, string name) =>
        effect.VertexInputs.First(input => input.Name.EndsWith(name, StringComparison.Ordinal)).Location;
}
