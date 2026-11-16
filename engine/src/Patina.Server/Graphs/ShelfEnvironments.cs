using Patina.Contracts;
using Vixen.NodeGraph;

namespace Patina.Server.Graphs;

/// <summary>
///     The panoramas on the shelf (#203), which light the viewport: a project's HDR or EXR picture is listed as a
///     picture and as an environment, whose source is the picture's path behind <see cref="Prefix" />, so the two are
///     told apart and drawn as the same picture. A graph that makes a sky is listed as itself and as an environment,
///     whose source is the graph's path behind the same prefix.
/// </summary>
public static class ShelfEnvironments {
    /// <summary>The type of an environment on the shelf.</summary>
    public const string Role = "environment";

    /// <summary>What an environment's source starts with, before its picture's path below the library.</summary>
    public const string Prefix = "environment:";

    /// <summary>What the type of every node that makes a sky starts with.</summary>
    const string NodeCategory = "Environment/";

    /// <summary>The environments among some pictures on the shelf.</summary>
    public static IEnumerable<MaterialInfo> Among(IEnumerable<MaterialInfo> pictures) =>
        pictures.Where(static picture => PictureFormats.IsEnvironment(picture.Source)
                && !PictureFormats.IsEnvironmentGraph(picture.Source)
            )
            .Select(static picture => picture with { Source = Prefix + picture.Source, Role = Role });

    /// <summary>
    ///     Whether a graph makes a sky, which is what the Environment nodes are for: one that uses any of them is on the
    ///     shelf as an environment too, and lights the viewport as its file draws it (ADR 0049).
    /// </summary>
    public static bool Lights(NodeGraphAsset graph) =>
        graph.Nodes.Any(static node => node.Type.StartsWith(NodeCategory, StringComparison.Ordinal));

    /// <summary>A graph on the shelf as the environment it makes.</summary>
    public static MaterialInfo Of(MaterialInfo graph) => graph with { Source = Prefix + graph.Source, Role = Role };

    /// <summary>The picture an environment's source names, or null when it names none.</summary>
    public static string? PictureOf(string source) =>
        source.StartsWith(Prefix, StringComparison.Ordinal) ? source[Prefix.Length..] : null;
}
