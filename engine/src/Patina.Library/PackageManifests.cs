using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     A package's <c>package.yaml</c> (ADR 0027), keyed as the API names a <see cref="PackageManifest" />. The folder
///     is the truth; nothing indexes it.
/// </summary>
public static class PackageManifests {
    /// <summary>What a package folder ends in.</summary>
    public const string Extension = ".ptpkg";

    /// <summary>The manifest's file, at the top of a package.</summary>
    public const string File = "package.yaml";

    /// <summary>Where a package keeps the pictures it carries.</summary>
    public const string FilesFolder = "files";

    /// <summary>Where a library keeps the packages it has, beside <c>projects/</c>.</summary>
    public const string LibraryFolder = "packages";

    /// <summary>The package shape this reads and writes.</summary>
    public const int Format = 1;

    /// <summary>A manifest as text. The same manifest always gives the same bytes.</summary>
    public static string Text(PackageManifest manifest) =>
        YamlText.Write(YamlRecords.Write(manifest, LibraryTreeJsonContext.Default.PackageManifest));

    /// <summary>A manifest read from text.</summary>
    /// <exception cref="LibraryFormatException">It is not a manifest, or is of a newer shape than this reads.</exception>
    public static PackageManifest Parse(string text, string path) {
        var fields = new YamlFields(YamlText.Parse(text, path), path);
        var manifest = YamlRecords.Read(
            fields,
            LibraryTreeJsonContext.Default.PackageManifest,
            new(Format, string.Empty, string.Empty, "material", string.Empty, [], [], [])
        );
        if (manifest.Format > Format) {
            throw new LibraryFormatException(
                path,
                $"it is a package of format {manifest.Format}, and this Patina reads format {Format}. "
                + "A newer Patina published it.",
                true
            );
        }

        return manifest.Graph.Length == 0 || Path.GetFileName(manifest.Graph) != manifest.Graph
            ? throw fields.Problem("'graph' names no file beside the manifest.")
            : manifest;
    }

    /// <summary>Whether a path is a package folder: one ending in the extension with a manifest in it.</summary>
    public static bool IsPackage(string path) =>
        Path.TrimEndingDirectorySeparator(path).EndsWith(Extension, StringComparison.Ordinal)
        && System.IO.File.Exists(Path.Combine(path, File));

    /// <summary>Reads a package's manifest.</summary>
    /// <exception cref="LibraryFormatException">It is not a manifest this reads.</exception>
    public static PackageManifest Read(string package) {
        var path = Path.Combine(package, File);
        return Parse(SharedFile.ReadAllText(path), path);
    }

    /// <summary>A package's graph file.</summary>
    /// <exception cref="LibraryFormatException">The manifest is not one this reads.</exception>
    public static string GraphOf(string package) => LibraryLayout.Beside(package, Read(package).Graph);

    /// <summary>The package a graph file is the graph of, or null when it is in none.</summary>
    public static string? PackageOf(string graph) {
        var folder = Path.GetDirectoryName(Path.GetFullPath(graph));
        return folder is not null && IsPackage(folder) ? folder : null;
    }

    /// <summary>The packages a library has, as folders, by name.</summary>
    public static IReadOnlyList<string> In(string library) {
        var folder = Path.Combine(library, LibraryFolder);
        return Directory.Exists(folder)
            ? [..Directory.EnumerateDirectories(folder, "*" + Extension).Where(IsPackage).Order(StringComparer.Ordinal)]
            : [];
    }
}
