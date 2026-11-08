using Patina.Contracts;
using Patina.Server.Api;
using Patina.Server.Stacks;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

/// <summary>Named anchor points (#111) and flattened folders (#107), as commands with an undo.</summary>
public sealed class StackAnchorFlattenTests {
    static readonly IReadOnlyList<float> White = [1f, 1f, 1f, 1f];

    [Fact]
    public void Renaming_an_anchor_point_moves_every_reference_in_one_undo_step() {
        var editor = Anchored();

        editor.Apply(new([new StackSetLayer("base", AnchorPoint: "Ground")]));

        Assert.Equal("Ground", editor.Set.Layers[0].AnchorPoint);
        Assert.Equal("Ground", Layer(editor, "over").Mask.Anchor.Anchor);
        Assert.Equal("Ground", Layer(editor, "over").Anchor.Anchor);
        Assert.Equal("Ground", Assert.Single(StackDocuments.Anchors(editor.Set)).Name);

        Assert.Equal("Change layer", editor.Undo());

        Assert.Equal("Base", editor.Set.Layers[0].AnchorPoint);
        Assert.Equal("Base", Layer(editor, "over").Mask.Anchor.Anchor);
        Assert.Equal("Base", Layer(editor, "over").Anchor.Anchor);
    }

    [Fact]
    public void The_anchor_list_says_who_reads_each_point_and_in_which_channel() {
        var anchor = Assert.Single(StackDocuments.Anchors(Anchored().Set));

        Assert.Equal(("Base", "base"), (anchor.Name, anchor.Layer));
        Assert.Equal(
            [new StackAnchorUse("over", "fill", ""), new StackAnchorUse("over", "mask", "roughness")],
            anchor.Uses
        );
    }

