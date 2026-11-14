using Patina.Export;
using Patina.Library;
using System.Collections.Concurrent;
using Vixen.Core.Imaging;

namespace Patina.Server;

/// <summary>
///     The pictures a client is shown, kept as PNGs in the library's cache by their content (#269). A picture stored as
///     anything but a PNG or a JPEG, and any picture asked for as a thumbnail, is decoded once per size and not on
///     every request, so a pick dialog over a folder of large PSDs or EXRs is slow once. A picture that has decoded
///     whole is known to read, so bringing it in after it was shown does not decode it again. It is all derived:
///     deleting the cache costs the decoding and nothing else.
/// </summary>
public sealed class PicturePreviews(LibraryHost host) {
    /// <summary>The smallest and the largest side a thumbnail is asked for at.</summary>
    public const int LeastSide = 16;

    /// <inheritdoc cref="LeastSide" />
    public const int MostSide = 4096;

    /// <summary>Goes up when a preview would come out differently from the same picture.</summary>
    const string Version = "1";

    const string Folder = "pictures";
    const string ReadMark = ".read";

    /// <summary>Each file's digest, by its path, while its size and time stay as they were.</summary>
    readonly ConcurrentDictionary<string, (long Length, long Written, string Digest)> digests =
        new(StringComparer.Ordinal);

    /// <summary>
    ///     The PNG a client is shown of a picture, as a file: whole, or with its longer side at most
    ///     <paramref name="side" /> texels. Made the first time it is asked for.
    /// </summary>
    /// <exception cref="InvalidDataException">It does not read as a picture, or as others a decoder throws.</exception>
    public string Preview(string file, int? side) {
        var folder = CacheFolder() ?? throw new InvalidOperationException("Previews are kept in a library.");
        var digest = Digest(file);
        var kept = Path.Combine(folder, $"{digest}-{side ?? 0}.v{Version}.png");
        if (File.Exists(kept)) {
            return kept;
        }

        var image = MapPictures.ReadFile(file);
        Mark(folder, digest);
        LibraryFiles.Replace(kept, MapPictures.Preview(Fitted(image, side)));
        return kept;
    }

    /// <summary>
    ///     Reads a picture whole unless it is known to read already, as one shown before is.
    /// </summary>
    /// <exception cref="InvalidDataException">It does not read as a picture, or as others a decoder throws.</exception>
    public void Check(string file) {
        if (CacheFolder() is not { } folder) {
            _ = MapPictures.ReadFile(file);
            return;
        }

        var digest = Digest(file);
        if (!File.Exists(Path.Combine(folder, digest + ReadMark))) {
            _ = MapPictures.ReadFile(file);
            Mark(folder, digest);
        }
    }

    static FloatImage Fitted(FloatImage image, int? side) {
        if (side is not { } most || Math.Max(image.Width, image.Height) <= most) {
            return image;
        }

        var scale = (double)most / Math.Max(image.Width, image.Height);
        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));
        return ImageResize.Resize(image, width, height);
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
