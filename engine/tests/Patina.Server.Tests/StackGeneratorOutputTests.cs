using Patina.Contracts;
using Patina.Library;
using Patina.Server.Stacks;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

/// <summary>
///     A generator that writes several pictures is read for the one a mask, a mask entry or an image input names
///     (#120): it is offered with its pictures, the name reaches the file and comes back on the wire, and another
///     generator starts from its only picture.
/// </summary>
public sealed class StackGeneratorOutputTests : IDisposable {
    const string Generator = "Two Pictures";

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-outputs-");

    public void Dispose() => folder.Delete(true);

    [Fact]
    public void A_generator_of_several_pictures_is_offered_with_them_and_one_of_one_without() {
        var compounds = Directory.CreateDirectory(Path.Combine(folder.FullName, LibraryLayout.CompoundsFolder));
        File.WriteAllText(
            Path.Combine(compounds.FullName, Generator + ".vxtexgraph"),
            """
            name: Two Pictures
            nodes:
              - { id: 1, type: Source/Uniform }
              - { id: 2, type: Sub-graph/Output }
            edges:
              - { fromNode: 1, fromPort: Out, toNode: 2, toPort: Mask }
              - { fromNode: 1, fromPort: Out, toNode: 2, toPort: Edge }
            interface:
              - { name: Mask, direction: Output, kind: Image }
              - { name: Edge, direction: Output, kind: Image }
            """
        );

        var kind = StackNodeKinds.Kind(Generator, folder.FullName);
        Assert.NotNull(kind);
        Assert.Equal(["Mask", "Edge"], kind.Outputs);
        Assert.Null(StackNodeKinds.Kind("Generators/Dirt", folder.FullName)!.Outputs);
        Assert.All(StackNodeKinds.Sources(), static source => Assert.Empty(source.Inputs ?? []));
    }

    [Fact]
    public void A_masks_chosen_picture_reaches_the_file_and_another_generator_starts_from_none() {
        var editor = Editor();
        editor.Apply(new([new StackSetLayer("paint", Mask: new("Generator", Generator: Generator, Output: "Edge"))]));

        Assert.Equal("Edge", Layer(editor).Mask.GeneratorOutput);
        var wired = StackDocuments.Layers([Layer(editor)])[0].Mask;
        Assert.Equal("Edge", wired.Output);
        Assert.Equal("Edge", Saved(editor).Mask.GeneratorOutput);

        // A client sends the mask back as it was told it, with another generator: the picture does not follow.
        editor.Apply(new([new StackSetLayer("paint", Mask: wired with { Generator = "Generators/Dirt" })]));
        Assert.Equal(string.Empty, Layer(editor).Mask.GeneratorOutput);
    }

    [Fact]
    public void A_mask_entrys_chosen_picture_reaches_the_file_and_another_generator_starts_from_none() {
        var editor = Editor();
        editor.Apply(
            new(
                [
                    new StackAddMaskLayer("paint", "Generator", Generator: Generator),
                    new StackSetMaskLayer("paint", 0, Output: "Mask")
                ]
            )
        );

        Assert.Equal("Mask", Layer(editor).Mask.Layers[0].GeneratorOutput);
        Assert.Equal("Mask", StackDocuments.Layers([Layer(editor)])[0].Mask.Layers![0].Output);
        Assert.Equal("Mask", Saved(editor).Mask.Layers[0].GeneratorOutput);

        editor.Apply(new([new StackSetMaskLayer("paint", 0, Opacity: 0.5f)]));
        Assert.Equal("Mask", Layer(editor).Mask.Layers[0].GeneratorOutput);

        editor.Apply(new([new StackSetMaskLayer("paint", 0, Generator: "Generators/Dirt")]));
        Assert.Equal(string.Empty, Layer(editor).Mask.Layers[0].GeneratorOutput);
    }

    [Fact]
    public void An_inputs_chosen_picture_reaches_the_file_and_the_wire() {
        var editor = Editor();
        editor.Apply(new([new StackAddLayer("filter", "Warp", FilterNode: "Filters/Warp")]));
        var warp = editor.Document.Sets[0].Layers[^1].Id;
        editor.Apply(
            new(
                [
                    new StackSetLayer(
                        warp,
                        Inputs: new Dictionary<string, StackInput> {
                            ["Warp"] = new("Generator", Generator: Generator, Output: "Edge")
                        }
                    )
                ]
            )
        );

        var saved = LayerStackYaml.Read(LayerStackYaml.Write(editor.Document)).Sets[0].Layers[^1];
        Assert.Equal("Edge", saved.Inputs["Warp"].Output);
        Assert.Equal("Edge", StackDocuments.Layers([saved])[0].Inputs!["Warp"].Output);
    }

    static LayerAsset Layer(StackEditor editor) => LayerStackEdit.Find(editor.Document, new("Body", "paint"))!;

    static LayerAsset Saved(StackEditor editor) =>
        LayerStackYaml.Read(LayerStackYaml.Write(editor.Document)).Sets[0].Layers.Single(static l => l.Id == "paint");

    static StackEditor Editor() =>
        new(
            "/tmp/Outputs.vxlayers",
            new() {
                Name = "Crate",
                BaseWidth = 64,
                BaseHeight = 64,
                Sets = [
                    new() {
                        Name = "Body",
                        Channels = [new() { Usage = "baseColor", Default = [0.5f, 0.5f, 0.5f, 1f] }],
                        Layers = [
                            new() { Id = "base", Name = "Base", Values = { ["baseColor"] = [0.5f, 0.5f, 0.5f, 1f] } },
                            new() { Id = "paint", Name = "Paint", Kind = LayerKind.Paint }
                        ]
                    }
                ]
            }
        );
}
