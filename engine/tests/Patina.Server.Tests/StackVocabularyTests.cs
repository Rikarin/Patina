using Patina.Contracts;
using Patina.Server.Stacks;
using System.Text.RegularExpressions;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

/// <summary>The words a client is sent, held to what the editor calls them and to what a channel starts as.</summary>
public sealed partial class StackVocabularyTests {
    static readonly string Labels = File.ReadAllText(
        Path.Combine(Engine.Root, "..", "editor", "apps", "studio", "src", "app", "core", "labels.ts")
    );

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

    public static TheoryData<string> Usages => [..StackDocuments.Vocabulary.Usages];

    public static TheoryData<string> Blends => [..StackDocuments.Vocabulary.Blends];

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

    [Theory]
    [MemberData(nameof(Usages))]
    public void Every_usage_has_a_label(string usage) {
        Assert.Contains(usage, KeysOf("Channels"));
    }

    [Theory]
    [MemberData(nameof(Blends))]
    public void Every_blend_has_a_label(string blend) {
        Assert.Contains(blend, KeysOf("Blends"));
    }

    /// <summary>The other way: a key no word reads is the mistake these tests are here for.</summary>
    [Fact]
    public void Every_label_is_for_a_word_that_exists() {
        Assert.Empty(KeysOf("Channels").Except(StackDocuments.Vocabulary.Usages));
        Assert.Empty(KeysOf("Blends").Except(StackDocuments.Vocabulary.Blends));
        Assert.Equal(Enum.GetNames<LayerBlendMode>().Order(), KeysOf("Blends").Order());
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

    /// <summary>The list comes from the registry, so a node with one image in and out cannot be missing from it.</summary>
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

    /// <summary>A mask's colour selection has its own fields, and the same node in the list would be taken for it.</summary>
    [Fact]
    public void A_mask_runs_every_filter_but_its_own_colour_selection() {
        var vocabulary = StackDocuments.Vocabulary;

        Assert.DoesNotContain(vocabulary.MaskEffects, static effect => effect.Node == "Analysis/Colour Select");
        Assert.Equal(vocabulary.Filters.Count - 1, vocabulary.MaskEffects.Count);
    }

    /// <summary>Another filter has other numbers: what was set for the last one would only be refused by name.</summary>
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

    /// <summary>Nothing moves on a spacing of nothing, and the Worker's fallback must not turn it into something else.</summary>
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

    static List<string> KeysOf(string table) {
        var body = Regex.Match(Labels, $@"const {table}:[^=]*= \{{(?<body>[^}}]*)\}}").Groups["body"].Value;
        return [..Key().Matches(body).Select(static match => match.Groups[1].Value)];
    }

    [GeneratedRegex(@"^\s*(\w+):", RegexOptions.Multiline)]
    private static partial Regex Key();
}
