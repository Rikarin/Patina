using Vixen.TextureGraph;

namespace Patina.Worker;

/// <summary>One texel of a map, as the plan stored it, as the four numbers a stroke's colour is given in.</summary>
static class StackSampling {
    /// <summary>
    ///     Red, green, blue and alpha, 0 to 1. A one-number map repeats its number in the first three, as a grey; a
    ///     two-number map, a normal, rebuilds its third from the two.
    /// </summary>
    /// <param name="texel">The texel's bytes.</param>
    /// <param name="format">How they are laid out.</param>
    public static IReadOnlyList<float> Value(byte[] texel, TextureFormat format) {
        switch (format) {
            case TextureFormat.R8:
                return Grey(texel[0] / 255f);
            case TextureFormat.Rg8: {
                var (x, y) = ((texel[0] / 255f * 2f) - 1f, (texel[1] / 255f * 2f) - 1f);
                var z = MathF.Sqrt(MathF.Max(0f, 1f - (x * x) - (y * y)));
                return [texel[0] / 255f, texel[1] / 255f, (z * 0.5f) + 0.5f, 1f];
            }
            case TextureFormat.R16Float:
                return Grey((float)BitConverter.ToHalf(texel, 0));
            case TextureFormat.Rgba16Float:
                return [..Enumerable.Range(0, 4).Select(lane => Unit((float)BitConverter.ToHalf(texel, lane * 2)))];
            case TextureFormat.R32Float:
                return Grey(BitConverter.ToSingle(texel, 0));
            case TextureFormat.Rgba32Float:
                return [..Enumerable.Range(0, 4).Select(lane => Unit(BitConverter.ToSingle(texel, lane * 4)))];
            default:
                return [..texel.Take(4).Select(static lane => lane / 255f)];
        }
    }

    static float[] Grey(float value) {
        var unit = Unit(value);
        return [unit, unit, unit, 1f];
    }

    /// <summary>A colour is given 0 to 1, so a wider float is held to that.</summary>
    static float Unit(float value) => float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
}
