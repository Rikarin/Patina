namespace Patina.Contracts;

/// <summary>The body of <c>POST /api/v1/library/project/materials</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>, that the materials become graphs of.</param>
/// <param name="File">
///     A file on the Server's machine that describes materials: a glTF (<c>.gltf</c>, <c>.glb</c>) or another model
///     file, a MaterialX document (<c>.mtlx</c>), a USD layer in its text form (<c>.usda</c>) or a USD package
///     (<c>.usdz</c>) whose layer is text.
/// </param>
public sealed record MaterialImportRequest(string Project, string File);

/// <summary>What importing a material file made.</summary>
/// <param name="Documents">
///     The graph made for each material, in the file's order: its path below the library, which the graph routes take.
/// </param>
/// <param name="Warnings">What a person should know: an input left out, and why.</param>
public sealed record MaterialImportResult(IReadOnlyList<string> Documents, IReadOnlyList<string> Warnings);

/// <summary>A material as a file describes it, as the maps a graph's outputs make.</summary>
/// <param name="Name">What the file calls it.</param>
/// <param name="Slots">Its maps, one per output.</param>
/// <param name="Warnings">What of it was left out, and why.</param>
public sealed record ImportedMaterial(string Name, IReadOnlyList<ImportedSlot> Slots, IReadOnlyList<string> Warnings);

/// <summary>One map of a material: lanes of a picture times a factor, or a constant.</summary>
/// <param name="Usage">
///     The output it is: baseColor, normal, roughness, metalness, occlusion, emissive, opacity or height.
/// </param>
/// <param name="Texture">The picture, absolute, or null for a constant.</param>
/// <param name="Lane">The picture's lane that holds a grey map, 0 to 3, or -1 for its first three in order.</param>
/// <param name="Inverted">Whether the lane holds one minus the map.</param>
/// <param name="Value">
///     Four numbers: what the picture's lanes are multiplied by, or the constant when there is no picture. A
///     color's are linear, as a material file's factors are.
/// </param>
public sealed record ImportedSlot(
    string Usage,
    string? Texture,
    int Lane,
    bool Inverted,
    IReadOnlyList<float> Value
) {
    /// <summary>For a normal, whether its green runs up the picture, as glTF's, MaterialX's and USD's do.</summary>
    public bool GreenUp { get; init; } = true;

    /// <summary>Where the picture lands on the mesh's coordinates, or null when it lands as it is.</summary>
    public ImportedPlacement? Placement { get; init; }
}

/// <summary>
///     Where a material's picture lands on the mesh's coordinates, in glTF's <c>KHR_texture_transform</c> terms: the
///     picture is read at the coordinate scaled, then turned, then moved.
/// </summary>
/// <param name="OffsetX">How far the coordinate moves along U, in fractions of the picture.</param>
/// <param name="OffsetY">The same along V, which runs down the picture.</param>
/// <param name="Rotation">The turn, in radians, counter-clockwise in UV.</param>
/// <param name="ScaleX">What U is multiplied by.</param>
/// <param name="ScaleY">What V is multiplied by.</param>
public sealed record ImportedPlacement(float OffsetX, float OffsetY, float Rotation, float ScaleX, float ScaleY) {
    /// <summary>
    ///     What is read beyond the picture's edges, as a <c>Space/Transform 2D</c> names it: <c>Wrap</c>, <c>Clamp</c>
    ///     or <c>Mirror</c>. glTF's sampler says it per axis, and the node has one for both.
    /// </summary>
    public string Tiling { get; init; } = "Wrap";
}
