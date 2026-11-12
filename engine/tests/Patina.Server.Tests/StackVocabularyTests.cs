using Patina.Contracts;
using Patina.Server.Stacks;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

/// <summary>The words a client is sent, each with what a person calls it, and what a channel starts as.</summary>
public sealed class StackVocabularyTests {
    /// <summary>What leaves a material alone, which for most channels is not black.</summary>
    [Theory]
    [InlineData("occlusion", 1f)]
    [InlineData("opacity", 1f)]
    [InlineData("mask", 1f)]
    [InlineData("height", 0.5f)]
    [InlineData("emissive", 0f)]
    public void A_new_channel_starts_as_what_leaves_the_material_alone(string usage, float red) {
        Assert.Equal(red, StackEditor.DefaultOf(usage)[0]);
    }

    public static TheoryData<string> Usages => [..StackDocuments.Vocabulary.Usages.Select(static term => term.Value)];

    /// <summary>Every word of every list a client shows.</summary>
    public static TheoryData<string, string> Words {
        get {
            var vocabulary = StackDocuments.Vocabulary;
            TheoryData<string, string> words = [];
            foreach (var term in new[] {
                         vocabulary.Blends, vocabulary.Masks, vocabulary.Fills, vocabulary.Projections,
                         vocabulary.Generators, vocabulary.MeshMaps, vocabulary.Alphas, vocabulary.Usages
                     }.SelectMany(static list => list)) {
                words.Add(term.Value, term.Label);
            }

            return words;
        }
    }

    /// <summary>A word with no arm is refused by name, so one the engine adds cannot quietly start black.</summary>
    [Theory]
    [MemberData(nameof(Usages))]
    public void Every_usage_has_a_default(string usage) {
        Assert.Equal(4, StackEditor.DefaultOf(usage).Length);
    }

    /// <summary>A channel that exists adds as that channel and not as a word nothing reads.</summary>
    [Theory]
    [MemberData(nameof(Usages))]
    public void Every_usage_adds_a_channel(string usage) {
        var editor = new StackEditor(
            "/tmp/Words.vxlayers",
            new() { Name = "Words", Sets = [new() { Name = "Body", Channels = [new() { Usage = "none" }] }] }
        );

        editor.Apply(new([new StackAddChannel(usage)]));

        Assert.Equal(StackEditor.DefaultOf(usage), editor.Set.Channels[^1].Default);
    }

    /// <summary>
    ///     A label is words: capitalised, with no camel case, no path and no enum spelling, so nothing raw reaches a
    ///     person (#198).
    /// </summary>
    [Theory]
    [MemberData(nameof(Words))]
    public void Every_word_has_a_label_a_person_reads(string value, string label) {
        Assert.False(string.IsNullOrWhiteSpace(label), value);
        Assert.Matches("^[A-Z0-9]", label);
        Assert.DoesNotMatch("[a-z][A-Z]", label);
        Assert.DoesNotContain('/', label);
    }

    /// <summary>The words a painting tool uses, where the engine's would be wrong or raw.</summary>
    [Theory]
    [InlineData("Copy", "Normal")]
    [InlineData("ColourDodge", "Color Dodge")]
    [InlineData("ColourBurn", "Color Burn")]
    [InlineData("SignedAdd", "Signed Add")]
    [InlineData("HardLight", "Hard Light")]
    [InlineData("Colour", "Color")]
    [InlineData("Hue", "Hue (Tint)")]
    [InlineData("NormalMapInverseDetail", "Normal Map Inverse Detail")]
    [InlineData("VividLight", "Vivid Light")]
    public void A_blend_reads_as_a_painting_tool_names_it(string blend, string label) {
        Assert.Equal(label, StackDocuments.Vocabulary.Blends.Single(term => term.Value == blend).Label);
    }

    [Theory]
    [InlineData("ao", "Ambient Occlusion")]
    [InlineData("bent", "Bent Normal")]
    [InlineData("world", "World Normal")]
    [InlineData("id", "ID")]
    public void A_mesh_map_reads_as_what_it_is(string map, string label) {
        Assert.Equal(label, StackDocuments.Vocabulary.MeshMaps.Single(term => term.Value == map).Label);
    }

