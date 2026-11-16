using Patina.Contracts;
using Patina.Server.Graphs;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Server.Tests;

/// <summary>
///     The graph editor's authoring commands: switching nodes off, reroute dots, delete-and-relink, layout, frames,
///     describing parameters and presets. Each is checked against the document, its undo and what the Worker is told.
/// </summary>
public sealed class GraphAuthoringTests {
    static readonly NodeTypeRegistry Registry = Catalogue();

    static NodeTypeRegistry Catalogue() {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        return registry;
    }

    static GraphEditor Editor() => new(GraphTesting.Sample(), Registry);

    static GraphNodeAsset NodeOf(GraphEditor editor, int id) => editor.Document.Nodes.Single(node => node.Id == id);

    static GraphEdgeAsset? Into(GraphEditor editor, int node, string port) =>
        editor.Document.Edges.SingleOrDefault(edge => edge.ToNode == node && edge.ToPort == port);

    static GraphSettingDefinition Float(string name, string value = "0.5") =>
        new(name, value, string.Empty, "Float", 0f, 1f, string.Empty, []);

    /// <summary>A graph setting's undo step says what a person calls it, never its key (#198).</summary>
    [Fact]
    public void A_settings_undo_step_names_it_in_words() {
        var editor = Editor();

        editor.Apply(new([new GraphSetSetting("bitDepth", "32")]));

        Assert.Equal("Set Bit Depth", editor.History.UndoLabel);
    }

    [Fact]
    public void Switching_a_node_off_is_one_undo_and_the_worker_is_told_the_whole_document() {
        var editor = Editor();
        var before = editor.Document;

        editor.Apply(new([new GraphSetBypass([2], true)]));

        Assert.True(NodeOf(editor, 2).Bypassed);
        Assert.Equal("Bypass", editor.History.UndoLabel);
        // Not a number: the compiler walks a different graph, so the Worker needs the document.
        Assert.Null(GraphDocuments.Values(before, editor.Document));
        Assert.Contains(
            GraphDocuments.Delta(before, editor.Document).Nodes,
            static node => node is { Id: 2, Bypassed: true }
        );

        editor.Undo();

        Assert.False(NodeOf(editor, 2).Bypassed);
    }

    /// <summary>A node's size is an edit that undoes, and the Worker compiles it rather than refreshing.</summary>
    [Fact]
    public void Sizing_a_node_is_one_undo_and_the_worker_is_told_the_whole_document() {
        var editor = Editor();
        var before = editor.Document;

        editor.Apply(new([new GraphSetLevel([2], 1)]));

        Assert.Equal(1, NodeOf(editor, 2).Level);
        Assert.Equal("Set Size", editor.History.UndoLabel);
        Assert.Contains("level: 1", GraphDocuments.Text(editor.Document), StringComparison.Ordinal);
        Assert.Null(GraphDocuments.Values(before, editor.Document));
        Assert.Contains(
            GraphDocuments.Delta(before, editor.Document).Nodes,
            static node => node is { Id: 2, Level: 1 }
        );

        editor.Apply(new([new GraphSetLevel([2], null)]));
        Assert.Null(NodeOf(editor, 2).Level);

        editor.Undo();
        editor.Undo();

        Assert.Null(NodeOf(editor, 2).Level);
        Assert.Equal(GraphDocuments.Text(before), GraphDocuments.Text(editor.Document));
    }

