using Patina.Contracts;
using Patina.Library;
using Vixen.TextureGraph;
using Vixen.Texturing;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Server.Stacks;

/// <summary>What a stack looks like on the wire and in a file.</summary>
public static class StackDocuments {
    public const string Extension = ".vxlayers";

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
        [..TextureCompoundLibrary.Shipped.Where(static path => path.StartsWith("Generators/", StringComparison.Ordinal))],
        TextureNodeLibrary.MeshMaps,
        PaintAlphas.Names
    );

    /// <summary>The stack a new file starts as: a base, and somewhere to paint.</summary>
    public static void Create(string path, int width, int height) {
        var name = Path.GetFileNameWithoutExtension(path);
        List<ChannelAsset> channels = [
            new() { Usage = "baseColor", Default = [0.5f, 0.5f, 0.5f, 1f] },
            new() { Usage = "roughness", Default = [0.5f, 0.5f, 0.5f, 1f] },
            new() { Usage = "metalness", Default = [0f, 0f, 0f, 1f] }
        ];
        LayerStackAsset stack = new() {
            Name = name,
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
                layer.Mask.Map
            )
        );

    static Dictionary<string, IReadOnlyList<float>> Numbers(Dictionary<string, float[]> numbers) =>
        numbers.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyList<float>)pair.Value);
}