    [Fact]
    public void A_generator_reads_as_its_name_and_not_its_path() {
        Assert.Contains(
            StackDocuments.Vocabulary.Generators,
            static term => term.Value == "Generators/Dirt" && term.Label == "Dirt"
        );
    }

    [Fact]
    public void Every_list_holds_every_word_of_its_enumeration_once() {
        var vocabulary = StackDocuments.Vocabulary;
        Assert.Equal(Enum.GetNames<LayerBlendMode>(), vocabulary.Blends.Select(static term => term.Value));
        Assert.Equal(Enum.GetNames<LayerMaskSource>(), vocabulary.Masks.Select(static term => term.Value));
        Assert.Equal(Enum.GetNames<LayerFillSource>(), vocabulary.Fills.Select(static term => term.Value));
        Assert.Equal(Enum.GetNames<LayerProjection>(), vocabulary.Projections.Select(static term => term.Value));
        Assert.Equal(Enum.GetNames<LayerTag>(), vocabulary.Tags!.Select(static term => term.Value));
    }

    /// <summary>
    ///     An export's padding modes and filters go out with labels too, every one of them, in their order.
    /// </summary>
    [Fact]
    public void An_exports_paddings_and_filters_say_what_each_is_called() {
        var paddings = Terms.InExport(Patina.Export.Presets.ExportPadding.Names);
        var filters = Terms.InExport(Patina.Export.Presets.ExportFilters.All);

        Assert.Equal(Patina.Export.Presets.ExportPadding.Names, paddings.Select(static term => term.Value));
        Assert.Equal("Dilation + transparent", paddings.Single(static term => term.Value == "transparent").Label);
        Assert.Equal(["Box", "Bilinear", "Nearest"], filters.Select(static term => term.Label));
    }

    /// <summary>A setting's choices carry labels too, in their order, and a free one carries none.</summary>
    [Fact]
    public void A_setting_with_choices_says_what_each_is_called() {
        var usage = Graphs.GraphDocuments.Definition(
            new Vixen.NodeGraph.SettingDefinition("Usage", "baseColor", Accepted: ["baseColor", "metalness", "ao"])
        );
        Assert.Equal(["Base Color", "Metallic", "Ambient Occlusion"], usage.Labels);
        Assert.Null(
            Graphs.GraphDocuments.Definition(new Vixen.NodeGraph.SettingDefinition("Name", string.Empty)).Labels
        );
    }

    /// <summary>
    ///     What Vixen spells the British way a person reads as the product spells it: a node filed under Colour is
    ///     a Color one, and a mode named Colour or Grey reads Color or Gray, while the values stay as files hold them
    ///     (#198, docs/02-glossary.md).
    /// </summary>
    [Fact]
    public void Vixens_colour_reads_as_color_and_its_grey_as_gray() {
        var levels = StackDocuments.Vocabulary.Filters.Single(static held => held.Node == "Colour/Levels");
        Assert.Equal("Color", levels.Category);
        Assert.DoesNotContain(
            StackDocuments.Vocabulary.Filters.Concat(StackDocuments.Vocabulary.Sources),
            static kind => kind.Category.Contains("Colour", StringComparison.Ordinal)
                || kind.Name.Contains("Colour", StringComparison.Ordinal)
        );
        Assert.Equal(["Color", "Gray", "Luminance"], Terms.LabelsOf(["Colour", "Grey", "Luminance"]));
        Assert.Equal("Colored Grayscale", Terms.Spelled("Coloured Greyscale"));
        Assert.Equal("Colourful", Terms.Spelled("Colourful"));
    }

    /// <summary>A number a client shows until it is set has to be the one the filter runs with.</summary>
    [Theory]
    [InlineData("Filters/Blur", "Radius", "Float", 8f)]
    [InlineData("Colour/HSL", "Saturation", "Float", 1f)]
    [InlineData("Colour/Levels", "Input White", "Float", 1f)]
    [InlineData("Colour/Levels", "Gamma", "Float", 1f)]
    [InlineData("Colour/Levels", "Output White", "Float", 1f)]
    [InlineData("Colour/Invert", "Red", "Bool", 1f)]
    [InlineData("Colour/Invert", "Alpha", "Bool", 0f)]
    public void A_filter_says_what_each_number_is_and_starts_as(string filter, string name, string kind, float start) {
        var port = StackDocuments.Vocabulary.Filters.Single(held => held.Node == filter)
            .Ports.Single(held => held.Name == name);

        Assert.Equal(kind, port.Kind);
        Assert.Equal(start, port.Default[0]);
    }

