using Patina.Contracts;
using System.Net.Http.Json;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>What a camera cannot see, a stroke on the mesh leaves alone, with a real Worker (#26).</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     The housing's strap lies 0.03 over its top. A dab on the strap from above reached the top under it, since depth
    ///     under a brush was only a limit, and painted it through the strap. Both halves: off, the top's chart takes
    ///     paint; on, it takes none and the strap takes the same.
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
    ///     A dab's red texels in the top cap's chart and in the strap's, from an exported base colour, then undone. The
    ///     low housing lays its charts on a four-by-four grid: the top is chart 8, the strap chart 10.
    /// </summary>
    async Task<(int Top, int Strap)> ChartsAfterAsync(string id, string name, StackStroke stroke, CancellationToken token) {
        await EditAsync(id, token, stroke);
        var exported = await ExportAsync(id, Path.Combine(folder.FullName, name), token);
        var file = PngCodec.Decode(File.ReadAllBytes(exported.Single(static map => map.Usage == "baseColor").File!));
        var (top, strap) = (0, 0);
        for (var y = file.Height / 2; y < file.Height * 3 / 4; y++) {
            for (var x = 0; x < file.Width * 3 / 4; x++) {
                var at = (y * file.Width) + x;
                if (file.Pixels[at * 4] - file.Pixels[at * 4 + 1] <= 100) {
                    continue;
                }

                top += x < file.Width / 4 ? 1 : 0;
                strap += x >= file.Width / 2 ? 1 : 0;
            }
        }

        await ActAsync(id, "undo", token);
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
