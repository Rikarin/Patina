using Patina.Library;

namespace Patina.Export;

/// <summary>
///     The UDIM tile a texture set's layout sits on, where the Server needs it before anything is built (#281): a set's
///     state, and the names a preset gives its files. The tiles are read by <see cref="IMeshSetReads" />, which in the
///     Server is what the Worker read (#285).
/// </summary>
public static class SetUdim {
    /// <summary>
    ///     The tile one texture set's layout sits on: 1001 when it is there, when it reaches several, or when nothing
    ///     can be read. A set named for no material of the mesh is the whole mesh, as the Worker paints it.
    /// </summary>
    /// <param name="meshes">What reads the mesh.</param>
    /// <param name="file">The mesh file, or null for a set on none.</param>
    /// <param name="set">The set's material, or nothing for the whole mesh.</param>
    /// <param name="uvSet">The UV set it is laid out in.</param>
    public static int Of(IMeshSetReads meshes, string? file, string? set, int uvSet) {
        ArgumentNullException.ThrowIfNull(meshes);
        if (file is null) {
            return OneTile.First;
        }

        // The set's own material's tile, or else the whole mesh's. A mesh that will not read is said elsewhere.
        return (string.IsNullOrEmpty(set) ? null : meshes.Udim(file, set, uvSet))
            ?? meshes.Udim(file, null, uvSet)
            ?? OneTile.First;
    }
}
