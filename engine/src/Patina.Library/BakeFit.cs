using Patina.Contracts;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Patina.Library;

/// <summary>
///     Whether a texture set's bake still fits it. A bake is of one mesh, laid out in one of its UV sets at one shape,
///     and a set that stands on another mesh, chose another UV set or changed its shape reads its maps in the wrong
///     place or stretched: decals, paths and mesh-map masks read them per texel (ADR 0021), and the set's shading
///     reads them beneath its layers (#86). Nothing is rebaked here; the set is told, and so is whoever shows it.
/// </summary>
public static class BakeFit {
    /// <summary>The bake was made on a mesh the set no longer stands on.</summary>
    public const string Mesh = "mesh";

    /// <summary>The bake is laid out in another of the mesh's UV sets than the one the set chose (#238).</summary>
    public const string UvSet = "uvSet";

    /// <summary>The bake is of another shape than the set's Width by Height, so it reads stretched (#262).</summary>
    public const string Shape = "shape";

    /// <summary>The fewest rows a bake takes.</summary>
    const int FewestRows = 16;

    /// <summary>The most rows a bake takes.</summary>
    const int MostRows = 16384;

    static readonly ConcurrentDictionary<string, (long Length, DateTime Written, string Digest)> Digests =
        new(StringComparer.Ordinal);

    /// <summary>
    ///     Reads the revision of one texture set's faces in a mesh file, as a bake records them in
    ///     <see cref="BakeManifest.Geometry" />, or null when it cannot: the file, the set's material or null for the
    ///     whole mesh, and the UV set. The Server sets it to what the Worker read (#285), since reading a mesh is not
    ///     this library's, nor the Server's. Unset, any change to the mesh's file is a change to every set on it.
    /// </summary>
    public static Func<string, string?, int, string?>? Geometry { get; set; }

    /// <summary>Why a set's bake no longer fits it, or null when it does or there is none.</summary>
    /// <param name="folder">The folder of the bake the set reads, or null.</param>
    /// <param name="mesh">The digest of the mesh the set stands on, or null when that is not known.</param>
    /// <param name="set">The UV set the set is laid out in, and its shape.</param>
    /// <param name="meshFile">
    ///     The mesh's file, or null. With it, a bake of another digest still fits when the set's own faces are those
    ///     it was made on, so a change to one set's faces leaves the others' bakes fitting (#151).
    /// </param>
    public static string? Stale(string? folder, string? mesh, SetLayout set, string? meshFile = null) {
        if (folder is null || !Directory.Exists(folder)) {
            return null;
        }

        var manifests = Directory.EnumerateFiles(folder, $"*{LibraryLayout.BakeManifestFile}")
            .Order(StringComparer.Ordinal)
            .Select(Read)
            .OfType<BakeManifest>()
            .ToList();
        if (mesh is not null
            && manifests.Exists(m => m.Mesh is not null && m.Mesh != mesh && !SameFaces(m, meshFile))) {
            return Mesh;
        }

        return manifests.Exists(m => m.UvSet != set.UvSet) ? UvSet
            : manifests.Exists(m => !OfShape(m, set)) ? Shape
            : null;
    }

    /// <summary>
    ///     The height of a bake <paramref name="resolution" /> wide that keeps a set's shape: null, a square, when the
    ///     set is square or its shape is not known, and within what a bake takes otherwise (#253).
    /// </summary>
    public static int? RowsAt(int resolution, int width, int height) {
        if (width <= 0 || height <= 0 || width == height) {
            return null;
        }

        var rows = Math.Clamp((int)Math.Round((double)resolution * height / width), FewestRows, MostRows);
        return rows == resolution ? null : rows;
    }

    /// <summary>
    ///     Whether a bake's maps are as tall as the set's shape makes them at their width. A set of no known shape, and
    ///     a bake that lists no maps, fit.
    /// </summary>
    static bool OfShape(BakeManifest manifest, SetLayout set) =>
        set.Width <= 0
        || set.Height <= 0
        || manifest.Result.Maps.Count == 0
        || RowsOf(manifest.Result.Maps[0]) == RowsAt(manifest.Result.Maps[0].Resolution, set.Width, set.Height);

    /// <summary>A map's height when it is not square.</summary>
    static int? RowsOf(BakedMapFile map) => map.Rows is { } rows && rows != map.Resolution ? rows : null;

    /// <summary>The same, for a set on a mesh file, whose digest is kept while the file stays as it was.</summary>
    /// <param name="folder">The folder of the bake the set reads, or null.</param>
    /// <param name="model">The mesh the set stands on, or null for none.</param>
    /// <param name="set">The UV set the set is laid out in, and its shape.</param>
    public static string? OfModel(string? folder, string? model, SetLayout set) =>
        folder is null ? null : Stale(folder, DigestOf(model), set, model);

    /// <summary>
    ///     Whether a mesh file still has the faces a bake was made on, in the bake's UV set: the bake's material's
    ///     faces, or the whole mesh's. False when the bake did not record them, or nothing can read them.
    /// </summary>
    /// <param name="manifest">The bake.</param>
    /// <param name="meshFile">The mesh file, or null.</param>
    public static bool SameFaces(BakeManifest manifest, string? meshFile) {
        ArgumentNullException.ThrowIfNull(manifest);
        return manifest.Geometry is { } geometry
            && meshFile is not null
            && FacesOf(meshFile, manifest.Material, manifest.UvSet) == geometry;
    }

    /// <summary>The revision of a set's faces, as whoever set <see cref="Geometry" /> keeps it.</summary>
    static string? FacesOf(string file, string? material, int uvSet) =>
        Geometry is { } read && File.Exists(file) ? read(file, material, uvSet) : null;

    /// <summary>A file's digest, read again only when its length or time changes. Null when it is not there.</summary>
    static string? DigestOf(string? file) {
        if (file is null) {
            return null;
        }

        FileInfo info = new(file);
        if (!info.Exists) {
            return null;
        }

        if (Digests.TryGetValue(file, out var known)
            && known.Length == info.Length
            && known.Written == info.LastWriteTimeUtc) {
            return known.Digest;
        }

        try {
            var digest = LibraryFiles.Digest(file);
            Digests[file] = (info.Length, info.LastWriteTimeUtc, digest);
            return digest;
        } catch (IOException) {
            return null; // being written: the next look says
        }
    }

    /// <summary>A bake's manifest, or null when the file is not a whole one.</summary>
    internal static BakeManifest? Read(string file) {
        try {
            return JsonSerializer.Deserialize(SharedFile.ReadAllBytes(file), ContractsJsonContext.Default.BakeManifest);
        } catch (Exception e) when (e is JsonException or IOException) {
            return null; // something half written says nothing about the mesh
        }
    }

    /// <summary>What a texture set reads its bake by.</summary>
    /// <param name="UvSet">The UV set it is laid out in.</param>
    /// <param name="Width">Its width, or zero when not known, which leaves its shape out.</param>
    /// <param name="Height">Its height, the same.</param>
    public readonly record struct SetLayout(int UvSet, int Width = 0, int Height = 0);
}
