using Vixen.TextureGraph;
using Vixen.Texturing.Layers;

namespace Patina.Worker;

/// <summary>
///     What a click that finds a layer weighs (#136): each layer that could be on top, top first, as a texture set of its
///     own that holds nothing but that layer inside the folders around it.
/// </summary>
static class StackFinding {
    /// <summary>The least share of a texel a layer has to cover to be the one found there.</summary>
    /// <remarks>
    ///     More than an eight-bit step, so that a soft brush's last faint ring does not take the click from what is
    ///     plainly under it, and well under any coverage a person would call there.
    /// </remarks>
    const float Least = 0.05f;

    /// <summary>
    ///     Each layer a click can find, top first, with the set it is weighed in: its channels, each read as how much the
    ///     layer covers there. A folder is walked rather than found,
    ///     a flattened folder is one layer, and a filter covers nothing of its own, so it is never the answer.
    /// </summary>
    public static IEnumerable<(string Layer, TextureSetAsset Alone)> Candidates(TextureSetAsset set) {
        // ⚠ In the set's own channels, each starting from nothing. `LayerStackSplat.Coverage` weighs in one channel
        // called `mask`, and a paint layer weighed there reads its canvas's `mask` channel, which is not its strokes.
        List<ChannelAsset> empty = [..set.Channels.Select(static channel => channel with { Default = [0f, 0f, 0f, 1f] })];
        foreach (var (layer, around) in TopFirst(set.Layers, [])) {
            var wrapped = layer with { Blend = LayerBlendMode.Copy };
            for (var at = around.Count - 1; at >= 0; at--) {
                wrapped = around[at] with { Blend = LayerBlendMode.Copy, Children = [wrapped] };
            }

            yield return (layer.Id, set with { Channels = empty, Layers = [wrapped] });
        }
    }

    static IEnumerable<(LayerAsset Layer, List<LayerAsset> Around)> TopFirst(
        List<LayerAsset> layers,
        List<LayerAsset> around
    ) {
        for (var at = layers.Count - 1; at >= 0; at--) {
            var layer = layers[at];
            if (!layer.Enabled) {
                continue;
            }

            if (layer.Kind == LayerKind.Group && !layer.Flattened) {
                foreach (var inside in TopFirst(layer.Children, [..around, layer])) {
                    yield return inside;
                }
            } else if (layer.Kind != LayerKind.Filter) {
                yield return (layer, around);
            }
        }
    }

    /// <summary>Whether one texel of a coverage, as the plan stored it, says the layer is there.</summary>
    public static bool Covers(byte[] texel, TextureFormat format) =>
        format switch {
            TextureFormat.R16Float or TextureFormat.Rgba16Float => (float)BitConverter.ToHalf(texel, 0) > Least,
            TextureFormat.R32Float or TextureFormat.Rgba32Float => BitConverter.ToSingle(texel, 0) > Least,
            _ => texel[0] / 255f > Least
        };
}
