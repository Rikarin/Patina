using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;
using Vixen.NodeGraph;
using Vixen.TextureGraph;
using Vixen.Texturing;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Server.Stacks;

/// <summary>What a stack looks like on the wire and in a file.</summary>
public static class StackDocuments {
    public const string Extension = ".vxlayers";
    const string ColourSelect = "Analysis/Colour Select";
    const string SelectColour = "Colour";
    const string SelectTolerance = "Tolerance";
    const string SelectSoftness = "Softness";
    const float DefaultTolerance = 0.02f;

    /// <exception cref="StackEditRefusedException">It is not a stack.</exception>
    public static LayerStackAsset Parse(string text) {
        try {
            return LayerStackYaml.Read(text);
        } catch (InvalidDataException e) {
            throw new StackEditRefusedException(e.Message, e);
        }
    }

    public static string Text(LayerStackAsset document) => LayerStackYaml.Write(document);

    public static IReadOnlyList<StackLayer> Layers(IEnumerable<LayerAsset> layers) => [..layers.Select(State)];

    /// <summary>The words a stack's fields take, from the enumerations and libraries that mean them.</summary>
    public static StackVocabulary Vocabulary { get; } = new(
        Enum.GetNames<LayerBlendMode>(),
        [
            ..Enum.GetValues<LayerFilterKind>()
                .Select(static kind => new StackFilterKind(kind.ToString(), LayerStackGraph.Filter(kind).Ports))
        ],
        Enum.GetNames<LayerMaskSource>(),
        [
            ..TextureCompoundLibrary.Shipped.Where(static path => path.StartsWith(
                    "Generators/",
                    StringComparison.Ordinal
                )
            )
        ],
        TextureNodeLibrary.MeshMaps,
        PaintAlphas.Names,
        MaskEffects(),
        Usages(),
        GeneratorParameters()
    );

    /// <summary>The stack a new file starts as: a base, and somewhere to paint.</summary>
    /// <param name="path">The file to write.</param>
    /// <param name="width">The width of the stack's maps.</param>
    /// <param name="height">Their height.</param>
    /// <param name="model">The mesh, relative to the file, or null for none yet.</param>
    public static void Create(string path, int width, int height, string? model = null) {
        var name = Path.GetFileNameWithoutExtension(path);
        List<ChannelAsset> channels = [
            new() { Usage = "baseColor", Default = [0.5f, 0.5f, 0.5f, 1f] },
            new() { Usage = "roughness", Default = [0.5f, 0.5f, 0.5f, 1f] },
            new() { Usage = "metalness", Default = [0f, 0f, 0f, 1f] }
        ];
        LayerStackAsset stack = new() {
            Name = name,
            Model = model ?? string.Empty,
            BaseWidth = width,
            BaseHeight = height,
            Sets = [
                new() {
                    Name = "Body",
                    Channels = channels,
                    Layers = [
                        new() {
                            Id = "base",
                            Name = "Base",
                            Kind = LayerKind.Fill,
                            Values = {
                                ["baseColor"] = [0.5f, 0.5f, 0.5f, 1f],
                                ["roughness"] = [0.5f, 0.5f, 0.5f, 1f],
                                ["metalness"] = [0f, 0f, 0f, 1f]
                            }
                        },
                        new() { Id = "paint", Name = "Paint", Kind = LayerKind.Paint }
                    ]
                }
            ]
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        LibraryFiles.Replace(path, Text(stack));
    }

    /// <summary>A mask's effects with its colour selection as the mask asks, and the others as they were.</summary>
    public static List<MaskEffectAsset> WithSelection(List<MaskEffectAsset> effects, StackMask mask) {
        List<MaskEffectAsset> kept = mask.Effects is { } chosen
            ? [..chosen.Select(static effect => Effect(effect))]
            : [..effects.Where(static effect => effect.Node != ColourSelect)];
        if (mask.Colour is { Count: >= 3 } colour) {
            kept.Insert(
                0,
                new() {
                    Node = ColourSelect,
                    Values = {
                        [SelectColour] = [colour[0], colour[1], colour[2], 1f],
                        [SelectTolerance] = [mask.Tolerance],
                        [SelectSoftness] = [mask.Softness]
                    }
                }
            );
        }

        return kept;
    }

    static MaskEffectAsset Effect(StackMaskEffect effect) =>
        new() {
            Node = effect.Node,
            Enabled = effect.Enabled,
            Values = effect.Values?.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.ToArray(),
                    StringComparer.Ordinal
                )
                ?? [],
            Texts = effect.Texts?.ToDictionary(StringComparer.Ordinal) ?? []
        };

