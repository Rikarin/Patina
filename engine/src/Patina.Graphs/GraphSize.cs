using Patina.Contracts;
using System.Globalization;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Graphs;

/// <summary>
///     How big a graph is made this time, which everything that compiles a graph answers here: the Worker's previews,
///     a build and <c>patina bake</c>. The base stays what the file says, because a radius is counted in its texels.
/// </summary>
public static class GraphSize {
    /// <summary>The id of the warning a graph gets for a size that means nothing.</summary>
    public const string ProblemId = "PG0001";

    /// <summary>Tells a compiler how big to make a graph.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="graph">The graph.</param>
    /// <param name="requested">A side that overrides the graph's own for this run, or null.</param>
    /// <returns>What was wrong with the side, in which case the graph evaluates at its base, or null.</returns>
    public static string? Apply(TextureGraphCompiler compiler, NodeGraphModel graph, string? requested = null) {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(graph);

        compiler.BakeLevelOffset = 0;
        var text = requested ?? graph.SettingOf(TextureSizes.GraphSetting);
        if (text.Length == 0) {
            return null;
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var side)
            || side <= 0
            || side > TextureSizes.Largest) {
            return $"'{text}' is no size for a graph, so it evaluated at its base. A size is 1 to {TextureSizes.Largest}.";
        }

        var width = TextureGraphSettings.Extent(graph, TextureGraphSettings.BaseWidth, compiler.BaseWidth, out _);
        var height = TextureGraphSettings.Extent(graph, TextureGraphSettings.BaseHeight, compiler.BaseHeight, out _);
        compiler.BakeLevelOffset = TextureSizes.LevelOf(width, height, side);
        return null;
    }
}
