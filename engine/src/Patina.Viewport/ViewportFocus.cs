// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     Depth of field, through Vixen's thin-lens gather (#138). The forward pass resolves its light into
///     <see cref="UnfocusedView" /> and its nearest depth into <see cref="DepthView" />, and this draws the light
///     defocused into the frame's own HDR picture, which the bloom and the curve read as they would with none. It runs
///     before them, since a lens spreads light and not the curve's grey.
/// </summary>
/// <remarks>
///     <para>
///         ⚠ <b>The lens is one for a model of five centimetres</b> (<see cref="ApparentRadius" />): Vixen's pass is
///         physical, a model has no unit the viewport trusts, and a real lens on a model a metre wide at the distance
///         that frames it has a depth of field deeper than the model. So the sensor is scaled with the model, and the
///         blur at a setting is the same whatever the model's size: that of a 36 mm sensor on something small, which
///         is the shot an artist wants of a material.
///     </para>
///     <para>
///         ⚠ <b>Orthographic has next to none.</b> It is drawn from far off through half a degree, where front and
///         back are nearly the same distance, which is what a lens would do too.
///     </para>
/// </remarks>
sealed class ViewportFocus : IDisposable {
    /// <summary>The size a model's bounding radius is taken to have, in metres.</summary>
    const float ApparentRadius = 0.05f;

    /// <summary>A full-frame sensor's width, in metres.</summary>
    const float Sensor = 0.036f;

    /// <summary>The widest a defocused pixel spreads, in pixels: a ceiling on the cost as much as on the look.</summary>
    const float Widest = 10f;

    /// <summary>The f-number at full strength.</summary>
    const float WidestAperture = 1.4f;

    readonly VulkanDevice device;
    readonly Effect effect;
    readonly PipelineHandle pipeline;
    readonly SamplerHandle linear;
    readonly SamplerHandle point;
    readonly ConstantBlock block;
    readonly BufferHandle constants;
    readonly DescriptorSetHandle set;
    (int Width, int Height) size;
    TextureHandle unfocused;
    TextureHandle depth;

    public ViewportFocus(VulkanDevice device, (Effect Effect, PipelineHandle Pipeline) pass) {
        this.device = device;
        (effect, pipeline) = pass;
        linear = device.CreateSampler(SamplerDescription.LinearClamp);
        point = device.CreateSampler(SamplerDescription.PointClamp);
        var layout = effect.BlockOf(DescriptorSetSlot.PerMaterial);
        block = new(layout);
        constants = device.CreateBuffer(
            new(block.Bytes.Length, BufferUsage.Uniform, MemoryAccess.HostUpload, "viewport focus")
        );
        set = device.CreateDescriptorSet(effect.SetLayouts[(int)DescriptorSetSlot.PerMaterial], "viewport focus");
        device.UpdateDescriptorSet(
            set,
            [
                DescriptorWrite.Uniform(layout.Binding, constants),
                DescriptorWrite.SamplerAt(effect.BindingOf("sourceSampler")!.Value.Binding, linear),
                DescriptorWrite.SamplerAt(effect.BindingOf("pointSampler")!.Value.Binding, point)
            ]
        );
    }

    /// <summary>Where the forward pass resolves its light when the frame is defocused.</summary>
    public TextureViewHandle UnfocusedView { get; private set; }

    /// <summary>Where the forward pass resolves its nearest depth when the frame is defocused.</summary>
    public TextureViewHandle DepthView { get; private set; }

    /// <summary>
    ///     The f-number a strength from 0 to 1 opens the lens to: f/1.4 at one, closing two stops for each quarter of
    ///     the way down, to f/22 at zero.
    /// </summary>
    public static float Aperture(float strength) =>
        WidestAperture * MathF.Pow(2f, 4f * (1f - Math.Clamp(strength, 0f, 1f)));

