using Patina.Export.Presets;
using Vixen.Core.Imaging;

namespace Patina.Export;

/// <summary>What a preset's lanes came to, before they are a file.</summary>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
/// <param name="Lanes">One to four.</param>
/// <param name="Samples">Lanes interleaved, row zero first.</param>
/// <param name="Warnings">What a person should know.</param>
public sealed record PackedPicture(int Width, int Height, int Lanes, float[] Samples, IReadOnlyList<string> Warnings);

/// <summary>
///     Fills a file's lanes from a project's maps. The one place that turns green over, and the one place that moves a
///     number between sRGB and linear, so a preset says what it wants and neither is done twice. Rows are never turned
///     over: row zero is v = 0 in every map, and every engine here reads a picture's top row as the same.
/// </summary>
public static class ChannelPacker {
    /// <summary>Whether a project has anything this texture reads.</summary>
    public static bool Applies(ExportTexture texture, IReadOnlyDictionary<string, MapPicture> maps) {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(maps);
        return !texture.Maps.Any() || texture.Maps.Any(maps.ContainsKey);
    }

    /// <summary>Packs one texture.</summary>
    /// <param name="texture">What the file holds.</param>
    /// <param name="maps">The project's maps by name.</param>
    /// <param name="green">The green the target reads.</param>
    public static PackedPicture Pack(
        ExportTexture texture,
        IReadOnlyDictionary<string, MapPicture> maps,
        GreenAxis green
    ) {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(maps);

        var present = texture.Maps.Where(maps.ContainsKey).Select(m => maps[m]).ToList();
        var width = present.Count == 0 ? 1 : present.Max(static m => m.Image.Width);
        var height = present.Count == 0 ? 1 : present.Max(static m => m.Image.Height);
        List<string> warnings = [];
        foreach (var missing in texture.Maps.Where(m => !maps.ContainsKey(m))) {
            warnings.Add($"{texture.File}: no {missing} map, so its lanes hold the default.");
        }

        foreach (var map in present.Where(m => m.Image.Width != width || m.Image.Height != height)) {
            warnings.Add($"{texture.File}: {map.Name} was {map.Image.Width}×{map.Image.Height} and was scaled to fit.");
        }

        var lanes = texture.Channels.Count;
        var samples = new float[width * height * lanes];
        for (var lane = 0; lane < lanes; lane++) {
            Fill(samples, lane, lanes, (width, height), texture, texture.Channels[lane], maps, green);
        }

        return new(width, height, lanes, samples, warnings);
    }

    static void Fill(
        float[] samples,
        int lane,
        int lanes,
        (int Width, int Height) size,
        ExportTexture texture,
        ChannelSource source,
        IReadOnlyDictionary<string, MapPicture> maps,
        GreenAxis green
    ) {
        var (width, height) = size;
        if (source.Map is not { } name) {
            ForEach(width, height, (x, y) => samples[(y * width + x) * lanes + lane] = source.Constant);
            return;
        }

        if (!maps.TryGetValue(name, out var picture)) {
            var value = ExportMaps.Default(name, source.Lane);
            value = source.Inverted ? 1f - value : value;
            ForEach(width, height, (x, y) => samples[(y * width + x) * lanes + lane] = value);
            return;
        }

        var turn = name == ExportMaps.Normal && source.Lane == 1 && picture.Green != green;
        var transfer = Transfer(name, source.Lane, texture.Space);
        var image = picture.Image;
        var scaled = image.Width != width || image.Height != height;
        ForEach(
            width,
            height,
            (x, y) => {
                var value = scaled ? Bilinear(image, source.Lane, x, y, width, height) : Lane(image, x, y, source.Lane);
                value = transfer(turn ? 1f - value : value);
                samples[(y * width + x) * lanes + lane] = source.Inverted ? 1f - value : value;
            }
        );
    }

    /// <summary>What moves a lane's numbers into the file's space. Alpha is never encoded.</summary>
    static Func<float, float> Transfer(string map, int lane, string space) {
        if (lane == 3) {
            return static value => value;
        }

        return (ExportMaps.IsColour(map), space == "srgb") switch {
            (true, false) => MipChain.ToLinear,
            (false, true) => MipChain.ToSrgb,
            _ => static value => value
        };
    }

    static void ForEach(int width, int height, Action<int, int> set) =>
        Parallel.For(
            0,
            height,
            y => {
                for (var x = 0; x < width; x++) {
                    set(x, y);
                }
            }
        );

    /// <summary>A lane of a texel. A grey map's one lane is every colour lane, and absent alpha is one.</summary>
    static float Lane(FloatImage image, int x, int y, int lane) {
        var at = (y * image.Width + x) * image.Channels;
        return lane < image.Channels ? image.Samples[at + lane]
            : lane == 3 ? 1f
            : image.Channels == 1 ? image.Samples[at] : 0f;
    }

    static float Bilinear(FloatImage image, int lane, int x, int y, int width, int height) {
        var u = (x + 0.5f) / width * image.Width - 0.5f;
        var v = (y + 0.5f) / height * image.Height - 0.5f;
        var left = Math.Clamp((int)MathF.Floor(u), 0, image.Width - 1);
        var top = Math.Clamp((int)MathF.Floor(v), 0, image.Height - 1);
        var right = Math.Min(left + 1, image.Width - 1);
        var bottom = Math.Min(top + 1, image.Height - 1);
        var s = Math.Clamp(u - left, 0f, 1f);
        var t = Math.Clamp(v - top, 0f, 1f);
        var upper = float.Lerp(Lane(image, left, top, lane), Lane(image, right, top, lane), s);
        var lower = float.Lerp(Lane(image, left, bottom, lane), Lane(image, right, bottom, lane), s);
        return float.Lerp(upper, lower, t);
    }
}
