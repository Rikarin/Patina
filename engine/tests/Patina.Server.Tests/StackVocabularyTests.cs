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

    static List<string> KeysOf(string table) {
        var body = Regex.Match(Labels, $@"const {table}:[^=]*= \{{(?<body>[^}}]*)\}}").Groups["body"].Value;
        return [..Key().Matches(body).Select(static match => match.Groups[1].Value)];
    }

    [GeneratedRegex(@"^\s*(\w+):", RegexOptions.Multiline)]
    private static partial Regex Key();
}
