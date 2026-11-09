using System.Text;

namespace Patina.Contracts;

/// <summary>
///     How every Patina process reads a file: sharing write and delete, and closed when the read is done. Files are the
///     truth, and an artist, a second editor or a git checkout rewrites them while the Server and Worker run.
/// </summary>
/// <remarks>
///     <c>File.ReadAllText</c>, <c>File.ReadAllBytes</c> and <c>File.OpenRead</c> share reading only. On Windows that
///     refuses anybody who opens the file to write, replace or delete it while the read is under way, with "being used
///     by another process": a library scan that hashed a graph the moment a client saved it was enough. Share modes are
///     advisory on Linux and macOS, so the fault only ever shows on Windows. A test keeps those three out of
///     <c>engine/src</c>.
/// </remarks>
public static class SharedFile {
    /// <summary>What any other program may do to a file while one of these reads it.</summary>
    public const FileShare Share = FileShare.ReadWrite | FileShare.Delete;

    /// <summary>The options every read opens with, made fresh so nobody can change them for the next caller.</summary>
    /// <param name="asynchronous">Whether the handle is for asynchronous reads.</param>
    public static FileStreamOptions Reading(bool asynchronous = false) =>
        new() {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = Share,
            Options = FileOptions.SequentialScan | (asynchronous ? FileOptions.Asynchronous : FileOptions.None)
        };

    /// <summary>Opens a file to read, as <c>File.OpenRead</c> does but sharing write and delete.</summary>
    public static FileStream OpenRead(string path) => new(path, Reading());

    /// <summary>A whole file's bytes, read to its end, so one that grows or shrinks during the read is still read whole.</summary>
    public static byte[] ReadAllBytes(string path) {
        using var file = OpenRead(path);
        using MemoryStream bytes = new(file.Length <= Array.MaxLength ? (int)file.Length : 0);
        file.CopyTo(bytes);
        return bytes.Length == bytes.Capacity ? bytes.GetBuffer() : bytes.ToArray();
    }

    /// <summary>A whole file's text, UTF-8 unless a byte order mark says otherwise, as <c>File.ReadAllText</c>.</summary>
    public static string ReadAllText(string path) {
        using StreamReader reader = new(OpenRead(path), Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    /// <summary>A whole file's text, read asynchronously.</summary>
    public static async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) {
        using StreamReader reader = new(
            new FileStream(path, Reading(true)),
            Encoding.UTF8,
            true
        );
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A file's lines, opened now and read as they are enumerated, as <c>File.ReadLines</c>.</summary>
    public static IEnumerable<string> ReadLines(string path) => Lines(new(OpenRead(path), Encoding.UTF8, true));

    static IEnumerable<string> Lines(StreamReader reader) {
        using (reader) {
            while (reader.ReadLine() is { } line) {
                yield return line;
            }
        }
    }
}
