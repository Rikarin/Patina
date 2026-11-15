using Patina.Contracts;
using System.Text.Json;

namespace Patina.Library;

/// <summary>
///     Maps baked in another program, brought in as a texture set's bake (#144). They go where the set's own bake goes,
///     beside a manifest that says they are of the set's mesh, UV set and material as they stand, so generators, mesh
///     map masks, decals and the set's shading read them as they read a bake of Patina's, and a later change to the
///     mesh says they no longer fit as it does for a bake.
/// </summary>
/// <remarks>
///     <para>
///         The files are copied as they are, never decoded: the Server reads no picture (ADR 0042), so only a PNG is
///         taken, which is what every reader of a bake reads, and a normal map's green must run up the picture, as
///         Patina's bakes and glTF's do. A map's size is not known here and is kept as 0, which fits any shape.
///     </para>
///     <para>
///         A map brought in replaces the map of that name in whatever bake the folder holds, so exactly one file
///         answers for each map; bringing in more adds to what was brought in before.
///     </para>
/// </remarks>
public static class BakeImport {
    /// <summary>The name of the bake in its folder, which its files and manifest are named after.</summary>
    public const string Name = "imported";

    /// <summary>The bake format a PNG of any depth reads as: nothing is centred from a float.</summary>
    const string Format = "png16";

    static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Copies the pictures into the document's bake folder and lists them in its manifest.</summary>
    /// <param name="root">The library.</param>
    /// <param name="request">The project, the texturing document, the maps and their space.</param>
    /// <returns>The bake folder, which the document is to read from now, and the document's file.</returns>
    /// <exception cref="ProjectRefusedException">The request breaks a rule.</exception>
    /// <exception cref="FileNotFoundException">The project, the document or a picture is not there.</exception>
    public static (string Folder, string Document) Run(string root, BakeImportRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        Check(request);
        var folder = Path.GetDirectoryName(LibraryFiles.ProjectFile(root, LibraryFolders.Existing(request.Project)))!;
        var project = ProjectBakes.Read(folder);
        var item = project.Documents.FirstOrDefault(d => d.Id.Value == request.Document)
            ?? throw new FileNotFoundException($"The project lists no document {request.Document}.");
        var mesh = project.Meshes.FirstOrDefault(m => m.Id == item.Mesh)
            ?? throw new ProjectRefusedException("Only a texturing document on a mesh has a bake.");
        var bake = Path.Combine(folder, ProjectFiles.BakeFolder, ProjectBakes.MapsFolder);
        bake = item.Material is { } material ? Path.Combine(bake, material) : bake;
        Directory.CreateDirectory(bake);

        HashSet<string> names = [..request.Maps.Select(static map => map.Map)];
        foreach (var other in Directory.EnumerateFiles(bake, DocumentBakes.ManifestPattern).ToList()) {
            Release(bake, other, names);
        }

        var manifest = Path.Combine(bake, $"{Name}.{LibraryLayout.BakeManifestFile}");
        var before = BakeFit.Read(manifest)?.Result.Maps ?? [];
        List<BakedMapFile> files = [..before.Where(map => !names.Contains(map.Map))];
        foreach (var map in request.Maps) {
            var file = $"{Name}_{map.Map}.png";
            Copy(map.File, Path.Combine(bake, file));
            files.Add(new(map.Map, file, 0, 1));
        }

        var layout = StackInputs.LayoutOf(SharedFile.ReadAllText(Path.Combine(folder, item.File)), item.Material);
        var meshFile = Path.Combine(folder, mesh.File);
        BakeManifest written = new(
            Name,
            Format,
            new(files, 0, 0, [], false),
            File.Exists(meshFile) ? LibraryFiles.Digest(meshFile) : mesh.Hash,
            layout.UvSet,
            request.Space,
            item.Material
        );
        Write(manifest, written);
        return (bake, Path.GetFullPath(item.File, folder));
    }

    /// <exception cref="ProjectRefusedException">A map is unknown or twice, the space unknown, or no PNG.</exception>
    /// <exception cref="FileNotFoundException">A picture is not there.</exception>
    static void Check(BakeImportRequest request) {
        if (request.Maps.Count == 0) {
            throw new ProjectRefusedException("Bringing in a bake takes one map at least.");
        }

        if (!BakeOptions.Spaces.Contains(request.Space, StringComparer.Ordinal)) {
            throw new ProjectRefusedException($"A normal is in tangent or object space, not '{request.Space}'.");
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (var map in request.Maps) {
            if (!BakeOptions.AllMaps.Contains(map.Map, StringComparer.Ordinal) || !seen.Add(map.Map)) {
                throw new ProjectRefusedException($"'{map.Map}' is not a map a bake has, or it is named twice.");
            }

            if (!File.Exists(map.File)) {
                throw new FileNotFoundException($"There is no picture at '{map.File}'.", map.File);
            }

            if (!IsPng(map.File)) {
                throw new ProjectRefusedException(
                    $"'{Path.GetFileName(map.File)}' is no PNG, which is what a bake is read as."
                );
            }
        }
    }

    /// <summary>Whether a file starts as a PNG does. Its eight first bytes are read, and nothing is decoded.</summary>
    static bool IsPng(string file) {
        using var stream = SharedFile.OpenRead(file);
        Span<byte> head = stackalloc byte[PngSignature.Length];
        return stream.ReadAtLeast(head, head.Length, false) == head.Length && head.SequenceEqual(PngSignature);
    }

    /// <summary>Takes the maps brought in out of another bake in the folder, deleting their files.</summary>
    static void Release(string bake, string manifest, HashSet<string> names) {
        if (Path.GetFileName(manifest) == $"{Name}.{LibraryLayout.BakeManifestFile}"
            || BakeFit.Read(manifest) is not { } read
            || !read.Result.Maps.Any(map => names.Contains(map.Map))) {
            return;
        }

        foreach (var map in read.Result.Maps.Where(map => names.Contains(map.Map))) {
            File.Delete(LibraryLayout.Beside(bake, map.Path));
        }

        List<BakedMapFile> left = [..read.Result.Maps.Where(map => !names.Contains(map.Map))];
        if (left.Count == 0) {
            File.Delete(manifest);
        } else {
            Write(manifest, read with { Result = read.Result with { Maps = left } });
        }
    }

    static void Write(string manifest, BakeManifest bake) =>
        LibraryFiles.Replace(
            manifest,
            JsonSerializer.SerializeToUtf8Bytes(bake, ContractsJsonContext.Default.BakeManifest)
        );

    /// <summary>Copies a file whole: a reader never finds half of it.</summary>
    static void Copy(string from, string to) {
        var part = $"{to}.{Guid.NewGuid():N}.part";
        File.Copy(from, part);
        SharedFile.MoveOver(part, to);
    }
}
