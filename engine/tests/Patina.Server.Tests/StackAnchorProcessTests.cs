using Patina.Contracts;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>Named anchor points with a real Worker: what a client is told, and what a mask reads.</summary>
public sealed partial class StackProcessTests {
    /// <summary>The list of anchor points follows every edit, and a mask reading one names its channel.</summary>
    [Fact]
    public async Task An_anchor_point_is_listed_with_who_reads_it_after_each_edit() {
        using var deadline = Deadline(180);
        var (instance, state) = await StartAsync("Anchors.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);

        await EditAsync(state.Id, deadline.Token, new StackSetLayer("base", AnchorPoint: "Ground"));
        Assert.Equal("Ground", Assert.Single((await StateAsync(state.Id, deadline.Token)).Anchors!).Name);

        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", Mask: new("Constant", 1f)));
        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", Mask: new("Anchor", 1f)));
        Assert.Equal("Ground", Assert.Single((await StateAsync(state.Id, deadline.Token)).Anchors!).Name);

        var read = await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer("paint", Mask: new("Anchor", 1f, Anchor: new("Ground", "roughness")))
        );
        var anchor = Assert.Single((await StateAsync(state.Id, deadline.Token)).Anchors!);
        Assert.Equal([new StackAnchorUse("paint", "mask", "roughness")], anchor.Uses);
        var evaluation = await EvaluatedAsync(socket, read.Revision, deadline.Token);
        Assert.DoesNotContain(evaluation.Problems, static problem => problem.Severity == "Error");
    }

    /// <summary>
    ///     A filter's image input and a mask generator's read an anchor point through the API alone (#111). The base is
    ///     a tint published as Ground, and black covers it. A filter inverting Ground is the tint inverted, not the
    ///     black beneath inverted; a white fill masked by a generator fed Ground is masked by the tint's red, as a
    ///     constant mask of that red is. Each is held to the picture the same thing makes another way, and to differ
    ///     from the picture of reading what is beneath.
    /// </summary>
    [Fact]
    public async Task A_filter_and_a_mask_generator_read_an_anchor_point_through_the_api() {
        using var deadline = Deadline(300);
        var (_, state) = await StartAsync("Fed.vxlayers", deadline.Token);
        var invert = await KindAsync(state.Id, "Colour/Invert", deadline.Token);
        var levels = await KindAsync(state.Id, "Colour/Levels", deadline.Token);
        var subject = Assert.Single(invert.Inputs!, static input => input.Subject).Port;
        var fed = Assert.Single(levels.Inputs!, static input => input.Subject).Port;

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer("base", Values: Tint, AnchorPoint: "Ground"),
            new StackSetLayer("paint", Enabled: false),
            new StackAddLayer("fill", "Black", new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = [0f, 0f, 0f, 1f] }),
            new StackAddLayer("filter", "Flip", FilterNode: "Colour/Invert")
        );
        var layers = (await StateAsync(state.Id, deadline.Token)).Layers;
        var (black, flip) = (layers[^2].Id, layers[^1].Id);

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(flip, Inputs: new Dictionary<string, StackInput> { [subject] = new("Anchor", Anchor: new("Ground", "baseColor")) })
        );
        var anchored = await TexelsAsync(state.Id, "anchored", deadline.Token);
        Assert.Contains(
            Assert.Single((await StateAsync(state.Id, deadline.Token)).Anchors!).Uses,
            use => use.Layer == flip && use.Channel == "baseColor"
        );

        await EditAsync(state.Id, deadline.Token, new StackSetLayer(flip, Inputs: new Dictionary<string, StackInput>()));
        var beneath = await TexelsAsync(state.Id, "beneath", deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(black, Enabled: false));
        var tintInverted = await TexelsAsync(state.Id, "tint-inverted", deadline.Token);

        Near(Texel(tintInverted, 500, 500), Texel(anchored, 500, 500));
        Assert.NotEqual(Texel(beneath, 500, 500), Texel(anchored, 500, 500));

        // A white fill over the black, masked by Levels at its defaults fed Ground in base colour, whose red is 0.8.
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(black, Enabled: true),
            new StackSetLayer(flip, Enabled: false),
            new StackAddLayer("fill", "White", new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = [1f, 1f, 1f, 1f] })
        );
        var white = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(
                white,
                Mask: new(
                    "Generator",
                    Generator: "Colour/Levels",
                    Inputs: new Dictionary<string, StackAnchorRef> { [fed] = new("Ground", "baseColor") }
                )
            )
        );
        var generated = await TexelsAsync(state.Id, "generated", deadline.Token);
        Assert.Contains(
            Assert.Single((await StateAsync(state.Id, deadline.Token)).Anchors!).Uses,
            use => use.Layer == white && use.Channel == "baseColor"
        );

        await EditAsync(state.Id, deadline.Token, new StackSetLayer(white, Mask: new("Constant", 0.8f)));
        var constant = await TexelsAsync(state.Id, "constant", deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(white, Mask: new("Constant", 0f)));
        var hidden = await TexelsAsync(state.Id, "hidden", deadline.Token);

        Near(Texel(constant, 500, 500), Texel(generated, 500, 500));
        Assert.NotEqual(Texel(hidden, 500, 500), Texel(generated, 500, 500));
    }

    /// <summary>What a node takes is answered for any node a stack can run, and a name it cannot run is refused by code.</summary>
    [Fact]
    public async Task A_stack_says_what_any_node_takes() {
        using var deadline = Deadline(120);
        var (_, state) = await StartAsync("Kinds.vxlayers", deadline.Token);

        var warp = await KindAsync(state.Id, "Filters/Warp", deadline.Token);
        Assert.True(warp.Inputs!.Count >= 2);
        Assert.Single(warp.Inputs!, static input => input.Subject);

        using var response = await http.GetAsync(
            $"/api/v1/stacks/{state.Id}/kind?node={Uri.EscapeDataString("Nowhere/Nothing")}",
            deadline.Token
        );
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("node-unknown", await response.Content.ReadAsStringAsync(deadline.Token), StringComparison.Ordinal);
    }

    async Task<StackFilterKind> KindAsync(string id, string node, CancellationToken token) {
        using var response = await http.GetAsync(
            $"/api/v1/stacks/{id}/kind?node={Uri.EscapeDataString(node)}",
            token
        );
        await SucceededAsync(response, token);
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackFilterKind, token))!;
    }
}
