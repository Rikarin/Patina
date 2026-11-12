using Patina.Contracts;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>What a camera cannot see, a stroke on the mesh leaves alone, with a real Worker (#26).</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     The housing's strap lies 0.03 over its top. A dab on the strap from above reached the top under it, since
    ///     depth under a brush was only a limit, and painted it through the strap. Both halves: off, the top's chart
    ///     takes paint; on, it takes none and the strap takes the same.
    /// </summary>
    [Fact]
    public async Task A_stroke_on_the_strap_leaves_the_top_under_it_alone() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("Strap.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        using var frames = await WearAsync(instance, state.Id, deadline.Token);
        var above = await AboveAsync(deadline.Token);

        StackStroke Dab(bool occlusion) =>
            new(
                "dab",
                "paint",
                "baseColor",
                new(24f, Occlusion: occlusion),
                Red,
                [new(Picture / 2f, Picture / 2f)],
                View: above
            );

        var (topThrough, strapThrough) = await ChartsAfterAsync(state.Id, "through", Dab(false), deadline.Token);
        var (topSeen, strapSeen) = await ChartsAfterAsync(state.Id, "seen", Dab(true), deadline.Token);

        Assert.True(topThrough > 50, $"With no test the top under the strap took {topThrough} texels.");
        Assert.Equal(0, topSeen);
        Assert.True(strapSeen > 50, $"The strap took {strapSeen} texels, so the dab missed it.");
        Assert.Equal(strapThrough, strapSeen);
    }

    /// <summary>
    ///     A stroke reaches only what the viewport draws (#136): with the housing shown alone, the strap isolated away
    ///     takes no paint and no longer hides the top under it, so the same dab from above lands on the top. The stroke
    ///     keeps what was shown when it began, so showing the strap again afterwards changes nothing it painted.
    /// </summary>
    [Fact]
    public async Task A_stroke_leaves_what_is_isolated_away_alone() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("StrapAlone.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        using var frames = await WearAsync(instance, state.Id, deadline.Token);
        var above = await AboveAsync(deadline.Token);
        await IsolateAsync(new(["housing_low"], []), deadline.Token);

        var dab = new StackStroke(
            "dab",
            "paint",
            "baseColor",
            new(24f, Occlusion: true),
            Red,
            [new(Picture / 2f, Picture / 2f)],
            View: above
        );
        await EditAsync(state.Id, deadline.Token, dab);
        await IsolateAsync(new([], []), deadline.Token);
        var (top, strap) = await ChartsAsync(state.Id, "alone", deadline.Token);

        Assert.Equal(0, strap);
        Assert.True(top > 50, $"The top took {top} texels with the strap isolated away.");
    }

    async Task IsolateAsync(ViewportIsolation isolation, CancellationToken token) {
        using var response = await http.PutAsJsonAsync(
            "/api/v1/viewport/isolation",
            isolation,
            ContractsJsonContext.Default.ViewportIsolation,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
    }

    /// <summary>
    ///     A dab's red texels in the top cap's chart and in the strap's, from an exported base colour, then undone. The
    ///     low housing lays its charts on a four-by-four grid: the top is chart 8, the strap chart 10.
    /// </summary>
    async Task<(int Top, int Strap)> ChartsAfterAsync(
        string id,
        string name,
        StackStroke stroke,
        CancellationToken token
    ) {
        await EditAsync(id, token, stroke);
        var counted = await ChartsAsync(id, name, token);
        await ActAsync(id, "undo", token);
        return counted;
    }

    /// <summary>The red texels in the top cap's chart and in the strap's, from an exported base colour.</summary>
    async Task<(int Top, int Strap)> ChartsAsync(string id, string name, CancellationToken token) {
        var exported = await ExportAsync(id, Path.Combine(folder.FullName, name), token);
        var file = PngCodec.Decode(File.ReadAllBytes(exported.Single(static map => map.Usage == "baseColor").File!));
        var (top, strap) = (0, 0);
        for (var y = file.Height / 2; y < file.Height * 3 / 4; y++) {
            for (var x = 0; x < file.Width * 3 / 4; x++) {
                var at = y * file.Width + x;
                if (file.Pixels[at * 4] - file.Pixels[at * 4 + 1] <= 100) {
                    continue;
                }

                top += x < file.Width / 4 ? 1 : 0;
                strap += x >= file.Width / 2 ? 1 : 0;
            }
        }

        return (top, strap);
    }

    /// <summary>A camera straight down on the housing, whose picture's middle is on the strap.</summary>
    async Task<StackView> AboveAsync(CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/viewport/pick",
            new(0.5, 0.5, new(0.0, 1.55, 1.05), Picture, Picture),
            ContractsJsonContext.Default.ViewportPickRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        var picked = (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.ViewportPick, token))!;
        Assert.True(picked.Found, "The middle of the picture should be on the strap.");
        return picked.View;
    }
}
