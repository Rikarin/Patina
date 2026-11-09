namespace Patina.Contracts;

/// <summary>
///     A bake's setup to show in the viewport before it runs (#149): the meshes and the options a bake would be sent,
///     and what of it to draw. Paths on the Server's machine, as in a bake request.
/// </summary>
public sealed record ViewportBakePreview {
    /// <summary>The detailed meshes, in order.</summary>
    public IReadOnlyList<string> High { get; set; } = [];

    /// <summary>The mesh baked onto, or empty to take the preview off.</summary>
    public string Low { get; set; } = string.Empty;

    /// <summary>A cage, or null for the ray distances.</summary>
    public string? Cage { get; set; }

    /// <summary>The texture set of the low mesh, or null for the whole mesh.</summary>
    public string? Material { get; set; }

    /// <summary>The options the bake would run with: its distances, units, cage correction and matching.</summary>
    public BakeOptions Options { get; set; } = new();

    /// <summary>Whether to draw where the rays start and how far they reach: the cage or the ray distances.</summary>
    public bool Reach { get; set; } = true;

    /// <summary>Whether to draw the high meshes, see-through.</summary>
    public bool HighMesh { get; set; } = true;

    /// <summary>Whether to colour the parts by which matched which, when parts are matched by name.</summary>
    public bool Matches { get; set; } = true;
}

/// <summary>One part name as matching by name sees it: how many faces each side has and the colour it is drawn in.</summary>
/// <param name="Name">The name both sides share once their endings are gone.</param>
/// <param name="LowFaces">The low mesh's faces of that name, zero for a high part no low part matched.</param>
/// <param name="HighFaces">The high mesh's faces of that name, zero for a low part that matched nothing.</param>
/// <param name="Colour">How it is drawn, as <c>#rrggbb</c>.</param>
public sealed record BakePreviewPart(string Name, int LowFaces, int HighFaces, string Colour);

/// <summary>What the viewport shows of a bake's setup: the key to its colours, and what it could not show.</summary>
/// <param name="Preview">What was asked for, or null when the preview is off.</param>
/// <param name="Parts">
///     The parts by name, in the low mesh's order and then the high's, when parts are matched by name; empty when
///     they are not.
/// </param>
/// <param name="Warnings">What the preview left out or could not read.</param>
public sealed record BakePreviewState(
    ViewportBakePreview? Preview,
    IReadOnlyList<BakePreviewPart> Parts,
    IReadOnlyList<string> Warnings
) {
    /// <summary>No preview.</summary>
    public static BakePreviewState Off { get; } = new(null, [], []);
}
