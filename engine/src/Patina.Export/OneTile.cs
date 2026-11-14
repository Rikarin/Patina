using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Geometry.Uv;

namespace Patina.Export;

/// <summary>
///     A texture set paints one UV tile (#20): the one all its triangles sit in, which need not be 1001. A set's
///     layout is moved home onto 1001 wherever it is read, so a bake, a stroke and an export's padding land in the
///     set's own maps and the export names the tile. A set reaching several tiles is said out loud and refused,
///     because one set of maps cannot hold two tiles: a bake would leave the others empty, a stroke on them would land
///     nowhere, and the viewport wraps the first tile's maps onto them so it all looks right until the export.
/// </summary>
public static class OneTile {
    /// <summary>Why a layout cannot be worked on, or null when it is all in the first tile.</summary>
    /// <param name="corners">Three coordinates per triangle, as a reader hands them and after any move home.</param>
    /// <param name="what">What is laid out, as a sentence names it: a file, or a texture set.</param>
    /// <returns>A sentence naming every tile it reaches, or null.</returns>
    public static string? Problem(ReadOnlySpan<Vector2> corners, string what) {
        var tiles = UvTiles.Reached(corners);
        if (tiles.All(static tile => tile == default)) {
            return null;
        }

        return $"{what} is laid out on UV tiles {string.Join(", ", tiles)}, and a texture set paints one UDIM tile: "
            + "a stroke past its first paints nothing, a bake is refused, and the viewport shows the first tile's "
            + "maps on every tile. Give each tile a material of its own, move the set's islands onto one tile, or "
            + "export it with no UVs and let Auto Unwrap lay it out.";
    }

    /// <summary>The tile a layout is moved home from, or null when it stays where it is.</summary>
    /// <param name="corners">Three coordinates per triangle.</param>
    /// <returns>Its one tile, when that is not 1001 and UDIM has a number for it.</returns>
    public static UvTile? Away(ReadOnlySpan<Vector2> corners) =>
        UvTiles.Only(corners) is { Udim: not null and not First } tile ? tile : null;

    /// <summary>The first tile, which a set is on until its layout says otherwise.</summary>
    public const int First = 1001;

    /// <summary>The UDIM number of the one tile a layout sits on, or 1001 for none or several.</summary>
    /// <param name="corners">Three coordinates per triangle.</param>
    public static int Udim(ReadOnlySpan<Vector2> corners) => UvTiles.Only(corners)?.Udim ?? First;

    /// <summary>A mesh's layout, each face fanned into triangles, three corners at a time.</summary>
    /// <param name="mesh">The mesh, with one coordinate per corner.</param>
    /// <returns>The triangles' coordinates, or none for a mesh without a layout.</returns>
    public static Vector2[] Triangles(EditMesh mesh) {
        ArgumentNullException.ThrowIfNull(mesh);
        var corners = mesh.TexCoords;
        if (corners.Length == 0 || corners.Length != mesh.CornerCount) {
            return [];
        }

        List<Vector2> triangles = [];
        foreach (var face in mesh.Faces) {
            for (var corner = 1; corner + 1 < face.Count; corner++) {
                triangles.Add(corners[face.Start]);
                triangles.Add(corners[face.Start + corner]);
                triangles.Add(corners[face.Start + corner + 1]);
            }
        }

        return [..triangles];
    }

    /// <summary>Moves a mesh's layout home onto tile 1001 when it sits on one other tile.</summary>
    /// <param name="mesh">The mesh, with one coordinate per corner.</param>
    /// <returns>The tile it is on: 1001 when it did not move, or when it reaches several.</returns>
    public static int Home(EditMesh mesh) {
        var triangles = Triangles(mesh);
        if (Away(triangles) is not { } tile) {
            return Udim(triangles);
        }

        var moved = new Vector2[mesh.CornerCount];
        var corners = mesh.TexCoords;
        for (var at = 0; at < moved.Length; at++) {
            moved[at] = tile.Home(corners[at]);
        }

        mesh.SetTexCoords(moved);
        return tile.Udim!.Value;
    }

    /// <summary>The UDIM number as Mari and some importers name a tile: <c>u2_v1</c> for 1002.</summary>
    /// <param name="udim">The tile's number, from 1001.</param>
    public static string MariName(int udim) {
        var (row, column) = Math.DivRem(Math.Max(udim - First, 0), 10);
        return FormattableString.Invariant($"u{column + 1}_v{row + 1}");
    }
}
