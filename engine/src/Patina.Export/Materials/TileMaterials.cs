using Patina.Contracts;
using Patina.Export.Presets;
using System.Globalization;

namespace Patina.Export.Materials;

/// <summary>
///     The material files of one set of files that lies on several UDIM tiles, as a merged per-tile export writes it
///     (#281, #289). USD and MaterialX name every tile at once with the <c>&lt;UDIM&gt;</c> token in a texture's path,
///     when the files' names differ only by the tile's number; glTF has no tiles, so it writes a material per tile, as
///     do the others when the names say the tile some other way, such as <c>{tile}</c>'s <c>u1_v1</c>.
/// </summary>
public static class TileMaterials {
    /// <summary>The token USD's <c>UsdUVTexture</c> and MaterialX's <c>image</c> read as every tile's number.</summary>
    public const string Token = "<UDIM>";

    /// <summary>Writes a set's material files: one per kind for a set on one tile, and as above for several.</summary>
    /// <param name="preset">The preset, which names the kinds.</param>
    /// <param name="files">The set's files and where each was delivered.</param>
    /// <param name="folder">Where the material files go.</param>
    /// <param name="stem">Their name without an extension; a material per tile adds <c>_</c> and its number.</param>
    /// <param name="material">The material's name, or nothing for the file's own stem.</param>
    public static IReadOnlyList<MaterialFiles.Written> Write(
        ExportPreset preset,
        IReadOnlyList<(ExportedFile File, string Path)> files,
        string folder,
        string stem,
        string material
    ) {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(files);
        var tiles = files.GroupBy(static f => f.File.Udim).OrderBy(static g => g.Key).ToList();
        if (tiles.Count < 2) {
            return [MaterialFiles.Write(preset, files, folder, stem, material)];
        }

        List<MaterialFiles.Written> written = [];
        var tokened = Tokened(tiles);
        var shared = tokened is null
            ? []
            : preset.Materials.Where(static kind => kind != ExportMaterials.Gltf).ToList();
        if (tokened is not null && shared.Count > 0) {
            written.Add(MaterialFiles.Write(preset with { Materials = shared }, tokened, folder, stem, material));
        }

        var apart = preset with { Materials = [..preset.Materials.Except(shared, StringComparer.Ordinal)] };
        if (apart.Materials.Count == 0) {
            return written;
        }

        foreach (var tile in tiles) {
            var number = tile.Key.ToString(CultureInfo.InvariantCulture);
            var name = material.Length > 0 ? $"{material}_{number}" : string.Empty;
            written.Add(MaterialFiles.Write(apart, [..tile], folder, $"{stem}_{number}", name));
        }

        return written;
    }

    /// <summary>
    ///     The first tile's files with its number in each file's name turned to <see cref="Token" />, or null when
    ///     that does not name every tile's files alike: a name without the number, or tiles with different files.
    /// </summary>
    static List<(ExportedFile File, string Path)>? Tokened(
        List<IGrouping<int, (ExportedFile File, string Path)>> tiles
    ) {
        List<HashSet<string>> named = [];
        foreach (var tile in tiles) {
            var number = tile.Key.ToString(CultureInfo.InvariantCulture);
            HashSet<string> paths = new(StringComparer.Ordinal);
            foreach (var (file, path) in tile) {
                var name = Path.GetFileName(path);
                if (!name.Contains(number, StringComparison.Ordinal)) {
                    return null;
                }

                paths.Add($"{file.Template}|{name.Replace(number, Token, StringComparison.Ordinal)}");
            }

            named.Add(paths);
        }

        if (named.Skip(1).Any(paths => !paths.SetEquals(named[0]))) {
            return null;
        }

        var first = tiles[0].Key.ToString(CultureInfo.InvariantCulture);
        return [
            ..tiles[0]
                .Select(f => (f.File, Path.Combine(
                        Path.GetDirectoryName(f.Path)!,
                        Path.GetFileName(f.Path).Replace(first, Token, StringComparison.Ordinal)
                    ))
                )
        ];
    }
}
