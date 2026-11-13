using Vixen.Core.Imaging;

namespace Patina.Export;

/// <summary>
///     A picture chosen as a brush's alpha (#122): how much each of its texels lays down, 0 to 1, from any picture the
///     library reads.
/// </summary>
/// <remarks>
///     ⚠ <b>A picture with any see-through texel is read by its opacity, and an opaque one by its brightness.</b> An
///     alpha drawn as a shape on a clear ground is a shape whatever colour it was painted in, and one drawn white on
///     black has nothing in its opacity to read. Reading only the red, as the first version did, made a black shape on a
///     clear ground paint nothing.
/// </remarks>
public static class BrushAlphaPictures {
    /// <summary>Reads a picture file as an alpha's coverage, top row first.</summary>
    /// <exception cref="InvalidDataException">It is not a picture this reads.</exception>
    public static (int Width, int Height, float[] Coverage) Read(string path) {
        var picture = MapPictures.ReadFile(path);
        return (picture.Width, picture.Height, Coverage(picture));
    }

    /// <summary>How much each texel lays down: its opacity if any texel is see-through, else its brightness.</summary>
    public static float[] Coverage(FloatImage picture) {
        ArgumentNullException.ThrowIfNull(picture);
        var count = picture.Width * picture.Height;
        var channels = picture.Channels;
        var samples = picture.Samples;
        var alphaAt = channels is 2 or 4 ? channels - 1 : -1;
        var clear = alphaAt >= 0 && Enumerable.Range(0, count).Any(at => samples[at * channels + alphaAt] < 0.999f);
        var coverage = new float[count];
        for (var at = 0; at < count; at++) {
            var first = at * channels;
            var value = clear
                ? samples[first + alphaAt]
                : channels >= 3
                    ? 0.2126f * samples[first] + 0.7152f * samples[first + 1] + 0.0722f * samples[first + 2]
                    : samples[first];
            coverage[at] = Math.Clamp(value, 0f, 1f);
        }

        return coverage;
    }
}
