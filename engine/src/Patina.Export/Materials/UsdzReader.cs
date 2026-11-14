using Patina.Contracts;
using System.IO.Compression;

namespace Patina.Export.Materials;

/// <summary>
///     Reads the materials of a USD package (<c>.usdz</c>, #157): an uncompressed zip whose first file is its root
///     layer. A package whose root layer is text is unpacked and read by <see cref="UsdaReader" />, its pictures from
///     where the package holds them. A root layer in USD's binary form (<c>.usdc</c>), which is what most tools write,
///     needs USD's own library and is refused with what to do instead.
/// </summary>
public static class UsdzReader {
    /// <summary>The materials of a USD package.</summary>
    /// <param name="path">The package.</param>
    /// <param name="unpacked">
    ///     An empty folder the package is unpacked into, which the caller removes once the pictures are copied.
    /// </param>
    /// <exception cref="InvalidDataException">
    ///     It is not a zip, its root layer is binary, or that layer is not a USD text layer with a material.
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
            if (kind == ".usdc" || (kind == ".usd" && !IsText(root))) {
                throw new InvalidDataException(
                    $"'{name}' holds its layer in USD's binary form ({root.FullName}), which needs USD's own library. "
                    + "Save the material as .usda, or as a .usdz whose layer is .usda, to bring it in."
                );
            }

            if (kind is not (".usda" or ".usd")) {
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

    /// <summary>Whether a layer starts as USD's text form does.</summary>
    static bool IsText(ZipArchiveEntry entry) {
        using var stream = entry.Open();
        Span<byte> head = stackalloc byte[5];
        return stream.ReadAtLeast(head, head.Length, false) == head.Length && head.SequenceEqual("#usda"u8);
    }
}
