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
    (string[] Parts, string[] Materials) marked = ([], []);

    /// <summary>The mesh the marks were last made for, so a new one, or a new isolation, makes them again.</summary>
    ViewportMesh? markedOn;

    /// <summary>A symmetry, whose lines across the model are drawn with the marks (#132), or null.</summary>
    ViewportMirror? mirror;

    readonly ViewportShadows shadows;
    readonly ViewportPost post;
    readonly ViewportTint tint;
    readonly ViewportShaders shaders;

    /// <summary>The depth of field, made the first time a frame asks for it.</summary>
    ViewportFocus? focus;

    readonly BufferHandle frameConstants;
    readonly BufferHandle skyConstants;
    readonly DescriptorSetHandle frameSet;
    readonly DescriptorSetHandle skySet;
    readonly ConstantBlock frameBlock;
    readonly ConstantBlock skyBlock;
    readonly ViewportWire wire;
    Geometry? geometry;
    ViewportTargets? targets;

    /// <summary>The key of a live map on the whole model.</summary>
    public const int AllMaterials = -1;

    /// <summary>What each material binds: the pictures laid over the mesh and the live maps a stack paints.</summary>
    readonly ViewportMaterials materials;

    ViewportRenderer(VulkanDevice device, ShaderCache cache) {
        this.device = device;
        loader = new(device);
        shaders = new(cache);
        sampler = device.CreateSampler(SamplerDescription.LinearClamp);
        cleanup.Add(() => device.Destroy(sampler));
        mapSampler = device.CreateSampler(
            SamplerDescription.LinearRepeat with { Anisotropy = 8f, Name = "viewport maps" }
        );
        cleanup.Add(() => device.Destroy(mapSampler));
        textures = new(device);
        cleanup.Add(textures.Dispose);
        materials = new(device, textures, mapSampler);
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

        // The model writes its light and its quick mask's tint (#286).
        ColourTargetState[] both = [new(HdrFormat, BlendState.Opaque), new(ViewportTargets.TintFormat, BlendState.Opaque)];
        forward = Pipeline(shaders.Forward(), both, Samples, DepthStencilState.Default, DepthFormat, true);
        // The sky is added to the background colour the frame is cleared to, each scaled by its share.
        sky = Pipeline(
            shaders.Sky(),
            ViewportTargets.Light(ViewportTargets.Added),
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
            DepthFormat,
            Samples
        );
        cleanup.Add(wire.Dispose);
        var tinting = Pipeline(
            shaders.Tint(),
            ViewportTint.Targets,
            1,
            DepthStencilState.Disabled,
            PixelFormat.Undefined,
            false
        );
        tint = new(device, (tinting.Effect, tinting.Pipeline));
        cleanup.Add(tint.Dispose);
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

    /// <summary>
    ///     Whether the lit view draws the model in clay, so that shapes laid over it, such as a bake's preview, read
    ///     against plain grey (#149). Every other view is left as it is asked for.
    /// </summary>
    public bool Neutral { get; set; }

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
        materials.Maps = ViewportMaps.None;
        materials.MeshFolder = Path.GetDirectoryName(Path.GetFullPath(path));
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

        materials.Maps = replacement;
        Show(scene.Mesh);
    }

    /// <summary>
    ///     Replaces a slot's picture with live pixels, the bytes an export writes: RGBA, tightly packed, top row first.
    ///     They win over any file, and stay until <see cref="ClearLive()" /> or another mesh's stack replaces them.
    /// </summary>
    public void SetLive(
        ViewportSlot slot,
        int width,
        int height,
        ReadOnlySpan<byte> pixels,
        int material = AllMaterials
    ) {
        if (materials.SetLive(slot, width, height, pixels, material) && geometry is { } scene) {
            Show(scene.Mesh);
        }
    }

    /// <summary>Rewrites only the rectangles that changed, with the mips above them. Nothing rebinds.</summary>
    public void RepairLive(ViewportSlot slot, IReadOnlyList<ViewportRepair> repairs, int material = AllMaterials) =>
        materials.RepairLive(slot, repairs, material);

    /// <summary>
    ///     Takes one slot's live map off a material, for a stack that no longer writes it: a channel removed, or a bake
    ///     no longer beneath the set (#86). What the slot shows is then whatever it would without the stack.
    /// </summary>
    public void ClearLive(ViewportSlot slot, int material = AllMaterials) {
        if (materials.ClearLive(slot, material) && geometry is { } scene) {
            Show(scene.Mesh);
        }
    }

    /// <summary>Takes the live maps off.</summary>
    public void ClearLive() {
        if (materials.ClearLive() && geometry is { } scene) {
            Show(scene.Mesh);
        }
    }

    /// <summary>
    ///     The top level of the texture a slot's draw call samples, read back from the GPU: RGBA, tightly packed, top
    ///     row first. Null when the slot has no live map. This is the picture before lighting and tonemapping, which
    ///     is the one that can equal an exported file.
    /// </summary>
    public byte[]? Sampled(ViewportSlot slot, int material = AllMaterials) => materials.Sampled(slot, material);

    /// <summary>Which material a texture set paints, by name, or every material for a name that is none here.</summary>
    public int MaterialOf(string name) => geometry?.Mesh.MaterialOf(name) is >= 0 and var at ? at : AllMaterials;

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
        if (isolatedParts.SequenceEqual(parts) && isolatedMaterials.SequenceEqual(materials)) {
            return;
        }

        isolatedParts = [..parts];
        isolatedMaterials = [..materials];
        Remark(true);
    }

    /// <summary>
    ///     Marks these named meshes and materials on the model (#136), of what is drawn; both empty marks nothing.
    /// </summary>
    public void Highlight(IReadOnlyList<string> parts, IReadOnlyList<string> materials) {
        if (marked.Parts.SequenceEqual(parts) && marked.Materials.SequenceEqual(materials)) {
            return;
        }

        marked = ([..parts], [..materials]);
        Remark(true);
    }

    /// <summary>
    ///     Draws where a symmetry's mirror, or the half-planes between a radial symmetry's copies, meet the model (#132),
    ///     of what is drawn, or nothing for null. It stands against the whole model's box, as a stroke's does, so it is
    ///     found again on each mesh opened.
    /// </summary>
    /// <param name="next">The symmetry, or null.</param>
    public void Mirror(ViewportMirror? next) {
        if (mirror == next) {
            return;
        }

        mirror = next;
        Remark(true);
    }

    /// <summary>Makes the marks again for the mesh drawn, when it or what they mark has changed.</summary>
    void Remark(bool changed) {
        var mesh = geometry?.Mesh;
        if (!changed && ReferenceEquals(mesh, markedOn)) {
            return;
        }

        markedOn = mesh;
        if (mesh is null) {
            wire.SetHighlight(ViewportShapes.None);
            return;
        }

        wire.SetHighlight(ViewportMarks.Shapes(mesh, marked, (isolatedParts, isolatedMaterials), mirror));
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
            tint.Forget();
            focus?.Forget();
            current = targets = new(device, width, height);
        }

        var shading = Shading;
        WriteFrame(camera, shading, scene.Mesh.Bounds, (float)width / height);
        post.Plan(shading, current);
        var lens = Defocuses(shading) ? Focus() : null;
        lens?.Plan(current, camera, scene.Mesh.Bounds, shading.DepthOfField, shading.Focus);

        if (shading.Wireframe) {
            scene.EnsureEdges();
        }

        device.BeginFrame();
        using (var commands = device.BeginCommandList(QueueKind.Graphics, "viewport")) {
            Record(commands, current, scene, shading, lens);
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
        focus?.Dispose();
        geometry?.Dispose();
        materials.Dispose();

        foreach (var action in Enumerable.Reverse(cleanup)) {
            action();
        }

        loader.Release();
        device.Dispose();
    }

    /// <summary>Puts a mesh on the GPU with its materials as they stand, dropping textures no longer used.</summary>
    void Show(ViewportMesh mesh) {
        var ready = materials.WantsNormals || mesh.WantsTangents
            ? mesh.WithTangents()
            : mesh;
        var next = ReferenceEquals(ready, geometry?.Mesh) ? geometry! : new Geometry(device, ready);
        textures.BeginUse();
        var sets = next.Bind(forward, (index, material) => materials.Bind(index, material, ready));
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
        Remark(false);
    }

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
            .Set("view", shading.ViewNumberFor(Neutral))
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
        ViewportShading shading,
        ViewportFocus? lens
    ) {
        var shown = Shown(scene.Mesh);
        shadows.Record(commands, scene.Vertices, scene.Indices, shown);
        commands.Barrier(
            new BarrierGroup(
                [],
                [
                    new(target.HdrSamples, ResourceState.Undefined, ResourceState.ColourTarget),
                    ..lens?.Resolving() ?? [new(target.Hdr, ResourceState.Undefined, ResourceState.ColourTarget)],
                    new(target.Depth, ResourceState.Undefined, ResourceState.DepthStencilWrite),
                    new(target.TintSamples, ResourceState.Undefined, ResourceState.ColourTarget),
                    new(target.Tint, ResourceState.Undefined, ResourceState.ColourTarget)
                ]
            )
        );
        var behind = shading.BackgroundColour * (1f - Math.Clamp(shading.BackgroundOpacity, 0f, 1f));
        ViewportFocus.BeginForward(commands, target, behind, lens);
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

        lens?.Record(commands, target);
        var (reading, view) = post.Record(commands, target);
        tint.Record(commands, target, view);
        commands.Barrier(new BarrierGroup([], [new(reading, ResourceState.ColourTarget, ResourceState.CopySource)]));
        commands.CopyTextureToBuffer(new(reading), new(target.Width, target.Height, 1), target.Readback, 0);
    }

    /// <summary>Whether a frame is defocused: a lens opened at all, on a picture and not a stored value.</summary>
    static bool Defocuses(ViewportShading shading) => shading.DepthOfField > 0f && !shading.ShowsData;

    ViewportFocus Focus() {
        if (focus is null) {
            var pass = Pipeline(
                shaders.DepthOfField(),
                [new(HdrFormat, BlendState.Opaque)],
                1,
                DepthStencilState.Disabled,
                PixelFormat.Undefined,
                false
            );
            focus = new(device, (pass.Effect, pass.Pipeline));
        }

        return focus;
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
            Func<int, ViewportMaterial, ViewportMaterials.MaterialBinding> describe
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

        (DescriptorSetHandle, BufferHandle) Material(
            Pass forward,
            EffectBlock block,
            ViewportMaterials.MaterialBinding material
        ) {
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
                    DescriptorWrite.Texture(Binding("quickMaskMap"), material.Maps.QuickMask),
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
