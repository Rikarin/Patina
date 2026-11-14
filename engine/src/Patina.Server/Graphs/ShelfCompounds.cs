using Patina.Content;
using Patina.Contracts;
using Patina.Library;
using Vixen.NodeGraph;
using Vixen.TextureGraph;
using Vixen.Texturing.Layers;

namespace Patina.Server.Graphs;

/// <summary>
///     The compounds Vixen and Patina ship, on the shelf by what each can be (#122): one that reads no picture and
///     writes one, a grunge, a noise, a pattern or a generator, is a mask; one that changes the picture it reads is a
///     filter. A compound with no such shape stays in the graph editor. Using one names its node type, so nothing is
///     published into the project.
/// </summary>
public static class ShelfCompounds {
    /// <summary>What a shipped compound's source starts with, before its node type: <c>compound:Grunges/Speckle</c>.</summary>
    public const string Prefix = "compound:";

    const string Origin = "shipped";

    static readonly Lazy<List<MaterialInfo>> Listed = new(List);

    /// <summary>Every shipped compound the shelf offers, each once, by its node type.</summary>
    public static IReadOnlyList<MaterialInfo> All => Listed.Value;

    /// <summary>The node type a compound's source names, when the shelf offers it.</summary>
    /// <param name="source">A source the shelf gave.</param>
    /// <returns>The node type, or null when the source is not a shipped compound's.</returns>
    public static string? NodeOf(string source) {
        if (!source.StartsWith(Prefix, StringComparison.Ordinal)) {
            return null;
        }

        return All.Any(compound => compound.Source == source) ? source[Prefix.Length..] : null;
    }

    static List<MaterialInfo> List() {
        FactoryCompounds.Register();
        var library = LayerStackCompiler.Library(out _);
        List<MaterialInfo> found = [];
        foreach (var path in TextureCompoundLibrary.Shipped) {
            if (library.TryGet(path, out var type) && RoleOf(type) is { } role) {
                found.Add(
                    new(
                        Terms.Spelled(type.Title),
                        DocumentKinds.Graph,
                        Prefix + path,
                        role,
                        Origin,
                        [],
                        [..type.Settings.Select(GraphDocuments.Definition)],
                        Tags: [Terms.Spelled(type.Category)],
                        Description: type.Summary
                    )
                );
            }
        }

        return found;
    }

    /// <summary>A mask for a compound that draws one picture from nothing, a filter for one that changes what it reads.</summary>
    static string? RoleOf(NodeTypeDefinition type) {
        var reads = type.Ports.Count(static port => port is { Direction: PortDirection.Input, Kind: PortKind.Image });
        var writes = type.Ports.Count(static port => port is { Direction: PortDirection.Output, Kind: PortKind.Image });
        return (reads, writes) switch {
            (0, 1) => GraphRoles.Mask,
            (> 0, _) when LayerStackGraph.IsAdjustment(type) => GraphRoles.Filter,
            _ => null
        };
    }
}
