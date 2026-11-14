namespace Patina.Library;

/// <summary>
///     What is read of a mesh file's texture sets: their tiles, their faces, and the pictures the file names. The
///     Worker and the command line read the file; the Server only keeps what the Worker read (plan §3.1, #285).
/// </summary>
public interface IMeshSetReads {
    /// <summary>The UDIM tile one material's faces sit on in a UV set, or null when they cannot be read so.</summary>
    /// <param name="file">The mesh file.</param>
    /// <param name="material">The material, or null for the whole mesh.</param>
    /// <param name="uvSet">The UV set.</param>
    int? Udim(string file, string? material, int uvSet);

    /// <summary>
    ///     The revision of one material's faces in a UV set, as a bake records it, or null when they cannot be read so.
    /// </summary>
    /// <param name="file">The mesh file.</param>
    /// <param name="material">The material, or null for the whole mesh.</param>
    /// <param name="uvSet">The UV set.</param>
    string? Geometry(string file, string? material, int uvSet);

    /// <summary>
    ///     The base colour pictures the file's materials name beside it, by full path, there or not. None for a file
    ///     that will not read as a mesh: a bake of it says why.
    /// </summary>
    /// <param name="file">The mesh file.</param>
    IReadOnlyList<string> Pictures(string file);
}
