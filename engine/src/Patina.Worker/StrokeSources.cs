using Patina.Contracts;
using Patina.Ipc;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>
///     Where a stroke's colour comes from and what masks its brush, as Vixen paints them: a smear of the canvas, the
///     composite at an offset, a material's map, a picture on the screen. Nothing here holds state of its own, so a
///     replay makes the same stroke from the same journal.
/// </summary>
static class StrokeSources {
    /// <summary>A stroke's source, or null for one that paints its colour.</summary>
    /// <param name="part">The stroke's first part.</param>
    /// <param name="canvas">The channel it paints.</param>
    /// <param name="coverage">Which texels are surface.</param>
    /// <param name="picture">Reads a picture the stroke names.</param>
    /// <param name="composite">What a stack written out composites in a channel, at the canvas's size.</param>
    public static IPaintSource? Source(
        StackPaintPart part,
        PaintImage canvas,
        PaintCoverage coverage,
        Func<string, PaintImage> picture,
        Func<string, string, PaintImage?> composite
    ) {
        switch (part.Source) {
            case null:
                return null;
            case { Kind: "smudge" }:
                return new PaintSmudge(canvas, coverage);
            case { Kind: "picture", Picture: { } shown }:
                return new PaintProjectedPicture(picture(shown.Image), Frame(shown));
            case { Kind: "clone" or "material" } read:
                var document = part.SourceDocument
                    ?? throw new IpcException("A clone or a material stroke came without what it reads.");
                var offset = read.Offset is [var x, var y] ? new Vector2(x, y) : Vector2.Zero;

                // ⚠ Made as the stroke's turn comes and not as it begins: a replay begins every stroke before it
                // stamps any, and a composite read then would be of canvases the strokes before it have not reached.
                return new Deferred(() => composite(document, part.Usage) is { } image
                        ? new PaintAtlasSource(image, canvas.Width, canvas.Height, offset)
                        : null
                );
            default:
                throw new IpcException($"'{part.Source.Kind}' is not where a stroke's colour can come from.");
        }
    }

    /// <summary>A stroke's stencil, or null.</summary>
    public static IPaintStencil? Stencil(StackPicture? stencil, Func<string, PaintImage> picture) =>
        stencil is null ? null : new PaintProjectedPicture(picture(stencil.Image), Frame(stencil));

    /// <summary>How much of the mesh a click of a fill takes.</summary>
    public static PaintFillMode Fill(string? mode) =>
        mode switch {
            "triangle" => PaintFillMode.Triangle,
            "part" => PaintFillMode.Part,
            "shell" => PaintFillMode.Shell,
            _ => PaintFillMode.Polygon
        };

    /// <summary>A picture file as a stroke reads it, its colours and transparency as they are.</summary>
    public static PaintImage Read(string file) {
        try {
            var decoded = PngCodec.Decode(SharedFile.ReadAllBytes(file));
            PaintImage image = new(decoded.Width, decoded.Height);
            image.CopyFrom(decoded.Pixels);
            return image;
        } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) {
            throw new IpcException($"'{Path.GetFileName(file)}' would not read as a picture: {e.Message}");
        }
    }

    static PaintFrame Frame(StackPicture picture) =>
        new(new(picture.Centre[0], picture.Centre[1]), picture.Scale, picture.Angle, picture.Tiled);

    /// <summary>A source made the first time a stamp asks, and nothing at all when there is nothing to read.</summary>
    sealed class Deferred(Func<IPaintSource?> make) : IPaintSource {
        IPaintSource? made;
        bool asked;

        public bool ReadsCanvas => false;

        public void Stamp(in PaintStamp stamp, Vector2? atlas) {
            if (!asked) {
                asked = true;
                made = make();
            }

            made?.Stamp(stamp, atlas);
        }

        public bool TryColour(int texel, Vector2 picture, out uint colour) {
            colour = 0u;
            return made is not null && made.TryColour(texel, picture, out colour);
        }
    }
}
