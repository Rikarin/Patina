using Patina.Contracts;
using Patina.Export;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Vixen.Core.Imaging;
using Vixen.Graphics;
using Vixen.Interchange;
using Vixen.NodeGraph;
using Vixen.TextureGraph;
using Vixen.TextureGraph.Drawing;
using Vixen.Texturing.Painting;

namespace Patina.Graphs;

/// <summary>A file a compiled graph read, as it was when it was read.</summary>
public readonly record struct PictureStamp(string Path, long Length, DateTime Written);

/// <summary>What filling a compilation's external images came to.</summary>
/// <param name="Refusals">One per image that could not be filled. With any here, the plan cannot run.</param>
/// <param name="Stamps">The files that were read, to notice one changing.</param>
public sealed record FilledPictures(IReadOnlyList<NodeDiagnostic> Refusals, IReadOnlyList<PictureStamp> Stamps) {
    /// <summary>The channels read from a canvas being painted, which <see cref="GraphPaintTiles" /> carries.</summary>
    public IReadOnlyList<LivePicture> Live { get; init; } = [];
}

/// <summary>A channel read from a canvas being painted, and the plan's image it was uploaded to.</summary>
public readonly record struct LivePicture(int Image, PictureStamp Stamp);

/// <summary>
///     Fills the external images a compilation names and cannot carry: bitmaps from files through Vixen's decoder, and
///     the text and SVG drawings Vixen draws from a font or an SVG file found the way a bitmap is.
///     Compiling loads them again after every structural edit, so decoded pixels are kept, up to a budget, under each
///     file's length and time. Everything here can be read again, so a new Worker starts without it.
/// </summary>
public sealed class GraphPictures {
    /// <summary>What a graph hears when a picture it reads cannot be read.</summary>
    public const string Problem = "PT0001";

    const long Budget = 512L * 1024 * 1024;

    /// <summary>
    ///     What a mesh map reads as with no bake: mid grey, so that one worn-edge mask leaves the rest of a stack to be
    ///     seen. The caller is told, and says so.
    /// </summary>
    static readonly byte[] NoBake = [128, 128, 128, 255];

    /// <summary>What an unpainted layer or channel reads as: nothing, as in the stack.</summary>
    static readonly Decoded Clear = new(1, 1, [0, 0, 0, 0], TextureFormat.Rgba8);

    /// <summary>When a stamp says it was read from a canvas still being painted rather than from its file.</summary>
    static readonly DateTime Live = DateTime.MaxValue;

    readonly Dictionary<PictureStamp, Decoded> decoded = [];
    long held;

    /// <summary>
    ///     The canvas a painting session holds for a <c>.vxpaint</c>, by its full path, strokes not yet saved and all,
    ///     or null when nobody is painting it. A graph reads that in place of the file, so a painted mask feeds a graph
    ///     as it is painted (#205), as an instance in another texture set reads it (#108).
    /// </summary>
    public Func<string, PaintCanvas?> Painting { get; set; } = static _ => null;

    /// <summary>Whether a file is no longer what was read.</summary>
    public static bool Moved(PictureStamp stamp) => StampOf(stamp.Path) != stamp;

    /// <summary>
    ///     Whether what was read is no longer what there is: a file that moved, a canvas painted since, a canvas whose
    ///     painting ended, and a file somebody has since begun to paint.
    /// </summary>
    public bool Stale(PictureStamp stamp) {
        if (stamp.Written != Live) {
            return Moved(stamp) || Painting(stamp.Path) is not null;
        }

        return Canvas(stamp, out var usage) is not { } canvas || Revision(canvas, usage) != stamp.Length;
    }

    /// <summary>
    ///     The channel a stamp read from a canvas being painted is now, or null when the stamp read a file, the
    ///     painting has ended, or the channel is not painted.
    /// </summary>
    public PaintImage? Channel(PictureStamp stamp) =>
        stamp.Written == Live && Canvas(stamp, out var usage) is { } canvas && canvas.Has(usage)
            ? canvas.Channel(usage)
            : null;

