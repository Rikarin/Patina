using System.Collections.Immutable;
using Vixen.Graphics;
using Vixen.Interchange;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Worker;

/// <summary>A file a compiled graph read, as it was when it was read.</summary>
readonly record struct PictureStamp(string Path, long Length, DateTime Written);

/// <summary>What filling a compilation's external images came to.</summary>
/// <param name="Refusals">One per image that could not be filled. With any here, the plan cannot run.</param>
/// <param name="Stamps">The files that were read, to notice one changing.</param>
sealed record FilledPictures(IReadOnlyList<NodeDiagnostic> Refusals, IReadOnlyList<PictureStamp> Stamps);

/// <summary>
///     Fills the external images a compilation names and cannot carry: bitmaps from files through Vixen's decoder, and
///     the tables an inline ramp or curve bakes to. Compiling loads them again after every structural edit, so decoded
///     pixels are kept, up to a budget, under each file's length and time. Everything here can be read again, so a new
///     Worker starts without it.
/// </summary>
sealed class GraphPictures {
    const string Problem = "PT0001";
    const string MeshMapScheme = "meshmap:";
    const long Budget = 512L * 1024 * 1024;

    readonly Dictionary<PictureStamp, Decoded> decoded = [];
    readonly StbImageDecoder decoder = new();
    long held;

    /// <summary>Whether a file is no longer what was read.</summary>
    public static bool Moved(PictureStamp stamp) => StampOf(stamp.Path) != stamp;

    /// <summary>Uploads every image in <paramref name="owed" />, and says which could not be.</summary>
    public FilledPictures Fill(
        TextureUploads uploads,
        TexturePlan plan,
        ImmutableArray<TextureGraphExternal> owed,
        IReadOnlyList<string> roots
    ) {
        List<NodeDiagnostic> refusals = [];
        List<PictureStamp> stamps = [];
        foreach (var external in owed) {
            var reference = external.Asset.Trim();
            try {
                if (GraphTables.Bake(reference) is { } table) {
                    uploads.Add(plan, external.Image, GraphTables.Entries, 1, table);
                    continue;
                }

                if (reference.StartsWith(MeshMapScheme, StringComparison.Ordinal)) {
                    throw new InvalidDataException("it is a measurement of a mesh, and nothing has been baked yet");
                }

                var path = Locate(reference, roots);
                var stamp = StampOf(path);
                stamps.Add(stamp);
                var picture = Read(stamp);
                uploads.Add(plan, external.Image, picture.Width, picture.Height, picture.Texels);
            } catch (Exception e) when (e is IOException
                                            or InvalidDataException
                                            or UnauthorizedAccessException
                                            or ArgumentException
                                            or FormatException) {
                refusals.Add(
                    new(Problem, $"This graph reads '{reference}', which cannot be read: {e.Message}", external.Node)
                );
            }
        }

        return new(refusals, stamps);
    }

    static PictureStamp StampOf(string path) {
        FileInfo file = new(path);
        return file.Exists ? new(path, file.Length, file.LastWriteTimeUtc) : new(path, -1, default);
    }

    static string Locate(string reference, IReadOnlyList<string> roots) {
        if (Path.IsPathRooted(reference)) {
            return File.Exists(reference) ? reference : throw new FileNotFoundException("there is no such file");
        }

        foreach (var root in roots) {
            var candidate = Path.GetFullPath(Path.Combine(root, reference));
            if (File.Exists(candidate)) {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"there is no such file beside the graph or in the library, among {roots.Count} place(s) looked in"
        );
    }

    Decoded Read(PictureStamp stamp) {
        if (decoded.TryGetValue(stamp, out var known)) {
            return known;
        }

        using var stream = File.OpenRead(stamp.Path);
        var data = decoder.Decode(stream, Path.GetExtension(stamp.Path));
        if (data.Format != PixelFormat.Rgba8UNorm) {
            throw new InvalidDataException($"a bitmap is 8-bit RGBA, and this file decodes to {data.Format}");
        }

        Decoded picture = new(data.Width, data.Height, [..data.Pixels]);
        if (held + picture.Texels.Length > Budget) {
            decoded.Clear();
            held = 0;
        }

        decoded[stamp] = picture;
        held += picture.Texels.Length;
        return picture;
    }

    sealed record Decoded(int Width, int Height, byte[] Texels);
}
