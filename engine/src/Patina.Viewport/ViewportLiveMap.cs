// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Graphics;
using Vixen.Graphics.Vulkan;

namespace Patina.Viewport;

/// <summary>The material slots a picture can fill.</summary>
public enum ViewportSlot {
    /// <summary>The base colour.</summary>
    BaseColor,

    /// <summary>The tangent-space normal.</summary>
    Normal,

    /// <summary>Perceptual roughness.</summary>
    Roughness,

    /// <summary>How metallic it is.</summary>
    Metallic,

    /// <summary>Ambient occlusion.</summary>
    Occlusion
}

/// <summary>A rectangle of a picture.</summary>
/// <param name="X">Its left column.</param>
/// <param name="Y">Its top row.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
public readonly record struct ViewportRect(int X, int Y, int Width, int Height) {
    /// <summary>One past its right column.</summary>
    public int EndX => X + Width;

    /// <summary>One past its bottom row.</summary>
    public int EndY => Y + Height;
}

/// <summary>New pixels for a rectangle.</summary>
/// <param name="Rect">
///     Where. Its left and top must be even, or at the image's edge, since a mip is made of whole pairs of
///     texels.
/// </param>
/// <param name="Pixels">RGBA, tightly packed, top row first: the bytes an export writes.</param>
public sealed record ViewportRepair(ViewportRect Rect, byte[] Pixels);

/// <summary>
///     A picture the viewport samples and a stack keeps changing, in the bytes an export writes.
/// </summary>
/// <remarks>
///     <para>
///         <b>Eight bits, in the same formats a file of them gets, so that a stack's map is seen as its exported file
///         would be.</b> The stack's own outputs are half-float, and sampling one would filter sRGB numbers without
///         decoding them and show precision no export has. The price is one read-back per stamp, which ADR 0009's
///         table already includes.
///     </para>
///     <para>
///         ⚠ <b>The first level has no copy here</b>, which is most of a picture: a 4K map keeps about 28 MB of mips
///         and not 89. A repair brings the texels of the rectangle it rewrites, and each level above needs only those
///         and the level below it, which is why the rectangle has to be whole pairs.
///     </para>
/// </remarks>
sealed class ViewportLiveMap : IDisposable {
    const int BytesPerTexel = 4;

    static readonly float[] ToLinear = Enumerable.Range(0, 256)
        .Select(static value => Decode(value / 255f))
        .ToArray();

    /// <summary>Linear back to sRGB in steps fine enough for the dark end, since a stamp's mips run this per texel.</summary>
    static readonly byte[] FromLinear = Enumerable.Range(0, 1 << 14)
        .Select(static value => Coded(value / (float)((1 << 14) - 1)))
        .ToArray();

    readonly VulkanDevice device;
    readonly MapMeaning meaning;
    readonly byte[][] above;
    readonly (int Width, int Height)[] sizes;
    readonly TextureHandle texture;
    bool written;

    public ViewportLiveMap(VulkanDevice device, MapMeaning meaning, int width, int height) {
        this.device = device;
        this.meaning = meaning;
        Width = width;
        Height = height;
        List<(int Width, int Height)> levels = [(width, height)];
        while (levels[^1] is not (1, 1)) {
            levels.Add((Math.Max(levels[^1].Width / 2, 1), Math.Max(levels[^1].Height / 2, 1)));
        }

        sizes = [..levels];
        above = [..sizes.Skip(1).Select(static size => new byte[size.Width * size.Height * BytesPerTexel])];
        texture = device.CreateTexture(
            new(
                meaning == MapMeaning.Colour ? PixelFormat.Rgba8UNormSrgb : PixelFormat.Rgba8UNorm,
                width,
                height,
                TextureUsage.Sampled | TextureUsage.CopyDestination | TextureUsage.CopySource,
                MipLevels: sizes.Length,
                Name: "viewport live map"
            )
        );
        View = device.CreateTextureView(texture);
    }

    public int Width { get; }

    public int Height { get; }

    public TextureViewHandle View { get; }

    public void Dispose() {
        device.WaitIdle();
        device.Destroy(View);
        device.Destroy(texture);
    }

    /// <summary>Rewrites rectangles of the top level and what they change above it.</summary>
    public void Repair(IReadOnlyList<ViewportRepair> repairs) {
        if (repairs.Count == 0) {
            return;
        }

        List<(int Level, ViewportRect Rect, byte[] Pixels)> writes = [];
        foreach (var repair in repairs) {
            writes.Add((0, repair.Rect, repair.Pixels));
            var (rect, pixels) = (repair.Rect, repair.Pixels);
            for (var level = 1; level < sizes.Length; level++) {
                (rect, pixels) = Reduce(level, rect, pixels);
                writes.Add((level, rect, pixels));
            }
        }

        Upload(writes);
    }

    /// <summary>The top level, as the GPU holds it.</summary>
    public byte[] ReadBack() {
        var bytes = (long)Width * Height * BytesPerTexel;
        var readback = device.CreateBuffer(
            new(bytes, BufferUsage.CopyDestination, MemoryAccess.HostReadback, "viewport live map readback")
        );
        using (var commands = device.BeginCommandList(QueueKind.Graphics, "viewport live map readback")) {
            commands.Barrier(
                new BarrierGroup([], [new(texture, ResourceState.ShaderRead, ResourceState.CopySource)])
            );
            commands.CopyTextureToBuffer(new(texture), new(Width, Height, 1), readback, 0);
            commands.Barrier(
                new BarrierGroup([], [new(texture, ResourceState.CopySource, ResourceState.ShaderRead)])
            );
            commands.Finish();
            device.GraphicsQueue.Submit([commands]);
        }

        device.WaitIdle();
        var pixels = new byte[bytes];
        device.Read(readback, 0, pixels);
        device.Destroy(readback);
        return pixels;
    }