    /// <summary>
    ///     Writes the lens for a frame and makes the pictures for its size: focused on the point the camera circles,
    ///     moved by <paramref name="focus" /> bounding radii away from the eye, opened by
    ///     <paramref name="strength" />.
    /// </summary>
    public void Plan(
        ViewportTargets target,
        OrbitCamera camera,
        BoundingBox bounds,
        float strength,
        float focus
    ) {
        if (size != (target.Width, target.Height)) {
            Build(target.Width, target.Height);
        }

        var aspect = (float)target.Width / target.Height;
        var radius = Math.Max(bounds.Extent.Length(), 1e-4f);
        var (near, far) = camera.Planes(bounds, aspect);
        var reach = camera.Reach(bounds, aspect);
        // The sensor is as wide against the model as a full frame's is against something five centimetres round.
        var sensor = Sensor * radius / ApparentRadius;
        var across = 2f * MathF.Atan(aspect * MathF.Tan(camera.Lens * 0.5f));
        var focal = sensor * 0.5f / MathF.Tan(across * 0.5f);
        block.Set("texelSize", new Vector2(1f / target.Width, 1f / target.Height))
            .Set("focusDistance", Math.Clamp(reach + focus * radius, near * 1.01f, far))
            .Set("focalLength", focal)
            .Set("aperture", Aperture(strength))
            .Set("sensorWidth", sensor)
            .Set("maxRadius", Widest)
            .Set("bladeCount", 6f)
            .Set("nearPlane", near)
            .Set("farPlane", far);
        device.Write(constants, 0, block.Bytes);
    }

    /// <summary>Draws the defocused light into the frame's HDR picture, after the forward pass has resolved.</summary>
    public void Record(ICommandList commands, ViewportTargets target) {
        commands.Barrier(
            new BarrierGroup(
                [],
                [
                    new(unfocused, ResourceState.ColourTarget, ResourceState.ShaderRead),
                    new(depth, ResourceState.DepthStencilWrite, ResourceState.ShaderRead),
                    new(target.Hdr, ResourceState.Undefined, ResourceState.ColourTarget)
                ]
            )
        );
        commands.BeginRenderPass(new([new(target.HdrView, LoadAction.DontCare)], name: "viewport focus"));
        ViewportShadows.Cover(commands, target.Width, target.Height);
        commands.BindPipeline(pipeline);
        commands.BindDescriptorSet(DescriptorSetSlot.PerMaterial, set);
        commands.Draw(3);
        commands.EndRenderPass();
    }

    /// <summary>The barriers that make the pictures ready for the forward pass to resolve into.</summary>
    public IEnumerable<TextureBarrier> Resolving() =>
        [
            new(unfocused, ResourceState.Undefined, ResourceState.ColourTarget),
            new(depth, ResourceState.Undefined, ResourceState.DepthStencilWrite)
        ];

    /// <summary>The depth attachment for a defocused frame: tested as ever, its nearest sample kept.</summary>
    public DepthStencilAttachment Depth(TextureViewHandle samples) =>
        new(
            samples,
            LoadAction.Clear,
            StoreAction.Resolve,
            ResolveView: DepthView,
            ResolveMode: device.Features.ClampDepthResolveMode(DepthResolveMode.Max)
        );

    /// <summary>Lets go of the pictures, which are the old frame's size.</summary>
    public void Forget() {
        if (size == default) {
            return;
        }

        device.WaitIdle();
        device.Destroy(UnfocusedView);
        device.Destroy(DepthView);
        device.Destroy(unfocused);
        device.Destroy(depth);
        size = default;
    }

    public void Dispose() {
        Forget();
        device.Destroy(set);
        device.Destroy(constants);
        device.Destroy(point);
        device.Destroy(linear);
    }

    void Build(int width, int height) {
        Forget();
        size = (width, height);
        unfocused = device.CreateTexture(
            new(
                ViewportTargets.HdrFormat,
                width,
                height,
                TextureUsage.ColourTarget | TextureUsage.Sampled,
                Name: "viewport unfocused"
            )
        );
        depth = device.CreateTexture(
            new(
                ViewportTargets.DepthFormat,
                width,
                height,
                TextureUsage.DepthStencilTarget | TextureUsage.Sampled,
                Name: "viewport focus depth"
            )
        );
        UnfocusedView = device.CreateTextureView(unfocused);
        DepthView = device.CreateTextureView(depth);
        device.UpdateDescriptorSet(
            set,
            [
                DescriptorWrite.Texture(effect.BindingOf("source")!.Value.Binding, UnfocusedView),
                DescriptorWrite.Texture(effect.BindingOf("depthBuffer")!.Value.Binding, DepthView)
            ]
        );
    }
}
