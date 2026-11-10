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
    const PixelFormat HdrFormat = ViewportTargets.HdrFormat;
    const PixelFormat DepthFormat = ViewportTargets.DepthFormat;
    const int Samples = ViewportTargets.Samples;

    /// <summary>How bright the background is at full opacity, against the light it gives the model.</summary>
    const float SkyIntensity = 0.55f;

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
    readonly SamplerHandle sampler;
    readonly SamplerHandle mapSampler;
    readonly ViewportTextures textures;
    TextureViewHandle environmentView;
    TextureViewHandle pictureView;
    ViewportMapFile? picture;
    string[] isolatedParts = [];
    string[] isolatedMaterials = [];
    readonly ViewportShadows shadows;
    readonly ViewportPost post;
    readonly BufferHandle frameConstants;
    readonly BufferHandle skyConstants;
    readonly DescriptorSetHandle frameSet;
    readonly DescriptorSetHandle skySet;
    readonly ConstantBlock frameBlock;
    readonly ConstantBlock skyBlock;
    readonly ViewportWire wire;
    Geometry? geometry;
    ViewportTargets? targets;
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
        // The sky is added to the background colour the frame is cleared to, each scaled by its share.
        sky = Pipeline(
            shaders.Sky(),
            [new(HdrFormat, ViewportTargets.Added)],
            Samples,
            DepthStencilState.Disabled,
            DepthFormat,
            false
        );
        var wireEffect = loader.Load(shaders.Wire());
        var gridEffect = loader.Load(shaders.Grid());
        var overlayEffect = loader.Load(shaders.Overlay());
        wire = new(
            device,
            (wireEffect, Module(wireEffect, ShaderStage.Vertex), Module(wireEffect, ShaderStage.Fragment)),
            (gridEffect, Module(gridEffect, ShaderStage.Vertex), Module(gridEffect, ShaderStage.Fragment)),
            (overlayEffect, Module(overlayEffect, ShaderStage.Vertex), Module(overlayEffect, ShaderStage.Fragment)),
            HdrFormat,
            DepthFormat,
            Samples
        );
        cleanup.Add(wire.Dispose);
        post = new(
            device,
            shaders,
            (data, format) => {
                var pass = Pipeline(
                    data,
                    [new(format, BlendState.Opaque)],
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
        frameConstants = Constants(frameBlock.Bytes.Length, "viewport frame");
        skyConstants = Constants(skyBlock.Bytes.Length, "viewport sky");
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
    }

    /// <summary>How the model is lit, shown and finished, from the next frame on.</summary>
    public ViewportShading Shading { get; set; } = new();

    /// <summary>Lays shapes over the model from the next frame on, such as a bake's preview, or none.</summary>
    /// <param name="triangles">See-through triangles, three vertices each of position, colour and opacity.</param>
    /// <param name="lines">Lines, two vertices each, laid out the same.</param>
    public void SetOverlay(float[] triangles, float[] lines) => wire.SetOverlay(triangles, lines);

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

    /// <summary>Replaces the mesh, each material drawn in the UV set its texture set is laid out in (#210).</summary>
    /// <exception cref="ModelFormatException">The file is not a readable model, or a part lacks its set.</exception>
    /// <exception cref="InvalidDataException">A material's image is not one this reads.</exception>
    /// <exception cref="IOException">A material's image cannot be read.</exception>
    public void OpenMesh(string path, Dictionary<string, int>? uvSets = null) {
        var mesh = ViewportMesh.Read(path, uvSets);
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

    /// <summary>Lights the model with a built-in room by name, or an <c>.hdr</c> or <c>.exr</c> panorama.</summary>
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

    /// <summary>
    ///     Draws only these named meshes and materials, and no shadow from the rest (#136). Both empty draws the whole
    ///     model; a name the model does not have draws nothing of its own.
    /// </summary>
    public void Isolate(IReadOnlyList<string> parts, IReadOnlyList<string> materials) {
        isolatedParts = [..parts];
        isolatedMaterials = [..materials];
    }

    /// <summary>The parts a frame draws: all of them, or those the isolation keeps.</summary>
    ViewportPart[] Shown(ViewportMesh mesh) => mesh.Shown(isolatedParts, isolatedMaterials);

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
        post.Plan(shading, current);

        if (shading.Wireframe) {
            scene.EnsureEdges();
        }

        device.BeginFrame();
        using (var commands = device.BeginCommandList(QueueKind.Graphics, "viewport")) {
            Record(commands, current, scene, shading);
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
        var colourMap = Slot(
            worn,
            ViewportSlot.BaseColor,
            maps.BaseColor,
            own.BaseColor,
            MapMeaning.Colour,
            textures.White
        );

        return new(
            new(colour.X, colour.Y, colour.Z),
            Covered(worn, ViewportSlot.Metallic, maps.Metallic) ? 1f : factors.Metallic,
            Covered(worn, ViewportSlot.Roughness, maps.Roughness) ? 1f : factors.Roughness,
            laidOver ? 1f : own.NormalScale,
            hasNormal,
            // A stack's normal is what its graph fills made, and those run down as any graph's does (ADR 0011).
            worn[(int)ViewportSlot.Normal] is not null || (laidOver && maps.GreenDown) ? -1f : 1f,
            new(
                colourMap,
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
                Slot(worn, ViewportSlot.Opacity, maps.Opacity, null, MapMeaning.Measurement, textures.Open),
                SizeOf(worn, colourMap)
            ),
            mapSampler
        );
    }

    /// <summary>
    ///     The side a material is painted at, which its texel density is measured in: a stack's live maps, or its base
    ///     colour's picture, or zero where it wears neither and the frame's resolution stands in.
    /// </summary>
    float SizeOf(ViewportLiveMap?[] worn, TextureViewHandle colour) =>
        worn.FirstOrDefault(static map => map is not null) is { } live ? live.Width : textures.WidthOf(colour);

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
            .Set("view", shading.ViewNumber)
            .Set("pictureLane", (float)Math.Clamp(shading.PictureLane, 0, 4))
            .Set("pictureScale", shading.PictureScale)
            .Set("densityTarget", Math.Max(shading.DensityTarget, 0f))
            .Set("densityResolution", Math.Max(shading.DensityResolution, 1f));
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

        wire.Write(viewProjection, eye, radius);
        if (shading.Grid) {
            wire.WriteGrid(viewProjection, bounds);
        }
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
        ViewportTargets target,
        Geometry scene,
        ViewportShading shading
    ) {
        var shown = Shown(scene.Mesh);
        shadows.Record(commands, scene.Vertices, scene.Indices, shown);
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
        var behind = shading.BackgroundColour * (1f - Math.Clamp(shading.BackgroundOpacity, 0f, 1f));
        commands.BeginRenderPass(
            new(
                [target.Cleared(behind)],
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
        foreach (var part in shown) {
            commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, scene.SetOf(part));
            commands.DrawIndexed(part.IndexCount, firstIndex: part.FirstIndex);
        }

        if (shading.Wireframe && scene.Edges is { } edges) {
            wire.Record(commands, scene.Vertices, edges, scene.EdgeIndexCount);
        }

        if (shading.Grid) {
            wire.RecordGrid(commands);
        }

        wire.RecordOverlay(commands);

        commands.EndRenderPass();

        var reading = post.Record(commands, target);
        commands.Barrier(new BarrierGroup([], [new(reading, ResourceState.ColourTarget, ResourceState.CopySource)]));
        commands.CopyTextureToBuffer(new(reading), new(target.Width, target.Height, 1), target.Readback, 0);
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
                hasVertices ? [ViewportMesh.Layout(effect)] : [],
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

    /// <summary>One material's textures, and the side they are painted at, which its texel density is in.</summary>
    readonly record struct MaterialMaps(
        TextureViewHandle BaseColor,
        TextureViewHandle Normal,
        TextureViewHandle Roughness,
        TextureViewHandle Metallic,
        TextureViewHandle Occlusion,
        TextureViewHandle Emissive,
        TextureViewHandle Opacity,
        float Side
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

        /// <summary>Finds the edges the first time a wireframe is drawn: a mesh never seen so pays nothing.</summary>
        public void EnsureEdges() {
            if (Edges is not null) {
                return;
            }

            var pairs = Mesh.Edges();
            EdgeIndexCount = pairs.Length;
            Edges = Upload(pairs, BufferUsage.Index, "viewport edges");
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
                .Set("normalGreen", material.NormalGreen)
                .Set("mapSize", material.Maps.Side);
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
}