    static float Decode(float value) => value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);

    static byte Encode(float value) =>
        FromLinear[Math.Clamp((int)(value * (FromLinear.Length - 1) + 0.5f), 0, FromLinear.Length - 1)];

    static byte Coded(float value) =>
        Byte(value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f);

    static byte Byte(float value) => (byte)Math.Clamp((int)(value * 255f + 0.5f), 0, 255);

    /// <summary>Makes the rectangle of a level that a rectangle of the level below decides.</summary>
    (ViewportRect Rect, byte[] Pixels) Reduce(int level, ViewportRect from, byte[] source) {
        var (width, height) = sizes[level];
        var (sourceWidth, sourceHeight) = sizes[level - 1];
        var x = from.X / 2;
        var y = from.Y / 2;
        var endX = Math.Min(width, (from.EndX + 1) / 2);
        var endY = Math.Min(height, (from.EndY + 1) / 2);
        var rect = new ViewportRect(x, y, Math.Max(endX - x, 1), Math.Max(endY - y, 1));
        var target = above[level - 1];
        var below = level > 1 ? above[level - 2] : null;

        // Below the top the whole level is here; at the top only the rectangle is.
        int Source(int column, int row) =>
            below is null
                ? ((Math.Min(row, from.EndY - 1) - from.Y) * from.Width + Math.Min(column, from.EndX - 1) - from.X)
                * BytesPerTexel
                : (Math.Min(row, sourceHeight - 1) * sourceWidth + Math.Min(column, sourceWidth - 1)) * BytesPerTexel;
        var read = below ?? source;

        Parallel.For(
            y,
            rect.Y + rect.Height,
            row => {
                for (var column = x; column < rect.X + rect.Width; column++) {
                    Span<int> corners = [
                        Source(column * 2, row * 2),
                        Source(column * 2 + 1, row * 2),
                        Source(column * 2, row * 2 + 1),
                        Source(column * 2 + 1, row * 2 + 1)
                    ];
                    Mean(read, corners, target.AsSpan((row * width + column) * BytesPerTexel, BytesPerTexel));
                }
            }
        );

        var pixels = new byte[rect.Width * rect.Height * BytesPerTexel];
        for (var row = 0; row < rect.Height; row++) {
            target.AsSpan(((rect.Y + row) * width + rect.X) * BytesPerTexel, rect.Width * BytesPerTexel)
                .CopyTo(pixels.AsSpan(row * rect.Width * BytesPerTexel));
        }

        return (rect, pixels);
    }

    /// <summary>The mean of four texels, in what their numbers mean.</summary>
    void Mean(byte[] source, ReadOnlySpan<int> corners, Span<byte> into) {
        Span<float> sum = stackalloc float[BytesPerTexel];
        foreach (var at in corners) {
            for (var lane = 0; lane < BytesPerTexel; lane++) {
                var value = source[at + lane];
                sum[lane] += meaning == MapMeaning.Colour && lane < 3 ? ToLinear[value] : value / 255f;
            }
        }

        for (var lane = 0; lane < BytesPerTexel; lane++) {
            sum[lane] *= 0.25f;
        }

        if (meaning == MapMeaning.Normal) {
            var (x, y, z) = (sum[0] * 2f - 1f, sum[1] * 2f - 1f, sum[2] * 2f - 1f);
            var length = MathF.Sqrt(x * x + y * y + z * z);
            if (length > 0f) {
                sum[0] = x / length * 0.5f + 0.5f;
                sum[1] = y / length * 0.5f + 0.5f;
                sum[2] = z / length * 0.5f + 0.5f;
            }
        }

        for (var lane = 0; lane < BytesPerTexel; lane++) {
            into[lane] = meaning == MapMeaning.Colour && lane < 3 ? Encode(sum[lane]) : Byte(sum[lane]);
        }
    }

    void Upload(List<(int Level, ViewportRect Rect, byte[] Pixels)> writes) {
        var total = writes.Sum(static write => (long)write.Pixels.Length);
        var staging = device.CreateBuffer(
            new(total, BufferUsage.CopySource, MemoryAccess.HostUpload, "viewport live map staging")
        );
        var offset = 0L;
        foreach (var (_, _, pixels) in writes) {
            device.Write(staging, offset, pixels);
            offset += pixels.Length;
        }

        using (var commands = device.BeginCommandList(QueueKind.Graphics, "viewport live map")) {
            var before = written ? ResourceState.ShaderRead : ResourceState.Undefined;
            commands.Barrier(new BarrierGroup([], [new(texture, before, ResourceState.CopyDestination)]));
            offset = 0L;
            foreach (var (level, rect, pixels) in writes) {
                commands.CopyBufferToTexture(
                    staging,
                    offset,
                    new(texture, level, Origin: new(rect.X, rect.Y, 0)),
                    new(rect.Width, rect.Height, 1)
                );
                offset += pixels.Length;
            }

            commands.Barrier(
                new BarrierGroup([], [new(texture, ResourceState.CopyDestination, ResourceState.ShaderRead)])
            );
            commands.Finish();
            device.GraphicsQueue.Submit([commands]);
        }

        device.WaitIdle();
        device.Destroy(staging);
        written = true;
    }
}
