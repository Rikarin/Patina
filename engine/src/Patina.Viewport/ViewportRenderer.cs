// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Interchange;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     The whole viewport: a forward pass over the sky at 4x MSAA in HDR, the triangles' edges if asked, a tonemap and
///     the post effects that are on, and a readback into caller memory. It holds a device and things derived from a
///     file, and all of it can be rebuilt, which is why a Worker may die.
/// </summary>
public sealed class ViewportRenderer : IDisposable {
    const PixelFormat HdrFormat = PixelFormat.Rgba16Float;
    const PixelFormat DepthFormat = PixelFormat.Depth32Float;
    const PixelFormat OutputFormat = PixelFormat.Rgba8UNorm;
    const int Samples = 4;

    /// <summary>How bright the background is at full opacity, against the light it gives the model.</summary>
    const float SkyIntensity = 0.55f;

    /// <summary>How far the edges are lifted towards the eye, in bounding radii.</summary>
    const float WireLift = 0.002f;

    const int SettleWidth = 320;
    const int SettleHeight = 200;
    const int SettledFrames = 8;
    const int SettleLimit = 64;

    readonly VulkanDevice device;
    readonly EffectLoader loader;
    ViewportEnvironment environment = ViewportEnvironment.Load(ViewportEnvironment.Default);
    readonly List<Action> cleanup = [];
    readonly Pass forward;
    readonly Pass sky;
    readonly Pass wire;
    readonly SamplerHandle sampler;
    readonly SamplerHandle mapSampler;
    readonly ViewportTextures textures;
    TextureViewHandle environmentView;
    TextureViewHandle pictureView;
    ViewportMapFile? picture;
    readonly ViewportShadows shadows;
    readonly ViewportPost post;
    readonly BufferHandle frameConstants;
    readonly BufferHandle skyConstants;
    readonly BufferHandle wireConstants;
    readonly DescriptorSetHandle frameSet;
    readonly DescriptorSetHandle skySet;
    readonly DescriptorSetHandle wireSet;
    readonly ConstantBlock frameBlock;
    readonly ConstantBlock skyBlock;
    readonly ConstantBlock wireBlock;
    Geometry? geometry;
    Targets? targets;
    string? meshFolder;
    ViewportMaps maps = ViewportMaps.None;

    /// <summary>The key of a live map on the whole model.</summary>
    public const int AllMaterials = -1;

    /// <summary>Live maps by material. <see cref="AllMaterials" /> is a stack that paints the whole model.</summary>
    readonly Dictionary<int, ViewportLiveMap?[]> live = [];

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
        pictureView = textures.Black;

        var shadowEffect = loader.Load(shaders.Shadow());
        shadows = new(
            device,
            shadowEffect,
            Module(shadowEffect, ShaderStage.Vertex),
            Module(shadowEffect, ShaderStage.Fragment),
            DepthFormat
        );
        cleanup.Add(shadows.Dispose);

        var hdr = new[] { new ColourTargetState(HdrFormat, BlendState.Opaque) };
        forward = Pipeline(shaders.Forward(), hdr, Samples, DepthStencilState.Default, DepthFormat, true);
        sky = Pipeline(shaders.Sky(), hdr, Samples, DepthStencilState.Disabled, DepthFormat, false);
        wire = WirePipeline(shaders.Wire());
        post = new(
            device,
            shaders,
            data => {
                var pass = Pipeline(
                    data,
                    [new(OutputFormat, BlendState.Opaque)],
                    1,
                    DepthStencilState.Disabled,
                    PixelFormat.Undefined,
                    false
                );
                return (pass.Effect, pass.Pipeline);
            }
        );
        cleanup.Add(post.Dispose);

