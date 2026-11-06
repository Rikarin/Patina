using Patina.Export;
using System.Collections.Immutable;
using Vixen.Graphics;
using Vixen.Interchange;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Graphs;

/// <summary>A file a compiled graph read, as it was when it was read.</summary>
public readonly record struct PictureStamp(string Path, long Length, DateTime Written);

/// <summary>What filling a compilation's external images came to.</summary>
/// <param name="Refusals">One per image that could not be filled. With any here, the plan cannot run.</param>
/// <param name="Stamps">The files that were read, to notice one changing.</param>
public sealed record FilledPictures(IReadOnlyList<NodeDiagnostic> Refusals, IReadOnlyList<PictureStamp> Stamps);

/// <summary>
///     Fills the external images a compilation names and cannot carry: bitmaps from files through Vixen's decoder.
///     Compiling loads them again after every structural edit, so decoded pixels are kept, up to a budget, under each
///     file's length and time. Everything here can be read again, so a new Worker starts without it.
/// </summary>
public sealed class GraphPictures {
    /// <summary>What a graph hears when a picture it reads cannot be read.</summary>
    public const string Problem = "PT0001";

    const string MeshMapScheme = "meshmap:";
    const long Budget = 512L * 1024 * 1024;

    /// <summary>
    ///     What a mesh map reads as with no bake: mid grey, so that one worn-edge mask leaves the rest of a stack to be
    ///     seen. The caller is told, and says so.
    /// </summary>
    static readonly byte[] NoBake = [128, 128, 128, 255];

    readonly Dictionary<PictureStamp, Decoded> decoded = [];
    readonly StbImageDecoder decoder = new();
    long held;

    /// <summary>Whether a file is no longer what was read.</summary>
    public static bool Moved(PictureStamp stamp) => StampOf(stamp.Path) != stamp;

    /// <summary>Uploads every image in <paramref name="owed" />, and says which could not be.</summary>
    /// <param name="uploads">Where they go.</param>
    /// <param name="plan">The plan that reads them.</param>
    /// <param name="owed">What it reads.</param>
    /// <param name="roots">The folders a relative picture is read from.</param>
    /// <param name="meshMaps">The bake chosen, or null.</param>
    /// <param name="unbaked">
    ///     Collects the mesh maps read with no bake, which then stand in as grey. Null refuses them, as a graph does: a
    ///     graph's maps mean nothing without its bake, and a stack's other layers still do.
    /// </param>
    public FilledPictures Fill(
        TextureUploads uploads,
        TexturePlan plan,
        ImmutableArray<TextureGraphExternal> owed,
        IReadOnlyList<string> roots,
        string? meshMaps,
        ICollection<string>? unbaked = null
    ) {
        List<NodeDiagnostic> refusals = [];
        List<PictureStamp> stamps = [];
        foreach (var external in owed) {
            var reference = external.Asset.Trim();
            try {
                PictureStamp stamp;
                Decoded picture;
                if (meshMaps is null
                    && unbaked is not null
                    && reference.StartsWith(MeshMapScheme, StringComparison.Ordinal)) {
                    unbaked.Add(reference[MeshMapScheme.Length..]);
                    uploads.Add(plan, external.Image, 1, 1, NoBake);
                    continue;
                }

                if (reference.StartsWith(MeshMapScheme, StringComparison.Ordinal)) {
                    (stamp, picture) = ReadMeshMap(reference[MeshMapScheme.Length..], meshMaps);
                } else {
                    stamp = StampOf(Locate(reference, roots));
                    picture = Read(stamp);
                }

                stamps.Add(stamp);
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

    /// <summary>A map of the bake this graph reads, by what it measures.</summary>
    (PictureStamp Stamp, Decoded Picture) ReadMeshMap(string map, string? folder) {
        if (folder is null) {
            throw new InvalidDataException("it is a measurement of a mesh, and no bake has been chosen for this graph");
        }

        if (BakedMapPicture.Find(folder, map) is not { } found) {
            throw new FileNotFoundException($"the bake in '{folder}' has no {map} map");
        }

        var stamp = StampOf(found.Path);
        if (decoded.TryGetValue(stamp, out var known)) {
            return (stamp, known);
        }

        var bitmap = BakedMapPicture.Read(found.Path, found.Map, found.Format);
        return (stamp, Keep(stamp, new(bitmap.Width, bitmap.Height, bitmap.Pixels)));
    }

    static PictureStamp StampOf(string path) {
        FileInfo file = new(path);
        return file.Exists ? new(path, file.Length, file.LastWriteTimeUtc) : new(path, -1, default);
    }

    static bool Inside(string root, string candidate) =>
        candidate.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    static string Locate(string reference, IReadOnlyList<string> roots) {
        if (Path.IsPathRooted(reference)) {
            return File.Exists(reference) ? reference : throw new FileNotFoundException("there is no such file");
        }

        foreach (var root in roots) {
            var candidate = Path.GetFullPath(Path.Combine(root, reference));
            if (File.Exists(candidate) && Inside(root, candidate)) {
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

        return Keep(stamp, new(data.Width, data.Height, [..data.Pixels]));
    }

    Decoded Keep(PictureStamp stamp, Decoded picture) {
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
