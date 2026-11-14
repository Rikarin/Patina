// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Interchange;

namespace Patina.Viewport;

/// <summary>
///     What each material of the mesh binds: its own pictures, the ones laid over them, and the live maps a stack
///     paints, each slot read as what it means. Apart from the renderer so that what a material wears is in one place
///     and the frame in another (#282).
/// </summary>
sealed class ViewportMaterials(VulkanDevice device, ViewportTextures textures, SamplerHandle sampler) : IDisposable {
    /// <summary>Live maps by material; <see cref="ViewportRenderer.AllMaterials" /> paints the whole model.</summary>
    readonly Dictionary<int, ViewportLiveMap?[]> live = [];

    /// <summary>The pictures laid over the mesh's own materials.</summary>
    public ViewportMaps Maps { get; set; } = ViewportMaps.None;

    /// <summary>The folder the mesh was read from, which its materials' images are relative to.</summary>
    public string? MeshFolder { get; set; }

    /// <summary>Whether anything laid over the mesh is a normal map, which needs tangents.</summary>
    public bool WantsNormals =>
        Maps.Normal is not null || live.Values.Any(static worn => worn[(int)ViewportSlot.Normal] is not null);

    /// <summary>Writes a slot's live pixels.</summary>
    /// <returns>Whether the slot has a new texture, which the materials have to be bound to again.</returns>
    public bool SetLive(ViewportSlot slot, int width, int height, ReadOnlySpan<byte> pixels, int material) {
        var maps = LiveOf(material);
        var map = maps[(int)slot];
        var created = map is null || map.Width != width || map.Height != height;
        if (created) {
            map?.Dispose();
            map = maps[(int)slot] = new(device, Meaning(slot), width, height);
        }

        map!.Repair([new(new(0, 0, width, height), [..pixels])]);
        return created;
    }

    /// <summary>Rewrites only the rectangles that changed, with the mips above them.</summary>
    public void RepairLive(ViewportSlot slot, IReadOnlyList<ViewportRepair> repairs, int material) {
        ArgumentNullException.ThrowIfNull(repairs);
        (LiveOf(material)[(int)slot] ?? throw new InvalidOperationException($"{slot} has no live map.")).Repair(
            repairs
        );
    }

    /// <summary>Takes one slot's live map off a material.</summary>
    /// <returns>Whether it had one, so the materials have to be bound again.</returns>
    public bool ClearLive(ViewportSlot slot, int material) {
        var maps = LiveOf(material);
        if (maps[(int)slot] is not { } map) {
            return false;
        }

        device.WaitIdle();
        map.Dispose();
        maps[(int)slot] = null;
        return true;
    }

    /// <summary>Takes every live map off.</summary>
    /// <returns>Whether there was one, so the materials have to be bound again.</returns>
    public bool ClearLive() {
        var had = false;
        device.WaitIdle();
        foreach (var map in live.Values.SelectMany(static maps => maps).OfType<ViewportLiveMap>()) {
            had = true;
            map.Dispose();
        }

        live.Clear();
        return had;
    }

    /// <summary>The top level of a slot's live map, read back, or null when it has none.</summary>
    public byte[]? Sampled(ViewportSlot slot, int material) => LiveOf(material)[(int)slot]?.ReadBack();

    public void Dispose() {
        foreach (var map in live.Values.SelectMany(static maps => maps)) {
            map?.Dispose();
        }

        live.Clear();
    }

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
            : live.TryGetValue(ViewportRenderer.AllMaterials, out var whole)
                ? whole
                : new ViewportLiveMap?[Enum.GetValues<ViewportSlot>().Length];

    /// <summary>A material with the pictures laid over it, each slot read as what it means.</summary>
    public MaterialBinding Bind(int index, ViewportMaterial material, ViewportMesh mesh) {
        var own = material.Textures;
        var worn = Worn(index);

        var factors = material.Factors;
        var colour = Covered(worn, ViewportSlot.BaseColor, Maps.BaseColor)
            ? new Vector4(1f, 1f, 1f, 1f)
            : factors.BaseColor;
        var normal = mesh.HasTangents
            ? Slot(worn, ViewportSlot.Normal, Maps.Normal, own.Normal, MapMeaning.Normal, textures.Open)
            : textures.Open;
        var laidOver = Covered(worn, ViewportSlot.Normal, Maps.Normal);
        var hasNormal = mesh.HasTangents && (laidOver || own.Normal is not null);
        var colourMap = Slot(
            worn,
            ViewportSlot.BaseColor,
            Maps.BaseColor,
            own.BaseColor,
            MapMeaning.Colour,
            textures.White
        );

        return new(
            new(colour.X, colour.Y, colour.Z),
            Covered(worn, ViewportSlot.Metallic, Maps.Metallic) ? 1f : factors.Metallic,
            Covered(worn, ViewportSlot.Roughness, Maps.Roughness) ? 1f : factors.Roughness,
            laidOver ? 1f : own.NormalScale,
            hasNormal,
            // A stack's normal is what its graph fills made, and those run down as any graph's does (ADR 0011).
            worn[(int)ViewportSlot.Normal] is not null || (laidOver && Maps.GreenDown) ? -1f : 1f,
            new(
                colourMap,
                normal,
                Slot(
                    worn,
                    ViewportSlot.Roughness,
                    Maps.Roughness,
                    own.MetallicRoughness,
                    MapMeaning.Measurement,
                    textures.Open
                ),
                Slot(
                    worn,
                    ViewportSlot.Metallic,
                    Maps.Metallic,
                    own.MetallicRoughness,
                    MapMeaning.Measurement,
                    textures.Open
                ),
                Slot(
                    worn,
                    ViewportSlot.Occlusion,
                    Maps.Occlusion,
                    own.Occlusion,
                    MapMeaning.Measurement,
                    textures.Open
                ),
                Slot(worn, ViewportSlot.Emissive, Maps.Emissive, null, MapMeaning.Colour, textures.Black),
                Slot(worn, ViewportSlot.Opacity, Maps.Opacity, null, MapMeaning.Measurement, textures.Open),
                worn[(int)ViewportSlot.QuickMask]?.View ?? textures.Black,
                SizeOf(worn, colourMap)
            ),
            sampler
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
                    ? textures.Of(image, MeshFolder ?? string.Empty, meaning)
                    : empty;

    internal static MapMeaning Meaning(ViewportSlot slot) =>
        slot switch {
            ViewportSlot.BaseColor or ViewportSlot.Emissive => MapMeaning.Colour,
            ViewportSlot.Normal => MapMeaning.Normal,
            _ => MapMeaning.Measurement
        };

    /// <summary>What one material binds.</summary>
    internal readonly record struct MaterialBinding(
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
    // SK7005: one field for each texture the forward shader binds per material, which is what this record is.
#pragma warning disable SK7005
    internal readonly record struct MaterialMaps(
        TextureViewHandle BaseColor,
        TextureViewHandle Normal,
        TextureViewHandle Roughness,
        TextureViewHandle Metallic,
        TextureViewHandle Occlusion,
        TextureViewHandle Emissive,
        TextureViewHandle Opacity,
        TextureViewHandle QuickMask,
        float Side
    );
#pragma warning restore SK7005
}