        frameBlock = new(forward.Effect.BlockOf(DescriptorSetSlot.PerFrame));
        skyBlock = new(sky.Effect.BlockOf(DescriptorSetSlot.PerMaterial));
        wireBlock = new(wire.Effect.BlockOf(DescriptorSetSlot.PerMaterial));
        frameConstants = Constants(frameBlock.Bytes.Length, "viewport frame");
        skyConstants = Constants(skyBlock.Bytes.Length, "viewport sky");
        wireConstants = Constants(wireBlock.Bytes.Length, "viewport wire");
        frameSet = BindEnvironment(forward, DescriptorSetSlot.PerFrame, frameConstants);
        device.UpdateDescriptorSet(
            frameSet,
            [
                DescriptorWrite.Texture(forward.Effect.BindingOf("shadowMap")!.Value.Binding, shadows.MapView),
                DescriptorWrite.SamplerAt(forward.Effect.BindingOf("shadowSampler")!.Value.Binding, shadows.Sampler),
                DescriptorWrite.Texture(forward.Effect.BindingOf("picture")!.Value.Binding, pictureView),
                DescriptorWrite.SamplerAt(forward.Effect.BindingOf("pictureSampler")!.Value.Binding, mapSampler)
            ]
        );
        skySet = BindEnvironment(sky, DescriptorSetSlot.PerMaterial, skyConstants);
        wireSet = device.CreateDescriptorSet(
            wire.Effect.SetLayouts[(int)DescriptorSetSlot.PerMaterial],
            "viewport wire"
        );
        cleanup.Add(() => device.Destroy(wireSet));
        device.UpdateDescriptorSet(
            wireSet,
            [DescriptorWrite.Uniform(wire.Effect.BlockOf(DescriptorSetSlot.PerMaterial).Binding, wireConstants)]
        );
    }

    /// <summary>How the model is lit, shown and finished, from the next frame on.</summary>
    public ViewportShading Shading { get; set; } = new();


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
    public void SetLive(
        ViewportSlot slot,
        int width,
        int height,
        ReadOnlySpan<byte> pixels,
        int material = AllMaterials
    ) {
        var maps = LiveOf(material);
        var map = maps[(int)slot];
        var created = map is null || map.Width != width || map.Height != height;
        if (created) {
            map?.Dispose();
            map = maps[(int)slot] = new(device, Meaning(slot), width, height);
        }

        map!.Repair([new(new(0, 0, width, height), [..pixels])]);
        if (created && geometry is { } scene) {
            Show(scene.Mesh);
        }
    }

    /// <summary>Rewrites only the rectangles that changed, with the mips above them. Nothing rebinds.</summary>
    public void RepairLive(ViewportSlot slot, IReadOnlyList<ViewportRepair> repairs, int material = AllMaterials) {
        ArgumentNullException.ThrowIfNull(repairs);
        (LiveOf(material)[(int)slot] ?? throw new InvalidOperationException($"{slot} has no live map.")).Repair(
            repairs
        );
    }

    /// <summary>Takes the live maps off.</summary>
    public void ClearLive() {
        var had = false;
        device.WaitIdle();
        foreach (var map in live.Values.SelectMany(static maps => maps).OfType<ViewportLiveMap>()) {
            had = true;
            map.Dispose();
        }

        live.Clear();

        if (had && geometry is { } scene) {
            Show(scene.Mesh);
        }
    }

    /// <summary>
    ///     The top level of the texture a slot's draw call samples, read back from the GPU: RGBA, tightly packed, top
    ///     row first. Null when the slot has no live map. This is the picture before lighting and tonemapping, which
    ///     is the one that can equal an exported file.
    /// </summary>
    public byte[]? Sampled(ViewportSlot slot, int material = AllMaterials) => LiveOf(material)[(int)slot]?.ReadBack();

    /// <summary>Which material a texture set paints, by name, or every material for a name that is none here.</summary>
    public int MaterialOf(string name) => geometry?.Mesh.MaterialOf(name) is >= 0 and var at ? at : AllMaterials;

    ViewportLiveMap?[] LiveOf(int material) {
        if (!live.TryGetValue(material, out var maps)) {
            live[material] = maps = new ViewportLiveMap?[Enum.GetValues<ViewportSlot>().Length];
        }

        return maps;
    }

    /// <summary>What a material wears: a stack of its own, or the one on the whole model, or nothing.</summary>
    ViewportLiveMap?[] Worn(int material) =>
        live.TryGetValue(material, out var own)
            ? own
            : live.TryGetValue(AllMaterials, out var whole)
                ? whole
                : new ViewportLiveMap?[Enum.GetValues<ViewportSlot>().Length];

    /// <summary>Lights the model with a built-in room by name, or the <c>.hdr</c> at a path.</summary>
    /// <exception cref="InvalidDataException">The file cannot be read as a picture.</exception>
    public void SetEnvironment(string source) {
        var next = ViewportEnvironment.Load(source);
        device.WaitIdle();
        environment = next;
        environmentView = textures.UploadCube(next.Prefiltered, cleanup);
        Rebind(forward, frameSet);
        Rebind(sky, skySet);
    }

    /// <summary>
    ///     The picture <see cref="ViewportView.Picture" /> shows on the mesh, read as stored: a bake's mesh map, or a
    ///     mask. Null shows black.
    /// </summary>
    /// <exception cref="InvalidDataException">It is not a picture this reads.</exception>
    /// <exception cref="IOException">It cannot be read.</exception>
    public void SetPicture(ViewportMapFile? file) {
        var next = file is null ? textures.Black : textures.Of(file, MapMeaning.Measurement);
        device.WaitIdle();
        picture = file;
        pictureView = next;
        device.UpdateDescriptorSet(
            frameSet,
            [DescriptorWrite.Texture(forward.Effect.BindingOf("picture")!.Value.Binding, pictureView)]
        );
    }

    void Rebind(Pass pass, DescriptorSetHandle set) =>
        device.UpdateDescriptorSet(
            set,
            [DescriptorWrite.Texture(pass.Effect.BindingOf("environment")!.Value.Binding, environmentView)]
        );

    /// <summary>Renders one frame and copies it into <paramref name="destination" />.</summary>
    public void Render(OrbitCamera camera, int width, int height, Span<byte> destination) {
        if (geometry is not { } scene) {
            throw new InvalidOperationException("No mesh is open.");
        }

        if (targets is not { } current || current.Width != width || current.Height != height) {
            targets?.Dispose();
            post.Forget();
            current = targets = new(device, width, height);
        }

        var shading = Shading;
        WriteFrame(camera, shading, scene.Mesh.Bounds, (float)width / height);
        var stages = post.Plan(shading, width, height);
        if (shading.Wireframe) {
            scene.EnsureEdges();
        }

        device.BeginFrame();
        using (var commands = device.BeginCommandList(QueueKind.Graphics, "viewport")) {
            Record(commands, current, scene, shading.Wireframe, stages);
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
        foreach (var map in live.Values.SelectMany(static maps => maps)) {
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
        var ready = maps.Normal is not null
            || live.Values.Any(static worn => worn[(int)ViewportSlot.Normal] is not null)
            || mesh.WantsTangents
                ? mesh.WithTangents()
                : mesh;
        var next = ReferenceEquals(ready, geometry?.Mesh) ? geometry! : new Geometry(device, ready);
        textures.BeginUse();
        var sets = next.Bind(forward, (index, material) => Material(index, material, ready));
        if (!ReferenceEquals(next, geometry)) {
            geometry?.Dispose();
            geometry = next;
        }

        geometry.Use(sets);
        if (picture is not null) {
            // The picture on view is no material's, and must outlive the materials' sweep.
            textures.Of(picture, MapMeaning.Measurement);
        }

        textures.EndUse();
    }

    /// <summary>A material with the pictures laid over it, each slot read as what it means.</summary>
    MaterialBinding Material(int index, ViewportMaterial material, ViewportMesh mesh) {
        var own = material.Textures;
        var worn = Worn(index);

        var factors = material.Factors;
        var colour = Covered(worn, ViewportSlot.BaseColor, maps.BaseColor)
            ? new Vector4(1f, 1f, 1f, 1f)
            : factors.BaseColor;
        var normal = mesh.HasTangents
            ? Slot(worn, ViewportSlot.Normal, maps.Normal, own.Normal, MapMeaning.Normal, textures.Open)
            : textures.Open;
        var laidOver = Covered(worn, ViewportSlot.Normal, maps.Normal);
        var hasNormal = mesh.HasTangents && (laidOver || own.Normal is not null);

        return new(
            new(colour.X, colour.Y, colour.Z),
            Covered(worn, ViewportSlot.Metallic, maps.Metallic) ? 1f : factors.Metallic,
            Covered(worn, ViewportSlot.Roughness, maps.Roughness) ? 1f : factors.Roughness,
            laidOver ? 1f : own.NormalScale,
            hasNormal,
            // A stack's normal is what its graph fills made, and those run down as any graph's does (ADR 0011).
            worn[(int)ViewportSlot.Normal] is not null || (laidOver && maps.GreenDown) ? -1f : 1f,
            new(
                Slot(worn, ViewportSlot.BaseColor, maps.BaseColor, own.BaseColor, MapMeaning.Colour, textures.White),
                normal,
                Slot(
                    worn,
                    ViewportSlot.Roughness,
                    maps.Roughness,
                    own.MetallicRoughness,
                    MapMeaning.Measurement,
                    textures.Open
                ),
                Slot(
                    worn,
                    ViewportSlot.Metallic,
                    maps.Metallic,
                    own.MetallicRoughness,
                    MapMeaning.Measurement,
                    textures.Open
                ),
                Slot(
                    worn,
                    ViewportSlot.Occlusion,
                    maps.Occlusion,
                    own.Occlusion,
                    MapMeaning.Measurement,
                    textures.Open
                ),
                Slot(worn, ViewportSlot.Emissive, maps.Emissive, null, MapMeaning.Colour, textures.Black),
                Slot(worn, ViewportSlot.Opacity, maps.Opacity, null, MapMeaning.Measurement, textures.Open)
            ),
            mapSampler
        );
    }

    /// <summary>Whether a picture fills a slot, so the material's own factor for it stands down.</summary>
    static bool Covered(ViewportLiveMap?[] worn, ViewportSlot slot, ViewportMapFile? file) =>
        worn[(int)slot] is not null || file is not null;

    /// <summary>What a slot samples: live pixels, a picture laid over, the material's own image, or nothing.</summary>
    TextureViewHandle Slot(
        ViewportLiveMap?[] worn,
        ViewportSlot slot,
        ViewportMapFile? file,
        ModelTexture? image,
        MapMeaning meaning,
        TextureViewHandle empty
    ) =>
        worn[(int)slot] is { } map
            ? map.View
            : file is not null
                ? textures.Of(file, meaning)
                : image is not null
                    ? textures.Of(image, meshFolder ?? string.Empty, meaning)
                    : empty;

    static MapMeaning Meaning(ViewportSlot slot) =>
        slot switch {
            ViewportSlot.BaseColor or ViewportSlot.Emissive => MapMeaning.Colour,
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

    static Vector3 LightFrom(float rotation) {
        var (sin, cos) = MathF.SinCos(rotation);
        return new(0.6f * cos + 0.5f * sin, 0.7f, 0.5f * cos - 0.6f * sin);
    }

    void WriteFrame(OrbitCamera camera, ViewportShading shading, BoundingBox bounds, float aspectRatio) {
        var viewProjection = camera.ViewProjection(bounds, aspectRatio);
        var eye = camera.Eye(bounds, aspectRatio);
        var radius = Math.Max(bounds.Extent.Length(), 1e-4f);
        var light = Vector3.Normalize(LightFrom(shading.LightRotation));
        var lightView = ViewportShadows.LightViewProjection(bounds, light);
        shadows.Write(lightView);
        frameBlock.Set("viewProjection", viewProjection)
            .Set("cameraPosition", eye)
            .Set("lightDirection", light)
            .Set("lightViewProjection", lightView)
            .Set("shadowTexel", 1f / ViewportShadows.Size)
            .Set("shadowOffset", radius * 0.006f)
            .Set("shadowStrength", Math.Clamp(shading.Shadows, 0f, 1f))
            .Set("lightColor", new Vector3(1.2f, 1.15f, 1.1f))
            .Set("environmentYaw", shading.EnvironmentRotation)
            .Set("environmentMipCount", (float)environment.Prefiltered.LevelCount)
            .Set("view", (float)shading.View);
        WriteIrradiance();
        device.Write(frameConstants, 0, frameBlock.Bytes);

        Matrix4x4.Invert(camera.SkyViewProjection(aspectRatio, shading.EnvironmentRotation), out var inverse);
        // A view of stored numbers has no curve, so the sky would clip: it is dimmed to stay behind the model.
        var intensity = SkyIntensity * Math.Clamp(shading.BackgroundOpacity, 0f, 1f) * (shading.ShowsData ? 0.3f : 1f);
        skyBlock.Set("inverseViewProjection", inverse)
            .Set("environmentMipCount", (float)environment.Prefiltered.LevelCount)
            .Set("softness", Math.Clamp(shading.BackgroundBlur, 0f, 1f))
            .Set("intensity", intensity);
        device.Write(skyConstants, 0, skyBlock.Bytes);

        wireBlock.Set("viewProjection", viewProjection)
            .Set("cameraPosition", eye)
            .Set("lift", radius * WireLift)
            .Set("wireColor", new Vector4(0f, 0f, 0f, 0.55f));
        device.Write(wireConstants, 0, wireBlock.Bytes);
    }

    /// <summary>In <c>ShCoefficients</c>' order, which is the basis's: l00, then m from -l to l in each band.</summary>
    void WriteIrradiance() {
        ReadOnlySpan<string> names = ["l00", "l1m1", "l10", "l11", "l2m2", "l2m1", "l20", "l21", "l22"];
        var coefficients = environment.Irradiance.Coefficients;
        for (var i = 0; i < names.Length; i++) {
            frameBlock.Set(names[i], coefficients[i]);
        }
    }

    void Record(
        ICommandList commands,
        Targets target,
        Geometry scene,
        bool wireframe,
        IReadOnlyList<ViewportPost.Stage> stages
    ) {
        shadows.Record(commands, scene.Vertices, scene.Indices, scene.Mesh.Parts);
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
        commands.BeginRenderPass(
            new(
                [new(target.HdrSamplesView, LoadAction.Clear, StoreAction.Resolve, default, target.HdrView)],
                new DepthStencilAttachment(target.DepthView, LoadAction.Clear, StoreAction.DontCare),
                "viewport forward"
            )
        );
        ViewportShadows.Cover(commands, target.Width, target.Height);
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

        if (wireframe && scene.Edges is { } edges) {
            commands.BindPipeline(wire.Pipeline);
            commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, wireSet);
            commands.BindVertexBuffer(0, scene.Vertices);
            commands.BindIndexBuffer(edges, IndexFormat.UInt32);
            commands.DrawIndexed(scene.EdgeIndexCount);
        }

        commands.EndRenderPass();

        // Each stage reads the last picture and writes the other, so a frame needs two however many run.
        var (reading, readingView) = (target.Hdr, target.HdrView);
        for (var at = 0; at < stages.Count; at++) {
            var stage = stages[at];
            var (writing, writingView) =
                at % 2 == 0 ? (target.Output, target.OutputView) : (target.Spare, target.SpareView);
            commands.Barrier(
                new BarrierGroup(
                    [],
                    [
                        new(reading, ResourceState.ColourTarget, ResourceState.ShaderRead),
                        new(writing, ResourceState.Undefined, ResourceState.ColourTarget)
                    ]
                )
            );
            commands.BeginRenderPass(new([new(writingView, LoadAction.DontCare)], name: "viewport post"));
            ViewportShadows.Cover(commands, target.Width, target.Height);
            commands.BindPipeline(stage.Pipeline);
            commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, stage.Reading(readingView));
            commands.Draw(3);
            commands.EndRenderPass();
            (reading, readingView) = (writing, writingView);
        }

        commands.Barrier(new BarrierGroup([], [new(reading, ResourceState.ColourTarget, ResourceState.CopySource)]));
        commands.CopyTextureToBuffer(new(reading), new(target.Width, target.Height, 1), target.Readback, 0);
    }

    /// <summary>Lines, tested against the model's depth and not written to it, blended over what is there.</summary>
    Pass WirePipeline(EffectData data) {
        var effect = loader.Load(data);
        var location = (uint)effect.VertexInputs
            .First(static input => input.Name.EndsWith("position", StringComparison.Ordinal))
            .Location;
        var pipeline = device.CreateGraphicsPipeline(
            new(
                Module(effect, ShaderStage.Vertex),
                Module(effect, ShaderStage.Fragment),
                effect.Layout,
                [new(HdrFormat, BlendState.AlphaBlend)],
                [new(ViewportMesh.FloatsPerVertex * sizeof(float), [new(location, VertexFormat.Float32X3, 0)])],
                PrimitiveTopology.LineList,
                RasterizerState.TwoSided,
                DepthStencilState.TestOnly,
                DepthFormat,
                Samples,
                data.ShaderName
            )
        );
        cleanup.Add(() => device.Destroy(pipeline));
        return new(effect, pipeline);
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
        float NormalGreen,
        MaterialMaps Maps,
        SamplerHandle Sampler
    );

    /// <summary>One material's textures.</summary>
    readonly record struct MaterialMaps(
        TextureViewHandle BaseColor,
        TextureViewHandle Normal,
        TextureViewHandle Roughness,
        TextureViewHandle Metallic,
        TextureViewHandle Occlusion,
        TextureViewHandle Emissive,
        TextureViewHandle Opacity
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

        /// <summary>Each edge of the triangles once, as pairs of corners, or null until a wireframe asks.</summary>
        public BufferHandle? Edges { get; private set; }

        public int EdgeIndexCount { get; private set; }

        /// <summary>Finds the edges the first time a wireframe is drawn: a mesh that is never seen as one pays nothing.</summary>
        public void EnsureEdges() {
            if (Edges is not null) {
                return;
            }

            var indices = Mesh.Indices;
            HashSet<ulong> seen = [];
            List<uint> edges = [];
            for (var corner = 0; corner + 2 < indices.Length; corner += 3) {
                Add(indices[corner], indices[corner + 1]);
                Add(indices[corner + 1], indices[corner + 2]);
                Add(indices[corner + 2], indices[corner]);
            }

            uint[] pairs = edges.Count == 0 ? [0u, 0u] : [..edges];
            EdgeIndexCount = pairs.Length;
            Edges = Upload(pairs, BufferUsage.Index, "viewport edges");

            void Add(uint from, uint to) {
                var (low, high) = from < to ? (from, to) : (to, from);
                if (seen.Add(((ulong)low << 32) | high)) {
                    edges.Add(low);
                    edges.Add(high);
                }
            }
        }

        /// <summary>Makes a set for every material, for <see cref="Use" /> to swap in once it is all there.</summary>
        public List<(DescriptorSetHandle Set, BufferHandle Constants)> Bind(
            Pass forward,
            Func<int, ViewportMaterial, MaterialBinding> describe
        ) {
            var block = forward.Effect.BlockOf(DescriptorSetSlot.PerMaterial);
            List<(DescriptorSetHandle, BufferHandle)> made = [];
            for (var index = 0; index < Mesh.Materials.Length; index++) {
                made.Add(Material(forward, block, describe(index, Mesh.Materials[index])));
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
            if (Edges is { } edges) {
                device.Destroy(edges);
            }
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
                .Set("usesNormalMap", material.UsesNormalMap ? 1f : 0f)
                .Set("normalGreen", material.NormalGreen);
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
                    DescriptorWrite.Texture(Binding("emissiveMap"), material.Maps.Emissive),
                    DescriptorWrite.Texture(Binding("opacityMap"), material.Maps.Opacity),
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

        public Targets(VulkanDevice device, int width, int height) {
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
                    FrameBytes(width, height),
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
}
