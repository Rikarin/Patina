using System.Collections.Immutable;
using Vixen.TextureGraph;

namespace Patina.Content;

/// <summary>
///     Patina's own compounds, <c>content/compounds</c> under CC0, as shipped node types beside Vixen's. They are
///     graphs of Patina's nodes and nothing else, so each can be opened, read and regenerated in the app; none is a
///     picture.
/// </summary>
/// <remarks>
///     A registration rather than a folder each caller passes: the graph panel, a layer stack and <c>patina bake</c>
///     all read <see cref="TextureCompoundLibrary" />, and one that forgot would compile a graph the others refuse. A
///     compound whose path Vixen already ships is left out, so this content can never change a Vixen node.
/// </remarks>
public static class FactoryCompounds {
    /// <summary>The resource-name prefix the compounds are embedded under.</summary>
    public const string Root = "Patina.Content.Compounds.";

    /// <summary>The node-type paths this assembly embeds, whether or not they have been registered yet.</summary>
    public static ImmutableArray<string> Paths { get; } = [
        ..typeof(FactoryCompounds).Assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(Root, StringComparison.Ordinal)
                && name.EndsWith(TextureCompoundLibrary.Extension, StringComparison.Ordinal)
            )
            .Select(static name => name[Root.Length..^TextureCompoundLibrary.Extension.Length].Replace('.', '/'))
            .Order(StringComparer.Ordinal)
    ];

    /// <summary>Registers the compounds with Vixen's library, from every entry point and more than once.</summary>
    public static void Register() => TextureCompoundLibrary.Bundle(typeof(FactoryCompounds).Assembly, Root);
}
