using Patina.Contracts;
using System.IO.Compression;

namespace Patina.Export.Materials;

/// <summary>
///     Reads the materials of a USD package (<c>.usdz</c>, #157): an uncompressed zip whose first file is its root
///     layer. The package is unpacked and its layer, text or binary (<c>.usdc</c>, which is what most tools write),
///     read by <see cref="UsdaReader" />, its pictures from where the package holds them.
/// </summary>
public static class UsdzReader {
    /// <summary>The materials of a USD package.</summary>
    /// <param name="path">The package.</param>
    /// <param name="unpacked">
    ///     An empty folder the package is unpacked into, which the caller removes once the pictures are copied.
    /// </param>
    /// <exception cref="InvalidDataException">
    ///     It is not a zip, or its root layer is not a USD layer with a material.
    /// </exception>
    public static IReadOnlyList<ImportedMaterial> Read(string path, string unpacked) {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(unpacked);
        var name = Path.GetFileName(path);
        ZipArchive archive;
        try {
            archive = ZipFile.OpenRead(path);
        } catch (InvalidDataException e) {
            throw new InvalidDataException($"'{name}' is not a USD package: {e.Message}", e);
        }

        using (archive) {
            // USD reads the package's first file as its root layer, whatever the rest are.
            var root = archive.Entries.FirstOrDefault(static e => e.Name.Length > 0)
                ?? throw new InvalidDataException($"'{name}' is an empty USD package.");
            var kind = Path.GetExtension(root.FullName).ToLowerInvariant();
            if (kind is not (".usda" or ".usd" or ".usdc")) {
                throw new InvalidDataException($"'{name}' starts with '{root.FullName}', which is not a USD layer.");
            }

            var folder = Path.GetFullPath(unpacked);
            Directory.CreateDirectory(folder);
            foreach (var entry in archive.Entries.Where(static e => e.Name.Length > 0)) {
                var to = Path.GetFullPath(Path.Combine(folder, entry.FullName));
                // A name that climbs out of the folder is a zip made to write elsewhere, and is left in the package.
                if (!to.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                entry.ExtractToFile(to, true);
            }

            return UsdaReader.Read(Path.Combine(folder, root.FullName));
        }
    }
}