    /// <summary>
    ///     A saved graph leaves out what a node holds by default (#298): no size for a node that follows its inputs, no
    ///     bypass for one that is on, and it reads back as the same document.
    /// </summary>
    [Fact]
    public void A_saved_graph_writes_no_default_and_reads_back_the_same() {
        var editor = Editor();
        var plain = GraphDocuments.Text(editor.Document);

        Assert.DoesNotContain("level:", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("bypassed:", plain, StringComparison.Ordinal);
        Assert.Equal(plain, GraphDocuments.Text(GraphDocuments.Parse(plain)));

        editor.Apply(new([new GraphSetLevel([2], 0), new GraphSetBypass([2], true)]));
        var set = GraphDocuments.Text(editor.Document);

        Assert.Contains("level: 0", set, StringComparison.Ordinal);
        Assert.Contains("bypassed: true", set, StringComparison.Ordinal);
        Assert.Equal(set, GraphDocuments.Text(GraphDocuments.Parse(set)));
        Assert.Equal(0, GraphDocuments.Parse(set).Nodes.Single(static node => node.Id == 2).Level);
    }

    /// <summary>A size past what a node may be, or on a node that has none of its own, is refused.</summary>
    [Fact]
    public void A_size_out_of_range_or_on_an_output_or_a_reroute_is_refused() {
        var editor = Editor();
        var created = editor.Apply(new([new GraphAddNode(Reroutes.Type, 0, 0, -1)]));
        var output = editor.Document.Nodes.First(static node => node.Type.StartsWith(
                "Output/",
                StringComparison.Ordinal
            )
        );

        Assert.Throws<GraphEditRefusedException>(() => editor.Apply(new([new GraphSetLevel([2], 5)])));
        Assert.Throws<GraphEditRefusedException>(() => editor.Apply(new([new GraphSetLevel([2], -3)])));
        Assert.Throws<GraphEditRefusedException>(() => editor.Apply(new([new GraphSetLevel([created[0].Node], 1)])));
        Assert.Throws<GraphEditRefusedException>(() => editor.Apply(new([new GraphSetLevel([output.Id], 1)])));
        Assert.Throws<GraphEditRefusedException>(() => editor.Apply(new([new GraphSetLevel([], 1)])));
        Assert.Null(NodeOf(editor, 2).Level);
    }

    [Fact]
    public void A_boundary_node_or_a_reroute_has_nothing_to_switch_off() {
        var editor = Editor();
        var created = editor.Apply(new([new GraphAddNode(Reroutes.Type, 0, 0, -1)]));

        Assert.Throws<GraphEditRefusedException>(() => editor.Apply(new([new GraphSetBypass([created[0].Node], true)]))
        );
        Assert.Throws<GraphEditRefusedException>(() => editor.Apply(new([new GraphSetBypass([], true)])));
    }

    [Fact]
    public void Delete_and_relink_wires_what_fed_the_node_to_what_it_fed_and_undoes_as_one() {
        var editor = Editor();
        var original = GraphDocuments.Text(editor.Document);

        editor.Apply(new([new GraphRemoveNodes([2], true)]));

        Assert.DoesNotContain(editor.Document.Nodes, static node => node.Id == 2);
        var bridge = Into(editor, 3, "Input");
        Assert.NotNull(bridge);
        Assert.Equal(1, bridge.FromNode);
        Assert.Equal("Delete and Relink", editor.History.UndoLabel);

        editor.Undo();

        Assert.Equal(original, GraphDocuments.Text(editor.Document));
    }

    [Fact]
    public void A_plain_delete_leaves_the_chain_cut() {
        var editor = Editor();

        editor.Apply(new([new GraphRemoveNodes([2])]));

        Assert.Null(Into(editor, 3, "Input"));
    }

    [Fact]
    public void A_reroute_on_a_wire_carries_its_kind_and_refuses_one_it_cannot_carry() {
        var editor = Editor();

        // Noise's picture through a dot into the Invert, in one batch: the dot takes the kind of what reaches it first.
        editor.Apply(
            new(
                [
                    new GraphAddNode(Reroutes.Type, 450, 40, -1),
                    new GraphConnect(2, "Out", -1, Reroutes.In),
                    new GraphConnect(-1, Reroutes.Out, 3, "Input")
                ]
            )
        );

        var dot = editor.Document.Nodes.Single(static node => node.Type == Reroutes.Type).Id;
        Assert.Equal(dot, Into(editor, 3, "Input")?.FromNode);

        // An image dot cannot feed a number.
        Assert.Throws<GraphEditRefusedException>(() =>
            editor.Apply(new([new GraphConnect(dot, Reroutes.Out, 5, "Radius")]))
        );
    }

    [Fact]
    public void Arranging_lays_columns_left_to_right_and_a_block_keeps_its_corner() {
        var editor = Editor();

        editor.Apply(new([new GraphArrange([])]));

        float X(int id) => NodeOf(editor, id).X;
        Assert.True(X(1) < X(2) && X(2) < X(3) && X(3) < X(4));
        Assert.Equal("Auto-Layout", editor.History.UndoLabel);

        var before = (NodeOf(editor, 1).X, NodeOf(editor, 1).Y);
        editor.Apply(new([new GraphMoveNodes([new(2, 5000, 5000), new(3, 5100, 5300)])]));
        editor.Apply(new([new GraphArrange([2, 3])]));

        Assert.Equal(before, (NodeOf(editor, 1).X, NodeOf(editor, 1).Y));
        Assert.Equal(5000, Math.Min(X(2), X(3)));
        Assert.True(X(2) < X(3));
    }

    [Fact]
    public void A_frame_takes_a_colour_and_a_description_and_keeps_what_it_is_not_given() {
        var editor = Editor();
        var group = editor.Document.Groups[0];

        editor.Apply(new([new GraphSetGroup(group.Id, Color: " #1BAF7A ", Description: "The ground")]));
        editor.Apply(new([new GraphSetGroup(group.Id, "Base coat")]));

        var after = editor.Document.Groups[0];
        Assert.Equal(("Base coat", "#1baf7a", "The ground"), (after.Title, after.Color, after.Description));
        Assert.Equal(group.Nodes, after.Nodes);
    }

    /// <summary>
    ///     A frame's colour is a plain <c>#rrggbb</c> that any client can draw, never a theme's token, a name or a CSS
    ///     function only a browser understands, so each of those is refused and the frame keeps its colour (#225).
    /// </summary>
    [Theory]
    [InlineData("var(--color-chart-3)")]
    [InlineData("red")]
    [InlineData("rgb(1, 2, 3)")]
    [InlineData("#abc")]
    [InlineData("#1baf7a80")]
    [InlineData("#1baf7g")]
    public void A_frame_refuses_a_colour_that_is_not_plain(string colour) {
        var editor = Editor();
        var group = editor.Document.Groups[0];
        editor.Apply(new([new GraphSetGroup(group.Id, Color: "#2a78d6")]));

        var refused = Assert.Throws<GraphEditRefusedException>(() =>
            editor.Apply(new([new GraphSetGroup(group.Id, Color: colour)]))
        );

        Assert.Contains("#rrggbb", refused.Message, StringComparison.Ordinal);
        Assert.Equal("#2a78d6", editor.Document.Groups[0].Color);
        editor.Apply(new([new GraphSetGroup(group.Id, Color: string.Empty)]));
        Assert.Equal(string.Empty, editor.Document.Groups[0].Color);
    }

    /// <summary>A file written before colours were plain shows its frame in the client's own choice.</summary>
    [Fact]
    public void A_frame_colour_no_client_could_draw_reaches_clients_as_none() {
        Assert.Equal(string.Empty, GraphDocuments.State(new GraphGroupAsset { Color = "var(--color-chart-3)" }).Color);
        Assert.Equal("#1baf7a", GraphDocuments.State(new GraphGroupAsset { Color = "#1BAF7A" }).Color);
    }

    [Fact]
    public void A_parameter_is_described_apart_from_its_name() {
        var editor = Editor();

        editor.Apply(
            new(
                [
                    new GraphAddParameter(
                        new("Worn", "true", string.Empty, "Bool", null, null, string.Empty, [], Widget: "Toggle")
                    )
                ]
            )
        );
        editor.Apply(
            new(
                [
                    new GraphSetParameter(
                        "Wear",
                        Float("Wear") with {
                            Label = "Wear amount",
                            Group = "Rust",
                            Summary = "How far the rust has got.",
                            Widget = "Slider",
                            VisibleIf = new("Worn", "true")
                        }
                    )
                ]
            )
        );

        var wear = GraphDocuments.Definition(editor.Document.Parameters[0]);
        Assert.Equal(("Wear", "Wear amount", "Rust", "Slider"), (wear.Name, wear.Label, wear.Group, wear.Widget));
        Assert.Equal(new("Worn", "true"), wear.VisibleIf);
    }

    [Theory]
    [InlineData("Dial")]
    [InlineData("Toggle")]
    public void A_widget_that_cannot_draw_the_parameter_is_refused(string widget) {
        var editor = Editor();

        Assert.Throws<GraphEditRefusedException>(() =>
            editor.Apply(new([new GraphSetParameter("Wear", Float("Wear") with { Widget = widget })]))
        );
    }

    [Fact]
    public void A_slider_needs_a_range() {
        var editor = Editor();

        Assert.Throws<GraphEditRefusedException>(() =>
            editor.Apply(new([new GraphSetParameter("Wear", Float("Wear") with { Maximum = null, Widget = "Slider" })]))
        );
    }

    [Fact]
    public void A_condition_names_another_parameter_and_follows_its_rename_or_goes_with_it() {
        var editor = Editor();

        Assert.Throws<GraphEditRefusedException>(() =>
            editor.Apply(new([new GraphSetParameter("Wear", Float("Wear") with { VisibleIf = new("Wear", "1") })]))
        );
        Assert.Throws<GraphEditRefusedException>(() =>
            editor.Apply(new([new GraphSetParameter("Wear", Float("Wear") with { VisibleIf = new("Nothing", "1") })]))
        );

        editor.Apply(new([new GraphAddParameter(Float("Rust"))]));
        editor.Apply(new([new GraphSetParameter("Wear", Float("Wear") with { VisibleIf = new("Rust", "1", true) })]));
        editor.Apply(new([new GraphSetParameter("Rust", Float("Corrosion"))]));

        Assert.Equal("Corrosion", editor.Document.Parameters[0].VisibleIf);
        Assert.True(editor.Document.Parameters[0].VisibleNegated);

        editor.Apply(new([new GraphRemoveParameter("Corrosion")]));

        Assert.Equal(string.Empty, editor.Document.Parameters[0].VisibleIf);
    }

    [Fact]
    public void A_color_parameter_holds_a_color_and_a_preset_sets_one() {
        var editor = Editor();
        GraphSettingDefinition tint = new("Tint", "0.8,0.4,0.1,1", string.Empty, "Color", null, null, string.Empty, []);

        editor.Apply(new([new GraphAddParameter(tint)]));

        Assert.Throws<GraphEditRefusedException>(() =>
            editor.Apply(new([new GraphSetParameter("Tint", tint with { Default = "orange" })]))
        );
        Assert.Throws<GraphEditRefusedException>(() =>
            editor.Apply(new([new GraphSetParameter("Tint", tint with { Widget = "Slider" })]))
        );

        editor.Apply(new([new GraphSavePreset("Blue", new Dictionary<string, string> { ["Tint"] = "0,0,1" })]));
        editor.Apply(new([new GraphApplyPreset("Blue")]));

        var held = GraphDocuments.Definition(editor.Document.Parameters.Single(static p => p.Name == "Tint"));
        Assert.Equal(("Color", "0,0,1"), (held.Kind, held.Default));
    }

    /// <summary>
    ///     A Float2 parameter (#181): two numbers within its range, drawn as a position or a tiling and as nothing
    ///     else, and a preset sets one; a position widget does not suit a number.
    /// </summary>
    [Fact]
    public void A_float2_parameter_holds_two_numbers_in_its_range_and_takes_a_position_or_tiling() {
        var editor = Editor();
        GraphSettingDefinition offset = new(
            "Offset",
            "0.5,0.25",
            string.Empty,
            "Float2",
            0f,
            1f,
            string.Empty,
            [],
            Widget: "Position"
        );

        editor.Apply(new([new GraphAddParameter(offset)]));
        editor.Apply(new([new GraphSetParameter("Offset", offset with { Widget = "Tiling" })]));

        Refuses(editor, offset with { Default = "0.5" });
        Refuses(editor, offset with { Default = "0.5,0.5,0.5" });
        Refuses(editor, offset with { Default = "0.5,2" });
        Refuses(editor, offset with { Widget = "Angle" });

        Assert.Throws<GraphEditRefusedException>(() =>
            editor.Apply(new([new GraphSetParameter("Wear", Float("Wear") with { Widget = "Position" })]))
        );

        editor.Apply(new([new GraphSavePreset("Corner", new Dictionary<string, string> { ["Offset"] = "1,1" })]));
        editor.Apply(new([new GraphApplyPreset("Corner")]));

        var held = GraphDocuments.Definition(editor.Document.Parameters.Single(static p => p.Name == "Offset"));
        Assert.Equal(("Float2", "1,1", "Tiling"), (held.Kind, held.Default, held.Widget));
    }

    /// <summary>
    ///     An X and Y pair exposed as one Float2 (#311), as the Inspector sends it: the parameter and each port driven by
    ///     its half, as one undo step, which compiles with nothing to say.
    /// </summary>
    [Fact]
    public void An_x_and_y_pair_exposed_as_one_float2_drives_each_port_from_its_half() {
        var editor = Editor();
        GraphSettingDefinition offset = new(
            "Offset",
            "0.25,0.75",
            string.Empty,
            "Float2",
            0f,
            1f,
            string.Empty,
            [],
            Widget: "Position"
        );

        editor.Apply(
            new(
                [
                    new GraphAddParameter(offset),
                    new GraphSetText(1, "=Offset X", "Offset.x"),
                    new GraphSetText(1, "=Offset Y", "Offset.y")
                ],
                "Expose Offset"
            )
        );

        Assert.Equal("Expose Offset", editor.History.UndoLabel);
        var noise = NodeOf(editor, 1);
        Assert.Equal(("Offset.x", "Offset.y"), (noise.Texts["=Offset X"], noise.Texts["=Offset Y"]));
        var model = NodeGraphDocument.Load(editor.Document, out _);
        Assert.DoesNotContain(
            new TextureGraphCompiler(Registry).Compile(model).Diagnostics,
            static d => d.Severity == NodeSeverity.Error
        );
    }

    /// <summary>
    ///     A curve or a ramp as a parameter (#181): a setting written <c>$Shape</c> bakes the parameter's table, a
    ///     default that is not a curve written inline is refused, and an empty one is the node's own.
    /// </summary>
    [Fact]
    public void A_curve_or_a_ramp_parameter_holds_a_table_a_setting_names() {
        const string Falling = "curve:r=0 1,1 0;g=0 1,1 0;b=0 1,1 0";
        var editor = Editor();
        GraphSettingDefinition shape = new("Shape", Falling, string.Empty, "Curve", null, null, string.Empty, []);
        GraphSettingDefinition tint = shape with { Name = "Tint", Kind = "Ramp", Default = "ramp:0 000000ff,1 ff8000ff" };

        editor.Apply(
            new(
                [
                    new GraphAddParameter(shape),
                    new GraphAddParameter(tint),
                    new GraphAddNode("Colour/Curve", 0, 0, -1),
                    new GraphSetText(-1, "Curve", "$Shape"),
                    new GraphConnect(1, "Out", -1, "Input"),
                    new GraphConnect(-1, "Out", 4, "Input")
                ]
            )
        );

        Refuses(editor, shape with { Default = "Gold" });
        Refuses(editor, shape with { Default = tint.Default });
        Refuses(editor, tint with { Default = Falling });
        editor.Apply(new([new GraphSetParameter("Shape", shape with { Default = string.Empty })]));
        editor.Apply(new([new GraphSetParameter("Shape", shape)]));

        var held = GraphDocuments.Definition(editor.Document.Parameters.Single(static p => p.Name == "Shape"));
        Assert.Equal(("Curve", Falling), (held.Kind, held.Default));
        var curve = editor.Document.Nodes.Single(static n => n.Type == "Colour/Curve");
        Assert.Equal("$Shape", curve.Texts["Curve"]);
        var model = NodeGraphDocument.Load(editor.Document, out _);
        var compiler = new TextureGraphCompiler(Registry);
        compiler.Compile(model);
        Assert.Contains(
            compiler.Externals,
            external => external.Node.Value == curve.Id
                && external.Texels.SequenceEqual(TextureRamp.FromText(Falling)!)
        );
    }

    static void Refuses(GraphEditor editor, GraphSettingDefinition parameter) =>
        Assert.Throws<GraphEditRefusedException>(() =>
            editor.Apply(new([new GraphSetParameter(parameter.Name, parameter)]))
        );

    [Fact]
    public void A_preset_saves_the_defaults_applies_them_back_and_undoes() {
        var editor = Editor();
        editor.Apply(new([new GraphSavePreset("Clean")]));
        editor.Apply(new([new GraphSetParameter("Wear", Float("Wear", "0.9"))]));
        editor.Apply(new([new GraphSavePreset("Rusty")]));

        Assert.Equal(["Clean", "Rusty"], editor.Document.Presets.Select(static preset => preset.Name));
        Assert.Equal("0.9", editor.Document.Presets[1].Values["Wear"]);

        editor.Apply(new([new GraphApplyPreset("Clean")]));

        Assert.Equal("0.5", editor.Document.Parameters[0].Default);
        Assert.Equal("Preset Clean", editor.History.UndoLabel);

        editor.Undo();

        Assert.Equal("0.9", editor.Document.Parameters[0].Default);
    }

    [Fact]
    public void A_preset_replaces_one_of_its_name_skips_a_value_its_parameter_cannot_hold_and_is_removed() {
        var editor = Editor();
        editor.Apply(new([new GraphSavePreset("Odd", new Dictionary<string, string> { ["Wear"] = "7" })]));
        editor.Apply(new([new GraphSavePreset("Odd", new Dictionary<string, string> { ["Wear"] = "12" })]));

        Assert.Equal("12", Assert.Single(editor.Document.Presets).Values["Wear"]);

        // 12 is outside Wear's 0..1, so choosing the preset leaves Wear as it was rather than refusing.
        editor.Apply(new([new GraphApplyPreset("Odd")]));
        Assert.Equal("0.5", editor.Document.Parameters[0].Default);

        Assert.Throws<GraphEditRefusedException>(() =>
            editor.Apply(new([new GraphSavePreset("Bad", new Dictionary<string, string> { ["Nope"] = "1" })]))
        );
        Assert.Throws<GraphEditRefusedException>(() => editor.Apply(new([new GraphSavePreset("  ")])));

        editor.Apply(new([new GraphRemovePreset("Odd")]));
        Assert.Empty(editor.Document.Presets);
        Assert.Throws<GraphEditRefusedException>(() => editor.Apply(new([new GraphRemovePreset("Odd")])));
    }

    [Fact]
    public void Presets_and_frames_reach_the_file_and_a_preset_alone_is_no_evaluation() {
        var editor = Editor();
        var before = editor.Document;
        editor.Apply(new([new GraphSavePreset("Clean")]));

        Assert.Empty(GraphDocuments.Values(before, editor.Document)!);

        var reread = new GraphEditor(GraphDocuments.Parse(GraphDocuments.Text(editor.Document)), Registry);
        Assert.Equal("Clean", Assert.Single(reread.Document.Presets).Name);
    }
}
