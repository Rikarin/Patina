using Patina.Contracts;

namespace Patina.Server.Tests;

/// <summary>A loop fill on the housing with a real Worker, counted in the exported map (#129).</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     ⚠ The housing's eight sides are each one quad, laid one to a cell of the atlas with the quad's first edge, an
    ///     upright one, along the cell's bottom. A loop clicked near that edge runs round the housing, across the
    ///     uprights, and fills all eight sides: eight times the one side a polygon fill takes there. Clicked near the
    ///     side's top edge instead it runs up into the caps, whose fans are triangles and not quads, so it stops at
    ///     once both ways and takes the side alone. A loop sent as an outline is refused, since it runs from where a
    ///     click lands.
    /// </summary>
    [Fact]
    public async Task A_loop_fill_runs_round_the_ring_of_quads_across_the_edge_nearest_the_click() {
        using var deadline = Deadline(300);
        var (_, state) = await StartAsync("Loop.vxlayers", deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);

        // The first side's cell is the atlas's top-left quarter of a quarter; its edges are at 0.03 and 0.97 of it.
        static float In(float at) => Side * (0.03f + 0.94f * at) / 4f;

        StackStroke Click(string name, string mode, float across, float down) =>
            new(name, "paint", "baseColor", new(1f), Red, [new(In(across), In(down))], Curve: "fill", Fill: mode);

        var side = await RedAfterAsync(state.Id, "side", Click("side", "polygon", 0.5f, 0.9f), deadline.Token);
        var ring = await RedAfterAsync(state.Id, "ring", Click("ring", "loop", 0.5f, 0.9f), deadline.Token);
        var column = await RedAfterAsync(state.Id, "column", Click("column", "loop", 0.9f, 0.5f), deadline.Token);

        Assert.True(side > 10000, $"A side took {side} texels, so the click missed it.");
        Assert.InRange(ring, side * 7.6, side * 8.4);
        Assert.Equal(side, column);

        StackStamp[] box = [new(In(0.1f), In(0.1f)), new(In(0.9f), In(0.1f)), new(In(0.9f), In(0.9f))];
        await RefusedAsync(state.Id, deadline.Token, Click("outline", "loop", 0.5f, 0.5f) with {
            Stamps = box, Outline = true
        });
        await RefusedAsync(state.Id, deadline.Token, Click("unknown", "ring", 0.5f, 0.5f));
    }
}
