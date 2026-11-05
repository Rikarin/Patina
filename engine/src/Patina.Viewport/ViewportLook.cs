using System.Runtime.InteropServices;
using Vixen.Core.Imaging;

namespace Patina.Viewport;

/// <summary>
///     A viewing table: a <c>.cube</c> read through on the way to the screen. It changes what is shown and nothing a
///     file holds, so an export never sees it. A frame is already tonemapped and sRGB-encoded when it gets here, and a
///     table is read as taking those numbers, which is the usual meaning of a display table and is not stated in the
///     format. Full colour management stays out on purpose (plan P6).
/// </summary>
public sealed class ViewportLook {
    readonly int size;
    readonly float[] table;

    ViewportLook(int size, float[] table) {
        this.size = size;
        this.table = table;
    }

    /// <summary>Reads a table.</summary>
    /// <exception cref="FormatException">It is not a 3D table.</exception>
    public static ViewportLook Parse(string text) {
        var data = CubeLut.Parse(text);
        var halves = MemoryMarshal.Cast<byte, Half>(data.Level(0));
        var table = new float[data.Width * data.Width * data.Width * 3];
        for (var entry = 0; entry < table.Length / 3; entry++) {
            for (var lane = 0; lane < 3; lane++) {
                table[entry * 3 + lane] = Math.Clamp((float)halves[entry * 4 + lane], 0f, 1f);
            }
        }

        return new(data.Width, table);
    }

    /// <summary>Reads a tightly packed RGBA frame through the table, in place. Alpha is left alone.</summary>
    public unsafe void Apply(byte* frame, int width, int height) {
        Parallel.For(0, height, row => Line(frame + row * width * 4, width));
    }

    unsafe void Line(byte* line, int width) {
        Span<float> colour = stackalloc float[3];
        for (var x = 0; x < width; x++) {
            var pixel = line + x * 4;
            Sample(pixel[0], pixel[1], pixel[2], colour);
            pixel[0] = (byte)(colour[0] * 255f + 0.5f);
            pixel[1] = (byte)(colour[1] * 255f + 0.5f);
            pixel[2] = (byte)(colour[2] * 255f + 0.5f);
        }
    }

    /// <summary>A trilinear read. Red varies fastest in a table, as the format says.</summary>
    void Sample(byte red, byte green, byte blue, Span<float> colour) {
        var scale = (size - 1) / 255f;
        var (r0, rt) = Split(red * scale);
        var (g0, gt) = Split(green * scale);
        var (b0, bt) = Split(blue * scale);
        var r1 = Math.Min(r0 + 1, size - 1);
        var g1 = Math.Min(g0 + 1, size - 1);
        var b1 = Math.Min(b0 + 1, size - 1);

        for (var lane = 0; lane < 3; lane++) {
            var near = float.Lerp(
                float.Lerp(At(r0, g0, b0, lane), At(r1, g0, b0, lane), rt),
                float.Lerp(At(r0, g1, b0, lane), At(r1, g1, b0, lane), rt),
                gt
            );
            var far = float.Lerp(
                float.Lerp(At(r0, g0, b1, lane), At(r1, g0, b1, lane), rt),
                float.Lerp(At(r0, g1, b1, lane), At(r1, g1, b1, lane), rt),
                gt
            );
            colour[lane] = float.Lerp(near, far, bt);
        }
    }

    float At(int r, int g, int b, int lane) => table[((b * size + g) * size + r) * 3 + lane];

    static (int Whole, float Part) Split(float value) {
        var whole = (int)value;
        return (whole, value - whole);
    }
}
