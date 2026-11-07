namespace Patina.Contracts;

/// <summary>
///     The one list of texture sides, which every client offers: a graph's size, a new document's, a bake's. A request
///     may name another side where it says so; this is what a person picks from.
/// </summary>
public static class TextureSizes {
    /// <summary>
    ///     The setting of a graph that says how big it is evaluated. It is separate from <c>baseWidth</c> and
    ///     <c>baseHeight</c>, which say what a radius is counted in, so changing it changes the texel count and not the
    ///     picture. A graph with no size evaluates at its base.
    /// </summary>
    public const string GraphSetting = "size";

    /// <summary>The output setting that overrides a graph's size for one build.</summary>
    public const string OutputSetting = GraphSetting;

    /// <summary>The side a new document gets when nobody says.</summary>
    public const int Default = 2048;

    /// <summary>The largest side.</summary>
    public const int Largest = 8192;

    /// <summary>The sides, smallest first.</summary>
    public static IReadOnlyList<int> Sides { get; } = [256, 512, 1024, 2048, 4096, Largest];

    /// <summary>
    ///     How many halvings from a graph's base to a side, in Vixen's <c>BakeLevelOffset</c> currency: 0 is the base,
    ///     -1 is twice it. Only whole steps keep a radius round, so a side that is no step of the base takes the nearest.
    /// </summary>
    /// <param name="width">The base width.</param>
    /// <param name="height">The base height.</param>
    /// <param name="side">The side the longer axis should have.</param>
    public static int LevelOf(int width, int height, int side) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(side);
        return (int)Math.Round(Math.Log2(Math.Max(width, height) / (double)side), MidpointRounding.AwayFromZero);
    }
}
