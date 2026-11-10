namespace Patina.Contracts;

/// <summary>
///     Saves layers or a mask of an open stack into a project, as a layered material its shelf lists (#135): layers
///     become a smart material that unfolds into another stack, and a mask becomes a smart mask another layer takes.
/// </summary>
/// <param name="Project">The project it goes into, by its path below <c>projects/</c>.</param>
/// <param name="Name">What it is called on the shelf.</param>
/// <param name="Layers">
///     The layers, by id: a folder brings its children. With <paramref name="Mask" />, exactly one, whose mask is
///     saved.
/// </param>
/// <param name="Mask">Whether to save the layer's mask, with its entries and effects, rather than the layers.</param>
public sealed record StackSaveMaterialRequest(
    string Project,
    string Name,
    IReadOnlyList<string> Layers,
    bool Mask = false);

/// <summary>What a save made.</summary>
/// <param name="Path">The document, below the library, as the shelf names it.</param>
/// <param name="Role"><c>material</c> or <c>mask</c>, as the shelf files it.</param>
/// <param name="Status">
///     What came and what did not: painted pixels stay with the model they were painted on, and a reference to an
///     anchor point that did not come reads nothing until a layer publishes that name where it is used.
/// </param>
public sealed record StackSavedMaterial(string Path, string Role, string Status);

/// <summary>
///     Gives a layer a smart mask from the shelf (#135). Merged, it narrows the mask the layer has: the smart mask's
///     base multiplies over it as one more entry, its entries follow, and its effects run after the layer's own. Not
///     merged, it replaces the mask.
/// </summary>
/// <param name="Layer">The layer.</param>
/// <param name="Source">The smart mask, as the shelf names it.</param>
/// <param name="Merge">
///     Whether it joins the layer's mask rather than replacing it. A layer with no mask simply takes it.
/// </param>
public sealed record StackApplyMask(string Layer, string Source, bool Merge = true) : StackCommand;
