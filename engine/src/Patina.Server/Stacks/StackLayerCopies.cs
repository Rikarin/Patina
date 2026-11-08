using System.Globalization;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     Layers made again with ids, names and canvases nothing else has, and where each canvas comes from.
/// </summary>
sealed class StackLayerCopies(StackEditor editor, string set) {
    readonly HashSet<string> ids = [..StackEditor.Flattened(editor.Set.Layers).Select(static layer => layer.Id)];
    readonly HashSet<string> names = [..StackEditor.Flattened(editor.Set.Layers).Select(static layer => layer.Name)];
    readonly HashSet<string> paints = new(StringComparer.Ordinal);
    readonly HashSet<string> points = [..LayerStackEdit.AnchorPoints(editor.Set).Keys];
    readonly Dictionary<string, string> renamed = new(StringComparer.Ordinal);

    public List<(string From, string To)> Canvases { get; } = [];

    /// <summary>A layer of a fresh document, with everything in it made new.</summary>
    /// <param name="layer">It, which this changes nothing in.</param>
    /// <param name="source">Where a canvas it names is read from.</param>
    public LayerAsset Of(LayerAsset layer, Func<string, string> source) {
        var id = Free(ids, Stem(layer.Id), static (stem, count) => $"{stem}-{count}");
        var point = layer.AnchorPoint.Length > 0
            ? Free(points, layer.AnchorPoint, static (name, count) => $"{name} {count}")
            : string.Empty;
        if (point != layer.AnchorPoint) {
            renamed[layer.AnchorPoint] = point;
        }

        var mask = layer.Mask with {
            Paint = Canvas(layer.Mask.Paint, id, true, source),
            Layers = [
                ..layer.Mask.Layers.Select((entry, index) => entry with {
                        Paint = Canvas(
                            entry.Paint,
                            string.Create(CultureInfo.InvariantCulture, $"{id}-{index + 1}"),
                            true,
                            source
                        )
                    }
                )
            ]
        };
        return layer with {
            Id = id,
            Name = Free(names, layer.Name, static (name, count) => $"{name} {count}"),
            AnchorPoint = point,
            Paint = Canvas(layer.Paint, id, false, source),
            Mask = mask,
            Children = [..layer.Children.Select(child => Of(child, source))]
        };
    }

    /// <summary>
    ///     A reference to an anchor point that came along now reads the copy of it, and any other stays. A copy's
    ///     anchor point takes a name nothing in the stack publishes, so no copy's name is another's original.
    /// </summary>
    public LayerAsset Reanchored(LayerAsset layer) =>
        renamed.Aggregate(layer, static (held, move) => LayerStackEdit.Reanchored([held], move.Key, move.Value)[0]);

    string Canvas(string paint, string layer, bool mask, Func<string, string> source) {
        var from = paint.Trim();
        if (from.Length == 0) {
            return paint;
        }

        var to = editor.FreeCanvas(
            Path.GetFileNameWithoutExtension(editor.AssetPath),
            set,
            layer,
            mask,
            paints
        );
        paints.Add(to);
        Canvases.Add((source(from), to));
        return to;
    }

    /// <summary>An id with no copy number on it, so that a copy of a copy is not paint-1-1.</summary>
    static string Stem(string id) {
        var dash = id.LastIndexOf('-');
        return dash > 0 && id[(dash + 1)..].All(char.IsAsciiDigit) ? id[..dash] : id.Length > 0 ? id : "layer";
    }

    static string Free(HashSet<string> taken, string wanted, Func<string, int, string> numbered) {
        if (taken.Add(wanted)) {
            return wanted;
        }

        for (var count = 2;; count++) {
            var name = numbered(wanted, count);
            if (taken.Add(name)) {
                return name;
            }
        }
    }
}
