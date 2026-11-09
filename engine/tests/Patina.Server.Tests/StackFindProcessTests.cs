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
