using Patina.Contracts;
using System.IO.Compression;

namespace Patina.Library;

/// <summary>
///     A package sent as one file (ADR 0027): a zip of its <c>.ptpkg</c> folder, and bringing one, zipped or as a
///     folder, into a folder of packages. A zip comes from anywhere, so nothing in it is written until all of it has
///     been read as a package this Patina can use, and nothing in it is written outside the package's own folder.
/// </summary>
public static class PackageArchives {
    /// <summary>What a zipped package ends in.</summary>
    public const string ZipExtension = ".zip";

    /// <summary>The most a zip may unpack to, so that a small file cannot fill the disk.</summary>
    public const long Largest = 4L << 30;

    const string Importing = ".importing";
    const string Replaced = ".replaced";

    /// <summary>
    ///     Every entry carries the same time, so that the same package always zips to the same bytes: the earliest a
    ///     zip can say, in the local time a zip keeps.
    /// </summary>
    static readonly DateTimeOffset Stamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Zips a package into one file, as <c>&lt;name&gt;.ptpkg/…</c>, replacing what is there.</summary>
    /// <param name="package">The package's folder.</param>
    /// <param name="zip">The file to write.</param>
    /// <exception cref="LibraryFormatException">The folder is not a package this reads.</exception>
    public static void Zip(string package, string zip) {
        var manifest = PackageManifests.Read(package);
        var top = manifest.Name + PackageManifests.Extension + "/";
        var files = Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(package, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        var writing = zip + Importing;
        using (var archive = ZipFile.Open(writing, ZipArchiveMode.Create)) {
            foreach (var file in files) {
                var entry = archive.CreateEntry(top + file, CompressionLevel.Optimal);
                entry.LastWriteTime = Stamp;
                using var target = entry.Open();
                using var source = SharedFile.OpenRead(Path.Combine(package, file));
                source.CopyTo(target);
            }
        }

        SharedFile.MoveOver(writing, zip);
    }

    /// <summary>
    ///     Brings a package into a folder of packages, replacing one of its name: from a zip of it, or from its folder.
    /// </summary>
    /// <param name="source">The zip, or the <c>.ptpkg</c> folder.</param>
    /// <param name="packages">The folder it goes into, such as a library's <c>packages/</c>.</param>
    /// <returns>Its folder, what its manifest says, and whether a package of its name was there.</returns>
    /// <exception cref="FileNotFoundException">There is nothing at <paramref name="source" />.</exception>
    /// <exception cref="InvalidDataException">It is not a package, or names a file it does not hold.</exception>
    /// <exception cref="LibraryFormatException">Its manifest is not one this reads, or is newer.</exception>
    public static (string Package, PackageManifest Manifest, bool Replaced) Import(string source, string packages) {
        Directory.CreateDirectory(packages);
        var staging = Path.Combine(packages, Guid.NewGuid().ToString("N") + Importing);
        try {
            if (Directory.Exists(source)) {
                Copy(source, staging);
            } else if (File.Exists(source)) {
                Unzip(source, staging);
            } else {
                throw new FileNotFoundException($"There is no package at '{source}'.", source);
            }

            var manifest = Checked(staging);
            var target = Path.Combine(packages, manifest.Name + PackageManifests.Extension);
            var replaced = Directory.Exists(target);
            Place(staging, target);
            return (target, manifest, replaced);
        } finally {
            if (Directory.Exists(staging)) {
                Directory.Delete(staging, true);
            }
        }
    }

    /// <summary>A package's folder, copied whole, when it is one.</summary>
    static void Copy(string source, string staging) {
        if (!PackageManifests.IsPackage(source)) {
            throw new InvalidDataException(
                $"'{source}' is not a package: a package is a folder ending in {PackageManifests.Extension} "
                + $"with a {PackageManifests.File} in it, or a zip of one."
            );
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) {
            var target = Path.Combine(staging, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    /// <summary>
    ///     The package in a zip, unpacked: the folder holding its one manifest, at the top or one folder down. Every
    ///     other entry is left where it is, and none may climb out of the package or unpack to more than
    ///     <see cref="Largest" />.
    /// </summary>
    static void Unzip(string zip, string staging) {
        using var archive = OpenZip(zip);
        var top = TopOf(archive, zip);
        var entries = archive.Entries
            .Where(entry => entry.FullName.Replace('\\', '/').StartsWith(top, StringComparison.Ordinal))
            .Where(static entry => !entry.FullName.EndsWith('/'))
            .ToList();
        if (entries.Sum(static entry => entry.Length) > Largest) {
            throw new InvalidDataException($"'{zip}' unpacks to more than a package may hold, 4 GiB.");
        }

        Directory.CreateDirectory(staging);
        foreach (var entry in entries) {
            var name = entry.FullName.Replace('\\', '/')[top.Length..];
            entry.ExtractToFile(Inside(staging, name, zip), false);
        }
    }

    static ZipArchive OpenZip(string zip) {
        try {
            return ZipFile.OpenRead(zip);
        } catch (InvalidDataException e) {
            throw new InvalidDataException($"'{zip}' is not a zip, so it cannot hold a package.", e);
        }
    }

    /// <summary>Where a zip's package starts: the folder of its one manifest, at the top or one folder down.</summary>
    static string TopOf(ZipArchive archive, string zip) {
        var manifests = archive.Entries.Select(static entry => entry.FullName.Replace('\\', '/'))
            .Where(static name => name == PackageManifests.File
                || (name.EndsWith("/" + PackageManifests.File, StringComparison.Ordinal)
                    && name.Count(static c => c == '/') == 1)
            )
            .ToList();
        return manifests switch {
            [var one] => one[..^PackageManifests.File.Length],
            [] => throw new InvalidDataException(
                $"'{zip}' holds no {PackageManifests.File}, at its top or one folder down, so it holds no package."
            ),
            _ => throw new InvalidDataException(
                $"'{zip}' holds {manifests.Count} packages. Bring them in one zip at a time."
            )
        };
    }

    /// <summary>Where an entry goes, which must be inside the package: never rooted, and never above it.</summary>
    static string Inside(string staging, string name, string zip) {
        string full;
        try {
            full = LibraryLayout.Beside(staging, name);
        } catch (InvalidDataException) {
            throw new InvalidDataException(
                $"'{zip}' holds '{name}', which is outside its package. Nothing was brought in."
            );
        }

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return full;
    }

    /// <summary>
    ///     The manifest of an unpacked package, when it holds everything the manifest says it does and its name can
    ///     be a folder's.
    /// </summary>
    static PackageManifest Checked(string staging) {
        var manifest = PackageManifests.Read(staging);
        if (!PackageManifests.IsName(manifest.Name)) {
            throw new InvalidDataException($"A package cannot be called '{manifest.Name}': that is no folder's name.");
        }

        IEnumerable<string> named = [
            manifest.Graph,
            ..manifest.Files.Select(static file => file.Path),
            ..manifest.Compounds.Select(static compound =>
                $"{LibraryLayout.CompoundsFolder}/{compound.Type}{LibraryLayout.GraphExtension}"
            )
        ];
        foreach (var file in named) {
            if (!File.Exists(LibraryLayout.Beside(staging, file))) {
                throw new InvalidDataException(
                    $"The package {manifest.Name} names '{file}', which it does not hold. Nothing was brought in."
                );
            }
        }

        return manifest;
    }

    /// <summary>Moves an unpacked package into place, so that a reader sees the old one or the new one whole.</summary>
    static void Place(string staging, string target) {
        var old = target + Replaced;
        if (Directory.Exists(old)) {
            Directory.Delete(old, true);
        }

        var replacing = Directory.Exists(target);
        if (replacing) {
            SharedFile.MoveFolder(target, old);
        }

        SharedFile.MoveFolder(staging, target);
        if (replacing) {
            Directory.Delete(old, true);
        }
    }
}
