// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
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
    const int SettleWidth = 320;
    const int SettleHeight = 200;
    const int SettledFrames = 8;
    const int SettleLimit = 64;

    readonly VulkanDevice device;
    readonly EffectLoader loader;
    readonly ViewportEnvironment environment = ViewportEnvironment.Studio();
    readonly List<Action> cleanup = [];
    readonly Pass forward;
    readonly Pass sky;
    readonly Pass tonemap;
    readonly SamplerHandle sampler;
    readonly SamplerHandle mapSampler;
    readonly ViewportTextures textures;
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
    string? meshFolder;
    ViewportMaps maps = ViewportMaps.None;
    readonly ViewportLiveMap?[] live = new ViewportLiveMap?[Enum.GetValues<ViewportSlot>().Length];

    ViewportRenderer(VulkanDevice device, ShaderCache cache) {
        this.device = device;
        loader = new(device);
        var shaders = new ViewportShaders(cache);
        sampler = device.CreateSampler(SamplerDescription.LinearClamp);
        cleanup.Add(() => device.Destroy(sampler));
        mapSampler = device.CreateSampler(
            SamplerDescription.LinearRepeat with { Anisotropy = 8f, Name = "viewport maps" }
        );
        cleanup.Add(() => device.Destroy(mapSampler));
        textures = new(device);
        cleanup.Add(textures.Dispose);
        environmentView = textures.UploadCube(environment.Prefiltered, cleanup);

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
    /// <exception cref="InvalidDataException">A material's image is not one this reads.</exception>
    /// <exception cref="IOException">A material's image cannot be read.</exception>
    public void OpenMesh(string path) {
        var mesh = ViewportMesh.Read(path);
        maps = ViewportMaps.None;
        meshFolder = Path.GetDirectoryName(Path.GetFullPath(path));
        Show(mesh);
        Settle();
    }

    /// <summary>Replaces the pictures laid over the mesh's own materials.</summary>
    /// <exception cref="InvalidOperationException">No mesh is open.</exception>
    /// <exception cref="InvalidDataException">A picture is not one this reads.</exception>
    /// <exception cref="IOException">A picture cannot be read.</exception>
    public void SetMaps(ViewportMaps replacement) {
        ArgumentNullException.ThrowIfNull(replacement);
        if (geometry is not { } scene) {
            throw new InvalidOperationException("No mesh is open.");
        }

        maps = replacement;
        Show(scene.Mesh);
    }

    /// <summary>
    ///     Replaces a slot's picture with live pixels, the bytes an export writes: RGBA, tightly packed, top row first.
    ///     They win over any file, and stay until <see cref="ClearLive" /> or another mesh's stack replaces them.
    /// </summary>
    public void SetLive(ViewportSlot slot, int width, int height, ReadOnlySpan<byte> pixels) {
        var map = live[(int)slot];
        var created = map is null || map.Width != width || map.Height != height;
        if (created) {
            map?.Dispose();
            map = live[(int)slot] = new(device, Meaning(slot), width, height);
        }

        map!.Repair([new(new(0, 0, width, height), [..pixels])]);
        if (created && geometry is { } scene) {
            Show(scene.Mesh);
        }
    }

    /// <summary>Rewrites only the rectangles that changed, with the mips above them. Nothing rebinds.</summary>
    public void RepairLive(ViewportSlot slot, IReadOnlyList<ViewportRepair> repairs) {
        ArgumentNullException.ThrowIfNull(repairs);
        (live[(int)slot] ?? throw new InvalidOperationException($"{slot} has no live map.")).Repair(repairs);
    }

    /// <summary>Takes the live maps off.</summary>
    public void ClearLive() {
        var had = false;
        device.WaitIdle();
        for (var slot = 0; slot < live.Length; slot++) {
            had |= live[slot] is not null;
            live[slot]?.Dispose();
            live[slot] = null;
        }

        if (had && geometry is { } scene) {
            Show(scene.Mesh);
        }
    }

    /// <summary>
    ///     The top level of the texture a slot's draw call samples, read back from the GPU: RGBA, tightly packed, top
    ///     row first. Null when the slot has no live map. This is the picture before lighting and tonemapping, which
    ///     is the one that can equal an exported file.
    /// </summary>
    public byte[]? Sampled(ViewportSlot slot) => live[(int)slot]?.ReadBack();

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
        foreach (var map in live) {
            map?.Dispose();
        }

        foreach (var action in Enumerable.Reverse(cleanup)) {
            action();
        }

        loader.Release();
        device.Dispose();
    }

    /// <summary>Puts a mesh on the GPU with its materials as they stand, dropping textures no longer used.</summary>
    void Show(ViewportMesh mesh) {
        var ready = maps.Normal is not null || live[(int)ViewportSlot.Normal] is not null || mesh.WantsTangents
            ? mesh.WithTangents()
            : mesh;
        var next = ReferenceEquals(ready, geometry?.Mesh) ? geometry! : new Geometry(device, ready);
        textures.BeginUse();
        var sets = next.Bind(forward, material => Material(material, ready));
        if (!ReferenceEquals(next, geometry)) {
            geometry?.Dispose();
            geometry = next;
        }

        geometry.Use(sets);
        textures.EndUse();
    }

    /// <summary>A material with the pictures laid over it, each slot read as what it means.</summary>
    MaterialBinding Material(ViewportMaterial material, ViewportMesh mesh) {
        var own = material.Textures;

        var factors = material.Factors;
        var colour = Covered(ViewportSlot.BaseColor, maps.BaseColor) ? new Vector4(1f, 1f, 1f, 1f) : factors.BaseColor;
        var normal = mesh.HasTangents
            ? Slot(ViewportSlot.Normal, maps.Normal, own.Normal, MapMeaning.Normal, textures.Open)
            : textures.Open;
        var laidOver = Covered(ViewportSlot.Normal, maps.Normal);
        var hasNormal = mesh.HasTangents && (laidOver || own.Normal is not null);

        return new(
            new(colour.X, colour.Y, colour.Z),
            Covered(ViewportSlot.Metallic, maps.Metallic) ? 1f : factors.Metallic,
            Covered(ViewportSlot.Roughness, maps.Roughness) ? 1f : factors.Roughness,
            laidOver ? 1f : own.NormalScale,
            hasNormal,
            new(
                Slot(ViewportSlot.BaseColor, maps.BaseColor, own.BaseColor, MapMeaning.Colour, textures.White),
                normal,
                Slot(
                    ViewportSlot.Roughness,
                    maps.Roughness,
                    own.MetallicRoughness,
                    MapMeaning.Measurement,
                    textures.Open
                ),
                Slot(
                    ViewportSlot.Metallic,
                    maps.Metallic,
                    own.MetallicRoughness,
                    MapMeaning.Measurement,
                    textures.Open
                ),
                Slot(ViewportSlot.Occlusion, maps.Occlusion, own.Occlusion, MapMeaning.Measurement, textures.Open)
            ),
            mapSampler
        );
    }

    /// <summary>Whether a picture fills a slot, so the material's own factor for it stands down.</summary>
    bool Covered(ViewportSlot slot, ViewportMapFile? file) => live[(int)slot] is not null || file is not null;

    /// <summary>What a slot samples: live pixels, a picture laid over, the material's own image, or nothing.</summary>
    TextureViewHandle Slot(
        ViewportSlot slot,
        ViewportMapFile? file,
        ModelTexture? image,
        MapMeaning meaning,
        TextureViewHandle empty
    ) =>
        live[(int)slot] is { } map
            ? map.View
            : file is not null
                ? textures.Of(file, meaning)
                : image is not null
                    ? textures.Of(image, meshFolder ?? string.Empty, meaning)
                    : empty;

    static MapMeaning Meaning(ViewportSlot slot) =>
        slot switch {
            ViewportSlot.BaseColor => MapMeaning.Colour,
            ViewportSlot.Normal => MapMeaning.Normal,
            _ => MapMeaning.Measurement
        };

    /// <summary>
    ///     Draws the mesh until its frames stop changing, so that one camera gives one picture from the first frame a
    ///     caller sees. Apple's GPUs bin geometry into a buffer that starts small and is sized from frames that have
    ///     finished, a moment after they finish. A mesh that outgrows it is split, which moves a few pixels where the
    ///     mesh overlaps itself by a level or two, and no fence says when the new size has arrived. So this waits for
    ///     the picture: the frames it takes are the ones that carry the size back. The README has the evidence.
    /// </summary>
    void Settle() {
        var frame = new byte[FrameBytes(SettleWidth, SettleHeight)];
        var previous = new byte[frame.Length];
        var same = 0;
        for (var i = 0; i < SettleLimit && same < SettledFrames; i++) {
            Render(OrbitCamera.Default, SettleWidth, SettleHeight, frame);
            same = frame.AsSpan().SequenceEqual(previous) ? same + 1 : 0;
            (frame, previous) = (previous, frame);
        }
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
        foreach (var part in scene.Mesh.Parts) {
            commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, scene.SetOf(part));
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

    /// <summary>Raven numbers a shader's inputs, so the mesh's attributes are found by name.</summary>
    static VertexBufferLayout VerticesOf(Effect effect) {
        return new(
            ViewportMesh.FloatsPerVertex * sizeof(float),
            [
                new(Location("position"), VertexFormat.Float32X3, 0),
                new(Location("normal"), VertexFormat.Float32X3, 3 * sizeof(float)),
                new(Location("texCoord"), VertexFormat.Float32X2, 6 * sizeof(float)),
                new(Location("tangent"), VertexFormat.Float32X4, 8 * sizeof(float))
            ]
        );

        uint Location(string name) =>
            (uint)effect.VertexInputs.First(input => input.Name.EndsWith(name, StringComparison.Ordinal)).Location;
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

    readonly record struct Pass(Effect Effect, PipelineHandle Pipeline);

    /// <summary>What one material binds.</summary>
    readonly record struct MaterialBinding(
        Vector3 BaseColor,
        float Metallic,
        float Roughness,
        float NormalScale,
        bool UsesNormalMap,
        MaterialMaps Maps,
        SamplerHandle Sampler
    );

    /// <summary>One material's five textures.</summary>
    readonly record struct MaterialMaps(
        TextureViewHandle BaseColor,
        TextureViewHandle Normal,
        TextureViewHandle Roughness,
        TextureViewHandle Metallic,
        TextureViewHandle Occlusion
    );

    /// <summary>The GPU's copy of a mesh, and one descriptor set per material in it.</summary>
    sealed class Geometry : IDisposable {
        readonly VulkanDevice device;
        readonly BufferHandle vertices;
        readonly BufferHandle indices;
        readonly List<(DescriptorSetHandle Set, BufferHandle Constants)> bound = [];
        DescriptorSetHandle[] sets = [];

        public Geometry(VulkanDevice device, ViewportMesh mesh) {
            this.device = device;
            Mesh = mesh;
            vertices = Upload(mesh.Vertices, BufferUsage.Vertex, "viewport vertices");
            indices = Upload(mesh.Indices, BufferUsage.Index, "viewport indices");
        }

        public ViewportMesh Mesh { get; }

        public BufferHandle Vertices => vertices;

        public BufferHandle Indices => indices;

        public DescriptorSetHandle SetOf(ViewportPart part) => sets[Math.Clamp(part.Material, 0, sets.Length - 1)];

        /// <summary>Makes a set for every material, for <see cref="Use" /> to swap in once it is all there.</summary>
        public List<(DescriptorSetHandle Set, BufferHandle Constants)> Bind(
            Pass forward,
            Func<ViewportMaterial, MaterialBinding> describe
        ) {
            var block = forward.Effect.BlockOf(DescriptorSetSlot.PerMaterial);
            List<(DescriptorSetHandle, BufferHandle)> made = [];
            foreach (var material in Mesh.Materials) {
                made.Add(Material(forward, block, describe(material)));
            }

            return made;
        }

        public void Use(List<(DescriptorSetHandle Set, BufferHandle Constants)> next) {
            Release();
            bound.AddRange(next);
            sets = [..next.Select(static pair => pair.Set)];
        }

        public void Dispose() {
            Release();
            device.Destroy(vertices);
            device.Destroy(indices);
        }

        void Release() {
            device.WaitIdle();
            foreach (var (set, constants) in bound) {
                device.Destroy(set);
                device.Destroy(constants);
            }

            bound.Clear();
        }

        (DescriptorSetHandle, BufferHandle) Material(Pass forward, EffectBlock block, MaterialBinding material) {
            var constants = new ConstantBlock(block)
                .Set("baseColor", material.BaseColor)
                .Set("metallic", material.Metallic)
                .Set("roughness", material.Roughness)
                .Set("normalScale", material.NormalScale)
                .Set("usesNormalMap", material.UsesNormalMap ? 1f : 0f);
            var buffer = Upload(constants.Bytes, BufferUsage.Uniform, "viewport material");
            var set = device.CreateDescriptorSet(
                forward.Effect.SetLayouts[(int)DescriptorSetSlot.PerMaterial],
                "viewport material"
            );
            uint Binding(string name) => forward.Effect.BindingOf(name)!.Value.Binding;
            device.UpdateDescriptorSet(
                set,
                [
                    DescriptorWrite.Uniform(block.Binding, buffer),
                    DescriptorWrite.Texture(Binding("baseColorMap"), material.Maps.BaseColor),
                    DescriptorWrite.Texture(Binding("normalMap"), material.Maps.Normal),
                    DescriptorWrite.Texture(Binding("roughnessMap"), material.Maps.Roughness),
                    DescriptorWrite.Texture(Binding("metallicMap"), material.Maps.Metallic),
                    DescriptorWrite.Texture(Binding("occlusionMap"), material.Maps.Occlusion),
                    DescriptorWrite.SamplerAt(Binding("mapSampler"), material.Sampler)
                ]
            );
            return (set, buffer);
        }

        BufferHandle Upload<T>(ReadOnlySpan<T> data, BufferUsage usage, string name) where T : unmanaged {
            var bytes = MemoryMarshal.AsBytes(data);
            var buffer = device.CreateBuffer(new(bytes.Length, usage, MemoryAccess.HostUpload, name));
            device.Write(buffer, 0, bytes);
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
                new(
                    FrameBytes(width, height),
                    BufferUsage.CopyDestination,
                    MemoryAccess.HostReadback,
                    "viewport readback"
                )
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
