// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Interchange;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     The whole viewport: a forward pass over the sky at 4x MSAA in HDR, a tonemap, and a readback into caller memory.
///     It holds a device and things derived from a file, and all of it can be rebuilt, which is why a Worker may die.
/// </summary>
public sealed class ViewportRenderer : IDisposable {
    const PixelFormat HdrFormat = PixelFormat.Rgba16Float;
    const PixelFormat DepthFormat = PixelFormat.Depth32Float;
    const PixelFormat OutputFormat = PixelFormat.Rgba8UNorm;
    const int Samples = 4;
    const float Exposure = 0.9f;

    readonly VulkanDevice device;
    readonly EffectLoader loader;
    readonly ViewportEnvironment environment = ViewportEnvironment.Studio();
    readonly List<Action> cleanup = [];
    readonly Pass forward;
    readonly Pass sky;
    readonly Pass tonemap;
    readonly SamplerHandle sampler;
    readonly TextureViewHandle environmentView;
    readonly BufferHandle frameConstants;
    readonly BufferHandle skyConstants;
    readonly BufferHandle tonemapConstants;
    readonly BufferHandle meteredExposure;
    readonly DescriptorSetHandle frameSet;
    readonly DescriptorSetHandle skySet;
    readonly ConstantBlock frameBlock;
    readonly ConstantBlock skyBlock;
    Geometry? geometry;
    Targets? targets;

    ViewportRenderer(VulkanDevice device, ShaderCache cache) {
        this.device = device;
        loader = new(device);
        var shaders = new ViewportShaders(cache);
        sampler = device.CreateSampler(SamplerDescription.LinearClamp);
        cleanup.Add(() => device.Destroy(sampler));
        environmentView = UploadEnvironment();

        var hdr = new[] { new ColourTargetState(HdrFormat, BlendState.Opaque) };
        forward = Pipeline(shaders.Forward(), hdr, Samples, DepthStencilState.Default, DepthFormat, true);
        sky = Pipeline(shaders.Sky(), hdr, Samples, DepthStencilState.Disabled, DepthFormat, false);
        tonemap = Pipeline(
            shaders.Tonemap(),
            [new(OutputFormat, BlendState.Opaque)],
            1,
            DepthStencilState.Disabled,
            PixelFormat.Undefined,
            false
        );

        frameBlock = new(forward.Effect.BlockOf(DescriptorSetSlot.PerFrame));
        skyBlock = new(sky.Effect.BlockOf(DescriptorSetSlot.PerMaterial));
        frameConstants = Constants(frameBlock.Bytes.Length, "viewport frame");
        skyConstants = Constants(skyBlock.Bytes.Length, "viewport sky");
        frameSet = BindEnvironment(forward, DescriptorSetSlot.PerFrame, frameConstants);
        skySet = BindEnvironment(sky, DescriptorSetSlot.PerMaterial, skyConstants);

        var tone = new ConstantBlock(tonemap.Effect.BlockOf(DescriptorSetSlot.PerMaterial));
        var neutral = new Vector3(1f, 1f, 1f);
        tone.Set("exposure", Exposure)
            .Set("whitePoint", 4f)
            .Set("contrast", 1f)
            .Set("saturation", 1f)
            .Set("whiteBalance", neutral)
            .Set("colorFilter", neutral)
            .Set("shadowTint", neutral)
            .Set("highlightTint", neutral);
        tonemapConstants = Constants(tone.Bytes.Length, "viewport tonemap");
        device.Write(tonemapConstants, 0, tone.Bytes);

        // Raven keeps the auto-exposure binding in this variant although the permutation that reads it is off.
        meteredExposure = device.CreateBuffer(
            new(sizeof(float), BufferUsage.Storage, MemoryAccess.HostUpload, "viewport metered exposure")
        );
        device.Write(meteredExposure, 0, BitConverter.GetBytes(Exposure));
        cleanup.Add(() => device.Destroy(meteredExposure));
    }

    /// <summary>The adapter's name, for logs.</summary>
    public string Adapter => device.Adapter.Name;

    /// <summary>Whether a mesh is open.</summary>
    public bool HasMesh => geometry is not null;

