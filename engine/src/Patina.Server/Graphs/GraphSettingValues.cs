using Patina.Contracts;
using System.Globalization;
using Vixen.TextureGraph;

namespace Patina.Server.Graphs;

/// <summary>What each of a graph's own settings takes. A setting no graph has is refused rather than stored.</summary>
static class GraphSettingValues {
    /// <summary>Whether <paramref name="value" /> is something the setting <paramref name="key" /> can hold.</summary>
    /// <exception cref="GraphEditRefusedException">A graph has no such setting.</exception>
    public static bool Accepts(string key, string value) =>
        key switch {
            TextureGraphSettings.BaseWidth or TextureGraphSettings.BaseHeight =>
                int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var extent)
                && extent is > 0 and <= TexturePlan.MaxExtent,
            TextureGraphSettings.Seed => uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _),
            TextureGraphSettings.BitDepth => value is "16" or "32",
            TextureSizes.GraphSetting =>
                int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var side)
                && TextureSizes.Sides.Contains(side),
            TextureGraphSettings.TilingX or TextureGraphSettings.TilingY => value is "true" or "false",
            _ => throw new GraphEditRefusedException($"There is no graph setting '{key}'.")
        };
}
