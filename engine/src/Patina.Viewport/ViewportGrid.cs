// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     A grid on the floor the model stands on, and the world's two level axes across it: x in red and z in blue, as
///     the axes are coloured where y is up. The lines are spaced by a power of ten of the model's units, about a third
///     of its radius, and reach three of its radii round it. They are drawn in the forward pass after the model, tested
///     against its depth and not written to it, so the model hides what is behind it and casts nothing on them.
/// </summary>
sealed class ViewportGrid : IDisposable {
    /// <summary>How far round the model's centre the grid reaches, in bounding radii.</summary>
    const float Reach = 3f;

    static readonly Vector4 Line = new(0.32f, 0.32f, 0.32f, 0.45f);
    static readonly Vector4 Major = new(0.42f, 0.42f, 0.42f, 0.6f);
    static readonly Vector4 AxisX = new(0.85f, 0.16f, 0.14f, 0.9f);
    static readonly Vector4 AxisZ = new(0.16f, 0.32f, 0.9f, 0.9f);

    readonly VulkanDevice device;
    readonly ViewportLines pass;
    BufferHandle? lines;
    int count;
    BoundingBox drawn;

    public ViewportGrid(
        VulkanDevice device,
        Effect effect,
        ShaderHandle vertex,
        ShaderHandle fragment,
        PixelFormat colourFormat,
        PixelFormat depthFormat,
        int samples
    ) {
        this.device = device;
        pass = new(device, (effect, vertex, fragment), colourFormat, depthFormat, samples);
    }

    /// <summary>
    ///     The spacing of the lines for a model of this size: the power of ten nearest a third of its radius.
    /// </summary>
    public static float Spacing(BoundingBox bounds) {
        var radius = Math.Max(bounds.Extent.Length(), 1e-4f);
        return MathF.Pow(10f, MathF.Round(MathF.Log10(radius / 3f)));
    }

    /// <summary>
    ///     The lines for a model with these bounds, as mesh vertices: position, then the colour in the normal's place
    ///     and the opacity in the first texture coordinate's. Every tenth line is brighter, and the axes are lines of
    ///     their own wherever the grid reaches them.
    /// </summary>
    public static float[] Lines(BoundingBox bounds) {
        var spacing = Spacing(bounds);
        var radius = Math.Max(bounds.Extent.Length(), 1e-4f);
        var floor = bounds.Minimum.Y;
        var (low, high) = (bounds.Center - new Vector3(radius * Reach), bounds.Center + new Vector3(radius * Reach));
        var (firstX, lastX) = ((int)MathF.Ceiling(low.X / spacing), (int)MathF.Floor(high.X / spacing));
        var (firstZ, lastZ) = ((int)MathF.Ceiling(low.Z / spacing), (int)MathF.Floor(high.Z / spacing));
        List<float> vertices = [];
        for (var x = firstX; x <= lastX; x++) {
            var colour = x == 0 ? AxisZ : x % 10 == 0 ? Major : Line;
            Add(vertices, new(x * spacing, floor, low.Z), new(x * spacing, floor, high.Z), colour);
        }

        for (var z = firstZ; z <= lastZ; z++) {
            var colour = z == 0 ? AxisX : z % 10 == 0 ? Major : Line;
            Add(vertices, new(low.X, floor, z * spacing), new(high.X, floor, z * spacing), colour);
        }

        return [..vertices];
    }

    static void Add(List<float> vertices, Vector3 from, Vector3 to, Vector4 colour) {
        foreach (var at in new[] { from, to }) {
            vertices.AddRange([at.X, at.Y, at.Z, colour.X, colour.Y, colour.Z, colour.W, 0f, 0f, 0f, 0f, 0f]);
        }
    }

    /// <summary>The frame's camera, and the lines for the model's bounds when they have changed.</summary>
    public void Write(Matrix4x4 viewProjection, BoundingBox bounds) {
        pass.Block.Set("viewProjection", viewProjection);
        pass.Commit();
        if (lines is not null && bounds == drawn) {
            return;
        }

        var vertices = Lines(bounds);
        if (lines is { } old) {
            device.WaitIdle();
            device.Destroy(old);
        }

        var bytes = MemoryMarshal.AsBytes(vertices.AsSpan());
        lines = device.CreateBuffer(new(bytes.Length, BufferUsage.Vertex, MemoryAccess.HostUpload, "viewport grid"));
        device.Write(lines.Value, 0, bytes);
        count = vertices.Length / ViewportMesh.FloatsPerVertex;
        drawn = bounds;
    }

    /// <summary>Draws the lines inside the forward pass, after the model.</summary>
    public void Record(ICommandList commands) {
        if (lines is not { } buffer || count == 0) {
            return;
        }

        pass.Bind(commands);
        commands.BindVertexBuffer(0, buffer);
        commands.Draw(count);
    }

    public void Dispose() {
        if (lines is { } buffer) {
            device.Destroy(buffer);
        }

        pass.Dispose();
    }
}
