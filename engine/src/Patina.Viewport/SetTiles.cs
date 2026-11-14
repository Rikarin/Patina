// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Geometry.Uv;

namespace Patina.Viewport;

/// <summary>
///     The UDIM tile each texture set is laid out on (#20). A set paints one tile, the one all its triangles sit in,
///     and its coordinates are moved home onto tile 1001 wherever the Worker reads them, so a stroke, a pick, a part's
///     mask and the 2D view all land in the set's own maps.
/// </summary>
/// <remarks>
///     ⚠ <b>A set on several tiles is not moved</b>, so the problems that say so still see it past tile 1001: one set
///     of maps cannot hold two tiles.
/// </remarks>
static class SetTiles {
    /// <summary>
    ///     Each material's tile across all of its parts, for those that sit on one tile that is not the first.
    /// </summary>
    /// <param name="parts">
    ///     Each part's material, triangle indices and coordinates, one per vertex, or none for a part with no layout.
    /// </param>
    public static Dictionary<string, UvTile> Of(
        IEnumerable<(string Material, int[] Indices, Vector2[] Coordinates)> parts
    ) {
        Dictionary<string, List<Vector2>> corners = new(StringComparer.Ordinal);
        HashSet<string> unlaid = new(StringComparer.Ordinal);
        foreach (var (material, indices, coordinates) in parts) {
            if (coordinates.Length == 0) {
                unlaid.Add(material);
                continue;
            }

            if (!corners.TryGetValue(material, out var list)) {
                corners[material] = list = [];
            }

            foreach (var index in indices) {
                list.Add(coordinates[index]);
            }
        }

        Dictionary<string, UvTile> away = new(StringComparer.Ordinal);
        foreach (var (material, list) in corners) {
            if (!unlaid.Contains(material) && UvTiles.Only([..list]) is { Udim: not null and not 1001 } tile) {
                away[material] = tile;
            }
        }

        return away;
    }
}
