using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     A person's own work as a material or a mask the shelf offers (#135). Saving goes through Vixen's
///     <see cref="SmartMaterial.Extract" />, the one place that decides what survives a change of model, so painted
///     pixels stay behind, a mask that read them becomes a zero and an entry that did is switched off, as they do for
///     every smart material; applying a material is the <c>addLayer</c> with a material that already exists.
/// </summary>
static class StackSmartAssets {
    /// <summary>Writes the chosen layers, or one layer's mask, into a project as a layered material.</summary>
    /// <exception cref="StackEditRefusedException">
    ///     Nothing chosen, a layer is missing, or nothing would come.
    /// </exception>
    public static StackSavedMaterial Save(OpenLibrary library, StackSession session, StackSaveMaterialRequest request) {
        var name = request.Name.Trim();
        if (name.Length == 0) {
            throw new StackEditRefusedException("A smart material or mask needs a name.");
        }

        var document = StackDocuments.Parse(session.Text);
        var set = LayerStackEdit.SetFor(document, session.Set)
            ?? throw new StackEditRefusedException("This document has no texture set to save from.");

        // ⚠ Instances first become the layers they stand for: a source outside what was chosen would not come, and an
        // instance of nothing is a layer that writes nothing.
        var resolved = LayerStackInstances.Resolve(document, set, out _);
        var wanted = request.Layers.ToHashSet(StringComparer.Ordinal);
        var chosen = Chosen(resolved.Layers, wanted);
        var missing = wanted.Except(Ids(chosen)).ToArray();
        if (chosen.Count == 0 || missing.Length > 0) {
            throw new StackEditRefusedException(
                chosen.Count == 0 ? "Choose a layer to save." : $"There is no layer '{missing[0]}' in this document."
            );
        }

        if (request.Mask) {
            if (chosen is not [var only] || only.Mask is { Source: LayerMaskSource.None, Layers.Count: 0 }) {
                throw new StackEditRefusedException("A smart mask is one layer's mask, and this layer has none.");
            }

            chosen = [new() { Id = "mask", Name = name, Kind = LayerKind.Fill, Mask = only.Mask }];
        }

        var fragment = document with { Sets = [set with { Layers = chosen }] };
        var extract = SmartMaterial.Extract(fragment, name, set.Name);
        if (extract.Material is not { } material) {
            throw new StackEditRefusedException(extract.Status);
        }

        var file = library.NewDocumentFile(request.Project, name, StackDocuments.Extension);
        LibraryFiles.Replace(library.ProjectFile(request.Project, file), LayerStackYaml.Write(material));
        var item = library.AddDocument(request.Project, DocumentKinds.LayeredMaterial, file, name, null);
        var role = request.Mask ? GraphRoles.Mask : GraphRoles.Material;
        if (request.Mask) {
            library.SetDocumentRole(request.Project, item.Id, role);
        }

        var path = $"{LibraryLayout.ProjectsFolder}/{request.Project.Trim('/')}/{file}";
        return new(path, role, extract.Status + Unread(material));
    }

    /// <summary>A smart mask onto a layer, joined to its own or in its place (#135).</summary>
    /// <exception cref="StackEditRefusedException">
    ///     There is no such layer or mask, or the source carries no mask.
    /// </exception>
    public static SetLayerCommand ApplyMask(StackEditor editor, StackApplyMask apply, string set) {
        var layer = editor.Require(apply.Layer);
        var material = MaterialShelf.Read(editor.Library, apply.Source);
        var carried = material.Sets[0].Layers.FirstOrDefault()?.Mask;
        if (carried is null or { Source: LayerMaskSource.None, Layers.Count: 0, Effects.Count: 0 }) {
            throw new StackEditRefusedException($"'{material.Name}' carries no mask.");
        }

        editor.CompoundsArrived |= MaterialShelf.BringCompounds(editor.Library, apply.Source, editor.AssetPath);
        return new(
            editor,
            new(set, layer.Id),
            layer,
            layer with { Mask = Joined(layer.Mask, carried, apply.Merge) },
            $"Apply {material.Name}"
        );
    }

    /// <summary>
    ///     The layers that were chosen, in the stack's order, bottom first. A folder brings its children, so a child
    ///     of a chosen folder is not taken twice.
    /// </summary>
    static List<LayerAsset> Chosen(List<LayerAsset> layers, HashSet<string> wanted) {
        List<LayerAsset> found = [];
        foreach (var layer in layers) {
            if (wanted.Contains(layer.Id)) {
                found.Add(layer);
            } else {
                found.AddRange(Chosen(layer.Children, wanted));
            }
        }

        return found;
    }

    static IEnumerable<string> Ids(IEnumerable<LayerAsset> layers) =>
        layers.SelectMany(static layer => Ids(layer.Children).Prepend(layer.Id));

    /// <summary>
    ///     A sentence naming anchor points the material reads and does not publish: they are kept, and read whatever
    ///     a layer of that name publishes in the stack it is applied to, or nothing.
    /// </summary>
    static string Unread(LayerStackAsset material) {
        var set = material.Sets[0];
        var published = LayerStackEdit.AnchorPoints(set).Keys.ToHashSet(StringComparer.Ordinal);
        var read = LayerStackEdit.AnchorUses(set)
            .Select(static use => use.Reference.Anchor)
            .Where(anchor => anchor.Length > 0 && !published.Contains(anchor))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var names = string.Join(", ", read.Select(static anchor => $"'{anchor}'"));
        return read.Length == 0
            ? string.Empty
            : $" It reads {names}, which did not come: a layer publishing that anchor point where it is applied "
            + "feeds it, and nothing else does.";
    }

    /// <summary>
    ///     A smart mask joined to a layer's own mask, or in its place. Joined, the smart mask's base multiplies over
    ///     what the layer had as one more entry, so it can only narrow it; its entries follow with their own
    ///     operators, and its effects run after the layer's, over the whole.
    /// </summary>
    public static MaskAsset Joined(MaskAsset held, MaskAsset carried, bool merge) {
        // An entry that keeps a layer to named meshes names another model's meshes.
        carried = carried with {
            Layers = [..carried.Layers.Where(static entry => !StackDocuments.KeepsToMeshes(entry))]
        };
        var empty = held is { Source: LayerMaskSource.None, Layers.Count: 0, Effects.Count: 0 };
        if (!merge || empty) {
            // The meshes the layer is kept to stay its own.
            return carried with { Layers = [..carried.Layers, ..held.Layers.Where(StackDocuments.KeepsToMeshes)] };
        }

        List<MaskLayerAsset> layers = [..held.Layers];
        if (carried.Source != LayerMaskSource.None) {
            layers.Add(
                new() {
                    Source = carried.Source,
                    Value = carried.Value,
                    Asset = carried.Asset,
                    Anchor = carried.Anchor,
                    Generator = carried.Generator,
                    GeneratorValues = carried.GeneratorValues,
                    GeneratorTexts = carried.GeneratorTexts,
                    GeneratorInputs = carried.GeneratorInputs,
                    Map = carried.Map,
                    Lane = carried.Lane,
                    Blend = LayerBlendMode.Multiply
                }
            );
        }

        layers.AddRange(carried.Layers);
        return held with { Layers = layers, Effects = [..held.Effects, ..carried.Effects] };
    }
}
