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

    /// <summary>Why a set's bake no longer fits it, or null when it does or there is none.</summary>
    /// <param name="folder">The folder of the bake the set reads, or null.</param>
    /// <param name="mesh">The digest of the mesh the set stands on, or null when that is not known.</param>
    /// <param name="uvSet">The UV set the set is laid out in.</param>
    public static string? Stale(string? folder, string? mesh, int uvSet) {
        if (folder is null || !Directory.Exists(folder)) {
            return null;
        }

        var manifests = Directory.EnumerateFiles(folder, $"*{LibraryLayout.BakeManifestFile}")
            .Order(StringComparer.Ordinal)
            .Select(Read)
            .OfType<BakeManifest>()
            .ToList();
        if (mesh is not null && manifests.Exists(m => m.Mesh is not null && m.Mesh != mesh)) {
            return Mesh;
        }

        return manifests.Exists(m => m.UvSet != uvSet) ? UvSet : null;
    }

    /// <summary>The same, for a set on a mesh file, whose digest is kept while the file stays as it was.</summary>
    /// <param name="folder">The folder of the bake the set reads, or null.</param>
    /// <param name="model">The mesh the set stands on, or null for none.</param>
    /// <param name="uvSet">The UV set the set is laid out in.</param>
    public static string? OfModel(string? folder, string? model, int uvSet) =>
        folder is null ? null : Stale(folder, DigestOf(model), uvSet);

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

    static BakeManifest? Read(string file) {
        try {
            return JsonSerializer.Deserialize(SharedFile.ReadAllBytes(file), ContractsJsonContext.Default.BakeManifest);
        } catch (Exception e) when (e is JsonException or IOException) {
            return null; // something half written says nothing about the mesh
        }
    }
}
