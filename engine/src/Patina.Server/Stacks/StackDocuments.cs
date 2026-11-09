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

    /// <summary>Marks a mask entry as the one that keeps a layer to some meshes, and holds their names.</summary>
    const string MeshParts = "meshParts";

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
        Terms.InStack(Enum.GetNames<LayerBlendMode>()),
        Filters(),
        Terms.InStack(Enum.GetNames<LayerMaskSource>()),
        Terms.InStack(Enum.GetNames<LayerFillSource>()),
        Terms.InStack(Enum.GetNames<LayerProjection>()),
        Terms.InStack(
            TextureCompoundLibrary.Shipped.Where(static path => path.StartsWith("Generators/", StringComparison.Ordinal)
            )
        ),
        Terms.InStack(TextureNodeLibrary.MeshMaps),
        Terms.InStack(PaintAlphas.Names),
        MaskEffects(),
        Terms.InStack(Usages()),
        GeneratorParameters(),
        new(
            StackBrushes.Default,
            Terms.InBrush(StackBrushes.Falloffs),
            Terms.InBrush(StackBrushes.Rotations),
            Terms.InBrush(StackBrushes.SizeSpaces),
            Terms.InBrush(StackBrushes.ScatterAxes),
            Terms.InBrush(StackBrushes.Symmetries),
            Terms.InBrush(StackBrushes.Axes),
            Terms.InBrush(StackBrushes.Centres, true),
            Terms.InBrush(StackBrushes.Tools),
            Terms.InBrush(StackBrushes.Fills),
            StackBrushes.MaximumRadius,
            StackBrushes.MaximumAspect,
            StackBrushes.MaximumCount
        ),
        Terms.InStack(Enum.GetNames<LayerInputSource>()),
        Sources(),
        Terms.InStack(Enum.GetNames<LayerMaskLane>())
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
            Texts = effect.Texts?.ToDictionary(StringComparer.Ordinal) ?? [],
            Inputs = Sources(effect.Inputs) ?? []
        };

    /// <summary>Where each image input reads, as a file holds it, or null when none were given.</summary>
    public static Dictionary<string, LayerInputAsset>? Sources(IReadOnlyDictionary<string, StackInput>? inputs) =>
        inputs?.ToDictionary(static pair => pair.Key, static pair => Source(pair.Value), StringComparer.Ordinal);

    /// <summary>Where one image input reads, as a file holds it.</summary>
    /// <exception cref="StackEditRefusedException">The source is not one a stack knows.</exception>
    static LayerInputAsset Source(StackInput input) =>
        new() {
            Source = Enum.TryParse<LayerInputSource>(input.Source, false, out var source)
                && Enum.IsDefined(source)
                && !int.TryParse(input.Source, out _)
                    ? source
                    : throw new StackEditRefusedException(
                        $"'{input.Source}' is not where an input can read: it reads Beneath, Anchor, Generator, "
                        + "Texture or Bake."
                    ),
            Channel = input.Channel,
            Anchor = Reference(input.Anchor),
            Generator = input.Generator,
            Values = input.Values?.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.ToArray(),
                    StringComparer.Ordinal
                )
                ?? [],
            Texts = input.Parameters?.ToDictionary(StringComparer.Ordinal) ?? [],
            Asset = input.Asset,
            Map = input.Map
        };

    static Dictionary<string, StackInput>? Wire(Dictionary<string, LayerInputAsset> inputs) =>
        inputs.Count == 0
            ? null
            : inputs.ToDictionary(
                static pair => pair.Key,
                static pair => new StackInput(
                    pair.Value.Source.ToString(),
                    pair.Value.Channel,
                    Wire(pair.Value.Anchor),
                    pair.Value.Generator,
                    pair.Value.Values.Count == 0
                        ? null
                        : pair.Value.Values.ToDictionary(
                            static value => value.Key,
                            static value => (IReadOnlyList<float>)value.Value,
                            StringComparer.Ordinal
                        ),
                    pair.Value.Texts.Count == 0 ? null : pair.Value.Texts,
                    pair.Value.Asset,
                    pair.Value.Map
                ),
                StringComparer.Ordinal
            );

    /// <summary>A reference to an anchor point as a file holds it.</summary>
    public static AnchorReference Reference(StackAnchorRef? reference) =>
        reference is null ? new() : new() { Anchor = reference.Anchor, Channel = reference.Channel };

    /// <summary>Anchor point references by port, as a file holds them, or null when none were given.</summary>
    public static Dictionary<string, AnchorReference>? References(
        IReadOnlyDictionary<string, StackAnchorRef>? inputs
    ) =>
        inputs?.ToDictionary(static pair => pair.Key, static pair => Reference(pair.Value), StringComparer.Ordinal);

    static StackAnchorRef? Wire(AnchorReference reference) =>
        reference.Anchor.Length == 0 && reference.Channel.Length == 0 ? null : new(reference.Anchor, reference.Channel);

    static Dictionary<string, StackAnchorRef>? Wire(Dictionary<string, AnchorReference> inputs) =>
        inputs.Count == 0
            ? null
            : inputs.ToDictionary(static pair => pair.Key, static pair => Wire(pair.Value)!, StringComparer.Ordinal);

    /// <summary>The anchor points a set publishes, and who reads each, for a client to list.</summary>
    public static IReadOnlyList<StackAnchor> Anchors(TextureSetAsset set) {
        var uses = LayerStackEdit.AnchorUses(set);
        return [
            ..LayerStackEdit.AnchorPoints(set)
                .Select(point => new StackAnchor(
                        point.Key,
                        point.Value,
                        [
                            ..uses.Where(use => use.Reference.Anchor == point.Key)
                                .Select(static use => new StackAnchorUse(use.Layer, use.Where, use.Reference.Channel))
                        ]
                    )
                )
        ];
    }

    /// <summary>
    ///     Every adjustment a stack can run, read from the same registry and compounds its compile reads, each with
    ///     what its node declares, so that a client shows what will run.
    /// </summary>
    static IReadOnlyList<StackFilterKind> Filters() => Kinds(LayerStackGraph.IsAdjustment);

    /// <summary>
    ///     What an image input can read as a generator: every node and shipped compound that reads no image and writes
    ///     one, less the two an input names by their own sources, an image and a mesh map.
    /// </summary>
    static IReadOnlyList<StackFilterKind> Sources() =>
        Kinds(static type => type.Path is not ("Source/Bitmap" or "Source/Mesh Map")
            && type.Ports.Count(static port => port is { Direction: PortDirection.Output, Kind: PortKind.Image }) == 1
            && !type.Ports.Any(static port => port is { Direction: PortDirection.Input, Kind: PortKind.Image })
        );

    static IReadOnlyList<StackFilterKind> Kinds(Func<NodeTypeDefinition, bool> offered) {
        var library = LayerStackCompiler.Library(out var compounds);
        return [
            ..library.Types.Where(offered)
                .OrderBy(static type => type.Path, StringComparer.Ordinal)
                .Select(type => new StackFilterKind(
                        type.Path,
                        type.Title,
                        type.Category,
                        type.Summary,
                        [
                            ..type.Ports
                                .Where(static port => port is {
                                        Direction: PortDirection.Input, Kind: not PortKind.Image
                                    }
                                )
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
                    )
                )
        ];
    }

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

    /// <summary>
    ///     The filters with one image out, since a mask is one picture, less what a mask's colour selection is. That
    ///     one has its own fields, and an entry of its node in a mask's list is that selection.
    /// </summary>
    static IReadOnlyList<StackFilterKind> MaskEffects() => [
        ..Kinds(LayerStackGraph.IsMaskStep).Where(static filter => filter.Node != ColourSelect)
    ];

    static MaskEffectAsset? Selection(MaskAsset mask) =>
        mask.Effects.Find(static effect => effect.Node == ColourSelect);

    /// <summary>Points a stack that is not open at a mesh, relative to its file.</summary>
    /// <summary>Names a stack's texture set after the material it paints.</summary>
    public static void SetSetName(string path, string name) {
        var document = Parse(File.ReadAllText(path));
        LibraryFiles.Replace(
            path,
            Text(document with { Sets = [document.Sets[0] with { Name = name }, ..document.Sets.Skip(1)] })
        );
    }

    /// <summary>The side of a stack's maps.</summary>
    public static int Side(string path) => Parse(File.ReadAllText(path)).BaseWidth;

    public static void SetModel(string path, string model) {
        LibraryFiles.Replace(path, Text(Parse(File.ReadAllText(path)) with { Model = model }));
    }

    /// <summary>
    ///     Keeps a layer to some of the model's meshes. It is a mask entry that multiplies over the mask below, and is
    ///     the bottom of the stack when there is none, so it combines with any mask without being one.
    /// </summary>
    public static MaskAsset MeshMasked(MaskAsset mask, StackMeshMask? change) {
        List<MaskLayerAsset> others =
            [..mask.Layers.Where(static entry => !entry.GeneratorTexts.ContainsKey(MeshParts))];
        var held = mask.Layers.Find(static entry => entry.GeneratorTexts.ContainsKey(MeshParts));
        var entry = change is null
            ? held
            : change.Parts.Count == 0
                ? null
                : new() {
                    Source = LayerMaskSource.Texture,
                    Asset = change.Asset,
                    GeneratorTexts = new() { [MeshParts] = string.Join('\n', change.Parts) }
                };
        if (entry is not null) {
            var blend = mask.Source == LayerMaskSource.None && others.Count == 0
                ? LayerBlendMode.Copy
                : LayerBlendMode.Multiply;
            others.Add(entry with { Blend = blend });
        }

        return mask with { Layers = others };
    }

    /// <summary>Whether a mask entry keeps its layer to some meshes, which is the stack's and not the mask's.</summary>
    public static bool KeepsToMeshes(MaskLayerAsset entry) => entry.GeneratorTexts.ContainsKey(MeshParts);

    static string[]? MeshPartsOf(MaskAsset mask) =>
        mask.Layers.Find(static entry => entry.GeneratorTexts.ContainsKey(MeshParts))
            ?.GeneratorTexts[MeshParts]
            .Split('\n');

    /// <summary>What a filter layer runs, which a file may name either way. Any other layer runs nothing.</summary>
    public static string FilterNodeOf(LayerAsset layer) =>
        layer.FilterNode.Trim() is { Length: > 0 } node ? node
        : layer.Kind == LayerKind.Filter ? LayerStackGraph.Filter(layer.Filter).Type
        : string.Empty;

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
                Wire(layer.Mask.Anchor),
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
                                effect.Texts,
                                Wire(effect.Inputs)
                            )
                        )
                ],
                layer.Mask.GeneratorTexts,
                MeshPartsOf(layer.Mask),
                Wire(layer.Mask.GeneratorInputs),
                StackMaskLayers.Wire(layer.Mask),
                layer.Mask.Lane.ToString()
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
            FilterNodeOf(layer),
            layer.AnchorPoint,
            Wire(layer.Anchor),
            Wire(layer.Inputs),
            layer.Flattened,
            layer.Kind == LayerKind.Instance ? new(layer.Instance.Layer, layer.Instance.Set) : null
        );

    static Dictionary<string, IReadOnlyList<float>> Numbers(Dictionary<string, float[]> numbers) =>
        numbers.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyList<float>)pair.Value);
}