    [Fact]
    public void A_taken_name_and_a_point_still_read_are_refused_with_their_codes() {
        var editor = Anchored();
        editor.Apply(new([new StackAddLayer("fill", "Other")]));
        var other = editor.Set.Layers[^1].Id;

        var taken = Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackSetLayer(other, AnchorPoint: "Base")]))
        );
        Assert.Equal(Refusals.AnchorTaken, taken.Code);

        var read = Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackSetLayer("base", AnchorPoint: "")]))
        );
        Assert.Equal(Refusals.AnchorInUse, read.Code);
        Assert.Equal("Over", read.Values["layer"]);
    }

    [Fact]
    public void A_duplicate_publishes_its_anchor_point_under_a_name_of_its_own() {
        var editor = Anchored();

        editor.Apply(new([new StackDuplicateLayers(["base", "over"])]));

        var points = LayerStackEdit.AnchorPoints(editor.Set);
        Assert.Equal(["Base", "Base 2"], points.Keys);
        var copy = editor.Set.Layers.Single(layer => layer.Id != "over" && layer.Mask.Anchor.Anchor.Length > 0
            && layer.Id.StartsWith("over", StringComparison.Ordinal));
        Assert.Equal("Base 2", copy.Mask.Anchor.Anchor);
    }

    [Fact]
    public void Flattening_a_folder_names_a_canvas_and_journals_what_fills_it() {
        var editor = Foldered();

        editor.Apply(new([new StackFlattenLayer("folder")]));

        var folder = Layer(editor, "folder");
        Assert.True(folder.Flattened);
        Assert.True(StackDocuments.Layers(editor.Set.Layers).Single(layer => layer.Id == "folder").Flattened);
        var entry = Assert.Single(editor.Standing, static entry => entry.Capture is not null);
        Assert.Equal(folder.Paint, entry.Paint);
        Assert.Equal(("folder", false), (entry.Capture!.Layer, entry.Capture.Backdrop));
        Assert.False(LayerStackYaml.Read(entry.Capture.Document).Sets[0].Layers[1].Flattened);
        Assert.Equal(2, folder.Children.Count);

        Assert.Equal("Flatten folder", editor.Undo());
        Assert.False(Layer(editor, "folder").Flattened);
        Assert.DoesNotContain(editor.Standing, static entry => entry.Capture is not null);
    }

    [Fact]
    public void A_flattened_folder_unflattens_to_its_children_and_takes_a_brush() {
        var editor = Foldered();
        editor.Apply(new([new StackFlattenLayer("folder")]));

        editor.Apply(new([new StackStroke("s", "folder", "baseColor", new(4f), White, [new(1f, 1f)])]));
        Assert.Equal(Layer(editor, "folder").Paint, editor.Standing[^1].Paint);

        editor.Apply(new([new StackUnflattenLayer("folder")]));
        Assert.False(Layer(editor, "folder").Flattened);

        var again = Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackUnflattenLayer("folder")]))
        );
        Assert.Equal(Refusals.NotFlattened, again.Code);
    }

    [Fact]
    public void Only_a_folder_flattens_and_only_one_whose_picture_a_canvas_can_hold() {
        var editor = Foldered();

        var paint = Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackFlattenLayer("paint")]))
        );
        Assert.Equal(Refusals.FlattenRefused, paint.Code);

        editor.Apply(new([new StackAddLayer("filter", "Levels", Parent: "folder")]));
        var reads = Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackFlattenLayer("folder")]))
        );
        Assert.Equal(Refusals.FlattenRefused, reads.Code);
        Assert.Contains("passes through", reads.Values["reason"], StringComparison.Ordinal);
    }

    [Fact]
    public void Flattening_the_stack_gathers_every_layer_into_one_folder_in_one_step() {
        var editor = Foldered();
        editor.Apply(new([new StackAddLayer("filter", "Levels")]));
        var layers = editor.Set.Layers.Select(static layer => layer.Id).ToArray();

        editor.Apply(new([new StackFlattenLayer()]));

        var whole = Assert.Single(editor.Set.Layers);
        Assert.True(whole.Flattened);
        Assert.Equal(layers, whole.Children.Select(static layer => layer.Id));
        Assert.True(Assert.Single(editor.Standing, static entry => entry.Capture is not null).Capture!.Backdrop);

        Assert.Equal("Flatten stack", editor.Undo());
        Assert.Equal(layers, editor.Set.Layers.Select(static layer => layer.Id));
    }

    static LayerAsset Layer(StackEditor editor, string id) => LayerStackEdit.Find(editor.Document, new("Body", id))!;

    static StackEditor Anchored() {
        var editor = new StackEditor("/tmp/Anchored.vxlayers", Sample());
        editor.Apply(
            new([
                new StackSetLayer("base", AnchorPoint: "Base"),
                new StackAddLayer("fill", "Over")
            ])
        );
        var over = editor.Set.Layers[^1].Id;
        Assert.Equal("over-1", over);
        editor.Apply(
            new([
                new StackSetLayer(
                    over,
                    Fill: "Anchor",
                    Anchor: new("Base"),
                    Mask: new("Anchor", Anchor: new("Base", "roughness"))
                )
            ])
        );

        // An id a reader can name, so the asserts read as the stack does.
        editor.Apply(new([new StackSetLayer(over, Name: "Over")]));
        return Renamed(editor, over, "over");
    }

    static StackEditor Renamed(StackEditor editor, string from, string to) {
        var document = editor.Document;
        var set = document.Sets[0];
        var layers = set.Layers.Select(layer => layer.Id == from ? layer with { Id = to } : layer).ToList();
        return new(editor.AssetPath, document with { Sets = [set with { Layers = layers }] });
    }

    static StackEditor Foldered() {
        var editor = new StackEditor("/tmp/Folder.vxlayers", Sample());
        editor.Apply(new([new StackAddLayer("group", "Folder")]));
        var folder = editor.Set.Layers[^1].Id;
        editor.Apply(new([new StackAddLayer("fill", "Inner", Parent: folder), new StackNestLayer("paint", folder)]));
        return Renamed(editor, folder, "folder");
    }

    static LayerStackAsset Sample() =>
        new() {
            Name = "Crate",
            BaseWidth = 64,
            BaseHeight = 64,
            Sets = [
                new() {
                    Name = "Body",
                    Channels = [
                        new() { Usage = "baseColor", Default = [0.5f, 0.5f, 0.5f, 1f] },
                        new() { Usage = "roughness", Default = [0.5f, 0.5f, 0.5f, 1f] }
                    ],
                    Layers = [
                        new() { Id = "base", Name = "Base", Kind = LayerKind.Fill },
                        new() { Id = "paint", Name = "Paint", Kind = LayerKind.Paint }
                    ]
                }
            ]
        };
}
