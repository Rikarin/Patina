using CsCheck;
using Patina.Contracts;
using Patina.Server.Stacks;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

public sealed class StackEditorPropertyTests {
    static readonly IReadOnlyList<float> White = [1f, 1f, 1f, 1f];

    static readonly StackView View = new([0f, 0f, 4f], [1f, 0f, 0f], [0f, 1f, 0f], [0f, 0f, -1f], 0.7f, 64, 64);

    /// <summary>
    ///     What keeps a layer to some meshes is the bottom of a mask with no base, and has to multiply once a stroke
    ///     gives it one: as a copy it would replace everything painted beneath it.
    /// </summary>
    [Fact]
    public void A_first_mask_stroke_puts_the_canvas_under_what_keeps_a_layer_to_some_meshes() {
        var editor = new StackEditor("/tmp/Kept.vxlayers", Sample());
        editor.Apply(new([new StackSetLayer("paint", MeshMask: new(["Lid"], "lid.png"))]));
        Assert.Equal(LayerBlendMode.Copy, Mask(editor).Layers[0].Blend);

        editor.Apply(
            new([new StackStroke("mask", "paint", "baseColor", new(4f), White, [new(1f, 1f)], Mask: true)])
        );

        Assert.Equal(LayerMaskSource.Paint, Mask(editor).Source);
        Assert.Equal(LayerBlendMode.Multiply, Mask(editor).Layers[0].Blend);
    }

    static MaskAsset Mask(StackEditor editor) =>
        LayerStackEdit.Find(editor.Document, new(editor.Set.Name, "paint"))!.Mask;

    /// <summary>
    ///     Random batches of every command, some of which are refused, then undoing all of them, gives the original
    ///     document and an empty journal; redoing all of them gives the edited document and the same journal.
    /// </summary>
    [Fact]
    [Trait("Category", "Property")]
    public void Random_batches_undone_return_the_original_stack() =>
        Gen.Int.Array[1, 30]
            .Sample(
                static batches => {
                    var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
                    var original = StackDocuments.Text(editor.Document);
                    foreach (var seed in batches) {
                        var before = StackDocuments.Text(editor.Document);
                        var random = new Random(seed);
                        var history = editor.Standing.Count;
                        try {
                            editor.Apply(new(Picks(editor, random)));
                        } catch (StackEditRefusedException) {
                            Assert.Equal(before, StackDocuments.Text(editor.Document));
                            Assert.Equal(history, editor.Standing.Count);
                        }
                    }

                    var edited = StackDocuments.Text(editor.Document);
                    long[] journal = [..editor.Standing.Select(static entry => entry.Id)];
                    // A part of a drag adds to the step before it, so the history is how many steps there are.
                    var steps = 0;
                    while (editor.Undo() is not null) {
                        steps++;
                    }

                    Assert.Equal(original, StackDocuments.Text(editor.Document));
                    Assert.Empty(editor.Standing);
                    for (var step = 0; step < steps; step++) {
                        Assert.NotNull(editor.Redo());
                    }

                    Assert.Null(editor.Redo());
                    Assert.Equal(edited, StackDocuments.Text(editor.Document));
                    Assert.Equal(journal, editor.Standing.Select(static entry => entry.Id).ToArray());
                },
                iter: 200,
                threads: 1
            );