    /// <summary>Only the numbers: the image a filter reads is a wire, and must never be offered as one.</summary>
    [Fact]
    public void A_filter_offers_no_port_but_its_numbers() {
        Assert.All(
            StackDocuments.Vocabulary.Filters.SelectMany(static filter => filter.Ports),
            static port => {
                Assert.Equal("Input", port.Direction);
                Assert.NotEqual("Image", port.Kind);
            }
        );
    }

    /// <summary>
    ///     The list comes from the registry, so a node with one image in and out cannot be missing from it.
    /// </summary>
    [Fact]
    public void Every_adjustment_the_compile_takes_is_offered() {
        var library = LayerStackCompiler.Library(out _);
        string[] adjustments = [..library.Types.Where(LayerStackGraph.IsAdjustment).Select(static type => type.Path)];

        Assert.Equal(
            adjustments.Order(StringComparer.Ordinal),
            StackDocuments.Vocabulary.Filters.Select(static f => f.Node)
        );
        Assert.Contains("Space/Mirror", adjustments);
        Assert.Contains("Colour/Gradient Map", adjustments);
        Assert.Contains("Surface/Bevel", adjustments);
        Assert.DoesNotContain("Colour/Blend", adjustments);
    }

    /// <summary>A person picks by name, and only the numbers a form can show may be asked for.</summary>
    [Fact]
    public void Every_filter_can_be_told_apart_and_filled_in() {
        var filters = StackDocuments.Vocabulary.Filters;

        Assert.Equal(
            filters.Count,
            filters.Select(static filter => filter.Name).Distinct(StringComparer.Ordinal).Count()
        );
        Assert.All(
            filters.SelectMany(static filter => filter.Ports),
            static port => Assert.Contains(port.Kind, new[] { "Float", "Int", "Bool", "Float4" })
        );
    }

    /// <summary>
    ///     A mask's colour selection has its own fields, and the same node in the list would be taken for it.
    /// </summary>
    [Fact]
    public void A_mask_runs_every_filter_but_its_own_colour_selection() {
        var vocabulary = StackDocuments.Vocabulary;

        Assert.DoesNotContain(vocabulary.MaskEffects, static effect => effect.Node == "Analysis/Colour Select");
        var library = LayerStackCompiler.Library(out _);
        Assert.Equal(
            library.Types.Where(LayerStackGraph.IsMaskStep)
                .Select(static type => type.Path)
                .Where(static path => path != "Analysis/Colour Select")
                .Order(StringComparer.Ordinal),
            vocabulary.MaskEffects.Select(static effect => effect.Node)
        );
    }

    /// <summary>
    ///     Patina #220: a node whose output measures its input gives a layer or a mask nothing of the surface back,
    ///     so no statistics node is offered as either, and the equaliser, whose output is a picture, still is. Nor is a
    ///     node whose one image is a mesh map (#179), a composite of two normal maps, or one with two pictures out
    ///     (#176); and wave 5's one-picture nodes are both, Compare among them (#110), and Distance Colour, whose first
    ///     image is the colour it pads out from a mask an input names. Wave 8's placement helpers, Instance Range and
    ///     Atlas Pack, read a placement's numbers or pack pictures and say so in their files; its distance compounds
    ///     and its two filter atoms are offered.
    /// </summary>
    [Fact]
    public void No_statistics_node_is_offered_as_a_filter_or_a_mask_step() {
        var vocabulary = StackDocuments.Vocabulary;
        string[] refused = [
            "Analysis/Histogram", "Analysis/Histogram Render", "Analysis/Summed Area Table", "Source/Volume Noise",
            "Source/Volume Mask", "Source/Volume Gradient", "Surface/Normal Blend", "Utility/Frequency Split",
            "Utility/Atlas Pack", "Utility/Instance Range"
        ];
        string[] offered = [
            "Colour/Histogram Equalise", "Analysis/Distance", "Analysis/Flood Fill", "Surface/Height Shadow",
            "Surface/Bevel Height", "Analysis/Compare", "Analysis/Distance Colour", "Analysis/Directional Distance",
            "Surface/Height Extrude", "Adjust/Outline", "Utility/Fill from Edges"
        ];

        Assert.All(
            refused,
            node => {
                Assert.DoesNotContain(vocabulary.Filters, filter => filter.Node == node);
                Assert.DoesNotContain(vocabulary.MaskEffects, filter => filter.Node == node);
            }
        );
        Assert.All(
            offered,
            node => {
                Assert.Contains(vocabulary.Filters, filter => filter.Node == node);
                Assert.Contains(vocabulary.MaskEffects, filter => filter.Node == node);
            }
        );
    }