    /// <summary>The bytes of one frame of this size: tightly packed, top row first, RGBA in sRGB.</summary>
    public static int FrameBytes(int width, int height) => width * height * 4;

    /// <summary>Opens a device and builds everything but a mesh, or says why it could not.</summary>
    public static bool TryCreate(ShaderCache cache, out ViewportRenderer? renderer, out string? refusal) {
        ArgumentNullException.ThrowIfNull(cache);
        if (!VulkanDevice.TryCreate(new(), out var device, out refusal)) {
            renderer = null;
            return false;
        }

        try {
            renderer = new(device!, cache);
            return true;
        } catch {
            device!.Dispose();
            throw;
        }
    }

    /// <summary>Replaces the mesh with the one in a file.</summary>
    /// <exception cref="ModelFormatException">The file is not a readable model.</exception>
    public void OpenMesh(string path) {
        var mesh = ViewportMesh.Read(path);
        geometry?.Dispose();
        geometry = new(device, forward, mesh);
    }

    /// <summary>Renders one frame and copies it into <paramref name="destination" />.</summary>
    public void Render(OrbitCamera camera, int width, int height, Span<byte> destination) {
        if (geometry is not { } scene) {
            throw new InvalidOperationException("No mesh is open.");
        }

        if (targets is not { } current || current.Width != width || current.Height != height) {
            targets?.Dispose();
            current = targets = new(device, tonemap, sampler, tonemapConstants, meteredExposure, width, height);
        }

        WriteFrame(camera, scene.Mesh.Bounds, (float)width / height);
        device.BeginFrame();
        using (var commands = device.BeginCommandList(QueueKind.Graphics, "viewport")) {
            Record(commands, current, scene);
            commands.Finish();
            device.GraphicsQueue.Submit([commands]);
        }

        device.EndFrame();
        device.WaitIdle();
        device.Read(current.Readback, 0, destination[..FrameBytes(width, height)]);
    }

    public void Dispose() {
        device.WaitIdle();
        targets?.Dispose();
        geometry?.Dispose();
        foreach (var action in Enumerable.Reverse(cleanup)) {
            action();
        }

        loader.Release();
        device.Dispose();
    }

    void WriteFrame(OrbitCamera camera, BoundingBox bounds, float aspectRatio) {
        var viewProjection = camera.ViewProjection(bounds, aspectRatio);
        frameBlock.Set("viewProjection", viewProjection)
            .Set("cameraPosition", camera.Eye(bounds))
            .Set("lightDirection", Vector3.Normalize(new(0.6f, 0.7f, 0.5f)))
            .Set("lightColor", new Vector3(1.2f, 1.15f, 1.1f))
            .Set("environmentMipCount", (float)environment.Prefiltered.LevelCount);
        WriteIrradiance();
        device.Write(frameConstants, 0, frameBlock.Bytes);

        Matrix4x4.Invert(viewProjection, out var inverse);
        skyBlock.Set("inverseViewProjection", inverse)
            .Set("environmentMipCount", (float)environment.Prefiltered.LevelCount)
            .Set("intensity", 0.55f);
        device.Write(skyConstants, 0, skyBlock.Bytes);
    }

    /// <summary>In <c>ShCoefficients</c>' order, which is the basis's: l00, then m from -l to l in each band.</summary>
    void WriteIrradiance() {
        ReadOnlySpan<string> names = ["l00", "l1m1", "l10", "l11", "l2m2", "l2m1", "l20", "l21", "l22"];
        var coefficients = environment.Irradiance.Coefficients;
        for (var i = 0; i < names.Length; i++) {
            frameBlock.Set(names[i], coefficients[i]);
        }
    }

