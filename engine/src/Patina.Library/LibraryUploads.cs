using Patina.Contracts;
using System.Buffers;
using System.Security.Cryptography;

namespace Patina.Library;

/// <summary>An upload larger than the library takes in one go (<see cref="LibraryUploads.MostBytes" />).</summary>
public sealed class UploadTooLargeException : IOException {
    public UploadTooLargeException() : base(
        $"A file sent to the library can be {LibraryUploads.MostBytes >> 30} GiB at the most."
    ) { }

    public UploadTooLargeException(string message) : base(message) { }

    public UploadTooLargeException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
///     Files a client sent from its own computer (#4, ADR 0041), kept in the library's <c>uploads</c> folder so that
///     everything that takes a file on the Server's machine takes one sent from anywhere: a project's mesh, a
///     picture brought into a project, a bake's high mesh. They are the person's files, not cache, so a bake that
///     reads one still finds it after the cache is deleted. Each is kept under its content's digest with its own
///     name, so the same file sent twice is kept once and two files of one name never meet.
/// </summary>
public static class LibraryUploads {
    /// <summary>The folder below the library the files are kept in.</summary>
    public const string Folder = "uploads";

    /// <summary>The largest file taken: a mesh with its textures packed in can be large, but not without end.</summary>
    public const long MostBytes = 4L << 30;

    /// <summary>How many hex digits of the digest name a file's folder: enough that no two files share one.</summary>
    const int DigestDigits = 16;

    /// <summary>
    ///     Whether a name is one a file can be kept under: a file's name alone, not hidden, of a picture or a mesh
    ///     Patina reads.
    /// </summary>
    public static bool Takes(string name) =>
        name.Length > 0
        && name == Path.GetFileName(name)
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && name.IndexOfAny(['/', '\\', ':']) < 0
        && !name.StartsWith('.')
        && (PictureFormats.IsPicture(name) || MeshFormats.IsMesh(name));

    /// <summary>Keeps a file sent to the library, and answers with its full path on this machine.</summary>
    /// <param name="root">The library.</param>
    /// <param name="name">The file's name, which <see cref="Takes" /> said yes to.</param>
    /// <param name="content">What is in it, read to its end.</param>
    /// <param name="cancellationToken">Stops reading; nothing is kept.</param>
    /// <param name="most">The largest file taken, <see cref="MostBytes" /> unless a test lowers it.</param>
    /// <exception cref="ArgumentException">The name is not one a file can be kept under.</exception>
    /// <exception cref="UploadTooLargeException">It is over <paramref name="most" />; nothing is kept.</exception>
    public static async Task<string> KeepAsync(
        string root,
        string name,
        Stream content,
        CancellationToken cancellationToken,
        long most = MostBytes
    ) {
        ArgumentNullException.ThrowIfNull(content);
        if (!Takes(name)) {
            throw new ArgumentException($"'{name}' is not a picture or a mesh Patina reads.", nameof(name));
        }

        var folder = Path.Combine(root, Folder);
        Directory.CreateDirectory(folder);
        var partial = Path.Combine(folder, $".partial-{Guid.NewGuid():N}");
        try {
            var digest = await WriteAsync(partial, content, most, cancellationToken);
            var kept = Path.Combine(folder, digest[..DigestDigits], name);
            if (!File.Exists(kept)) {
                Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
                SharedFile.MoveOver(partial, kept);
            } else {
                // Sent again: just sent, so a clearing of uploads nothing names leaves it for its sender to use (#297).
                File.SetLastWriteTimeUtc(kept, DateTime.UtcNow);
            }

            return kept;
        } finally {
            File.Delete(partial);
        }
    }

    /// <summary>Writes what a stream holds to a new file, and answers with its digest in hex.</summary>
    static async Task<string> WriteAsync(string file, Stream content, long most, CancellationToken cancellationToken) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(1 << 16);
        try {
            await using var output = new FileStream(
                file,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1 << 16,
                true
            );
            long total = 0;
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0) {
                total += read;
                if (total > most) {
                    throw new UploadTooLargeException();
                }

                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