    /// <summary>
    ///     Patina #118 to #120 and #177: a weathering filter writes every channel it is named for, so it is a filter
    ///     and no mask step, and reads each channel beneath by itself; a stylised filter is both; and a generator's
    ///     knobs, its seed among them, reach a client with their labels.
    /// </summary>
    [Fact]
    public void Weathering_stylised_filters_and_generators_are_offered_as_what_they_are() {
        var vocabulary = StackDocuments.Vocabulary;
        var rust = Assert.Single(vocabulary.Filters, static filter => filter.Node == "Weathering/Rust");

        Assert.DoesNotContain(
            vocabulary.MaskEffects,
            static filter => filter.Node.StartsWith("Weathering/", StringComparison.Ordinal)
        );
        Assert.Equal(
            ["baseColor", "roughness", "metalness", "height"],
            rust.Inputs!.Select(static input => input.Channel)
        );
        Assert.True(rust.Inputs![0].Subject);
        Assert.Contains(vocabulary.MaskEffects, static filter => filter.Node == "Stylised/Comic");
        Assert.Contains(vocabulary.Generators, static term => term is { Value: "Generators/Drips", Label: "Drips" });
        Assert.Contains(
            vocabulary.GeneratorParameters["Generators/Metal Edge Wear"],
            static knob => knob is { Name: "seed", Label: "Seed" }
        );
    }

    /// <summary>
    ///     Patina #113: a filter says which images it reads, its subject first, so a client offers a slot for each; and
    ///     the generators those slots can read are listed with their own fields.
    /// </summary>
    [Fact]
    public void A_filter_says_what_it_reads_and_a_slot_offers_the_generators() {
        var vocabulary = StackDocuments.Vocabulary;
        var warp = vocabulary.Filters.Single(static filter => filter.Node == "Filters/Warp");

        Assert.Equal(
            [("Input", true, false), ("Warp", false, false)],
            warp.Inputs!.Select(static input => (input.Port, input.Subject, input.Optional))
        );
        Assert.True(
            vocabulary.Filters.Single(static filter => filter.Node == "Colour/Channel Shuffle").Inputs![1].Optional
        );
        Assert.Contains(vocabulary.Sources, static source => source.Node == "Source/Noise");
        Assert.Contains(vocabulary.Sources, static source => source.Node == "Noise/Clouds");
        Assert.Contains(vocabulary.Sources, static source => source.Node == "Source/Blue Noise");
        Assert.DoesNotContain(vocabulary.Sources, static source => source.Node == "Source/Bitmap");
        Assert.All(vocabulary.Sources, static source => Assert.Empty(source.Inputs!));
        Assert.Equal(
            ["Layers Beneath", "Anchor", "Generator", "Bitmap", "Mesh Map"],
            vocabulary.Inputs.Select(static term => term.Label)
        );
    }