    /// <summary>A stamp of a canvas being painted as it is now, once what moved on it has been carried.</summary>
    public PictureStamp Restamped(PictureStamp stamp) =>
        stamp.Written == Live && Canvas(stamp, out var usage) is { } canvas
            ? stamp with { Length = Revision(canvas, usage) }
            : stamp;

    /// <summary>The canvas a live stamp read, if it is still being painted, and the channel it read.</summary>
    PaintCanvas? Canvas(PictureStamp stamp, out string usage) {
        var split = stamp.Path.LastIndexOf('|');
        usage = stamp.Path[(split + 1)..];
        return Painting(stamp.Path[..split]);
    }

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
        List<LivePicture> live = [];
        foreach (var external in owed) {
            var reference = external.Asset.Trim();
            if (TextureDrawings.Draws(reference)) {
                Draw(uploads, plan, external, roots, refusals, stamps);
                continue;
            }

            try {
                PictureStamp stamp;
                Decoded picture;
                if (meshMaps is null
                    && unbaked is not null
                    && reference.StartsWith(GraphFiles.MeshMapScheme, StringComparison.Ordinal)) {
                    unbaked.Add(reference[GraphFiles.MeshMapScheme.Length..]);
                    uploads.AddPicture(plan, external.Image, 1, 1, NoBake, TextureFormat.Rgba8);
                    continue;
                }

                if (reference.StartsWith(GraphFiles.MeshMapScheme, StringComparison.Ordinal)) {
                    (stamp, picture) = ReadMeshMap(reference[GraphFiles.MeshMapScheme.Length..], meshMaps);
                } else if (PaintReference.Claims(reference)) {
                    (stamp, picture) = ReadPaint(reference, roots);
                } else {
                    stamp = StampOf(Locate(reference, roots));
                    picture = Read(stamp);
                }

                stamps.Add(stamp);
                if (stamp.Written == Live) {
                    live.Add(new(external.Image, stamp));
                }

                // At the depth the picture is, whatever the plan declares: a bitmap is declared eight-bit before its
                // file is opened, and a mesh map half-float whether or not its bake wrote that deep.
                uploads.AddPicture(plan, external.Image, picture.Width, picture.Height, picture.Texels, picture.Format);
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

        return new(refusals, stamps) { Live = live };
    }

    /// <summary>
    ///     Draws a Source/Text or a Source/Vector. Vixen draws it at the bake's size; what Patina owns is where its
    ///     font or SVG is, found as a bitmap is and read sharing write, and the stamp that notices the file changing.
    /// </summary>
    static void Draw(
        TextureUploads uploads,
        TexturePlan plan,
        TextureGraphExternal external,
        IReadOnlyList<string> roots,
        List<NodeDiagnostic> refusals,
        List<PictureStamp> stamps
    ) {
        var file = TextureDrawings.FileOf(external.Asset);
        try {
            TextureDrawings.Fill(
                uploads,
                plan,
                external,
                name => {
                    var stamp = StampOf(Locate(name, roots));
                    stamps.Add(stamp);
                    return SharedFile.ReadAllBytes(stamp.Path);
                }
            );
        } catch (Exception e) when (e is IOException
                                        or InvalidDataException
                                        or UnauthorizedAccessException
                                        or ArgumentException
                                        or FormatException) {
            refusals.Add(
                new(
                    Problem,
                    file is null
                        ? $"This graph's text cannot be drawn: {e.Message}"
                        : $"This graph draws '{file}', which cannot be drawn: {e.Message}",
                    external.Node
                )
            );
        }
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

        // ⚠ At the depth it was baked, as half-floats: read at eight bits, a position map held 256 places across the
        // model and a volume noise finer than that drew stairs.
        return (stamp, Keep(stamp, Widened(BakedMapPicture.ReadDeep(found.Path, found.Map, found.Format))));
    }

    /// <summary>
    ///     One channel of a painted canvas, as a layer stack names it and a graph written from one keeps naming it
    ///     (#205): the canvas being painted when a session of this Worker paints it, else the file beside the graph
    ///     as it was last saved. A layer never painted has no file yet, and it and a channel never painted are clear,
    ///     as in the stack; the missing file is stamped, so its first save is noticed.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     The reference is malformed or reaches outside the graph's folder.
    /// </exception>
    (PictureStamp Stamp, Decoded Picture) ReadPaint(string reference, IReadOnlyList<string> roots) {
        if (!PaintReference.TryParse(reference, out var relative, out var usage) || roots.Count == 0) {
            throw new InvalidDataException("it does not name both a painted file and one of its channels");
        }

        // Beside the graph alone, as a stack's paint is beside the stack.
        var file = Path.GetFullPath(Path.Combine(roots[0], relative));
        if (!Inside(roots[0], file)) {
            throw new InvalidDataException("a painted file is read from beside the graph, and this is elsewhere");
        }

        if (Painting(file) is { } painting) {
            return Painted(painting, file, usage);
        }

        var stamp = StampOf(file);
        // One file holds a picture per channel, so each channel is kept under its own name.
        var key = stamp with { Path = $"{stamp.Path}|{usage}" };
        if (decoded.TryGetValue(key, out var known)) {
            return (stamp, known);
        }

        if (stamp.Length < 0) {
            return (stamp, Clear);
        }

        PaintCanvas canvas;
        using (var stream = SharedFile.OpenRead(stamp.Path)) {
            canvas = PaintCanvas.Read(stream);
        }

        if (!canvas.Has(usage)) {
            return (stamp, Clear);
        }

        return (stamp, Keep(key, Texels(canvas.Channel(usage))));
    }

    /// <summary>
    ///     One channel of a canvas being painted, stamped with the channel's revision. Never kept: the next stroke
    ///     makes it another picture, and the canvas holds it already.
    /// </summary>
    static (PictureStamp Stamp, Decoded Picture) Painted(PaintCanvas canvas, string file, string usage) {
        PictureStamp stamp = new($"{file}|{usage}", Revision(canvas, usage), Live);
        return (stamp, canvas.Has(usage) ? Texels(canvas.Channel(usage)) : Clear);
    }

    /// <summary>A channel's revision, or -1 for one never painted.</summary>
    static long Revision(PaintCanvas canvas, string usage) => canvas.Has(usage) ? canvas.Channel(usage).Revision : -1;

    static Decoded Texels(PaintImage image) {
        var format = image.Format == PaintTexelFormat.Rgba8
            ? TextureFormat.Rgba8
            : PaintTexelFormats.Texture(image.Format);
        return new(image.Width, image.Height, image.ToTexels(), format);
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

        if (MapPictures.IsDeep(stamp.Path)) {
            return Keep(stamp, Widened(MapPictures.ReadFile(stamp.Path)));
        }

        using var stream = SharedFile.OpenRead(stamp.Path);
        var extension = Path.GetExtension(stamp.Path).ToLowerInvariant();
        var data = (ImageDecoders.For(ImageDecoders.BuiltIn, extension)
                ?? throw new InvalidDataException($"'{extension}' is not a picture this reads"))
            .Decode(stream, extension);
        if (data.Format != PixelFormat.Rgba8UNorm) {
            throw new InvalidDataException($"a bitmap is 8-bit RGBA, and this file decodes to {data.Format}");
        }

        return Keep(stamp, new(data.Width, data.Height, [..data.Pixels], TextureFormat.Rgba8));
    }

    /// <summary>
    ///     A deep picture as half-floats, whatever its lanes: the depth a Mesh Map node declares, and the one a deep
    ///     bitmap is uploaded at. Half-floats and not floats, because a bitmap filters and not every device filters a
    ///     float.
    /// </summary>
    static Decoded Widened(FloatImage image) {
        var lanes = image.Channels;
        var texels = new Half[image.Width * image.Height * 4];
        for (var at = 0; at < image.Width * image.Height; at++) {
            var from = at * lanes;
            for (var lane = 0; lane < 3; lane++) {
                texels[at * 4 + lane] = (Half)image.Samples[from + (lanes >= 3 ? lane : 0)];
            }

            texels[at * 4 + 3] = lanes is 4 or 2 ? (Half)image.Samples[from + lanes - 1] : (Half)1f;
        }

        return new(image.Width, image.Height, [..MemoryMarshal.AsBytes(texels.AsSpan())], TextureFormat.Rgba16Float);
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

    sealed record Decoded(int Width, int Height, byte[] Texels, TextureFormat Format);
}
