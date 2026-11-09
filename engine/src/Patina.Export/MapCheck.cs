using Patina.Contracts;
using Vixen.Core.Imaging;

namespace Patina.Export;

/// <summary>
///     Measures what share of a texture set's texels holds a value no physical material has (#159), on the maps as an
///     export writes them. A texel is metal above 0.9 metalness and not metal below 0.1, and between is partial. Base
///     colour is judged by its luminance as sRGB: a surface that is not metal runs from 30 to 240, and bare metal from
///     180 up, the ranges Vixen's PBR Validate filter paints. Only texels the layout covers count, so the padding round
///     the islands and the space between them say nothing.
/// </summary>
public static class MapCheck {
    const float MetalBelow = 0.1f;
    const float MetalAbove = 0.9f;

    /// <summary>The luminance thresholds, decoded: sRGB 30 and 240 for a surface that is not metal, and 180 for metal.</summary>
    static readonly float Darkest = Linear(30f / 255f);

    static readonly float Brightest = Linear(240f / 255f);
    static readonly float DarkestMetal = Linear(180f / 255f);

    static readonly float[] Decoded = [..Enumerable.Range(0, 256).Select(static level => Linear(level / 255f))];

    /// <summary>
    ///     Each problem's share of the covered texels, and its first texel, for each problem the maps have. A set with
    ///     no metalness is all not metal.
    /// </summary>
    /// <param name="baseColour">The base colour, RGBA with colour in sRGB, top row first, or null.</param>
    /// <param name="metalness">Metalness in red, or null.</param>
    /// <param name="covered">Which texels of a square grid the layout covers, row by row, or null for every one.</param>
    /// <param name="side">The grid's side.</param>
    public static IReadOnlyList<MapMeasure> Measure(Bitmap? baseColour, Bitmap? metalness, bool[]? covered, int side) {
        var (width, height) = baseColour is { } colour
            ? (colour.Width, colour.Height)
            : metalness is { } metal
                ? (metal.Width, metal.Height)
                : (0, 0);
        int[] counts = new int[CheckCodes.All.Count];
        var first = new (int X, int Y)?[CheckCodes.All.Count];
        var total = 0;
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                if (covered is not null && !covered[y * side / height * side + x * side / width]) {
                    continue;
                }

                total++;
                var m = metalness is { } map ? Level(map, x, y, width, height) / 255f : 0f;
                var code = Judged(baseColour, x, y, width, height, m);
                if (code is null) {
                    continue;
                }

                var at = IndexOf(code);
                counts[at]++;
                first[at] ??= (x, y);
            }
        }

        List<MapMeasure> found = [];
        for (var at = 0; at < counts.Length; at++) {
            if (counts[at] > 0 && first[at] is var (x, y)) {
                var code = CheckCodes.All[at];
                found.Add(
                    new(code, (double)counts[at] / total, new(code, -1, (x + 0.5f) / width, (y + 0.5f) / height, null))
                );
            }
        }

        return found;
    }

    /// <summary>What is wrong with one texel, or null.</summary>
    static string? Judged(Bitmap? baseColour, int x, int y, int width, int height, float metal) {
        if (metal is > MetalBelow and < MetalAbove) {
            return CheckCodes.MetalPartial;
        }

        if (baseColour is not { } colour) {
            return null;
        }

        var (cx, cy) = (x * colour.Width / width, y * colour.Height / height);
        var at = colour.Offset(cx, cy);
        var luminance = 0.2126f * Decoded[colour.Pixels[at]]
            + 0.7152f * Decoded[colour.Pixels[at + 1]]
            + 0.0722f * Decoded[colour.Pixels[at + 2]];
        return metal >= MetalAbove
            ? luminance < DarkestMetal ? CheckCodes.MetalDark : null
            : luminance < Darkest
                ? CheckCodes.AlbedoDark
                : luminance > Brightest
                    ? CheckCodes.AlbedoBright
                    : null;
    }

    static byte Level(Bitmap map, int x, int y, int width, int height) =>
        map.Pixels[map.Offset(x * map.Width / width, y * map.Height / height)];

    static int IndexOf(string code) {
        for (var at = 0; at < CheckCodes.All.Count; at++) {
            if (CheckCodes.All[at] == code) {
                return at;
            }
        }

        return -1;
    }

    static float Linear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
}