    /// <summary>A step a mask can run, with the numbers its node takes.</summary>
    static StackEffectKind EffectKind(NodeTypeRegistry registry, string node, string name) =>
        new(
            node,
            name,
            [
                ..registry.Get(node)
                    .Ports.Where(static port => port is {
                            Direction: PortDirection.Input, Kind: not PortKind.Image, Default.Length: 1
                        }
                    )
                    .Select(static port => new StackEffectPort(
                            port.Name,
                            port.Default[0],
                            float.IsFinite(port.Minimum) ? port.Minimum : null,
                            float.IsFinite(port.Maximum) ? port.Maximum : null
                        )
                    )
            ]
        );

    /// <summary>The usages an Output node takes, which is the one list of what a channel can be.</summary>
    static IReadOnlyList<string> Usages() {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        return [..registry.Get("Output/Output").Setting("Usage")!.Accepted];
    }

    static Dictionary<string, IReadOnlyList<GraphSettingDefinition>> GeneratorParameters() {
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

    static IReadOnlyList<StackEffectKind> MaskEffects() {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        return [
            EffectKind(registry, "Colour/Levels", "Levels"),
            EffectKind(registry, "Colour/Invert", "Invert"),
            EffectKind(registry, "Colour/Auto Levels", "Auto Levels"),
            EffectKind(registry, "Filters/Blur", "Blur"),
            EffectKind(registry, "Filters/Sharpen", "Sharpen")
        ];
    }

    static MaskEffectAsset? Selection(MaskAsset mask) =>
        mask.Effects.Find(static effect => effect.Node == ColourSelect);

    /// <summary>Points a stack that is not open at a mesh, relative to its file.</summary>
    public static void SetModel(string path, string model) {
        LibraryFiles.Replace(path, Text(Parse(File.ReadAllText(path)) with { Model = model }));
    }

    static StackLayer State(LayerAsset layer) =>
        new(
            layer.Id,
            layer.Name.Length > 0 ? layer.Name : layer.Id,
            layer.Kind.ToString().ToLowerInvariant(),
            layer.Enabled,
            layer.Opacity,
            layer.Blend.ToString(),
            layer.Channels,
            layer.Paint.Trim().Length > 0,
            Layers(layer.Children),
            layer.Fill.ToString(),
            Numbers(layer.Values),
            layer.Textures,
            layer.Graph,
            layer.Projection.ToString(),
            layer.Filter.ToString(),
            Numbers(layer.Settings),
            new(
                layer.Mask.Source.ToString(),
                layer.Mask.Value,
                layer.Mask.Asset,
                layer.Mask.Anchor,
                layer.Mask.Generator,
                layer.Mask.Map,
                Selection(layer.Mask)?.Values.GetValueOrDefault(SelectColour),
                Selection(layer.Mask)?.Values.GetValueOrDefault(SelectTolerance)?[0] ?? DefaultTolerance,
                Selection(layer.Mask)?.Values.GetValueOrDefault(SelectSoftness)?[0] ?? 0f,
                [
                    ..layer.Mask.Effects.Where(static effect => effect.Node != ColourSelect)
                        .Select(static effect => new StackMaskEffect(
                                effect.Node,
                                effect.Enabled,
                                Numbers(effect.Values),
                                effect.Texts
                            )
                        )
                ],
                layer.Mask.GeneratorTexts
            ),
            layer.PlanarAxis.ToString(),
            new(
                layer.Placement.Scale,
                layer.Placement.Rotation,
                layer.Placement.OffsetX,
                layer.Placement.OffsetY,
                layer.Placement.Sharpness
            ),
            layer.Texts,
            layer.FilterNode
        );

    static Dictionary<string, IReadOnlyList<float>> Numbers(Dictionary<string, float[]> numbers) =>
        numbers.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyList<float>)pair.Value);
}