    /// <summary>A stroke keeps the mesh that stood, so binding another later cannot move what it painted.</summary>
    [Fact]
    public void A_stroke_on_the_mesh_needs_one_and_keeps_it() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        StackStroke aimed = new("s", "paint", "baseColor", new(2f), White, [new(1f, 1f)], false, false, View);

        Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([aimed])));

        editor.Apply(new([new StackSetModel("a.glb")]));
        editor.Apply(new([aimed]));
        editor.Apply(new([new StackSetModel("b.glb")]));

        Assert.Equal("a.glb", editor.Standing.Single().Model);
    }

    /// <summary>The parts of a drag are one step, and whatever else arrives ends it.</summary>
    [Fact]
    public void The_parts_of_a_drag_are_one_step() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        editor.Apply(new([Stroke("a", true, 2)]));
        editor.Apply(new([Stroke("a", true, 3)]));
        Assert.Equal(5, Assert.Single(editor.Standing).Stamps.Count);
        editor.Apply(new([Stroke("b", false, 1)]));
        Assert.False(editor.HasOpenStroke);
        Assert.Equal(2, editor.Standing.Count);
        Assert.NotNull(editor.Undo());
        Assert.Equal(5, Assert.Single(editor.Standing).Stamps.Count);
    }

    /// <summary>A first stroke names the file, and undoing it takes the name back.</summary>
    [Fact]
    public void A_channel_can_be_added_once_and_undone() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        var before = editor.Set.Channels.Count;

        editor.Apply(new([new StackAddChannel("height")]));

        Assert.Equal(before + 1, editor.Set.Channels.Count);
        Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([new StackAddChannel("height")])));
        Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([new StackAddChannel("sparkle")])));
        Assert.NotNull(editor.Undo());
        Assert.Equal(before, editor.Set.Channels.Count);
    }

    [Fact]
    public void One_stroke_can_paint_several_channels_as_one_step() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        editor.Apply(new([new StackAddChannel("roughness")]));

        editor.Apply(new([Stroke("a", true, 2), Stroke("a", true, 2) with { Channel = "roughness" }]));
        editor.Apply(new([Stroke("a", false, 2), Stroke("a", false, 2) with { Channel = "roughness" }]));

        Assert.Equal(2, editor.Standing.Count);
        Assert.All(editor.Standing, static entry => Assert.Equal(4, entry.Stamps.Count));
        Assert.NotNull(editor.Undo());
        Assert.Empty(editor.Standing);
        Assert.Contains(editor.Set.Channels, static channel => channel.Usage == "roughness");
    }

    [Fact]
    public void More_of_a_saved_drag_leaves_the_stack_unsaved() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        editor.Apply(new([Stroke("a", true, 2)]));
        editor.MarkSaved();
        Assert.False(editor.History.Dirty);

        editor.Apply(new([Stroke("a", true, 3)]));

        Assert.True(editor.History.Dirty);
    }

    [Fact]
    public void A_first_stroke_names_its_paint_file() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        Assert.Equal(string.Empty, LayerOf(editor).Paint);
        editor.Apply(new([Stroke("a", false, 1)]));
        Assert.Equal("Crate.Body.paint.vxpaint", LayerOf(editor).Paint);
        editor.Undo();
        Assert.Equal(string.Empty, LayerOf(editor).Paint);
    }

    static LayerAsset LayerOf(StackEditor editor) => editor.Set.Layers.Single(static layer => layer.Id == "paint");

    static StackStroke Stroke(string name, bool open, int stamps) =>
        new(
            name,
            "paint",
            "baseColor",
            new(8f),
            White,
            [..Enumerable.Range(0, stamps).Select(static index => new StackStamp(index, index))],
            open
        );

    static List<StackCommand> Picks(StackEditor editor, Random random) {
        List<StackCommand> commands = [];
        for (var count = random.Next(1, 4); count > 0; count--) {
            commands.Add(Pick(editor, random));
        }

        return commands;
    }

    static StackCommand Pick(StackEditor editor, Random random) {
        string[] ids = [..editor.Set.Layers.Select(static layer => layer.Id), "absent"];
        var id = ids[random.Next(ids.Length)];
        return random.Next(8) switch {
            7 => new StackAddLayer(
                random.Next(2) == 0 ? "group" : "filter",
                "Nest",
                null,
                random.Next(3) == 0 ? id : null,
                "Hsl"
            ),
            6 => new StackNestLayer(id, random.Next(2) == 0 ? ids[random.Next(ids.Length)] : null),
            5 => new StackSetModel(random.Next(3) == 0 ? string.Empty : $"meshes/{random.Next(3)}.glb"),
            0 => new StackAddLayer(random.Next(4) == 0 ? "nonsense" : "fill", $"Layer {random.Next(4)}"),
            1 => new StackRemoveLayer(id),
            2 => new StackMoveLayer(id, random.Next(-2, 3)),
            3 => new StackSetLayer(
                id,
                random.Next(2) == 0 ? "Renamed" : null,
                random.Next(2) == 0,
                random.NextSingle() * 1.2f,
                random.Next(3) == 0 ? "Multiply" : null,
                random.Next(3) == 0 ? ["baseColor"] : null,
                random.Next(4) == 0 ? "Texture" : null,
                random.Next(4) == 0 ? new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = White } : null,
                null,
                null,
                random.Next(4) == 0 ? "Triplanar" : null,
                random.Next(4) == 0 ? "Blur" : null,
                null,
                random.Next(3) == 0 ? new("Anchor", Anchor: id) : null
            ),
            _ => new StackStroke(
                $"s{random.Next(3)}",
                id,
                random.Next(4) == 0 ? "absent" : "baseColor",
                new(random.Next(3)),
                White,
                [new(random.NextSingle() * 64f, random.NextSingle() * 64f)],
                random.Next(2) == 0,
                random.Next(4) == 0,
                random.Next(3) == 0 ? View : null,
                random.Next(4) == 0 ? "line" : "free",
                random.Next(8) == 0 ? "x" : null
            )
        };
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
