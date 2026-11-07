using Vixen.Core.Mathematics;
using Vixen.Geometry.Uv;

namespace Patina.Export;

/// <summary>
///     Patina paints, bakes and exports one UV tile, 1001, until UDIM lands (#20). A layout past it is said out loud and
///     refused, because a bake would leave the other tiles empty, a stroke on them would land nowhere, and the viewport
///     wraps the first tile's maps onto them so it all looks right until the export.
/// </summary>
public static class OneTile {
    /// <summary>Why a layout cannot be worked on, or null when it is all in the first tile.</summary>
    /// <param name="corners">Three coordinates per triangle, as a reader hands them.</param>
    /// <param name="what">What is laid out, as a sentence names it: a file, or a texture set.</param>
    /// <returns>A sentence naming every tile it reaches, or null.</returns>
    public static string? Problem(ReadOnlySpan<Vector2> corners, string what) {
        var tiles = UvTiles.Reached(corners);
        if (tiles.All(static tile => tile == default)) {
            return null;
        }

        return $"{what} is laid out on UV tiles {string.Join(", ", tiles)}, and Patina paints, bakes and exports "
            + "only UDIM tile 1001 so far: a stroke on another tile paints nothing, a bake is refused, and the viewport "
            + "shows tile 1001's maps on every tile. Move its islands inside 0 to 1, or export it with no UVs and let "
            + "Auto Unwrap lay it out.";
    }
}
