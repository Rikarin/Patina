// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;

namespace Patina.Viewport;

/// <summary>Everything whose size is the frame's.</summary>
sealed class ViewportTargets : IDisposable {
    readonly VulkanDevice device;

    /// <summary>The forward pass's light, before the curve.</summary>
    public const PixelFormat HdrFormat = PixelFormat.Rgba16Float;

    public const PixelFormat DepthFormat = PixelFormat.Depth32Float;

    /// <summary>What a frame is read back as: RGBA, eight bits a lane, sRGB-encoded by the curve.</summary>
    public const PixelFormat OutputFormat = PixelFormat.Rgba8UNorm;

    /// <summary>The forward pass's samples a pixel.</summary>
    public const int Samples = 4;

    /// <summary>A draw added to what is there: the sky over the background colour the frame is cleared to.</summary>
    public static BlendState Added => new(true, BlendFactor.One, BlendFactor.One, BlendOperation.Add, BlendFactor.One);

    public ViewportTargets(VulkanDevice device, int width, int height) {
        this.device = device;
        Width = width;
        Height = height;
        HdrSamples = Texture(HdrFormat, TextureUsage.ColourTarget, Samples, "viewport hdr samples");
        Hdr = Texture(HdrFormat, TextureUsage.ColourTarget | TextureUsage.Sampled, 1, "viewport hdr");
        Depth = Texture(DepthFormat, TextureUsage.DepthStencilTarget, Samples, "viewport depth");
        var finished = TextureUsage.ColourTarget | TextureUsage.Sampled | TextureUsage.CopySource;
        Output = Texture(OutputFormat, finished, 1, "viewport output");
        Spare = Texture(OutputFormat, finished, 1, "viewport spare");
        HdrSamplesView = device.CreateTextureView(HdrSamples);
        HdrView = device.CreateTextureView(Hdr);
        DepthView = device.CreateTextureView(Depth);
        OutputView = device.CreateTextureView(Output);
        SpareView = device.CreateTextureView(Spare);
        Readback = device.CreateBuffer(
            new(
                ViewportRenderer.FrameBytes(width, height),
                BufferUsage.CopyDestination,
                MemoryAccess.HostReadback,
                "viewport readback"
            )
        );
    }

    public int Width { get; }

    public int Height { get; }

    public TextureHandle HdrSamples { get; }

    public TextureHandle Hdr { get; }

    public TextureHandle Depth { get; }

    /// <summary>Where the curve writes, and every other stage after it.</summary>
    public TextureHandle Output { get; }

    /// <summary>Where the stages between write.</summary>
    public TextureHandle Spare { get; }

    public TextureViewHandle HdrSamplesView { get; }

    public TextureViewHandle HdrView { get; }

    public TextureViewHandle DepthView { get; }

    public TextureViewHandle OutputView { get; }

    public TextureViewHandle SpareView { get; }

    public BufferHandle Readback { get; }

    /// <summary>
    ///     The forward pass's light, cleared to a colour in linear light and resolved into <see cref="Hdr" />.
    /// </summary>
    public ColourAttachment Cleared(Vector3 colour) =>
        new(
            HdrSamplesView,
            LoadAction.Clear,
            StoreAction.Resolve,
            new Color4(colour.X, colour.Y, colour.Z, 1f),
            HdrView
        );

    public void Dispose() {
        device.WaitIdle();
        device.Destroy(Readback);
        foreach (var view in new[] { HdrSamplesView, HdrView, DepthView, OutputView, SpareView }) {
            device.Destroy(view);
        }

        foreach (var texture in new[] { HdrSamples, Hdr, Depth, Output, Spare }) {
            device.Destroy(texture);
        }
    }

    TextureHandle Texture(PixelFormat format, TextureUsage usage, int samples, string name) =>
        device.CreateTexture(new(format, Width, Height, usage, SampleCount: samples, Name: name));
}
