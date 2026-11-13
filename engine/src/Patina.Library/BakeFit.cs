using Patina.Contracts;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Patina.Library;

/// <summary>
///     Whether a texture set's bake still fits it. A bake is of one mesh, laid out in one of its UV sets, and a set
///     that stands on another mesh or chose another UV set reads its maps in the wrong place: decals, paths and
///     mesh-map masks read them per texel (ADR 0021), and the set's shading reads them beneath its layers (#86).
///     Nothing is rebaked here; the set is told, and so is whoever shows it.
/// </summary>
public static class BakeFit {
    /// <summary>The bake was made on a mesh the set no longer stands on.</summary>
    public const string Mesh = "mesh";

    /// <summary>The bake is laid out in another of the mesh's UV sets than the one the set chose (#238).</summary>
    public const string UvSet = "uvSet";

    static readonly ConcurrentDictionary<string, (long Length, DateTime Written, string Digest)> Digests =
        new(StringComparer.Ordinal);

    static readonly ConcurrentDictionary<FacesKey, FacesRead> Faces = new();

    /// <summary>
    ///     Reads the revision of one texture set's faces in a mesh file, as a bake records them in
    ///     <see cref="BakeManifest.Geometry" />, or null when it cannot: the file, the set's material or null for the
    ///     whole mesh, and the UV set. The Server sets it, since reading a mesh is not this library's. Unset, any
    ///     change to the mesh's file is a change to every set on it.
    /// </summary>
    public static Func<string, string?, int, string?>? Geometry { get; set; }

    /// <summary>Why a set's bake no longer fits it, or null when it does or there is none.</summary>
    /// <param name="folder">The folder of the bake the set reads, or null.</param>
    /// <param name="mesh">The digest of the mesh the set stands on, or null when that is not known.</param>
    /// <param name="uvSet">The UV set the set is laid out in.</param>
    /// <param name="meshFile">
    ///     The mesh's file, or null. With it, a bake of another digest still fits when the set's own faces are those
    ///     it was made on, so a change to one set's faces leaves the others' bakes fitting (#151).
    /// </param>
    public static string? Stale(string? folder, string? mesh, int uvSet, string? meshFile = null) {
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

        return manifests.Exists(m => m.UvSet != uvSet) ? UvSet : null;
    }

    /// <summary>The same, for a set on a mesh file, whose digest is kept while the file stays as it was.</summary>
    /// <param name="folder">The folder of the bake the set reads, or null.</param>
    /// <param name="model">The mesh the set stands on, or null for none.</param>
    /// <param name="uvSet">The UV set the set is laid out in.</param>
    public static string? OfModel(string? folder, string? model, int uvSet) =>
        folder is null ? null : Stale(folder, DigestOf(model), uvSet, model);

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

    /// <summary>The revision of a set's faces, read again only when the file's length or time changes.</summary>
    static string? FacesOf(string file, string? material, int uvSet) {
        if (Geometry is not { } read) {
            return null;
        }

        FileInfo info = new(file);
        if (!info.Exists) {
            return null;
        }

        FacesKey key = new(file, material, uvSet);
        if (Faces.TryGetValue(key, out var known)
            && known.Length == info.Length
            && known.Written == info.LastWriteTimeUtc) {
            return known.Geometry;
        }

        var geometry = read(file, material, uvSet);
        Faces[key] = new(info.Length, info.LastWriteTimeUtc, geometry);
        return geometry;
    }

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

    readonly record struct FacesKey(string File, string? Material, int UvSet);

    readonly record struct FacesRead(long Length, DateTime Written, string? Geometry);
}
