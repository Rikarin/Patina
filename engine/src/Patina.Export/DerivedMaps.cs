using Patina.Export.Presets;
using Vixen.Core.Imaging;

namespace Patina.Export;

/// <summary>
///     The maps a preset reads that no output makes (<see cref="ExportMaps.Derived" />), worked out from a set's
///     metal-roughness maps. The conversion is the one glTF's specular-glossiness extension describes in reverse: a
///     dielectric reflects four per cent at normal incidence and scatters its base colour, and a metal reflects its
///     base colour and scatters nothing. Colour is mixed in linear numbers and stored as sRGB numbers, as base colour
///     is.
/// </summary>
public static class DerivedMaps {
    /// <summary>What a dielectric reflects at normal incidence, linear.</summary>
    public const float Dielectric = 0.04f;

    /// <summary><see cref="Dielectric" /> as the sRGB number a colour map holds.</summary>
    public static float DielectricSrgb { get; } = MipChain.ToSrgb(Dielectric);

    /// <summary>
    ///     A set's maps with every derived map a preset reads added, when the set has one of the maps it is worked out
    ///     from. A map the set already has is left as it is: a project that paints its own specular keeps it.
    /// </summary>
    /// <param name="maps">The set's maps, by name.</param>
    /// <param name="wanted">The maps the preset's files read.</param>
    public static IReadOnlyDictionary<string, MapPicture> Complete(
        IReadOnlyDictionary<string, MapPicture> maps,
        IEnumerable<string> wanted
    ) {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(wanted);
        var missing = wanted.Where(static map => ExportMaps.Derived.Contains(map, StringComparer.Ordinal))
            .Where(map => !maps.ContainsKey(map))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (missing.Count == 0) {
            return maps;
        }

        Dictionary<string, MapPicture> completed = new(maps, StringComparer.Ordinal);
        foreach (var map in missing) {
            if (Make(map, maps) is { } made) {
                completed[map] = new(map, made, GreenAxis.Up) { Depth = DepthOf(map, maps) };
            }
        }

        return completed;
    }

    /// <summary>A derived map is as deep as the deepest map it is worked out from (#141).</summary>
    static int DepthOf(string map, IReadOnlyDictionary<string, MapPicture> maps) {
        string[] from = map switch {
            ExportMaps.Glossiness => [ExportMaps.Roughness],
            ExportMaps.SheenTint => [ExportMaps.Sheen, ExportMaps.SheenColor],
            ExportMaps.AnisotropyVector => [ExportMaps.Anisotropy, ExportMaps.AnisotropyRotation],
            _ => [ExportMaps.BaseColor, ExportMaps.Metalness]
        };
        return from.Select(name => maps.GetValueOrDefault(name)?.Depth ?? 8).Max();
    }

    static FloatImage? Make(string map, IReadOnlyDictionary<string, MapPicture> maps) {
        var colour = maps.GetValueOrDefault(ExportMaps.BaseColor)?.Image;
        var metal = maps.GetValueOrDefault(ExportMaps.Metalness)?.Image;
        return map switch {
            ExportMaps.Glossiness when maps.GetValueOrDefault(ExportMaps.Roughness)?.Image is { } rough =>
                Each(rough.Width, rough.Height, 1, (x, y, _) => 1f - Lane(rough, x, y, 0, 0.5f)),
            ExportMaps.Specular or ExportMaps.Diffuse when colour is not null || metal is not null =>
                Colour(map == ExportMaps.Specular, colour, metal),
            ExportMaps.SheenTint when maps.GetValueOrDefault(ExportMaps.Sheen)?.Image is { } weight =>
                Tint(weight, maps.GetValueOrDefault(ExportMaps.SheenColor)?.Image),
            ExportMaps.AnisotropyVector when maps.GetValueOrDefault(ExportMaps.Anisotropy)?.Image is { } strength =>
                Turned(strength, maps.GetValueOrDefault(ExportMaps.AnisotropyRotation)?.Image),
            _ => null
        };
    }

    /// <summary>Specular or diffuse colour from base colour and metalness, at the larger of the two sizes.</summary>
    static FloatImage Colour(bool specular, FloatImage? colour, FloatImage? metal) {
        var width = Math.Max(colour?.Width ?? 0, metal?.Width ?? 0);
        var height = Math.Max(colour?.Height ?? 0, metal?.Height ?? 0);
        colour = Fit(colour, width, height);
        metal = Fit(metal, width, height);
        return Each(
            width,
            height,
            3,
            (x, y, lane) => {
                var albedo = MipChain.ToLinear(Lane(colour, x, y, lane, 0.5f));
                var metalness = Math.Clamp(Lane(metal, x, y, 0, 0f), 0f, 1f);
                var linear = specular ? float.Lerp(Dielectric, albedo, metalness) : albedo * (1f - metalness);
                return MipChain.ToSrgb(linear);
            }
        );
    }

    /// <summary>
    ///     glTF's sheen colour: OpenPBR's sheen colour times its weight, both linear, at the larger of the two sizes. A
    ///     set without a sheen colour has OpenPBR's white one.
    /// </summary>
    static FloatImage Tint(FloatImage weight, FloatImage? colour) {
        var width = Math.Max(weight.Width, colour?.Width ?? 0);
        var height = Math.Max(weight.Height, colour?.Height ?? 0);
        weight = Fit(weight, width, height)!;
        colour = Fit(colour, width, height);
        return Each(width, height, 3, (x, y, lane) => Lane(weight, x, y, 0, 0f) * Lane(colour, x, y, lane, 1f));
    }

    /// <summary>
    ///     glTF's anisotropy picture: the direction a turn of <paramref name="rotation" /> makes from the tangent, in
    ///     red and green from 0 to 1, and the strength in blue, at the larger of the two sizes. A set without a
    ///     rotation runs along the tangent.
    /// </summary>
    static FloatImage Turned(FloatImage strength, FloatImage? rotation) {
        var width = Math.Max(strength.Width, rotation?.Width ?? 0);
        var height = Math.Max(strength.Height, rotation?.Height ?? 0);
        strength = Fit(strength, width, height)!;
        rotation = Fit(rotation, width, height);
        return Each(
            width,
            height,
            3,
            (x, y, lane) => {
                if (lane == 2) {
                    return Math.Clamp(Lane(strength, x, y, 0, 0f), 0f, 1f);
                }

                var (sin, cos) = MathF.SinCos(Lane(rotation, x, y, 0, 0f) * MathF.Tau);
                return 0.5f + 0.5f * (lane == 0 ? cos : sin);
            }
        );
    }

    static FloatImage? Fit(FloatImage? image, int width, int height) =>
        image is null || (image.Width == width && image.Height == height)
            ? image
            : ImageResize.Resize(image, width, height, ResizeFilter.Bilinear);

    static FloatImage Each(int width, int height, int lanes, Func<int, int, int, float> value) {
        var samples = new float[width * height * lanes];
        Parallel.For(
            0,
            height,
            y => {
                for (var x = 0; x < width; x++) {
                    for (var lane = 0; lane < lanes; lane++) {
                        samples[(y * width + x) * lanes + lane] = value(x, y, lane);
                    }
                }
            }
        );
        return new(width, height, lanes, samples);
    }

    /// <summary>
    ///     A lane of a texel; a grey picture's one lane is every colour lane, and no picture is the fallback.
    /// </summary>
    static float Lane(FloatImage? image, int x, int y, int lane, float fallback) {
        if (image is null) {
            return fallback;
        }

        var at = (y * image.Width + x) * image.Channels;
        return image.Samples[at + (lane < image.Channels ? lane : 0)];
    }
}
