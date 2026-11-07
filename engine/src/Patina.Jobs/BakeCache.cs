using Patina.Contracts;
using Patina.Library;
using System.Text.Json;

namespace Patina.Jobs;

/// <summary>
///     What bakes have made, a folder per build key under the cache. It is cache: deleting it costs a bake and nothing
///     else. A folder only exists once it is whole, because the Worker makes it under another name and moves it.
/// </summary>
public sealed class BakeCache(string root) {
    /// <summary>The name of the file in a bake's folder that says what is there.</summary>
    public const string ManifestFile = LibraryLayout.BakeManifestFile;

    /// <summary>The folder a bake's key names.</summary>
    public string FolderOf(string key) => Path.Combine(root, key);

    /// <summary>What the cache holds under a key, or null.</summary>
    public BakeManifest? Find(string key) => ReadManifest(FolderOf(key));

    /// <summary>The 8-bit picture of one map of a cached bake, or null.</summary>
    public string? Preview(string key, string map) {
        var path = Path.Combine(FolderOf(key), $"{map}.preview.png");
        return File.Exists(path) ? path : null;
    }

    /// <summary>Reads the manifest in a folder, or null when the folder is not a finished bake.</summary>
    public static BakeManifest? ReadManifest(string folder) {
        var file = Path.Combine(folder, ManifestFile);
        if (!File.Exists(file)) {
            return null;
        }

        try {
            return JsonSerializer.Deserialize(File.ReadAllBytes(file), ContractsJsonContext.Default.BakeManifest);
        } catch (JsonException) {
            return null;
        }
    }

    /// <summary>
    ///     Copies a cached bake's maps to where its request wanted them, and leaves a manifest beside them so that
    ///     whoever reads them can undo their encoding.
    /// </summary>
    /// <returns>What the request made, with paths in the output folder.</returns>
    public BakeResult Deliver(string key, BakeManifest manifest, BakeRequest request, bool cached) {
        var stem = request.Name ?? manifest.Name;
        var source = FolderOf(key);
        Directory.CreateDirectory(request.Output);

        List<BakedMapFile> delivered = [];
        List<BakedMapFile> relative = [];
        foreach (var map in manifest.Result.Maps) {
            var name = $"{stem}_{map.Map}{Path.GetExtension(map.Path)}";
            var target = Path.Combine(request.Output, name);
            CopyAtomically(Path.Combine(source, map.Path), target);
            delivered.Add(map with { Path = target });
            relative.Add(map with { Path = name });
        }

        LibraryFiles.Replace(
            Path.Combine(request.Output, $"{stem}.{ManifestFile}"),
            JsonSerializer.SerializeToUtf8Bytes(
                manifest with { Name = stem, Result = manifest.Result with { Maps = relative } },
                ContractsJsonContext.Default.BakeManifest
            )
        );

        return manifest.Result with { Maps = delivered, Cached = cached };
    }

    static void CopyAtomically(string from, string to) {
        var part = $"{to}.{Guid.NewGuid():N}.part";
        File.Copy(from, part);
        File.Move(part, to, true);
    }
}