    /// <summary>
    ///     Where an input reads goes into the file and comes back as it went in, a new filter starts reading nothing
    ///     the last one read, and a source a stack does not know is refused with a sentence.
    /// </summary>
    [Fact]
    public void A_layers_inputs_round_trip_and_follow_only_their_own_filter() {
        var editor = new StackEditor(
            "/tmp/Inputs.vxlayers",
            new() {
                Name = "Inputs",
                Sets = [
                    new() {
                        Name = "Body",
                        Channels = [new() { Usage = "baseColor" }, new() { Usage = "height" }],
                        Layers = [new() { Id = "warp", Name = "Warp", Kind = LayerKind.Filter }]
                    }
                ]
            }
        );
        Dictionary<string, StackInput> inputs = new() {
            ["Input"] = new("Beneath", "height"),
            ["Warp"] = new(
                "Generator",
                Generator: "Noise/Clouds",
                Values: new Dictionary<string, IReadOnlyList<float>> { ["Scale"] = [3f] },
                Parameters: new Dictionary<string, string> { ["Seed Mode"] = "x" }
            )
        };

        editor.Apply(new([new StackSetLayer("warp", FilterNode: "Filters/Warp", Inputs: inputs)]));
        var read = StackDocuments.Parse(StackDocuments.Text(editor.Document)).Sets[0].Layers[0];
        var wired = StackDocuments.Layers([read])[0].Inputs!;

        Assert.Equal("Beneath", wired["Input"].Source);
        Assert.Equal("height", wired["Input"].Channel);
        Assert.Equal("Noise/Clouds", wired["Warp"].Generator);
        Assert.Equal([3f], wired["Warp"].Values!["Scale"]);
        Assert.Equal("x", wired["Warp"].Parameters!["Seed Mode"]);

        editor.Apply(new([new StackSetLayer("warp", FilterNode: "Filters/Slope Blur")]));
        Assert.Empty(editor.Set.Layers[0].Inputs);

        Assert.Throws<StackEditRefusedException>(() => editor.Apply(
                new(
                    [new StackSetLayer("warp", Inputs: new Dictionary<string, StackInput> { ["Slope"] = new("Noise") })]
                )
            )
        );
    }

    /// <summary>
    ///     Another filter has other numbers: what was set for the last one would only be refused by name.
    /// </summary>
    [Fact]
    public void A_new_filter_starts_from_its_own_defaults() {
        var editor = new StackEditor(
            "/tmp/Filtered.vxlayers",
            new() {
                Name = "Filtered",
                Sets = [
                    new() {
                        Name = "Body",
                        Channels = [new() { Usage = "baseColor" }],
                        Layers = [new() { Id = "soft", Name = "Soft", Kind = LayerKind.Filter }]
                    }
                ]
            }
        );
        Dictionary<string, IReadOnlyList<float>> gamma = new() { ["Gamma"] = [2f] };
        editor.Apply(new([new StackSetLayer("soft", Settings: gamma)]));

        editor.Apply(new([new StackSetLayer("soft", Settings: gamma)]));
        Assert.Equal([2f], editor.Set.Layers[0].Settings["Gamma"]);

        editor.Apply(new([new StackSetLayer("soft", FilterNode: "Filters/Blur")]));
        Assert.Empty(editor.Set.Layers[0].Settings);

        editor.Undo();
        Assert.Equal([2f], editor.Set.Layers[0].Settings["Gamma"]);
    }

    /// <summary>A file may name its filter either way, and a client reads one word for it.</summary>
    [Fact]
    public void A_named_filter_reads_as_the_node_it_runs() {
        var layers = StackDocuments.Layers(
            [
                new() { Id = "a", Kind = LayerKind.Filter, Filter = LayerFilterKind.Hsl },
                new() { Id = "b", Kind = LayerKind.Filter, Filter = LayerFilterKind.Hsl, FilterNode = "Space/Mirror" },
                new() { Id = "c", Kind = LayerKind.Fill }
            ]
        );

        Assert.Equal(["Colour/HSL", "Space/Mirror", string.Empty], layers.Select(static layer => layer.FilterNode));
    }

