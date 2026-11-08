using Patina.Contracts;
using Patina.Server.Api;
using Patina.Server.Stacks;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Server.Tests;

/// <summary>A layer's mask as a stack of its own (#109): entries over the base, each with its own blend and canvas.</summary>
public sealed class StackMaskStackTests {
    static readonly IReadOnlyList<float> White = [1f, 1f, 1f, 1f];

    [Fact]
    public void Paint_goes_over_a_generator_as_an_entry_and_the_generator_stays() {
        var editor = Generated();

        editor.Apply(new([new StackAddMaskLayer("paint", "Paint")]));
        editor.Apply(new([Stroke(0)]));

        var mask = Layer(editor).Mask;
        Assert.Equal(LayerMaskSource.Generator, mask.Source);
        Assert.Equal(string.Empty, mask.Paint);
        var entry = Assert.Single(mask.Layers);
        Assert.Equal(LayerMaskSource.Paint, entry.Source);
        Assert.NotEqual(string.Empty, entry.Paint);

        var stroke = Assert.Single(editor.Standing);
        Assert.Equal((entry.Paint, PaintReference.Mask), (stroke.Paint, stroke.Usage));
        Assert.Null(stroke.Start);

        var wire = Assert.Single(StackDocuments.Layers([Layer(editor)])[0].Mask.Layers!);
        Assert.Equal(("Paint", true, "Copy"), (wire.Source, wire.Painted, wire.Blend));
    }

    [Fact]
    public void A_layer_with_no_mask_gets_a_white_base_under_its_first_entry() {
        var editor = new StackEditor("/tmp/Masks.vxlayers", Sample());

        editor.Apply(new([new StackAddMaskLayer("paint", "Bake")]));

        Assert.Equal((LayerMaskSource.Constant, 1f), (Layer(editor).Mask.Source, Layer(editor).Mask.Value));
        Assert.Equal("Add Mesh Map to mask", editor.History.UndoLabel);
        Assert.Equal("Add Mesh Map to mask", editor.Undo());
        Assert.Equal(LayerMaskSource.None, Layer(editor).Mask.Source);
        Assert.Empty(Layer(editor).Mask.Layers);
    }

    [Fact]
    public void Entries_move_change_and_go_counted_from_the_bottom_and_the_mesh_limit_stays_on_top() {
        var editor = Generated();
        editor.Apply(new([new StackSetLayer("paint", MeshMask: new(["Lid"], "lid.png"))]));
        editor.Apply(
            new([new StackAddMaskLayer("paint", "Constant"), new StackAddMaskLayer("paint", "Bake", Index: 0)])
        );

        Assert.Equal(["Bake", "Constant"], Sources(editor));
        Assert.True(StackDocuments.KeepsToMeshes(Layer(editor).Mask.Layers[^1]));

        editor.Apply(new([new StackMoveMaskLayer("paint", 0, 1)]));
        Assert.Equal(["Constant", "Bake"], Sources(editor));
        Assert.True(StackDocuments.KeepsToMeshes(Layer(editor).Mask.Layers[^1]));

        editor.Apply(new([new StackSetMaskLayer("paint", 1, Map: "ao", Blend: "Multiply", Opacity: 0.5f)]));
        var bake = StackDocuments.Layers([Layer(editor)])[0].Mask.Layers![1];
        Assert.Equal(("ao", "Multiply", 0.5f), (bake.Map, bake.Blend, bake.Opacity));
        Assert.Equal("Change mask layer", editor.History.UndoLabel);

        editor.Apply(new([new StackRemoveMaskLayer("paint", 0)]));
        Assert.Equal(["Bake"], Sources(editor));
        Assert.Equal(["Lid"], StackDocuments.Layers([Layer(editor)])[0].Mask.MeshParts);
    }

    [Fact]
    public void Taking_the_mask_away_takes_its_entries_and_keeps_the_mesh_limit() {
        var editor = Generated();
        editor.Apply(new([new StackSetLayer("paint", MeshMask: new(["Lid"], "lid.png"))]));
        editor.Apply(new([new StackAddMaskLayer("paint", "Paint")]));

        editor.Apply(new([new StackSetLayer("paint", Mask: new("None"))]));

        Assert.Empty(Sources(editor));
        Assert.Single(Layer(editor).Mask.Layers);
        Assert.Equal(["Lid"], StackDocuments.Layers([Layer(editor)])[0].Mask.MeshParts);
    }

    [Fact]
    public void A_brush_on_an_entry_that_is_not_painted_or_not_there_is_refused_with_its_code() {
        var editor = Generated();
        editor.Apply(new([new StackAddMaskLayer("paint", "Constant")]));

        var notPaint = Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([Stroke(0)])));
        Assert.Equal(Refusals.MaskLayerNotPaint, notPaint.Code);
        Assert.Equal("Constant", notPaint.Values["source"]);

        var missing = Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([Stroke(3)])));
        Assert.Equal(Refusals.MaskLayerMissing, missing.Code);
        Assert.Equal("3", missing.Values["index"]);

        Assert.Empty(editor.Standing);
    }

    /// <summary>A mesh-map mask and a mask entry read the lane they are given, and the vocabulary offers the lanes (#110).</summary>
    [Fact]
    public void A_mask_and_its_entries_read_the_lane_they_are_given() {
        var editor = Generated();

        editor.Apply(new([new StackSetLayer("paint", Mask: new("Bake", Map: "world", Lane: "Green"))]));
        editor.Apply(new([new StackAddMaskLayer("paint", "Bake"), new StackSetMaskLayer("paint", 0, Map: "normal", Lane: "Blue")]));

        Assert.Equal(LayerMaskLane.Green, Layer(editor).Mask.Lane);
        Assert.Equal(LayerMaskLane.Blue, Layer(editor).Mask.Layers[0].Lane);
        var wire = StackDocuments.Layers([Layer(editor)])[0].Mask;
        Assert.Equal(("Green", "Blue"), (wire.Lane, wire.Layers![0].Lane));
        Assert.Contains(StackDocuments.Vocabulary.Lanes!, static term => term is { Value: "Blue", Label: "Blue (Z)" });
        Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackSetMaskLayer("paint", 0, Lane: "Purple")]))
        );
    }

    static StackStroke Stroke(int entry) =>
        new("s", "paint", "baseColor", new(4f), White, [new(10f, 10f)], Mask: true, MaskLayer: entry);

    static List<string> Sources(StackEditor editor) => [
        ..StackDocuments.Layers([Layer(editor)])[0].Mask.Layers!.Select(static entry => entry.Source)
    ];

    static LayerAsset Layer(StackEditor editor) => LayerStackEdit.Find(editor.Document, new("Body", "paint"))!;

    static StackEditor Generated() {
        var editor = new StackEditor("/tmp/Masks.vxlayers", Sample());
        editor.Apply(new([new StackSetLayer("paint", Mask: new("Generator", Generator: "Generators/Dirt"))]));
        return editor;
    }

    static LayerStackAsset Sample() =>
        new() {
            Name = "Crate",
            BaseWidth = 64,
            BaseHeight = 64,
            Sets = [
                new() {
                    Name = "Body",
                    Channels = [new() { Usage = "baseColor", Default = [0.5f, 0.5f, 0.5f, 1f] }],
                    Layers = [
                        new() { Id = "base", Name = "Base", Kind = LayerKind.Fill },
                        new() { Id = "paint", Name = "Paint", Kind = LayerKind.Paint }
                    ]
                }
            ]
        };
}
