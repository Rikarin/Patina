using CsCheck;
using Patina.Contracts;
using Patina.Server.Api;
using Patina.Server.Graphs;
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

        editor.Apply(new([new StackStroke("mask", "paint", "baseColor", new(4f), White, [new(1f, 1f)], Mask: true)]));

        Assert.Equal(LayerMaskSource.Paint, Mask(editor).Source);
        Assert.Equal(LayerBlendMode.Multiply, Mask(editor).Layers[0].Blend);
    }

    /// <summary>
    ///     What a new fill shows in Properties is what it has to hold, or it composites nothing until set.
    /// </summary>
    [Fact]
    public void A_new_fill_holds_the_defaults_it_shows() {
        var editor = new StackEditor("/tmp/Fill.vxlayers", Sample());

        editor.Apply(new([new StackAddLayer("fill", "Fill")]));

        Assert.Equal([0.5f, 0.5f, 0.5f, 1f], editor.Set.Layers[^1].Values["baseColor"]);
    }

    /// <summary>
    ///     A fill that says what it writes keeps to that: leaving a channel alone is what a fill is often for.
    /// </summary>
    [Fact]
    public void A_fill_given_values_writes_only_those() {
        var editor = new StackEditor("/tmp/Fill.vxlayers", Sample());

        editor.Apply(
            new(
                [
                    new StackAddLayer(
                        "fill",
                        "Fill",
                        new Dictionary<string, IReadOnlyList<float>> { ["roughness"] = White }
                    )
                ]
            )
        );

        Assert.Equal(["roughness"], editor.Set.Layers[^1].Values.Keys);
    }

    [Fact]
    public void Added_layers_are_numbered_whatever_they_sit_in() {
        var editor = new StackEditor("/tmp/Names.vxlayers", Sample());

        editor.Apply(new([new StackAddLayer("paint", "Paint"), new StackAddLayer("group", "Folder")]));
        editor.Apply(new([new StackAddLayer("paint", "Paint", Parent: editor.Set.Layers[^1].Id)]));
        editor.Apply(new([new StackAddLayer("paint", "Paint")]));

        Assert.Equal("Paint 2", editor.Set.Layers[2].Name);
        Assert.Equal("Paint 3", editor.Set.Layers[3].Children[0].Name);
        Assert.Equal("Paint 4", editor.Set.Layers[^1].Name);
    }

    /// <summary>A stroke the composite would never read is not a stroke: nothing tells the hand otherwise.</summary>
    [Fact]
    public void A_channel_a_layer_does_not_write_takes_no_brush() {
        var editor = new StackEditor("/tmp/Off.vxlayers", Sample());
        editor.Apply(new([new StackAddChannel("roughness")]));
        editor.Apply(new([new StackSetLayer("paint", Channels: ["roughness"])]));

        Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackStroke("off", "paint", "baseColor", new(4f), White, [new(1f, 1f)])]))
        );
        Assert.Single(editor.Apply(new([new StackStroke("on", "paint", "roughness", new(4f), White, [new(1f, 1f)])])));
    }

    /// <summary>A filter a graph brought can go back to being a named one, which is its only way out.</summary>
    [Fact]
    public void A_filter_from_a_graph_can_become_a_named_filter() {
        var editor = new StackEditor("/tmp/Filter.vxlayers", Sample());
        editor.Apply(new([new StackAddLayer("filter", "Soft", FilterNode: "Colour/Levels")]));
        var filter = editor.Set.Layers[^1].Id;

        editor.Apply(new([new StackSetLayer(filter, Filter: "Blur", FilterNode: string.Empty)]));

        Assert.Empty(editor.Set.Layers[^1].FilterNode);
        Assert.Equal(LayerFilterKind.Blur, editor.Set.Layers[^1].Filter);
    }

    [Fact]
    public void A_seed_is_in_the_state_and_undoes_like_any_edit() {
        var session = new StackSession(
            "/tmp/Seed.vxlayers",
            StackDocuments.Text(Sample() with { Seed = 7 }),
            static () => { }
        );
        Assert.Equal(7u, session.State.Seed);

        session.Apply(new([new StackSetSeed(9)]));
        Assert.Equal(9u, session.State.Seed);

        session.Undo();
        Assert.Equal(7u, session.State.Seed);
    }

    [Fact]
    public void How_height_becomes_the_normal_is_the_sets_and_undoes_like_any_edit() {
        var session = new StackSession("/tmp/Height.vxlayers", StackDocuments.Text(Sample()), static () => { });
        Assert.Equal("Smooth", session.State.HeightNormal);

        session.Apply(new([new StackSetHeightNormal("Sharp")]));
        Assert.Equal("Sharp", session.State.HeightNormal);
        Assert.Contains("heightNormal: Sharp", session.Text, StringComparison.Ordinal);

        Assert.Throws<StackEditRefusedException>(() => session.Apply(new([new StackSetHeightNormal("Blurry")])));
        session.Undo();
        Assert.Equal("Smooth", session.State.HeightNormal);
    }

    /// <summary>
    ///     A set's settings change together as one step, what is left out stays, a side no set has is refused, and undo
    ///     puts every one back (#19, #210).
    /// </summary>
    [Fact]
    public void A_sets_settings_change_as_one_step_and_undo_together() {
        var session = new StackSession("/tmp/Settings.vxlayers", StackDocuments.Text(Sample()), static () => { });
        var (width, height) = (session.State.Width, session.State.Height);

        session.Apply(new([new StackSetSettings(4096, 1024, 1, "Sharp")]));
        var state = session.State;
        Assert.Equal((4096, 1024, 1, "Sharp"), (state.Width, state.Height, state.UvSet, state.HeightNormal));
        Assert.Contains("uvSet: 1", session.Text, StringComparison.Ordinal);
        Assert.Equal("Texture set settings", session.Steps.Done[^1].Label);

        session.Apply(new([new StackSetSettings(UvSet: 0)]));
        Assert.Equal((4096, 1024, 0), (session.State.Width, session.State.Height, session.State.UvSet));

        Assert.Throws<StackEditRefusedException>(() => session.Apply(new([new StackSetSettings(3000)])));
        Assert.Throws<StackEditRefusedException>(() => session.Apply(new([new StackSetSettings(UvSet: 8)])));

        session.Undo();
        session.Undo();
        var back = session.State;
        Assert.Equal((width, height, 0, "Smooth"), (back.Width, back.Height, back.UvSet, back.HeightNormal));
    }

    /// <summary>
    ///     A stack's history is a list with labels, and a jump through it is one event that lands where single steps
    ///     would.
    /// </summary>
    [Fact]
    public void A_stack_history_lists_its_steps_and_jumps_as_one_event() {
        var session = new StackSession(
            "/tmp/History.vxlayers",
            StackDocuments.Text(Sample() with { Seed = 7 }),
            static () => { }
        );
        session.Apply(new([new StackSetSeed(9)], "Change Seed"));
        session.Apply(new([new StackSetSeed(11)]));
        session.Apply(new([new StackSetHeightNormal("Sharp")]));
        var list = session.Steps;
        Assert.Equal(3, list.Done.Count);
        Assert.Equal("Change Seed", list.Done[0].Label);
        Assert.Equal(list.Done[^1].Id, session.State.History.Step);

        var sequence = session.State.Sequence;
        var jumped = session.JumpTo(list.Done[0].Id);
        Assert.Equal(sequence + 1, jumped.Sequence);
        Assert.Equal(9u, session.State.Seed);
        Assert.Equal("Smooth", session.State.HeightNormal);
        Assert.Equal<HistoryStep>([list.Done[1], list.Done[2]], session.Steps.Undone);

        session.JumpTo(null);
        Assert.Equal(7u, session.State.Seed);
        Assert.Null(session.State.History.Step);
        Assert.Equal(sequence + 2, session.State.Sequence);

        Assert.Equal(sequence + 2, session.JumpTo(null).Sequence); // already there: no event
        session.JumpTo(list.Done[2].Id);
        Assert.Equal("Sharp", session.State.HeightNormal);
        Assert.Equal(11u, session.State.Seed);
        Assert.Throws<HistoryStepGoneException>(() => session.JumpTo(12345));
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

    /// <summary>
    ///     A symmetry or a size in the model's units is on the mesh, in the atlas as on the picture, and a brush or a
    ///     symmetry no Worker could stroke with is refused with a code a client can explain, before anything stands.
    /// </summary>
    [Fact]
    public void A_symmetric_or_out_of_range_stroke_is_refused_before_it_stands() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        StackStroke flat = new("s", "paint", "baseColor", new(2f), White, [new(1f, 1f)]);

        string Refused(StackStroke stroke) =>
            Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([stroke]))).Code;

        Assert.Equal(Refusals.NoMesh, Refused(flat with { Symmetry = new() }));
        Assert.Equal(Refusals.NoMesh, Refused(flat with { Brush = new(2f, SizeSpace: "model") }));

        editor.Apply(new([new StackSetModel("a.glb")]));
        Assert.Equal(Refusals.BrushRefused, Refused(flat with { Symmetry = new(Axis: "w") }));
        Assert.Equal(Refusals.BrushRefused, Refused(flat with { Symmetry = new("radial", Count: 65) }));
        Assert.Equal(Refusals.BrushRefused, Refused(flat with { Brush = new(2f, Aspect: 17f) }));
        Assert.Equal(Refusals.BrushRefused, Refused(flat with { Brush = new(5000f) }));
        Assert.Equal(Refusals.BrushRefused, Refused(flat with { Brush = new(2f, Curve: "cubic") }));
        Assert.Empty(editor.Standing);

        editor.Apply(new([flat with { Symmetry = new("radial", "z", Count: 8), Brush = new(2f, SizeSpace: "model") }]));
        Assert.Equal(8, editor.Standing.Single().Symmetry!.Count);
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

    /// <summary>A channel is added once, named as a person names it, and undoing takes it away.</summary>
    [Fact]
    public void A_channel_can_be_added_once_and_undone() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        var before = editor.Set.Channels.Count;

        editor.Apply(new([new StackAddChannel("occlusion")]));

        Assert.Equal(before + 1, editor.Set.Channels.Count);
        // The History list and a refusal say what a person calls the channel, never its stored value (#198).
        Assert.Equal("Add Ambient Occlusion", editor.History.UndoLabel);
        Assert.Equal(
            "This document has Ambient Occlusion already.",
            Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([new StackAddChannel("occlusion")])))
                .Message
        );
        Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([new StackAddChannel("sparkle")])));
        Assert.NotNull(editor.Undo());
        Assert.Equal(before, editor.Set.Channels.Count);
    }

    /// <summary>
    ///     A channel comes off the set as one step and comes back as it was (#141): the layers keep what they held for
    ///     it. The set keeps one channel, and a channel it lacks is refused with a code.
    /// </summary>
    [Fact]
    public void A_channel_can_be_removed_and_added_back_with_what_the_layers_held() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        editor.Apply(new([new StackAddChannel("roughness")]));
        editor.Apply(
            new(
                [
                    new StackSetLayer(
                        "paint",
                        Values: new Dictionary<string, IReadOnlyList<float>> {
                            ["roughness"] = [0.25f, 0.25f, 0.25f, 1f]
                        }
                    )
                ]
            )
        );
        var layers = editor.Set.Layers;

        editor.Apply(new([new StackRemoveChannel("roughness")]));

        Assert.DoesNotContain(editor.Set.Channels, static channel => channel.Usage == "roughness");
        Assert.Equal("Remove Roughness", editor.History.UndoLabel);
        Assert.Equal(layers, editor.Set.Layers);
        Assert.Equal(
            Refusals.ChannelMissing,
            Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([new StackRemoveChannel("roughness")])))
                .Code
        );

        editor.Apply(new([new StackAddChannel("roughness")]));
        Assert.Equal(layers, editor.Set.Layers);
        Assert.NotNull(editor.Undo());
        Assert.NotNull(editor.Undo());
        Assert.Contains(editor.Set.Channels, static channel => channel.Usage == "roughness");

        foreach (var channel in editor.Set.Channels.Skip(1).ToList()) {
            editor.Apply(new([new StackRemoveChannel(channel.Usage)]));
        }

        Assert.Equal(
            Refusals.ChannelLast,
            Assert.Throws<StackEditRefusedException>(() => editor.Apply(
                        new([new StackRemoveChannel(editor.Set.Channels.Single().Usage)])
                    )
                )
                .Code
        );
    }

    /// <summary>
    ///     A stroke aimed at a channel taken off the set is refused with the code a client has words for (#261), though
    ///     the layer still holds a value for it, and paints nothing.
    /// </summary>
    [Fact]
    public void A_stroke_on_a_channel_the_set_no_longer_has_is_refused_with_a_code() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        editor.Apply(new([new StackAddChannel("roughness")]));
        editor.Apply(new([Stroke("a", false, 2) with { Channel = "roughness" }]));
        editor.Apply(new([new StackRemoveChannel("roughness")]));
        var standing = editor.Standing.Count;

        var refused = Assert.Throws<StackEditRefusedException>(() => editor.Apply(
                new([Stroke("b", false, 2) with { Channel = "roughness" }])
            )
        );

        Assert.Equal(Refusals.ChannelMissing, refused.Code);
        Assert.Equal("Roughness", refused.Values["channel"]);
        Assert.Equal(standing, editor.Standing.Count);
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
                random.Next(3) == 0 ? new("Anchor", Anchor: new(id, "baseColor")) : null
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
                random.Next(8) == 0 ? new StackSymmetry() : null
            )
        };
    }

    /// <summary>
    ///     A duplicate takes everything inside it, and every canvas it names is one nothing else names: a stroke on the
    ///     copy must leave the original alone. An anchor onto a layer that came along reads the copy of it.
    /// </summary>
    [Fact]
    public void A_duplicate_shares_no_canvas_and_anchors_within_itself() {
        var editor = new StackEditor("/tmp/Twice.vxlayers", Sample());
        editor.Apply(new([new StackStroke("a", "paint", "baseColor", new(4f), White, [new(1f, 1f)])]));
        editor.Apply(new([new StackStroke("m", "paint", "baseColor", new(4f), White, [new(1f, 1f)], Mask: true)]));
        editor.Apply(new([new StackAddLayer("group", "Folder"), new StackAddLayer("fill", "Inner")]));
        var folder = editor.Set.Layers[^2].Id;
        var inner = editor.Set.Layers[^1].Id;
        editor.Apply(new([new StackNestLayer(inner, folder), new StackNestLayer("paint", folder)]));
        editor.Apply(new([new StackSetLayer("paint", AnchorPoint: "Paint")]));
        editor.Apply(new([new StackSetLayer(inner, Mask: new("Anchor", Anchor: new("Paint", "baseColor")))]));

        editor.Apply(new([new StackDuplicateLayers([folder, "paint"])]));

        var copy = editor.Set.Layers[^1];
        Assert.NotEqual(folder, copy.Id);
        Assert.Equal(2, copy.Children.Count);
        var (copiedInner, copiedPaint) = (copy.Children[0], copy.Children[1]);
        var original = editor.Set.Layers[^2].Children[1];
        Assert.NotEqual(original.Paint, copiedPaint.Paint);
        Assert.NotEqual(original.Mask.Paint, copiedPaint.Mask.Paint);
        Assert.Equal("Paint 2", copiedPaint.AnchorPoint);
        Assert.Equal("Paint 2", copiedInner.Mask.Anchor.Anchor);
        Assert.Equal(
            [(original.Mask.Paint, copiedPaint.Mask.Paint), (original.Paint, copiedPaint.Paint)],
            editor.Standing.Where(static entry => entry.From is not null)
                .Select(static entry => (entry.From!, entry.Paint))
                .Order()
        );

        // One step, and nothing of it left behind.
        editor.Undo();
        Assert.Equal(folder, editor.Set.Layers[^1].Id);
        Assert.DoesNotContain(editor.Standing, static entry => entry.From is not null);
    }

    /// <summary>
    ///     A layer removed and saved leaves its pixels under its id, and a new layer can be given that id. Its first
    ///     stroke must start a canvas of its own rather than open the old one.
    /// </summary>
    [Fact]
    public void A_first_stroke_never_opens_a_canvas_another_layer_left() {
        var folder = Directory.CreateTempSubdirectory("patina-canvas-");
        try {
            var left = LayerPaint.NameFor("Crate", "Body", "paint");
            File.WriteAllText(Path.Combine(folder.FullName, left), "pixels a removed layer kept");
            var editor = new StackEditor(Path.Combine(folder.FullName, "Crate.vxlayers"), Sample());

            editor.Apply(new([new StackStroke("a", "paint", "baseColor", new(4f), White, [new(1f, 1f)])]));

            Assert.NotEqual(left, editor.Set.Layers[^1].Paint);
        } finally {
            folder.Delete(true);
        }
    }

    /// <summary>A copy is held by the Server that made it, and a paste naming one that is gone is refused.</summary>
    [Fact]
    public void A_paste_of_a_copy_nothing_holds_is_refused() {
        var editor = new StackEditor("/tmp/Paste.vxlayers", Sample());

        Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([new StackPasteLayers("gone")])));
    }

    /// <summary>
    ///     A paste into another stack is new layers, with the copy's canvases and none of its mesh limits.
    /// </summary>
    [Fact]
    public void A_paste_into_another_stack_reads_the_copy_and_leaves_its_meshes() {
        var source = new StackEditor("/tmp/Source.vxlayers", Sample());
        source.Apply(new([new StackStroke("a", "paint", "baseColor", new(4f), White, [new(1f, 1f)])]));
        source.Apply(new([new StackSetLayer("paint", MeshMask: new(["Lid"], "lid.png"))]));
        var (text, count, canvases) = source.Copied(["paint"]);
        Assert.Equal(1, count);
        Assert.Equal([source.Set.Layers[^1].Paint], canvases);

        var clips = Path.Combine(Path.GetTempPath(), "clips", "one");
        var target = new StackEditor("/tmp/Target.vxlayers", Sample()) {
            Clip = id => id == "clip" ? new("clip", text, clips, "/tmp/Source.vxlayers", 64, 64) : null
        };
        target.Apply(new([new StackPasteLayers("clip", "base")]));

        var pasted = target.Set.Layers[1];
        var copied = target.Standing.Single(static entry => entry.From is not null);
        Assert.Equal("paint-2", pasted.Id);
        Assert.Null(StackDocuments.Layers([pasted])[0].Mask.MeshParts);
        Assert.Equal(Path.Combine(clips, canvases[0]), copied.From);
        Assert.Equal(pasted.Paint, copied.Paint);
    }

    /// <summary>
    ///     A copy pastes into a layer that is there (#106): its mask, with a canvas of its own and the layer's own
    ///     limit to some meshes kept, or its content, with the layer's name, mask and blend kept. Each is one undo
    ///     step.
    /// </summary>
    [Fact]
    public void A_copy_pastes_into_a_layer_as_its_mask_or_its_content() {
        var source = new StackEditor("/tmp/Source.vxlayers", Sample());
        source.Apply(new([new StackAddMaskLayer("paint", "Paint")]));
        source.Apply(
            new([new StackStroke("m", "paint", "baseColor", new(4f), White, [new(1f, 1f)], Mask: true, MaskLayer: 0)])
        );
        source.Apply(new([new StackStroke("s", "paint", "baseColor", new(4f), White, [new(2f, 2f)])]));
        var (text, _, _) = source.Copied(["paint"]);
        var clips = Path.Combine(Path.GetTempPath(), "clips", "into");
        var target = new StackEditor("/tmp/Target.vxlayers", Sample()) {
            Clip = id => id == "clip" ? new("clip", text, clips, "/tmp/Source.vxlayers", 64, 64) : null
        };
        target.Apply(new([new StackSetLayer("base", MeshMask: new(["Lid"], "lid.png"), Blend: "Multiply")]));

        target.Apply(new([new StackPasteLayers("clip", Into: "base", Part: "Mask")]));
        var masked = target.Set.Layers.Single(static layer => layer.Id == "base");
        Assert.Equal(LayerKind.Fill, masked.Kind);
        Assert.Equal(LayerMaskSource.Constant, masked.Mask.Source);
        var entry = masked.Mask.Layers[0];
        Assert.Equal(LayerMaskSource.Paint, entry.Source);
        var painted = source.Set.Layers.Single(static layer => layer.Id == "paint");
        Assert.NotEqual(painted.Mask.Layers[0].Paint, entry.Paint);
        Assert.True(StackDocuments.KeepsToMeshes(masked.Mask.Layers[^1]));
        Assert.Equal(entry.Paint, Assert.Single(target.Standing, static stroke => stroke.From is not null).Paint);
        Assert.Equal("Paste mask", target.History.UndoLabel);

        target.Apply(new([new StackPasteLayers("clip", Into: "base", Part: "Content")]));
        var filled = target.Set.Layers.Single(static layer => layer.Id == "base");
        Assert.Equal(LayerKind.Paint, filled.Kind);
        Assert.Equal(("Base", "Multiply"), (filled.Name, filled.Blend.ToString()));
        Assert.Equal(masked.Mask, filled.Mask);
        Assert.NotEqual(painted.Paint, filled.Paint);
        Assert.Equal("Paste content", target.History.UndoLabel);

        target.Undo();
        target.Undo();
        Assert.Equal(LayerMaskSource.None, target.Set.Layers.Single(static layer => layer.Id == "base").Mask.Source);
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
