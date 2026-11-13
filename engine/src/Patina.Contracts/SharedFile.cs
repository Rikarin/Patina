using System.Diagnostics;
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

    /// <summary>The longest wait, in milliseconds, between two tries of a move over a file somebody is reading.</summary>
    const int LongestPause = 50;

    /// <summary>How long a move over a file waits for that file's readers, at most.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(2);

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

    /// <summary>A whole file's bytes, read to its end, so one that grows or shrinks meanwhile is whole.</summary>
    public static byte[] ReadAllBytes(string path) {
        using var file = OpenRead(path);
        using MemoryStream bytes = new(file.Length <= Array.MaxLength ? (int)file.Length : 0);
        file.CopyTo(bytes);
        return bytes.Length == bytes.Capacity ? bytes.GetBuffer() : bytes.ToArray();
    }

    /// <summary>
    ///     Reads the files a model names beside itself, such as an OBJ's material library or a glTF's buffers: a path
    ///     as the model spells it, relative to the model's folder, read whole, or null for one that is not there.
    /// </summary>
    /// <param name="model">The model file.</param>
    /// <remarks>
    ///     A model written on Windows spells its folders with backslashes, which are a folder on every platform here.
    /// </remarks>
    public static Func<string, byte[]?> Beside(string model) {
        var folder = Path.GetDirectoryName(Path.GetFullPath(model)) ?? string.Empty;
        return name => {
            var file = Path.GetFullPath(name.Replace('\\', Path.DirectorySeparatorChar), folder);
            try {
                return ReadAllBytes(file);
            } catch (Exception missing) when (missing is FileNotFoundException or DirectoryNotFoundException) {
                return null;
            }
        };
    }

    /// <summary>A whole file's text, UTF-8 unless a byte order mark says otherwise.</summary>
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

    /// <summary>
    ///     Moves a finished file over another in one step, as <c>File.Move(from, to, true)</c> does, waiting briefly for
    ///     whoever is reading the file it replaces.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every overwriting move goes through here (#256). Windows refuses to rename onto a file anybody has open, with
    ///     "Access to the path is denied", and sharing delete does not help: a library scan that read a project's
    ///     <c>project.yaml</c> just as a client tagged the project turned the save into a 409 (#257). A read is short,
    ///     so the move is tried again until <see cref="Patience" /> has passed. On Linux and macOS the first try
    ///     succeeds.
    /// </remarks>
    /// <param name="from">The finished file, which is gone afterwards.</param>
    /// <param name="to">Where it goes, replacing what is there.</param>
    public static void MoveOver(string from, string to) {
        var waited = Stopwatch.StartNew();
        for (var pause = 1;; pause = Math.Min(pause * 2, LongestPause)) {
            try {
                File.Move(from, to, true);
                return;
            } catch (Exception e) when (Held(e) && waited.Elapsed < Patience && File.Exists(from)) {
                Thread.Sleep(pause);
            }
        }
    }

    /// <summary>Whether a move failed the way it does on Windows while the file it replaces is open.</summary>
    static bool Held(Exception e) =>
        e is UnauthorizedAccessException or IOException and not FileNotFoundException and not DirectoryNotFoundException;

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
