using Patina.Contracts;
using Patina.Export;

namespace Patina.Server.Stacks;

/// <summary>
///     What a bake's id map holds. Its colours are flat, so they can be listed for a person to choose from, and a
///     selection made from this list matches exactly.
/// </summary>
static class IdColours {
    const string Map = "id";
    const int MaxColours = 32;
    const int ByteMax = 255;

    /// <summary>The map's colours, or null when the folder has no id map.</summary>
    /// <param name="folder">A bake's folder.</param>
    /// <param name="u">Across the map, 0 to 1, to ask about a place.</param>
    /// <param name="v">Down it.</param>
    public static StackIdColours? Read(string folder, float? u, float? v) {
        if (BakedMapPicture.Find(folder, Map) is not { } found) {
            return null;
        }

        var picture = BakedMapPicture.Read(found.Path, found.Map, found.Format);
        var counts = new Dictionary<int, int>();
        for (var at = 0; at < picture.Width * picture.Height; at++) {
            var colour = Packed(picture.Pixels, at);
            // Black is what a bake leaves where there is no chart.
            if (colour != 0) {
                counts[colour] = counts.GetValueOrDefault(colour) + 1;
            }
        }

        var covered = Math.Max(1, counts.Values.Sum());
        List<StackIdColour> colours = [
            ..counts.OrderByDescending(static pair => pair.Value)
                .ThenBy(static pair => pair.Key)
                .Take(MaxColours)
                .Select(pair => new StackIdColour(Lanes(pair.Key), pair.Value / (float)covered))
        ];
        if (u is not { } across || v is not { } down) {
            return new(colours);
        }

        var column = Math.Clamp((int)(across * picture.Width), 0, picture.Width - 1);
        var row = Math.Clamp((int)(down * picture.Height), 0, picture.Height - 1);
        return new(colours, Lanes(Packed(picture.Pixels, row * picture.Width + column)));
    }

    static int Packed(byte[] pixels, int at) => pixels[at * 4] << 16 | pixels[at * 4 + 1] << 8 | pixels[at * 4 + 2];

    static float[] Lanes(int colour) => [
        (colour >> 16 & ByteMax) / (float)ByteMax, (colour >> 8 & ByteMax) / (float)ByteMax,
        (colour & ByteMax) / (float)ByteMax
    ];
}
