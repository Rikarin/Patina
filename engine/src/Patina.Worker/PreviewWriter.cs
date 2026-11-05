using Patina.Graphs;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Vixen.Core.Imaging;
using Vixen.TextureGraph;

namespace Patina.Worker;

/// <summary>A picture as the content-addressed folder holds it.</summary>
readonly record struct WrittenPicture(string Hash, int Width, int Height);

/// <summary>
///     Writes pictures into the preview folder, remembering which result is which file, so that a result that did not
///     change costs nothing.
/// </summary>
sealed class PreviewWriter {
    /// <summary>How many pictures are remembered, so that a long drag cannot grow this for ever.</summary>
    const int Memory = 8192;

    readonly Dictionary<TextureResultKey, WrittenPicture> written = [];

    /// <summary>The picture of some images of a bake, reading only those the folder lacks.</summary>
    public IReadOnlyList<WrittenPicture> Write(TextureBake bake, IReadOnlyList<int> images, string folder) {
        Dictionary<TextureResultKey, int> owed = [];
        foreach (var image in images) {
            var key = bake.KeyOf(image);
            if (!owed.ContainsKey(key) && !Held(key, folder)) {
                owed[key] = image;
            }
        }

        var pictures = bake.Read([..owed.Values]);
        var encoded = new ConcurrentDictionary<TextureResultKey, WrittenPicture>();
        Parallel.ForEach(
            owed.Keys.Zip(pictures),
            picture => encoded[picture.First] = Encode(picture.Second, folder)
        );
        foreach (var (key, picture) in encoded) {
            written[key] = picture;
        }

        WrittenPicture[] stored = [..images.Select(image => written[bake.KeyOf(image)])];
        if (written.Count > Memory) {
            written.Clear();
        }

        return stored;
    }

    /// <summary>One picture at full size, usually from memory.</summary>
    public WrittenPicture WriteOne(TextureBake bake, int image, string folder) {
        var key = bake.KeyOf(image);
        if (!written.TryGetValue(key, out var picture) || !Held(key, folder)) {
            picture = Encode(bake.Read(image), folder);
            written[key] = picture;
        }

        return picture;
    }

    bool Held(TextureResultKey key, string folder) =>
        written.TryGetValue(key, out var known) && File.Exists(Ipc.GraphPreviewFiles.PathOf(folder, known.Hash));

    static WrittenPicture Encode(Bitmap bitmap, string folder) {
        var png = PngCodec.Encode(bitmap);
        var hash = Convert.ToHexStringLower(SHA256.HashData(png));
        GraphPreviews.Store(folder, hash, png);
        return new(hash, bitmap.Width, bitmap.Height);
    }
}
