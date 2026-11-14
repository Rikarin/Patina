using System.Collections.Concurrent;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>
///     The UDIM tile a texture set's layout sits on, read from its mesh where the Server needs it before anything is
///     built (#281): a set's state, and the names a preset gives its files. A file is read again only when its length
///     or time changes, and once for each material and UV set asked about.
/// </summary>
public static class SetUdim {
    static readonly ConcurrentDictionary<(string File, string Set, int UvSet), (long Length, DateTime Written, int Udim)>
        Known = new();

    /// <summary>
    ///     The tile one texture set's layout sits on: 1001 when it is there, when it reaches several, or when nothing
    ///     can be read. A set named for no material of the mesh is the whole mesh, as the Worker paints it.
    /// </summary>
    /// <param name="file">The mesh file, or null for a set on none.</param>
    /// <param name="set">The set's material, or nothing for the whole mesh.</param>
    /// <param name="uvSet">The UV set it is laid out in.</param>
    public static int Of(string? file, string? set, int uvSet) {
        if (file is null) {
            return OneTile.First;
        }

        FileInfo info = new(file);
        if (!info.Exists) {
            return OneTile.First;
        }

        var key = (file, set ?? string.Empty, uvSet);
        if (Known.TryGetValue(key, out var known)
            && known.Length == info.Length
            && known.Written == info.LastWriteTimeUtc) {
            return known.Udim;
        }

        var udim = Read(file, set, uvSet);
        Known[key] = (info.Length, info.LastWriteTimeUtc, udim);
        return udim;
    }

    /// <summary>The set's own material's tile, or else the whole mesh's. A mesh that will not read is said elsewhere.</summary>
    static int Read(string file, string? set, int uvSet) =>
        (string.IsNullOrEmpty(set) ? null : Placed(file, set, uvSet)) ?? Placed(file, null, uvSet) ?? OneTile.First;

    static int? Placed(string file, string? material, int uvSet) {
        try {
            return MeshMapBakeRun.Placed(file, material, uvSet).Udim;
        } catch (Exception e) when (e is ModelFormatException or InvalidDataException or IOException) {
            return null;
        }
    }
}
