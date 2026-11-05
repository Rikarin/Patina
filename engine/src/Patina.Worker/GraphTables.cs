using System.Globalization;
using Vixen.Core.Curves;
using Vixen.Core.Mathematics;

namespace Patina.Worker;

/// <summary>
///     Reads a ramp or curve written inline in a node's text setting, where Vixen leaves a host to resolve an asset
///     reference, and bakes the one-row table the Curve, Gradient and Gradient Map kernels read. Vixen's own bake is
///     internal, so the rows are made here: a curve through its public <see cref="CurveEvaluation" />, and a ramp by
///     mixing neighbouring stops in the stored values. The document stays the only file:
///     <c>ramp:0 000000ff,1 ffffffff</c> lists stops as position and RGBA hex, and
///     <c>curve:r=0 0,1 1;g=0 0,1 1;b=0 0,1 1;a=0 0,1 1</c> lists keys per channel, a channel left out being straight.
/// </summary>
static class GraphTables {
    public const string RampScheme = "ramp:";
    public const string CurveScheme = "curve:";

    /// <summary>The table's width, which is what Vixen bakes.</summary>
    public const int Entries = 256;

    const int HexDigits = 8;
    const int HexChannels = 4;
    const float ByteMaximum = 255f;

    static readonly CurveSample[] Straight = [
        new(0f, 0f, 1f, 1f, TangentMode.Linear),
        new(1f, 1f, 1f, 1f, TangentMode.Linear)
    ];

    /// <summary>The table a reference bakes to, or null if it names something else.</summary>
    /// <exception cref="InvalidDataException">It is a ramp or a curve, written wrongly.</exception>
    public static byte[]? Bake(string reference) {
        if (reference.StartsWith(RampScheme, StringComparison.Ordinal)) {
            var stops = Stops(reference[RampScheme.Length..]);
            return Table(position => At(stops, position));
        }

        if (reference.StartsWith(CurveScheme, StringComparison.Ordinal)) {
            var channels = Channels(reference[CurveScheme.Length..]);
            var (red, green, blue, alpha) = (
                channels.GetValueOrDefault("r", Straight),
                channels.GetValueOrDefault("g", Straight),
                channels.GetValueOrDefault("b", Straight),
                channels.GetValueOrDefault("a", Straight)
            );
            return Table(at => new(
                    CurveEvaluation.Evaluate(red, at),
                    CurveEvaluation.Evaluate(green, at),
                    CurveEvaluation.Evaluate(blue, at),
                    CurveEvaluation.Evaluate(alpha, at)
                )
            );
        }

        return null;
    }

    static byte[] Table(Func<float, Color4> evaluate) {
        var pixels = new byte[Entries * HexChannels];
        for (var entry = 0; entry < Entries; entry++) {
            var colour = evaluate(entry / (float)(Entries - 1));
            pixels[entry * HexChannels] = Quantise(colour.R);
            pixels[(entry * HexChannels) + 1] = Quantise(colour.G);
            pixels[(entry * HexChannels) + 2] = Quantise(colour.B);
            pixels[(entry * HexChannels) + 3] = Quantise(colour.A);
        }

        return pixels;
    }

    static byte Quantise(float value) => (byte)Math.Clamp((int)MathF.Round(value * ByteMaximum), 0, (int)ByteMaximum);

    static Color4 At(List<(float Position, Color4 Colour)> stops, float position) {
        if (position <= stops[0].Position) {
            return stops[0].Colour;
        }

        for (var i = 1; i < stops.Count; i++) {
            var (end, to) = stops[i];
            if (position <= end) {
                var (start, from) = stops[i - 1];
                var t = end > start ? (position - start) / (end - start) : 1f;
                return new(
                    from.R + ((to.R - from.R) * t),
                    from.G + ((to.G - from.G) * t),
                    from.B + ((to.B - from.B) * t),
                    from.A + ((to.A - from.A) * t)
                );
            }
        }

        return stops[^1].Colour;
    }

    static List<(float Position, Color4 Colour)> Stops(string text) {
        List<(float Position, Color4 Colour)> stops = [];
        foreach (var stop in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) {
            var parts = stop.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || parts[1].Length != HexDigits) {
                throw new InvalidDataException($"a ramp stop is a position and eight hex digits, not '{stop}'");
            }

            stops.Add((Number(parts[0]), Colour(parts[1])));
        }

        return stops.Count > 0
            ? [..stops.OrderBy(static stop => stop.Position)]
            : throw new InvalidDataException("a ramp needs a stop");
    }

    static Color4 Colour(string hex) {
        var lanes = new float[HexChannels];
        for (var i = 0; i < HexChannels; i++) {
            if (!byte.TryParse(
                    hex.AsSpan(i * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var lane
                )) {
                throw new InvalidDataException($"'{hex}' is not hex");
            }

            lanes[i] = lane / ByteMaximum;
        }

        return new(lanes[0], lanes[1], lanes[2], lanes[3]);
    }

    static Dictionary<string, CurveSample[]> Channels(string text) {
        Dictionary<string, CurveSample[]> channels = new(StringComparer.Ordinal);
        foreach (var channel in text.Split(
                     ';',
                     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                 )) {
            var split = channel.IndexOf('=', StringComparison.Ordinal);
            if (split < 1) {
                throw new InvalidDataException($"a curve channel is a name and its keys, not '{channel}'");
            }

            channels[channel[..split]] = Keys(channel[(split + 1)..]);
        }

        return channels;
    }

    static CurveSample[] Keys(string text) {
        List<CurveSample> keys = [];
        foreach (var key in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) {
            var parts = key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) {
                throw new InvalidDataException($"a curve key is two numbers, not '{key}'");
            }

            keys.Add(new(Number(parts[0]), Number(parts[1]), 0f, 0f, TangentMode.Auto));
        }

        return keys.Count > 0
            ? [..keys.OrderBy(static key => key.Time)]
            : throw new InvalidDataException("a curve channel needs a key");
    }

    static float Number(string text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && float.IsFinite(number)
            ? number
            : throw new InvalidDataException($"'{text}' is not a number");
}
