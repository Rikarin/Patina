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

    /// <summary>
    ///     The forward pass's second target: the tints laid over the finished picture (#286), the quick mask's red and
    ///     the marks' wash, in the colours a person sees, premultiplied by how much they cover, which is in alpha.
    /// </summary>
    public const PixelFormat TintFormat = PixelFormat.Rgba8UNorm;

    /// <summary>The forward pass's samples a pixel.</summary>
    public const int Samples = 4;

    /// <summary>A target a pipeline leaves as it is.</summary>
    static BlendState Untouched => new(WriteMask: ColourWriteMask.None);

    /// <summary>The forward pass's targets for a draw that writes light, blended so, and leaves the tints.</summary>
    public static ColourTargetState[] Light(BlendState blend) => [new(HdrFormat, blend), new(TintFormat, Untouched)];

    /// <summary>The forward pass's targets for a draw that writes tints, blended so, and leaves the light.</summary>
    public static ColourTargetState[] Tinting(BlendState blend) =>
        [new(HdrFormat, Untouched), new(TintFormat, blend)];

    /// <summary>A draw added to what is there: the sky over the background colour the frame is cleared to.</summary>
    public static BlendState Added => new(true, BlendFactor.One, BlendFactor.One, BlendOperation.Add, BlendFactor.One);

    public ViewportTargets(VulkanDevice device, int width, int height) {
        this.device = device;
        Width = width;
        Height = height;
        HdrSamples = Texture(HdrFormat, TextureUsage.ColourTarget, Samples, "viewport hdr samples");
        Hdr = Texture(HdrFormat, TextureUsage.ColourTarget | TextureUsage.Sampled, 1, "viewport hdr");
        Depth = Texture(DepthFormat, TextureUsage.DepthStencilTarget, Samples, "viewport depth");
        TintSamples = Texture(TintFormat, TextureUsage.ColourTarget, Samples, "viewport tint samples");
        Tint = Texture(TintFormat, TextureUsage.ColourTarget | TextureUsage.Sampled, 1, "viewport tint");
        var finished = TextureUsage.ColourTarget | TextureUsage.Sampled | TextureUsage.CopySource;
        Output = Texture(OutputFormat, finished, 1, "viewport output");
        Spare = Texture(OutputFormat, finished, 1, "viewport spare");
        HdrSamplesView = device.CreateTextureView(HdrSamples);
        HdrView = device.CreateTextureView(Hdr);
        DepthView = device.CreateTextureView(Depth);
        TintSamplesView = device.CreateTextureView(TintSamples);
        TintView = device.CreateTextureView(Tint);
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

    public TextureHandle TintSamples { get; }

    /// <summary>The tints, resolved, which <see cref="ViewportTint" /> lays over the finished picture.</summary>
    public TextureHandle Tint { get; }

    /// <summary>Where the curve writes, and every other stage after it.</summary>
    public TextureHandle Output { get; }

    /// <summary>Where the stages between write.</summary>
    public TextureHandle Spare { get; }

    public TextureViewHandle HdrSamplesView { get; }

    public TextureViewHandle HdrView { get; }

    public TextureViewHandle DepthView { get; }

    public TextureViewHandle TintSamplesView { get; }

    public TextureViewHandle TintView { get; }

    public TextureViewHandle OutputView { get; }

    public TextureViewHandle SpareView { get; }

    public BufferHandle Readback { get; }

    /// <summary>
    ///     The forward pass's light, cleared to a colour in linear light and resolved into <see cref="Hdr" />, or into
    ///     <paramref name="elsewhere" /> for a pass that comes between, such as the depth of field.
    /// </summary>
    public ColourAttachment Cleared(Vector3 colour, TextureViewHandle? elsewhere = null) =>
        new(
            HdrSamplesView,
            LoadAction.Clear,
            StoreAction.Resolve,
            new Color4(colour.X, colour.Y, colour.Z, 1f),
            elsewhere ?? HdrView
        );

    /// <summary>The tints, cleared to none and resolved into <see cref="Tint" />.</summary>
    public ColourAttachment Tinted =>
        new(TintSamplesView, LoadAction.Clear, StoreAction.Resolve, new Color4(0f, 0f, 0f, 0f), TintView);

    public void Dispose() {
        device.WaitIdle();
        device.Destroy(Readback);
        var views = new[] { HdrSamplesView, HdrView, DepthView, TintSamplesView, TintView, OutputView, SpareView };
        foreach (var view in views) {
            device.Destroy(view);
        }

        foreach (var texture in new[] { HdrSamples, Hdr, Depth, TintSamples, Tint, Output, Spare }) {
            device.Destroy(texture);
        }
    }

    TextureHandle Texture(PixelFormat format, TextureUsage usage, int samples, string name) =>
        device.CreateTexture(new(format, Width, Height, usage, SampleCount: samples, Name: name));
}
