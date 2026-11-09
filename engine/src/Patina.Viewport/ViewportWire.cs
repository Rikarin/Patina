// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     The lines over the shaded model: the triangles' edges, and the floor's grid (<see cref="ViewportGrid" />). Each
///     is tested against the model's depth and not written to it, and blended over what is there. Segments rather than
///     a fill mode, which needs a device feature, as Vixen's own wireframe.
/// </summary>
sealed class ViewportWire(
    VulkanDevice device,
    (Effect Effect, ShaderHandle Vertex, ShaderHandle Fragment) edges,
    (Effect Effect, ShaderHandle Vertex, ShaderHandle Fragment) floor,
    PixelFormat colourFormat,
    PixelFormat depthFormat,
    int samples
) : IDisposable {
    /// <summary>How far the edges are lifted towards the eye, in bounding radii.</summary>
    const float Lift = 0.002f;

    readonly ViewportLines lines =
        new(device, edges.Effect, edges.Vertex, edges.Fragment, colourFormat, depthFormat, samples);

    readonly ViewportGrid grid =
        new(device, floor.Effect, floor.Vertex, floor.Fragment, colourFormat, depthFormat, samples);

    /// <summary>The frame's camera, and how far a line stands off its surface for a model of this size.</summary>
    public void Write(Matrix4x4 viewProjection, Vector3 eye, float radius) {
        lines.Block.Set("viewProjection", viewProjection)
            .Set("cameraPosition", eye)
            .Set("lift", radius * Lift)
            .Set("wireColor", new Vector4(0f, 0f, 0f, 0.55f));
        lines.Commit();
    }

    /// <summary>Draws the edges inside the forward pass, after the model.</summary>
    public void Record(ICommandList commands, BufferHandle vertices, BufferHandle edges, int count) {
        lines.Bind(commands);
        commands.BindVertexBuffer(0, vertices);
        commands.BindIndexBuffer(edges, IndexFormat.UInt32);
        commands.DrawIndexed(count);
    }

    /// <summary>The frame's camera for the grid, and its lines for the model's bounds when they have changed.</summary>
    public void WriteGrid(Matrix4x4 viewProjection, BoundingBox bounds) => grid.Write(viewProjection, bounds);

    /// <summary>Draws the grid inside the forward pass, after the model.</summary>
    public void RecordGrid(ICommandList commands) => grid.Record(commands);

    public void Dispose() {
        grid.Dispose();
        lines.Dispose();
    }
}