    void Record(ICommandList commands, Targets target, Geometry scene) {
        commands.Barrier(
            new BarrierGroup(
                [],
                [
                    new(target.HdrSamples, ResourceState.Undefined, ResourceState.ColourTarget),
                    new(target.Hdr, ResourceState.Undefined, ResourceState.ColourTarget),
                    new(target.Depth, ResourceState.Undefined, ResourceState.DepthStencilWrite)
                ]
            )
        );
        var viewport = new Vixen.Core.Mathematics.Viewport(0f, 0f, target.Width, target.Height);
        var scissor = new ScissorRect(0, 0, target.Width, target.Height);
        commands.BeginRenderPass(
            new(
                [new(target.HdrSamplesView, LoadAction.Clear, StoreAction.Resolve, default, target.HdrView)],
                new DepthStencilAttachment(target.DepthView, LoadAction.Clear, StoreAction.DontCare),
                "viewport forward"
            )
        );
        commands.SetViewport(viewport);
        commands.SetScissor(scissor);
        commands.BindPipeline(sky.Pipeline);
        commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, skySet);
        commands.Draw(3);

        commands.BindPipeline(forward.Pipeline);
        commands.BindDescriptorSet(DescriptorSetSlot.PerFrame, frameSet);
        commands.BindVertexBuffer(0, scene.Vertices);
        commands.BindIndexBuffer(scene.Indices, IndexFormat.UInt32);
        foreach (var (part, material) in scene.Parts) {
            commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, material);
            commands.DrawIndexed(part.IndexCount, firstIndex: part.FirstIndex);
        }

        commands.EndRenderPass();
        commands.Barrier(
            new BarrierGroup(
                [],
                [
                    new(target.Hdr, ResourceState.ColourTarget, ResourceState.ShaderRead),
                    new(target.Output, ResourceState.Undefined, ResourceState.ColourTarget)
                ]
            )
        );
        commands.BeginRenderPass(new([new(target.OutputView, LoadAction.DontCare)], name: "viewport tonemap"));
        commands.SetViewport(viewport);
        commands.SetScissor(scissor);
        commands.BindPipeline(tonemap.Pipeline);
        commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, target.ToneSet);
        commands.Draw(3);
        commands.EndRenderPass();
        commands.Barrier(
            new BarrierGroup([], [new(target.Output, ResourceState.ColourTarget, ResourceState.CopySource)])
        );
        commands.CopyTextureToBuffer(new(target.Output), new(target.Width, target.Height, 1), target.Readback, 0);
    }

    Pass Pipeline(
        EffectData data,
        ColourTargetState[] colour,
        int samples,
        DepthStencilState depth,
        PixelFormat depthFormat,
        bool hasVertices
    ) {
        var effect = loader.Load(data);
        var vertex = Module(effect, ShaderStage.Vertex);
        var fragment = Module(effect, ShaderStage.Fragment);
        var pipeline = device.CreateGraphicsPipeline(
            new(
                vertex,
                fragment,
                effect.Layout,
                colour,
                hasVertices ? [VerticesOf(effect)] : [],
                Rasterizer: hasVertices ? RasterizerState.Default : RasterizerState.TwoSided,
                DepthStencil: depth,
                DepthFormat: depthFormat,
                SampleCount: samples,
                Name: data.ShaderName
            )
        );
        cleanup.Add(() => device.Destroy(pipeline));
        return new(effect, pipeline);
    }

    /// <summary>Raven numbers a shader's inputs, so the mesh's two attributes are found by name.</summary>
    static VertexBufferLayout VerticesOf(Effect effect) {
        var position = effect.VertexInputs.First(static input => input.Name.EndsWith("position", StringComparison.Ordinal));
        var normal = effect.VertexInputs.First(static input => input.Name.EndsWith("normal", StringComparison.Ordinal));
        return new(
            ViewportMesh.FloatsPerVertex * sizeof(float),
            [
                new((uint)position.Location, VertexFormat.Float32X3, 0),
                new((uint)normal.Location, VertexFormat.Float32X3, 3 * sizeof(float))
            ]
        );
    }

    ShaderHandle Module(Effect effect, ShaderStage stage) {
        var compiled = effect.Stages.First(s => s.Stage == stage);
        var module = device.CreateShader(stage, compiled.Bytecode.AsSpan(), effect.Key.ShaderName);
        cleanup.Add(() => device.Destroy(module));
        return module;
    }

    BufferHandle Constants(int size, string name) {
        var buffer = device.CreateBuffer(new(size, BufferUsage.Uniform, MemoryAccess.HostUpload, name));
        cleanup.Add(() => device.Destroy(buffer));
        return buffer;
    }

    DescriptorSetHandle BindEnvironment(Pass pass, DescriptorSetSlot slot, BufferHandle constants) {
        var set = device.CreateDescriptorSet(pass.Effect.SetLayouts[(int)slot], pass.Effect.Key.ShaderName);
        cleanup.Add(() => device.Destroy(set));
        device.UpdateDescriptorSet(
            set,
            [
                DescriptorWrite.Uniform(pass.Effect.BlockOf(slot).Binding, constants),
                DescriptorWrite.Texture(pass.Effect.BindingOf("environment")!.Value.Binding, environmentView),
                DescriptorWrite.SamplerAt(pass.Effect.BindingOf("environmentSampler")!.Value.Binding, sampler)
            ]
        );
        return set;
    }

    TextureViewHandle UploadEnvironment() {
        var data = environment.Prefiltered;
        var texture = device.CreateTexture(
            new(
                data.Format,
                data.Width,
                data.Height,
                TextureUsage.Sampled | TextureUsage.CopyDestination,
                MipLevels: data.LevelCount,
                ArrayLayers: CubeMap.Faces,
                Dimension: TextureDimension.TextureCube,
                Name: "viewport environment"
            )
        );
        var view = device.CreateTextureView(texture);
        cleanup.Add(() => {
                device.Destroy(view);
                device.Destroy(texture);
            }
        );

        var staging = device.CreateBuffer(
            new(data.ByteLength, BufferUsage.CopySource, MemoryAccess.HostUpload, "viewport environment staging")
        );
        device.Write(staging, 0, data.Pixels);
        using (var commands = device.BeginCommandList(QueueKind.Graphics, "viewport environment")) {
            commands.Barrier(
                new BarrierGroup([], [new(texture, ResourceState.Undefined, ResourceState.CopyDestination)])
            );
            CopyFaces(commands, data, staging, texture);
            commands.Barrier(
                new BarrierGroup([], [new(texture, ResourceState.CopyDestination, ResourceState.ShaderRead)])
            );
            commands.Finish();
            device.GraphicsQueue.Submit([commands]);
        }

        device.WaitIdle();
        device.Destroy(staging);
        return view;
    }

    static void CopyFaces(ICommandList commands, TextureData data, BufferHandle staging, TextureHandle texture) {
        foreach (var level in data.Levels) {
            var faceBytes = level.Length / CubeMap.Faces;
            for (var face = 0; face < CubeMap.Faces; face++) {
                commands.CopyBufferToTexture(
                    staging,
                    level.Offset + (face * faceBytes),
                    new(texture, level.Level, face),
                    new(level.Width, level.Height, 1)
                );
            }
        }
    }

    readonly record struct Pass(Effect Effect, PipelineHandle Pipeline);

    /// <summary>The GPU's copy of a mesh, and one descriptor set per part's material.</summary>
    sealed class Geometry : IDisposable {
        readonly VulkanDevice device;
        readonly List<BufferHandle> buffers = [];
        readonly List<DescriptorSetHandle> sets = [];

        public Geometry(VulkanDevice device, Pass forward, ViewportMesh mesh) {
            this.device = device;
            Mesh = mesh;
            Vertices = Upload<float>(mesh.Vertices, BufferUsage.Vertex, "viewport vertices");
            Indices = Upload<uint>(mesh.Indices, BufferUsage.Index, "viewport indices");
            var block = forward.Effect.BlockOf(DescriptorSetSlot.PerMaterial);
            Parts = [.. mesh.Parts.Select(part => (part, Material(forward, block, part)))];
        }

        public ViewportMesh Mesh { get; }

        public BufferHandle Vertices { get; }

        public BufferHandle Indices { get; }

        public (ViewportPart Part, DescriptorSetHandle Material)[] Parts { get; }

        public void Dispose() {
            device.WaitIdle();
            foreach (var set in sets) {
                device.Destroy(set);
            }

            foreach (var buffer in buffers) {
                device.Destroy(buffer);
            }
        }

        DescriptorSetHandle Material(Pass forward, EffectBlock block, ViewportPart part) {
            var colour = part.Material.BaseColor;
            var constants = new ConstantBlock(block)
                .Set("baseColor", new Vector3(colour.X, colour.Y, colour.Z))
                .Set("metallic", part.Material.Metallic)
                .Set("roughness", part.Material.Roughness);
            var buffer = Upload(constants.Bytes, BufferUsage.Uniform, "viewport material");
            var set = device.CreateDescriptorSet(
                forward.Effect.SetLayouts[(int)DescriptorSetSlot.PerMaterial],
                "viewport material"
            );
            device.UpdateDescriptorSet(set, [DescriptorWrite.Uniform(block.Binding, buffer)]);
            sets.Add(set);
            return set;
        }

        BufferHandle Upload<T>(ReadOnlySpan<T> data, BufferUsage usage, string name) where T : unmanaged {
            var bytes = MemoryMarshal.AsBytes(data);
            var buffer = device.CreateBuffer(new(bytes.Length, usage, MemoryAccess.HostUpload, name));
            device.Write(buffer, 0, bytes);
            buffers.Add(buffer);
            return buffer;
        }
    }

    /// <summary>Everything whose size is the frame's.</summary>
    sealed class Targets : IDisposable {
        readonly VulkanDevice device;

        public Targets(
            VulkanDevice device,
            Pass tonemap,
            SamplerHandle sampler,
            BufferHandle constants,
            BufferHandle exposure,
            int width,
            int height
        ) {
            this.device = device;
            Width = width;
            Height = height;
            HdrSamples = Texture(HdrFormat, TextureUsage.ColourTarget, Samples, "viewport hdr samples");
            Hdr = Texture(HdrFormat, TextureUsage.ColourTarget | TextureUsage.Sampled, 1, "viewport hdr");
            Depth = Texture(DepthFormat, TextureUsage.DepthStencilTarget, Samples, "viewport depth");
            Output = Texture(OutputFormat, TextureUsage.ColourTarget | TextureUsage.CopySource, 1, "viewport output");
            HdrSamplesView = device.CreateTextureView(HdrSamples);
            HdrView = device.CreateTextureView(Hdr);
            DepthView = device.CreateTextureView(Depth);
            OutputView = device.CreateTextureView(Output);
            Readback = device.CreateBuffer(
                new(FrameBytes(width, height), BufferUsage.CopyDestination, MemoryAccess.HostReadback, "viewport readback")
            );
            ToneSet = device.CreateDescriptorSet(
                tonemap.Effect.SetLayouts[(int)DescriptorSetSlot.PerMaterial],
                "viewport tonemap"
            );
            device.UpdateDescriptorSet(
                ToneSet,
                [
                    DescriptorWrite.Uniform(tonemap.Effect.BlockOf(DescriptorSetSlot.PerMaterial).Binding, constants),
                    DescriptorWrite.Texture(tonemap.Effect.BindingOf("source")!.Value.Binding, HdrView),
                    DescriptorWrite.SamplerAt(tonemap.Effect.BindingOf("sourceSampler")!.Value.Binding, sampler),
                    DescriptorWrite.Storage(tonemap.Effect.BindingOf("exposureBuffer")!.Value.Binding, exposure)
                ]
            );
        }

        public int Width { get; }

        public int Height { get; }

        public TextureHandle HdrSamples { get; }

        public TextureHandle Hdr { get; }

        public TextureHandle Depth { get; }

        public TextureHandle Output { get; }

        public TextureViewHandle HdrSamplesView { get; }

        public TextureViewHandle HdrView { get; }

        public TextureViewHandle DepthView { get; }

        public TextureViewHandle OutputView { get; }

        public BufferHandle Readback { get; }

        public DescriptorSetHandle ToneSet { get; }

        public void Dispose() {
            device.WaitIdle();
            device.Destroy(ToneSet);
            device.Destroy(Readback);
            foreach (var view in new[] { HdrSamplesView, HdrView, DepthView, OutputView }) {
                device.Destroy(view);
            }

            foreach (var texture in new[] { HdrSamples, Hdr, Depth, Output }) {
                device.Destroy(texture);
            }
        }

        TextureHandle Texture(PixelFormat format, TextureUsage usage, int samples, string name) =>
            device.CreateTexture(new(format, Width, Height, usage, SampleCount: samples, Name: name));
    }
}
