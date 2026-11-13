using Patina.Contracts;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     The instances that tie one texture set's layers to the model's other sets (#108), read from the sets as they
///     stand: an open set as its session has it, unsaved, and the others from their files.
/// </summary>
public static class StackInstanceLinking {
    /// <summary>The links between this set and the others.</summary>
    /// <param name="set">This texture set.</param>
    /// <param name="siblings">The model's other texture sets.</param>
    public static StackInstanceLinks Of(TextureSetAsset set, IEnumerable<TextureSetAsset> siblings) {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(siblings);
        List<StackInstanceLink> instances = [];
        List<StackInstanceLink> sources = [];
        HashSet<(string Set, string Layer)> wanted = [
            ..Walk(set.Layers)
                .Where(layer => layer.Kind == LayerKind.Instance
                    && layer.Instance.Set.Length > 0
                    && layer.Instance.Set != set.Name
                )
                .Select(static layer => (layer.Instance.Set, layer.Instance.Layer))
        ];
        foreach (var other in siblings.Where(other => other.Name != set.Name)) {
            foreach (var layer in Walk(other.Layers)) {
                if (layer.Kind == LayerKind.Instance && layer.Instance.Set == set.Name) {
                    instances.Add(new(other.Name, layer.Id, layer.Name, layer.Instance.Layer));
                }

                if (wanted.Contains((other.Name, layer.Id))) {
                    sources.Add(new(other.Name, layer.Id, layer.Name));
                }
            }
        }

        return new(instances, sources);
    }

    /// <summary>Every layer, a folder's children after it.</summary>
    static IEnumerable<LayerAsset> Walk(IEnumerable<LayerAsset> layers) {
        foreach (var layer in layers) {
            yield return layer;
            foreach (var child in Walk(layer.Children)) {
                yield return child;
            }
        }
    }
}
