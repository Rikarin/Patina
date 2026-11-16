// SPDX-License-Identifier: GPL-3.0-or-later

namespace Patina.Viewport;

/// <summary>A picture on disk, and the hash that says which picture it is.</summary>
/// <param name="Path">The file.</param>
/// <param name="Hash">Its content, so that a file rewritten in place is read again and one that is not is not.</param>
public sealed record ViewportMapFile(string Path, string Hash);

/// <summary>
///     Pictures that replace what the mesh's own materials ask for, slot by slot, on every part: how a bake or a
///     graph's outputs are seen on a mesh. A slot left null keeps the material's own. Roughness is read from green,
///     metallic from blue and occlusion from red, so one packed file or three grey ones both work.
/// </summary>
public sealed record ViewportMaps(
    ViewportMapFile? BaseColor = null,
    ViewportMapFile? Normal = null,
    ViewportMapFile? Roughness = null,
    ViewportMapFile? Metallic = null,
    ViewportMapFile? Occlusion = null,
    ViewportMapFile? Emissive = null,
    ViewportMapFile? Opacity = null,
    bool GreenDown = false
) {
    /// <summary>Nothing replaced.</summary>
    public static ViewportMaps None { get; } = new();

    /// <summary>OpenPBR's coat, sheen and transmission laid over the material: a graph's or a bake's (#310).</summary>
    public ViewportLayerFiles Layers { get; init; } = ViewportLayerFiles.None;
}

/// <summary>
///     OpenPBR's layers over the base as pictures, each read from its red but the sheen's colour, which is linear (ADR
///     0050). A slot left null draws as OpenPBR's default, as a stack that does not paint it does.
/// </summary>
public sealed record ViewportLayerFiles(
    ViewportMapFile? Coat = null,
    ViewportMapFile? CoatRoughness = null,
    ViewportMapFile? Sheen = null,
    ViewportMapFile? SheenColor = null,
    ViewportMapFile? SheenRoughness = null,
    ViewportMapFile? Transmission = null
) {
    /// <summary>No layer laid over.</summary>
    public static ViewportLayerFiles None { get; } = new();
}
