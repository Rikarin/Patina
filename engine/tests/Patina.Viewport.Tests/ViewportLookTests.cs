using System.Globalization;
using System.Text;

namespace Patina.Viewport.Tests;

public sealed class ViewportLookTests {
    static string Table(int size, Func<float, float, float, (float R, float G, float B)> grade) {
        var text = new StringBuilder($"LUT_3D_SIZE {size}\n");
        for (var b = 0; b < size; b++) {
            for (var g = 0; g < size; g++) {
                for (var r = 0; r < size; r++) {
                    var (x, y, z) = grade(r / (size - 1f), g / (size - 1f), b / (size - 1f));
                    text.Append(CultureInfo.InvariantCulture, $"{x} {y} {z}\n");
                }
            }
        }

        return text.ToString();
    }

    static byte[] Frame() {
        var frame = new byte[256 * 4];
        for (var x = 0; x < 256; x++) {
            (frame[x * 4], frame[x * 4 + 1], frame[x * 4 + 2], frame[x * 4 + 3]) = ((byte)x, (byte)(255 - x), (byte)(x / 2), 200);
        }

        return frame;
    }

    [Fact]
    public void An_identity_table_changes_no_pixel() {
        var frame = Frame();

        ViewportLook.Parse(Table(17, static (r, g, b) => (r, g, b))).Apply(frame, 256, 1);

        Assert.Equal(Frame(), frame);
    }

    [Fact]
    public void A_table_takes_its_channels_in_order_with_red_fastest() {
        var frame = Frame();

        // Swapping red and blue is the grade that a table read the wrong way round cannot do.
        ViewportLook.Parse(Table(33, static (r, g, b) => (b, g, r))).Apply(frame, 256, 1);

        for (var x = 0; x < 256; x++) {
            Assert.InRange(frame[x * 4], (x / 2) - 1, (x / 2) + 1);
            Assert.InRange(frame[x * 4 + 2], x - 1, x + 1);
            Assert.Equal(200, frame[x * 4 + 3]);
        }
    }

    [Fact]
    public void A_file_that_is_not_a_table_is_refused() =>
        Assert.Throws<FormatException>(() => ViewportLook.Parse("this is not a table"));
}
