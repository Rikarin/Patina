using Patina.Contracts;
using Patina.Library;

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
                (index & 3) == 3
                    ? new StackAddLayer("filter", $"Filter {index}", Filter: "Hsl")
                    : new StackAddLayer("fill", $"Fill {index}", Tint)
            );
        }

        await EditAsync(state.Id, deadline.Token, [..layers]);
        var added = (await StateAsync(state.Id, deadline.Token)).Layers;
        List<StackCommand> set = [];
        foreach (var (layer, index) in added.Skip(1).Select(static (layer, index) => (layer, index))) {
            set.Add(
                new StackSetLayer(layer.Id, Opacity: 0.2f + index % 5 * 0.1f, Blend: blends[index % blends.Length])
            );
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

    /// <summary>
    ///     What each layer hands the next is kept in <c>.ptcache</c> by a save, so a Worker that never ran the stack
    ///     runs only the layer an edit changed and reads what is beneath it from disk (#208). Without the kept results a
    ///     new Worker runs the whole stack, which is the sabotage this goes red under. What it reads is the picture a
    ///     clean evaluation makes.
    /// </summary>
    [Fact]
    public async Task A_saved_stack_keeps_its_layers_for_a_new_worker() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("Kept.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        List<StackCommand> layers = [];
        for (var index = 0; index < 12; index++) {
            layers.Add(
                (index & 3) == 3
                    ? new StackAddLayer("filter", $"Filter {index}", Filter: "Hsl")
                    : new StackAddLayer("fill", $"Fill {index}", Tint)
            );
        }

        var built = await EditAsync(state.Id, deadline.Token, [..layers]);
        var whole = await EvaluatedAsync(socket, built.Revision, deadline.Token);
        await SaveAsync(state.Id, deadline.Token);
        var kept = LibraryLayout.ResultsPath(Library);
        Assert.True(
            Directory.Exists(kept) && Directory.EnumerateFiles(kept, "*.result", SearchOption.AllDirectories).Any(),
            "A save kept no layer's result."
        );

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();
        var top = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        var moved = await EditAsync(state.Id, deadline.Token, new StackSetLayer(top, Opacity: 0.4f));
        var after = await EvaluatedAsync(socket, moved.Revision, deadline.Token);
        TestContext.Current.SendDiagnosticMessage($"Whole: {whole.Ran} ops; after a restart: {after.Ran} ops.");
        Assert.True(after.Ran * 3 < whole.Ran, $"A new Worker ran {after.Ran} of the stack's {whole.Ran} ops.");

        var read = await PixelsAsync(state.Id, false, deadline.Token);
        var clean = await PixelsAsync(state.Id, true, deadline.Token);
        Assert.All(clean, map => Assert.Equal(map.Digest, Digest(read, map.Usage)));
    }

    /// <summary>
    ///     Only the stroke that makes a layer's canvas compiles: every stroke after it begins on the plan it left
    ///     (#279). Under a proxy a stroke is still repaired in tiles, scaled from the canvas to the plan's size,
    ///     rather than running every op above the canvas whole.
    /// </summary>
    [Fact]
    public async Task A_new_stroke_compiles_nothing_and_a_proxy_repairs_its_tiles() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Strokes.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer("fill", "Over", Tint),
            new StackAddLayer("filter", "Shift", Filter: "Hsl")
        );
        var made = await PaintAsync(state.Id, "a", Line(100, 100, 10), false, deadline.Token);
        await EvaluatedAsync(socket, made.Revision, deadline.Token);

        var begun = await PaintAsync(state.Id, "b", Line(300, 300, 10), true, deadline.Token);
        var second = await EvaluatedAsync(socket, begun.Revision, deadline.Token);
        Assert.False(second.Compiled, "A stroke on a canvas that was there already compiled.");
        Assert.True(second.Repaired > 0, "A new stroke ran whole.");
        var ended = await PaintAsync(state.Id, "b", Line(400, 300, 10), false, deadline.Token);
        await EvaluatedAsync(socket, ended.Revision, deadline.Token);

        await PaceAsync(state.Id, new(Proxy: 4), deadline.Token);
        var proxied = await PaintAsync(state.Id, "c", Line(600, 600, 10), true, deadline.Token);
        await EvaluatedAsync(socket, proxied.Revision, deadline.Token);
        var part = await PaintAsync(state.Id, "c", Line(700, 600, 10), false, deadline.Token);
        var small = await EvaluatedAsync(socket, part.Revision, deadline.Token);
        TestContext.Current.SendDiagnosticMessage($"Proxy stroke: {small.Ran} ops, {small.Repaired} repaired.");
        Assert.False(small.Compiled, "A stroke under a proxy compiled.");
        Assert.True(small.Repaired > 0, "A stroke under a proxy ran every op whole.");
    }
}
