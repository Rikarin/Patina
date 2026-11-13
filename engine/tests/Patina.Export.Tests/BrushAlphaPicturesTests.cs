using Vixen.Core.Imaging;
using Vixen.Interchange;

namespace Patina.Export.Tests;

/// <summary>A picture chosen as a brush's alpha (#122), read as how much each texel lays down.</summary>
public sealed class BrushAlphaPicturesTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-alpha-");

    public void Dispose() => folder.Delete(true);

    /// <summary>A black shape on a clear ground paints its shape, where its red alone would paint nothing.</summary>
    [Fact]
    public void A_shape_on_a_clear_ground_is_read_by_its_opacity() {
        FloatImage picture = new(2, 1, 4, [0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f]);

        Assert.Equal([1f, 0f], BrushAlphaPictures.Coverage(picture));
    }

    [Fact]
    public void An_opaque_picture_is_read_by_its_brightness() {
        FloatImage colour = new(3, 1, 4, [1f, 1f, 1f, 1f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 1f]);
        FloatImage grey = new(2, 1, 1, [0.25f, 0.75f]);

        var read = BrushAlphaPictures.Coverage(colour);

        Assert.Equal(1f, read[0], 1e-4f);
        Assert.Equal(0f, read[1]);
        Assert.Equal(0.7152f, read[2], 1e-4f);
        Assert.Equal([0.25f, 0.75f], BrushAlphaPictures.Coverage(grey));
    }

    /// <summary>Any picture the library reads will do, not only a PNG: here a TIFF the export writes.</summary>
    [Fact]
    public void A_tiff_reads_as_well_as_a_png() {
        var path = Path.Combine(folder.FullName, "dots.tif");
        File.WriteAllBytes(path, TiffWriter.Encode8(2, 1, 1, [0, 255]));

        var (width, height, coverage) = BrushAlphaPictures.Read(path);

        Assert.Equal((2, 1), (width, height));
        Assert.Equal([0f, 1f], coverage);
    }
}
