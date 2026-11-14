using Patina.Content;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;
using Vixen.NodeGraph;
using Vixen.TextureGraph;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     The nodes a stack can run, as a client is offered them: its filters, its sources, its mask effects, and what
///     each takes, read from the registry and compounds its compile reads.
/// </summary>
static class StackNodeKinds {
    /// <summary>
    ///     Every adjustment a stack can run, read from the same registry and compounds its compile reads, each with
    ///     what its node declares, so that a client shows what will run.
    /// </summary>
    public static IReadOnlyList<StackFilterKind> Filters() => Kinds(LayerStackGraph.IsAdjustment);

    /// <summary>
    ///     What an image input can read as a generator: every node and shipped compound that reads no image and writes
    ///     one, less the two an input names by their own sources, an image and a mesh map.
    /// </summary>
    public static IReadOnlyList<StackFilterKind> Sources() =>
        Kinds(static type => type.Path is not ("Source/Bitmap" or "Source/Mesh Map")
            && type.Ports.Count(static port => port is { Direction: PortDirection.Output, Kind: PortKind.Image }) == 1
            && !type.Ports.Any(static port => port is { Direction: PortDirection.Input, Kind: PortKind.Image })
        );

    static IReadOnlyList<StackFilterKind> Kinds(Func<NodeTypeDefinition, bool> offered) {
        var library = LayerStackCompiler.Library(out var compounds);
        return [
            ..library.Types.Where(offered)
                .OrderBy(static type => type.Path, StringComparer.Ordinal)
                .Select(type => Kind(type, library, compounds))
        ];
    }

    /// <summary>
    ///     What any node or compound a stack can run takes, the project's own compounds included: its numbers, its
    ///     settings and the images it reads (#111). A graph fill, a filter and a mask generator each have image inputs
    ///     a client offers a slot for, and a compound of the project's own is in no list the vocabulary holds.
    /// </summary>
    /// <param name="node">The node type or compound, as a layer names it.</param>
    /// <param name="folder">The folder of the stack, whose compounds folder the stack's Worker also reads.</param>
    /// <returns>It, or null when neither the shipped nodes nor the project's compounds have it.</returns>
    public static StackFilterKind? Kind(string node, string? folder) {
        // Patina's own compounds publish beside Vixen's, as they do for the Worker that runs the stack.
        FactoryCompounds.Register();
        NodeTypeRegistry library = new();
        NodeTypes.Register(library);
        var compounds = TextureCompoundLibrary.Publish(
            library,
            folder is null ? null : Path.Combine(folder, LibraryLayout.CompoundsFolder),
            out _
        );
        return library.TryGet(node.Trim(), out var type) ? Kind(type, library, compounds) : null;
    }

    static StackFilterKind Kind(NodeTypeDefinition type, NodeTypeRegistry library, ISubGraphSource compounds) =>
        new(
            type.Path,
            Terms.Spelled(type.Title),
            Terms.Spelled(type.Category),
            type.Summary,
            [
                ..type.Ports.Where(static port => port is { Direction: PortDirection.Input, Kind: not PortKind.Image })
                    .Select(GraphDocuments.Definition)
            ],
            [..type.Settings.Select(GraphDocuments.Definition)],
            [
                ..LayerStackGraph.Inputs(type, library, compounds)
                    .Select(static input => new StackFilterInput(
                            input.Port,
                            input.Summary,
                            input.Subject,
                            input.Optional,
                            input.Channel
                        )
                    )
            ]
        );

    /// <summary>The usages an Output node takes, which is the one list of what a channel can be.</summary>
    public static IReadOnlyList<string> Usages() {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        return [..registry.Get("Output/Output").Setting("Usage")!.Accepted];
    }

    public static Dictionary<string, IReadOnlyList<GraphSettingDefinition>> GeneratorParameters() {
        var library = LayerStackCompiler.Library(out _);
        return TextureCompoundLibrary.Shipped
            .Where(static path => path.StartsWith("Generators/", StringComparison.Ordinal))
            .Where(path => library.TryGet(path, out _))
            .ToDictionary(
                static path => path,
                path => {
                    library.TryGet(path, out var type);
                    return (IReadOnlyList<GraphSettingDefinition>)[..type!.Settings.Select(GraphDocuments.Definition)];
                }
            );
    }

    /// <summary>
    ///     The filters with one image out, since a mask is one picture, less what a mask's colour selection is. That
    ///     one has its own fields, and an entry of its node in a mask's list is that selection.
    /// </summary>
    public static IReadOnlyList<StackFilterKind> MaskEffects() => [
        ..Kinds(LayerStackGraph.IsMaskStep).Where(static filter => filter.Node != StackDocuments.ColourSelect)
    ];
}