    /// <summary>
    ///     Nothing moves on a spacing of nothing, and the Worker's fallback must not turn it into something else.
    /// </summary>
    [Fact]
    public void A_brush_with_no_spacing_is_refused() {
        var editor = new StackEditor(
            "/tmp/Spaced.vxlayers",
            new() {
                Name = "Spaced",
                Sets = [
                    new() {
                        Name = "Body",
                        Channels = [new() { Usage = "baseColor" }],
                        Layers = [new() { Id = "paint", Name = "Paint", Kind = LayerKind.Paint }]
                    }
                ]
            }
        );

        Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(
                new([new StackStroke("a", "paint", "baseColor", new(4f, Spacing: 0f), [1f, 1f, 1f, 1f], [new(1f, 1f)])])
            )
        );
    }

    /// <summary>
    ///     A refusal a client explains carries a code and the names it was written from, so the client words it, and
    ///     the API's own text can say it plainly for anyone else.
    /// </summary>
    [Fact]
    public void A_brush_on_a_fill_is_refused_with_a_code_and_the_layer() {
        var editor = new StackEditor(
            "/tmp/Filled.vxlayers",
            new() {
                Name = "Filled",
                Sets = [
                    new() {
                        Name = "Body",
                        Channels = [new() { Usage = "baseColor" }],
                        Layers = [new() { Id = "fill", Name = "Rust", Kind = LayerKind.Fill }]
                    }
                ]
            }
        );

        var refusal = Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackStroke("a", "fill", "baseColor", new(4f), [1f, 1f, 1f, 1f], [new(1f, 1f)])]))
        );

        Assert.Equal(Api.Refusals.NotPaintLayer, refusal.Code);
        Assert.Equal("Rust", refusal.Values["layer"]);
        Assert.DoesNotContain("takes a brush", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>A lazy mouse is a distance: one behind the pointer by less than nothing means nothing.</summary>
    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void A_brush_trailing_by_no_distance_is_refused(float lazy) {
        var editor = new StackEditor(
            "/tmp/Lazy.vxlayers",
            new() {
                Name = "Lazy",
                Sets = [
                    new() {
                        Name = "Body",
                        Channels = [new() { Usage = "baseColor" }],
                        Layers = [new() { Id = "paint", Name = "Paint", Kind = LayerKind.Paint }]
                    }
                ]
            }
        );

        Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(
                new([new StackStroke("a", "paint", "baseColor", new(4f, Lazy: lazy), [1f, 1f, 1f, 1f], [new(1f, 1f)])])
            )
        );
    }

    /// <summary>
    ///     A stroke carries the last one on from one place, as two numbers, and only a free one does: a line or a path
    ///     runs through points of its own.
    /// </summary>
    [Theory]
    [InlineData("free", 1)]
    [InlineData("free", 3)]
    [InlineData("line", 2)]
    public void A_stroke_carried_on_from_nowhere_or_along_a_line_is_refused(string curve, int numbers) {
        var editor = Painted("Resume");

        Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(
                new(
                    [
                        new StackStroke(
                            "a",
                            "paint",
                            "baseColor",
                            new(4f),
                            [1f, 1f, 1f, 1f],
                            [new(1f, 1f), new(9f, 1f)],
                            Curve: curve,
                            Resume: [..Enumerable.Repeat(4f, numbers)]
                        )
                    ]
                )
            )
        );
    }

    /// <summary>A face turns from a camera by a quarter turn at most, and by no less than nothing.</summary>
    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.6f)]
    [InlineData(float.NaN)]
    public void A_facing_angle_past_a_quarter_turn_is_refused(float facing) {
        Assert.NotNull(new StackBrush(4f, Facing: facing).Problem());
        Assert.Null(new StackBrush(4f, Facing: StackBrushes.AnyFacing).Problem());
        Assert.Null(new StackBrush(4f, Facing: 0f).Problem());
    }

    /// <summary>
    ///     A material brush lays its maps as a fill does, but not through a decal's box or along a path; one laid in
    ///     the model's space needs a model.
    /// </summary>
    [Theory]
    [InlineData("Decal", null)]
    [InlineData("Triplanar", Api.Refusals.NoMesh)]
    [InlineData("Planar", Api.Refusals.NoMesh)]
    public void A_material_laid_where_this_stack_cannot_lay_it_is_refused(string projection, string? code) {
        var editor = Painted("Laid");

        var refusal = Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(
                new(
                    [
                        new StackStroke(
                            "a",
                            "paint",
                            "baseColor",
                            new(4f),
                            [1f, 1f, 1f, 1f],
                            [new(1f, 1f)],
                            Source: new("material", "rust.vxtexgraph", Projection: projection)
                        )
                    ]
                )
            )
        );

        Assert.Equal(code ?? Api.Refusals.BrushRefused, refusal.Code);
    }

    static StackEditor Painted(string name) =>
        new(
            $"/tmp/{name}.vxlayers",
            new() {
                Name = name,
                Sets = [
                    new() {
                        Name = "Body",
                        Channels = [new() { Usage = "baseColor" }],
                        Layers = [new() { Id = "paint", Name = "Paint", Kind = LayerKind.Paint }]
                    }
                ]
            }
        );
}
