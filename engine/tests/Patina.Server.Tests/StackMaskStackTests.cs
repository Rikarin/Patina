using Patina.Contracts;
using Patina.Server.Api;
using Patina.Server.Stacks;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Server.Tests;

/// <summary>
///     A layer's mask as a stack of its own (#109): entries over the base, each with its own blend and canvas.
/// </summary>
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

    /// <summary>
    ///     A mask switches off and on again with all it holds, one undo step each, and the limit to some meshes is kept
    ///     whatever the switch says, so a layer switched unmasked stays on its meshes.
    /// </summary>
    [Fact]
    public void A_mask_switches_off_keeping_what_it_holds_and_the_mesh_limit() {
        var editor = Generated();
        editor.Apply(new([new StackSetLayer("paint", MeshMask: new(["Lid"], "lid.png"))]));
        editor.Apply(new([new StackAddMaskLayer("paint", "Constant")]));
        var wired = StackDocuments.Layers([Layer(editor)])[0].Mask;
        Assert.True(wired.Enabled);

        editor.Apply(new([new StackSetLayer("paint", Mask: wired with { Enabled = false })]));

        var mask = Layer(editor).Mask;
        Assert.False(mask.Enabled);
        Assert.Equal(LayerMaskSource.Generator, mask.Source);
        Assert.Equal(["Constant"], Sources(editor));
        Assert.True(mask.Layers[^1].Kept, "the mesh limit must apply with the mask off");
        Assert.False(StackDocuments.Layers([Layer(editor)])[0].Mask.Enabled);
        var saved = LayerStackYaml.Read(LayerStackYaml.Write(editor.Document)).Sets[0];
        Assert.False(saved.Layers.Single(static layer => layer.Id == "paint").Mask.Enabled);

        editor.Undo();
        Assert.True(Layer(editor).Mask.Enabled);
    }

    /// <summary>
    ///     History names a switch of the mask for what it did, and any other change of the mask as a change of it
    ///     (#109).
    /// </summary>
    [Fact]
    public void History_says_a_mask_was_switched_on_or_off() {
        var editor = Generated();
        var wired = StackDocuments.Layers([Layer(editor)])[0].Mask;

        editor.Apply(new([new StackSetLayer("paint", Mask: wired with { Enabled = false })]));
        Assert.Equal("Switch mask off", editor.History.UndoLabel);

        editor.Apply(new([new StackSetLayer("paint", Mask: wired with { Enabled = true })]));
        Assert.Equal("Switch mask on", editor.History.UndoLabel);

        editor.Apply(new([new StackSetLayer("paint", Mask: wired with { Enabled = false, Source = "Constant" })]));
        Assert.Equal("Change mask", editor.History.UndoLabel);
    }

    /// <summary>
    ///     A mask step's blend and opacity reach the file and come back on the wire, and a bad one is refused.
    /// </summary>
    [Fact]
    public void A_mask_steps_blend_and_opacity_reach_the_file() {
        var editor = Generated();
        var wired = StackDocuments.Layers([Layer(editor)])[0].Mask;

        editor.Apply(
            new(
                [
                    new StackSetLayer(
                        "paint",
                        Mask: wired with { Effects = [new("Colour/Invert", Blend: "Multiply", Opacity: 0.4f)] }
                    )
                ]
            )
        );

        var effect = Assert.Single(Layer(editor).Mask.Effects);
        Assert.Equal((LayerBlendMode.Multiply, 0.4f), (effect.Blend, effect.Opacity));
        var back = Assert.Single(StackDocuments.Layers([Layer(editor)])[0].Mask.Effects!);
        Assert.Equal(("Multiply", 0.4f), (back.Blend, back.Opacity));

        Assert.Throws<StackEditRefusedException>(() => editor.Apply(
                new([new StackSetLayer("paint", Mask: wired with { Effects = [new("Colour/Invert", Opacity: 2f)] })])
            )
        );
        Assert.Throws<StackEditRefusedException>(() => editor.Apply(
                new(
                    [
                        new StackSetLayer(
                            "paint",
                            Mask: wired with { Effects = [new("Colour/Invert", Blend: "Sideways")] }
                        )
                    ]
                )
            )
        );
    }

    /// <summary>
    ///     A generator entry's image inputs are fed from anchor points by a change of the entry (#111), and another
    ///     generator starts with none.
    /// </summary>
    [Fact]
    public void A_generator_entrys_inputs_read_anchor_points() {
        var editor = Generated();
        editor.Apply(new([new StackAddMaskLayer("paint", "Generator", Generator: "Filters/Blur")]));

        editor.Apply(
            new(
                [
                    new StackSetMaskLayer(
                        "paint",
                        0,
                        Inputs: new Dictionary<string, StackAnchorRef> { ["Input"] = new("Ground", "height") }
                    )
                ]
            )
        );

        Assert.Equal(
            new() { Anchor = "Ground", Channel = "height" },
            Layer(editor).Mask.Layers[0].GeneratorInputs["Input"]
        );
        Assert.Equal(
            new("Ground", "height"),
            StackDocuments.Layers([Layer(editor)])[0].Mask.Layers![0].Inputs!["Input"]
        );

        editor.Apply(new([new StackSetMaskLayer("paint", 0, Generator: "Colour/Levels")]));
        Assert.Empty(Layer(editor).Mask.Layers[0].GeneratorInputs);
    }

    /// <summary>
    ///     A layer's colour tag is set, named in History, read back, and refused when it is no colour (#106).
    /// </summary>
    [Fact]
    public void A_layer_takes_a_colour_tag() {
        var editor = Generated();

        editor.Apply(new([new StackSetLayer("paint", Tag: "Violet")]));

        Assert.Equal(LayerTag.Violet, Layer(editor).Tag);
        Assert.Equal("Violet", StackDocuments.Layers([Layer(editor)])[0].Tag);
        Assert.Equal("Tag layer", editor.History.UndoLabel);
        Assert.Contains(StackDocuments.Vocabulary.Tags!, static term => term is { Value: "Grey", Label: "Gray" });
        Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([new StackSetLayer("paint", Tag: "Plaid")])));

        editor.Undo();
        Assert.Equal(LayerTag.None, Layer(editor).Tag);
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
        editor.Apply(new([new StackAddMaskLayer("paint", "Constant"), new StackAddMaskLayer("paint", "Bake", 0)]));

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

    /// <summary>
    ///     A mesh-map mask and a mask entry read the lane they are given, and the vocabulary offers the lanes (#110).
    /// </summary>
    [Fact]
    public void A_mask_and_its_entries_read_the_lane_they_are_given() {
        var editor = Generated();

        editor.Apply(new([new StackSetLayer("paint", Mask: new("Bake", Map: "world", Lane: "Green"))]));
        editor.Apply(
            new(
                [new StackAddMaskLayer("paint", "Bake"), new StackSetMaskLayer("paint", 0, Map: "normal", Lane: "Blue")]
            )
        );

        Assert.Equal(LayerMaskLane.Green, Layer(editor).Mask.Lane);
        Assert.Equal(LayerMaskLane.Blue, Layer(editor).Mask.Layers[0].Lane);
        var wire = StackDocuments.Layers([Layer(editor)])[0].Mask;
        Assert.Equal(("Green", "Blue"), (wire.Lane, wire.Layers![0].Lane));
        Assert.Contains(StackDocuments.Vocabulary.Lanes!, static term => term is { Value: "Blue", Label: "Blue (Z)" });
        Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackSetMaskLayer("paint", 0, Lane: "Purple")]))
        );
    }

    /// <summary>
    ///     A selection of several colours is a Colour Select for each (#110): the first reads the mask beneath it, each
    ///     after it the mask's own map, joined by the lighter where the value is above the background and the darker
    ///     where it is below. The colours, the value and the background read back as they were set, and several colours
    ///     with no map or picture beneath to pick them from are refused.
    /// </summary>
    [Fact]
    public void A_selection_of_several_colours_picks_each_out_of_the_map() {
        var editor = Generated();
        IReadOnlyList<float> red = [1f, 0f, 0f];
        IReadOnlyList<float> blue = [0f, 0f, 1f];

        editor.Apply(
            new(
                [
                    new StackSetLayer(
                        "paint",
                        Mask: new("Bake", Map: "id", Colours: [red, blue], Inside: 0.75f, Outside: 0.25f)
                    )
                ]
            )
        );

        var effects = Layer(editor).Mask.Effects;
        Assert.Equal(2, effects.Count);
        Assert.All(effects, static effect => Assert.Equal("Analysis/Colour Select", effect.Node));
        Assert.Equal((LayerBlendMode.Copy, LayerBlendMode.Lighten), (effects[0].Blend, effects[1].Blend));
        Assert.Empty(effects[0].Inputs);
        Assert.Equal(
            (LayerInputSource.Bake, "id"),
            (effects[1].Inputs["Input"].Source, effects[1].Inputs["Input"].Map)
        );
        Assert.Equal([0.75f], effects[1].Values["Value"]);
        Assert.Equal([0.25f], effects[1].Values["Background"]);

        var wire = StackDocuments.Layers([Layer(editor)])[0].Mask;
        Assert.Equal([[1f, 0f, 0f, 1f], [0f, 0f, 1f, 1f]], wire.Colours!.Select(static colour => colour.ToArray()));
        Assert.Equal((0.75f, 0.25f), (wire.Inside, wire.Outside));
        Assert.Empty(wire.Effects!);

        editor.Apply(new([new StackSetLayer("paint", Mask: wire with { Inside = 0f, Outside = 1f })]));
        Assert.Equal(LayerBlendMode.Darken, Layer(editor).Mask.Effects[1].Blend);

        editor.Apply(new([new StackSetLayer("paint", Mask: wire with { Colours = [blue] })]));
        Assert.Single(Layer(editor).Mask.Effects);

        Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackSetLayer("paint", Mask: new("Constant", Colours: [red, blue]))]))
        );
        Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackSetLayer("paint", Mask: new("Bake", Map: "id", Colours: [[1f, 0f]]))]))
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
