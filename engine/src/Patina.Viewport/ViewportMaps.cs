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
}
