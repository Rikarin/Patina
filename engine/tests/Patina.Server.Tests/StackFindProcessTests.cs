using Patina.Contracts;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>Click-to-find with a real Worker (#136): the layer on top at a texel, by what it covers there.</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A stroke is found where it is and the base where it is not; a fill over both takes both until a black mask
    ///     hides it; a layer in a folder is found as itself; and with every layer off nothing is.
    /// </summary>
    [Fact]
    public async Task A_click_finds_the_layer_on_top_where_it_lands() {
        using var deadline = Deadline(300);
        var (_, state) = await StartAsync("Find.vxlayers", deadline.Token);
        const float Stroke = 150f / Side;
        const float Row = 101f / Side;
        const float Bare = 0.9f;
        await PaintAsync(state.Id, "line", Line(100, 100, 30), false, deadline.Token);

        Assert.Equal("paint", await FindAsync(state.Id, Stroke, Row, deadline.Token));
        Assert.Equal("base", await FindAsync(state.Id, Bare, Bare, deadline.Token));

        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Rust", Tint));
        var rust = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        Assert.Equal(rust, await FindAsync(state.Id, Stroke, Row, deadline.Token));
        Assert.Equal(rust, await FindAsync(state.Id, Bare, Bare, deadline.Token));

        await EditAsync(state.Id, deadline.Token, new StackSetLayer(rust, Mask: new("Constant", 0f)));
        Assert.Equal("paint", await FindAsync(state.Id, Stroke, Row, deadline.Token));

        await EditAsync(state.Id, deadline.Token, new StackAddLayer("group", "Folder"));
        var folder = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(state.Id, deadline.Token, new StackNestLayer("paint", folder));
        Assert.Equal("paint", await FindAsync(state.Id, Stroke, Row, deadline.Token));

        // A folder's own mask is the child's as well: hidden by it, the child is not found there.
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(folder, Mask: new("Constant", 0f)));
        Assert.Equal("base", await FindAsync(state.Id, Stroke, Row, deadline.Token));

        await EditAsync(state.Id, deadline.Token, new StackSetLayer("base", Enabled: false));
        Assert.Null(await FindAsync(state.Id, Bare, Bare, deadline.Token));
    }

    /// <summary>
    ///     An instance is found as itself, as its source is, and still with its source switched off, since it stands
    ///     for the source whatever the source's own eye says (#108 with #136). Weighed alone without being resolved
    ///     first, it stood for nothing and the click found the source beneath it.
    /// </summary>
    [Fact]
    public async Task A_click_finds_an_instance_as_itself() {
        using var deadline = Deadline(300);
        var (_, state) = await StartAsync("FindInstance.vxlayers", deadline.Token);
        const float Bare = 0.9f;

        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Rust", Tint));
        var rust = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(state.Id, deadline.Token, new StackAddInstance(new(rust), rust));
        var copy = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        Assert.Equal(copy, await FindAsync(state.Id, Bare, Bare, deadline.Token));

        await EditAsync(state.Id, deadline.Token, new StackSetLayer(copy, Enabled: false));
        Assert.Equal(rust, await FindAsync(state.Id, Bare, Bare, deadline.Token));

        await EditAsync(state.Id, deadline.Token, new StackSetLayer(copy, Enabled: true));
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(rust, Enabled: false));
        Assert.Equal(copy, await FindAsync(state.Id, Bare, Bare, deadline.Token));
    }

    /// <summary>
    ///     A layer whose mask reads the layers beneath is found where the picture shows it: its mask step takes the
    ///     red of the grey base, so it covers half of every texel. Weighed alone, as Find weighed every layer, the step
    ///     read nothing beneath, the layer covered nothing, and the click found the base under it. A layer half set up
    ///     beneath it leaves the picture working, and Find with it.
    /// </summary>
    [Fact]
    public async Task A_click_finds_a_layer_by_what_its_mask_reads_beneath() {
        using var deadline = Deadline(300);
        var (_, state) = await StartAsync("FindBeneath.vxlayers", deadline.Token);
        const float Bare = 0.9f;

        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Rust", Tint));
        var rust = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        StackMaskEffect beneath = new(
            "Colour/Channel Shuffle",
            Texts: new Dictionary<string, string> { ["Red From"] = "SecondRed" },
            Inputs: new Dictionary<string, StackInput> { ["Second"] = new("Beneath", "baseColor") }
        );
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(rust, Mask: new("Constant", 1f, Effects: [beneath]))
        );
        Assert.Equal(rust, await FindAsync(state.Id, Bare, Bare, deadline.Token));

        // And the step reads the picture as it is: under a black base it masks everything, and the base is found.
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(
                "base",
                Values: new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = [0f, 0f, 0f, 1f] }
            )
        );
        Assert.Equal("base", await FindAsync(state.Id, Bare, Bare, deadline.Token));

        // A fill beneath it whose mask is half set up draws whole in the picture, as the viewport draws it, and the
        // step reads its red over the black. Weighed as a build is, the empty slot refused the whole stack and the
        // click found the base again.
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Half", Tint));
        var half = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(half, Mask: new("Generator")));
        await EditAsync(state.Id, deadline.Token, new StackMoveLayer(half, -1));
        Assert.Equal(half, (await StateAsync(state.Id, deadline.Token)).Layers[^2].Id);
        Assert.Equal(rust, await FindAsync(state.Id, Bare, Bare, deadline.Token));
    }

    async Task<string?> FindAsync(string id, float u, float v, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{id}/find",
            new(u, v),
            ContractsJsonContext.Default.StackFindRequest,
            token
        );
        await SucceededAsync(response, token);
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackFound, token))!.Layer;
    }
}
