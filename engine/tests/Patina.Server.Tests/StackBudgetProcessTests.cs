using Patina.Contracts;

namespace Patina.Server.Tests;

/// <summary>
///     A big stack's budget (#208): what an edit of one layer costs must not grow with the layers that did not change.
///     Counted in ops as well as time, since a shared runner's clock says little and a count says exactly what ran.
/// </summary>
public sealed partial class StackProcessTests {
    /// <summary>How many layers the big stack has over its paint layer.</summary>
    const int BigStack = 64;

    /// <summary>
    ///     Sixty-four layers over a paint layer: a value on the top layer moves without a compile and runs only what is
    ///     above the layers it left alone, a stroke beneath them all is repaired in tiles, and each stays within a time
    ///     that a software GPU meets with room to spare.
    /// </summary>
    [Fact]
    public async Task A_big_stack_pays_for_the_layer_that_changed() {
        using var deadline = Deadline(480);
        var (instance, state) = await StartAsync("Big.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        string[] blends = ["Multiply", "Overlay", "Screen", "SoftLight"];
        List<StackCommand> layers = [];
        for (var index = 0; index < BigStack; index++) {
            layers.Add(
                index % 4 == 3
                    ? new StackAddLayer("filter", $"Filter {index}", Filter: "Hsl")
                    : new StackAddLayer("fill", $"Fill {index}", Tint)
            );
        }

        await EditAsync(state.Id, deadline.Token, [..layers]);
        var added = (await StateAsync(state.Id, deadline.Token)).Layers;
        List<StackCommand> set = [];
        foreach (var (layer, index) in added.Skip(1).Select(static (layer, index) => (layer, index))) {
            set.Add(new StackSetLayer(layer.Id, Opacity: 0.2f + index % 5 * 0.1f, Blend: blends[index % blends.Length]));
        }

        var built = await EditAsync(state.Id, deadline.Token, [..set]);
        var whole = await EvaluatedAsync(socket, built.Revision, deadline.Token);
        TestContext.Current.SendDiagnosticMessage($"Whole: {whole.Ran} ops in {whole.Milliseconds:F0} ms.");

        // A value on the top layer: no compile, and the 63 layers beneath it come from the cache.
        var top = added[^1].Id;
        var moved = await EditAsync(state.Id, deadline.Token, new StackSetLayer(top, Opacity: 0.55f));
        var value = await EvaluatedAsync(socket, moved.Revision, deadline.Token);
        TestContext.Current.SendDiagnosticMessage($"Top value: {value.Ran} ops in {value.Milliseconds:F0} ms.");
        Assert.False(value.Compiled, "A value compiled.");
        Assert.True(value.Ran * 8 < whole.Ran, $"A value on the top layer ran {value.Ran} of {whole.Ran} ops.");

        // A drag under every layer: past its first part, which begins the stroke and runs whole, no compile, and each
        // part repaired over the tiles it reached rather than run whole. That is what painting a big stack costs.
        var opened = await PaintAsync(state.Id, "under", Line(100, 100, 10), true, deadline.Token);
        await EvaluatedAsync(socket, opened.Revision, deadline.Token);
        var part = await PaintAsync(state.Id, "under", Line(200, 100, 10), false, deadline.Token);
        var painted = await EvaluatedAsync(socket, part.Revision, deadline.Token);
        TestContext.Current.SendDiagnosticMessage(
            $"Stroke: {painted.Ran} ops, {painted.Repaired} repaired, in {painted.Milliseconds:F0} ms."
        );
        Assert.False(painted.Compiled, "A stroke compiled.");
        Assert.True(painted.Repaired * 2 > painted.Ran, $"{painted.Repaired} of {painted.Ran} ops were repairs.");
        Assert.True(
            painted.Milliseconds < Math.Max(2000, whole.Milliseconds),
            $"A stroke took {painted.Milliseconds:F0} ms where the whole stack took {whole.Milliseconds:F0} ms."
        );
    }
}
