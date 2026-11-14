namespace Patina.Contracts;

/// <summary>How Auto Unwrap lays out a mesh. Each texture set gets an atlas of its own.</summary>
public sealed record UnwrapOptions {
    /// <summary>What islands may be turned by: 1 is never, 2 is a half turn, 4 is every quarter turn.</summary>
    public static IReadOnlyList<int> TurnCounts { get; } = [1, 2, 4];

    /// <summary>The words <see cref="Quality" /> takes, fastest first.</summary>
    public static IReadOnlyList<string> Qualities { get; } = ["boxes", "outlines", "groups"];

    /// <summary>The words <see cref="Overflow" /> takes.</summary>
    public static IReadOnlyList<string> Overflows { get; } = ["shrink", "refuse"];

    /// <summary>
    ///     The atlas side the margin is counted against, in texels. A map of another size only rescales it.
    /// </summary>
    public int Resolution { get; init; } = 2048;

    /// <summary>The empty texels between two islands.</summary>
    public int Margin { get; init; } = 4;

    /// <summary>
    ///     How islands are packed: <c>boxes</c> by their bounding boxes, <c>outlines</c> by their shapes, and
    ///     <c>groups</c> by their shapes after joining nearly square neighbours.
    /// </summary>
    public string Quality { get; init; } = "outlines";

    /// <summary>How many ways an island may be turned: 1 never, 2 a half turn, 4 every quarter turn.</summary>
    public int Rotations { get; init; } = 4;

    /// <summary>Texels per world unit every island is scaled to, or zero for each island at its own scale.</summary>
    public double TexelDensity { get; init; }

    /// <summary>
    ///     What happens when the islands do not fit at that density: <c>shrink</c> scales them all down, and
    ///     <c>refuse</c> stops and says so. Spilling into a second UDIM tile is not offered while a texture set
    ///     paints one tile.
    /// </summary>
    public string Overflow { get; init; } = "shrink";

    /// <summary>What is wrong with these options, or null when they can be used.</summary>
    public string? Problem() {
        (bool Wrong, string Why)[] checks = [
            (Resolution is < 64 or > 16384, $"An unwrap's resolution is 64 to 16384 texels, not {Resolution}."),
            (Margin is < 0 or > 64, $"An unwrap's margin is 0 to 64 texels, not {Margin}."),
            (!Qualities.Contains(Quality), $"'{Quality}' is no way to pack. Use {string.Join(", ", Qualities)}."),
            (!TurnCounts.Contains(Rotations), $"Islands turn 1, 2 or 4 ways, not {Rotations}."),
            (!(TexelDensity >= 0 && double.IsFinite(TexelDensity)), "A texel density is zero or more."),
            (!Overflows.Contains(Overflow),
                $"'{Overflow}' is nothing an unwrap does. Use {string.Join(", ", Overflows)}.")
        ];
        return checks.FirstOrDefault(static check => check.Wrong).Why;
    }
}

/// <summary>What one texture set's unwrap came to.</summary>
/// <param name="Material">The material, which is the texture set.</param>
/// <param name="Charts">How many islands. Fewer is better at the same stretch.</param>
/// <param name="Flipped">
///     Triangles turned over in the atlas. Anything but zero paints and bakes in the wrong place.
/// </param>
/// <param name="Stretch">The stretch averaged over area. 1 is none.</param>
/// <param name="WorstStretch">The stretch of the worst triangle.</param>
/// <param name="Packed">The share of the atlas the islands cover, after the margin.</param>
/// <param name="Warnings">What a person should know: an island split, a rescale.</param>
public sealed record UnwrapSet(
    string Material,
    int Charts,
    int Flipped,
    double Stretch,
    double WorstStretch,
    double Packed,
    IReadOnlyList<string> Warnings
);

/// <summary>What Auto Unwrap did to a mesh.</summary>
/// <param name="From">The file it unwrapped, relative to the project. Changing the options unwraps it again.</param>
/// <param name="Sets">
///     One entry per texture set it laid out. A set whose material had a layout kept it and is absent.
/// </param>
public sealed record UnwrapReport(string From, IReadOnlyList<UnwrapSet> Sets);
