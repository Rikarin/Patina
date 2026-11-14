using Patina.Ipc;
using Patina.Library;
using System.Collections.Concurrent;

namespace Patina.Server;

/// <summary>A picture that does not read: the decoder's sentence, or the Worker it took down.</summary>
public sealed class PictureUnreadableException : Exception {
    public PictureUnreadableException() { }

    public PictureUnreadableException(string message) : base(message) { }

    public PictureUnreadableException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
///     The pictures a client is shown, kept as PNGs in the library's cache by their content (#269). A picture stored as
///     anything but a PNG or a JPEG, and any picture asked for as a thumbnail, is decoded once per size and not on
///     every request, so a pick dialog over a folder of large PSDs or EXRs is slow once. A picture that has decoded
///     whole is known to read, so bringing it in after it was shown does not decode it again. It is all derived:
///     deleting the cache costs the decoding and nothing else.
/// </summary>
/// <remarks>
///     ⚠ <b>The Server decodes no picture</b> (#293, plan §3.1): the Worker does, and writes the PNG where it is told.
///     A picture the Worker went away while reading is kept as one that does not read until its bytes change, since
///     asked again it would take the next Worker down too.
/// </remarks>
public sealed class PicturePreviews(LibraryHost host, WorkerLink worker) {
    /// <summary>The smallest and the largest side a thumbnail is asked for at.</summary>
    public const int LeastSide = 16;

    /// <inheritdoc cref="LeastSide" />
    public const int MostSide = 4096;

    /// <summary>How long a picture waits for a Worker to come, when none is connected.</summary>
    static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>Goes up when a preview would come out differently from the same picture.</summary>
    const string Version = "1";

    const string Folder = "pictures";
    const string ReadMark = ".read";

    /// <summary>Each file's digest, by its path, while its size and time stay as they were.</summary>
    readonly ConcurrentDictionary<string, (long Length, long Written, string Digest)> digests =
        new(StringComparer.Ordinal);

    /// <summary>The digests of pictures a Worker went away while reading.</summary>
    readonly ConcurrentDictionary<string, string> fatal = new(StringComparer.Ordinal);

    /// <summary>
    ///     The PNG a client is shown of a picture, as a file: whole, or with its longer side at most
    ///     <paramref name="side" /> texels. Made the first time it is asked for.
    /// </summary>
    /// <exception cref="PictureUnreadableException">It does not read as a picture.</exception>
    /// <exception cref="RendererMissingException">No Worker came to read it.</exception>
    public async Task<string> PreviewAsync(string file, int? side, CancellationToken cancellationToken) {
        var folder = CacheFolder() ?? throw new InvalidOperationException("Previews are kept in a library.");
        var digest = Digest(file);
        var kept = Path.Combine(folder, $"{digest}-{side ?? 0}.v{Version}.png");
        if (File.Exists(kept)) {
            return kept;
        }

        Directory.CreateDirectory(folder);
        await ReadAsync(file, digest, new(file, kept, side), cancellationToken);
        Mark(folder, digest);
        return kept;
    }

    /// <summary>
    ///     Reads a picture whole unless it is known to read already, as one shown before is.
    /// </summary>
    /// <exception cref="PictureUnreadableException">It does not read as a picture.</exception>
    /// <exception cref="RendererMissingException">No Worker came to read it.</exception>
    public async Task CheckAsync(string file, CancellationToken cancellationToken) {
        var digest = Digest(file);
        var folder = CacheFolder();
        if (folder is not null && File.Exists(Path.Combine(folder, digest + ReadMark))) {
            return;
        }

        await ReadAsync(file, digest, new(file, null, null), cancellationToken);
        if (folder is not null) {
            Mark(folder, digest);
        }
    }

    async Task ReadAsync(string file, string digest, PictureReadAsk ask, CancellationToken cancellationToken) {
        if (fatal.TryGetValue(digest, out var lost)) {
            throw new PictureUnreadableException(lost);
        }

        var connection = await ConnectionAsync(cancellationToken);
        IpcMessage reply;
        try {
            reply = await connection.SendAsync(
                new() { Type = IpcTypes.PictureRead, PictureRead = ask },
                cancellationToken
            );
        } catch (IOException e) {
            // ⚠ It may be what took the Worker down; asked again, it would take the next one down too.
            var said = $"Reading {Path.GetFileName(file)} stopped the GPU engine.";
            fatal[digest] = said;
            throw new PictureUnreadableException(said, e);
        }

        if (reply.PictureReadEnd?.Unreadable is { } unreadable) {
            throw new PictureUnreadableException(unreadable);
        }
    }

    async Task<IpcConnection> ConnectionAsync(CancellationToken cancellationToken) {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);
        try {
            return await worker.ConnectionAsync(patience.Token);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new RendererMissingException();
        }
    }

    static void Mark(string folder, string digest) =>
        LibraryFiles.Replace(Path.Combine(folder, digest + ReadMark), string.Empty);

    string? CacheFolder() =>
        host.Library is { } library
            ? Path.Combine(LibraryLayout.Full(library.RootPath, LibraryLayout.CacheFolder), Folder)
            : null;

    string Digest(string file) {
        var info = new FileInfo(file);
        var (length, written) = (info.Length, info.LastWriteTimeUtc.Ticks);
        if (digests.TryGetValue(info.FullName, out var known) && known.Length == length && known.Written == written) {
            return known.Digest;
        }

        var digest = LibraryFiles.Digest(file);
        digests[info.FullName] = (length, written, digest);
        return digest;
    }
}
