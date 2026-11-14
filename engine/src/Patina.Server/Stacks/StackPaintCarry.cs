using Patina.Contracts;
using Patina.Ipc;
using System.Globalization;
using Vixen.Authoring;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     Paint carried from the layout of one of the mesh's UV sets onto the one the texture set is laid out in (#210).
///     Every canvas of the set, a layer's, its mask's and its mask entries', becomes a new canvas the Worker makes as a
///     copy of the old one laid out again, so the step is one document change and undoing it names the old canvases,
///     which were never touched. The layout is read on the surface, as a mesh's new layout is (Preserve Paint), so the
///     paint stays where it was on the model.
/// </summary>
sealed class StackPaintCarry(StackEditor editor, string set) {
    readonly HashSet<string> taken = new(StringComparer.Ordinal);
    readonly string stem = Path.GetFileNameWithoutExtension(editor.AssetPath);

    /// <summary>Each canvas carried, and the new canvas it becomes.</summary>
    public List<(string From, string To)> Canvases { get; } = [];

    /// <summary>
    ///     Carries the set's paint, as one step with the batch it is in: the document names the new canvases, and the
    ///     journal says what the Worker makes each of. A set with no canvas carries nothing.
    /// </summary>
    /// <param name="carry">The command.</param>
    /// <param name="run">Runs the document's change.</param>
    /// <param name="journal">The step's entries, which a copy for each canvas joins.</param>
    /// <exception cref="StackEditRefusedException">There is no mesh, or the paint is in this layout.</exception>
    public void Run(StackCarryPaint carry, Action<IAuthoringCommand> run, List<StackJournalEntry> journal) {
        if (Layouts(carry, run) is not { } layouts) {
            return;
        }

        var size = (editor.Document.BaseWidth, editor.Document.BaseHeight);
        foreach (var (from, to) in Canvases) {
            journal.Add(
                StackJournalEntry.CarriedCopy(editor.TakeEntry(), (to, from), editor.Document.Model, size, layouts)
            );
        }
    }

    /// <summary>The document's change, and the layouts the Worker carries the canvases between, or null.</summary>
    StackCarry? Layouts(StackCarryPaint carry, Action<IAuthoringCommand> run) {
        if (editor.Document.Model.Trim().Length == 0) {
            throw new StackEditRefusedException("This document has no mesh to paint on.", Api.Refusals.NoMesh);
        }

        var to = editor.Set.UvSet;
        if (carry.FromUvSet == to || carry.FromUvSet < 0) {
            throw new StackEditRefusedException(
                $"The texture set is laid out in UV set {to + 1} already.",
                Api.Refusals.CarrySameUvSet,
                new Dictionary<string, string> { ["uvSet"] = (to + 1).ToString(CultureInfo.InvariantCulture) }
            );
        }

        var chosen = editor.Set;
        var moved = chosen with { Layers = [..chosen.Layers.Select(Carried)] };
        if (Canvases.Count == 0) {
            return null;
        }

        run(
            editor.Replaced(
                editor.Document with { Sets = [..editor.Document.Sets.Select(held => held == chosen ? moved : held)] },
                "Carry paint"
            )
        );
        return new(carry.FromUvSet, to);
    }

    LayerAsset Carried(LayerAsset layer) =>
        layer with {
            Paint = Moved(layer.Paint, layer.Id, false),
            Mask = layer.Mask with {
                Paint = Moved(layer.Mask.Paint, layer.Id, true),
                Layers = [
                    ..layer.Mask.Layers.Select((entry, index) => entry with {
                            Paint = Moved(
                                entry.Paint,
                                string.Create(CultureInfo.InvariantCulture, $"{layer.Id}-{index + 1}"),
                                true
                            )
                        }
                    )
                ]
            },
            Children = [..layer.Children.Select(Carried)]
        };

    string Moved(string paint, string layer, bool mask) {
        var from = paint.Trim();
        if (from.Length == 0) {
            return paint;
        }

        var made = editor.FreeCanvas(stem, set, layer, mask, taken);
        taken.Add(made);
        Canvases.Add((from, made));
        return made;
    }
}
