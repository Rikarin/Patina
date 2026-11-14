namespace Patina.Ipc;

/// <summary>Where a preview lives in the cache, for the Worker that writes it and the Server that serves it.</summary>
public static class GraphPreviewFiles {
    public const string Extension = ".png";

    /// <summary>What a drawing of lines is kept as, such as a UV layout.</summary>
    public const string Drawing = ".svg";

    public static string PathOf(string folder, string hash, string extension = Extension) =>
        Path.Combine(folder, hash[..2], string.Concat(hash, extension));

    /// <summary>
    ///     Writes a file under its hash: whoever reads it sees all of it or no file. A file already there is the same
    ///     bytes, since the name is their hash, so it is kept and never replaced.
    /// </summary>
    /// <remarks>
    ///     ⚠ Never moved over (#250). On Windows a rename onto a file fails with "Access to the path is denied" while
    ///     anyone has that file open, and two nodes with one picture reach here at once under one hash, while the
    ///     Server may be serving it. One of them wins, and the others' pictures are already there.
    /// </remarks>
    /// <param name="folder">The preview cache.</param>
    /// <param name="hash">The digest of <paramref name="bytes" />.</param>
    /// <param name="bytes">What the file holds.</param>
    /// <param name="extension">What kind of file it is.</param>
    public static void Store(string folder, string hash, byte[] bytes, string extension = Extension) {
        var path = PathOf(folder, hash, extension);
        if (File.Exists(path)) {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // A name of its own, for the writers that reach here together.
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(temporary, bytes);
        try {
            File.Move(temporary, path);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException && File.Exists(path)) {
            File.Delete(temporary);
        }
    }
}
