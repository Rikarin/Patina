using Patina.Contracts;
using Patina.Library;
using System.Collections.Concurrent;

namespace Patina.Server.Graphs;

/// <summary>
///     The bake a generator on the shelf is drawn from (#301): the maps of the sample knot, CC0 under
///     <c>content/bakes/Knot</c>, so a generator shows what it makes on a model rather than what it makes from no
///     bake at all. The Worker reads a bake from a folder, so the maps are written once into the preview cache, and
///     written again only when the cache lost them or a new build ships different ones: a file kept as it was keeps
///     its time, and the pictures keyed on it stay drawn.
/// </summary>
public static class ShelfBake {
    const string Resource = "ShelfBake/";
    const string Folder = "shelf-bake";

    static readonly Lazy<IReadOnlyList<(string Name, byte[] Bytes)>> Maps = new(Read);

    /// <summary>The caches whose maps have been compared with this build's once.</summary>
    static readonly ConcurrentDictionary<string, bool> Compared = new(StringComparer.Ordinal);

    /// <summary>The folder of the shelf's bake in a preview cache, written there if it is not.</summary>
    /// <param name="cache">The preview cache's folder.</param>
    /// <returns>The folder, or null when this build ships no bake.</returns>
    /// <exception cref="IOException">The cache could not be written.</exception>
    public static string? In(string cache) {
        if (Maps.Value.Count == 0) {
            return null;
        }

        var folder = Path.Combine(cache, Folder);
        var compare = Compared.TryAdd(cache, true);
        Directory.CreateDirectory(folder);
        foreach (var (name, bytes) in Maps.Value) {
            var file = Path.Combine(folder, name);
            if (!File.Exists(file) || (compare && !SharedFile.ReadAllBytes(file).AsSpan().SequenceEqual(bytes))) {
                LibraryFiles.Replace(file, bytes);
            }
        }

        return folder;
    }

    static List<(string, byte[])> Read() {
        var assembly = typeof(ShelfBake).Assembly;
        List<(string, byte[])> maps = [];
        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(static name => name.StartsWith(Resource, StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal)) {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            maps.Add((name[Resource.Length..], memory.ToArray()));
        }

        return maps;
    }
}
