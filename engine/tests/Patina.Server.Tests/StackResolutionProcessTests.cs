using Patina.Contracts;
using Vixen.Texturing.Painting;

namespace Patina.Server.Tests;

/// <summary>A texture set's resolution changes without destroying what was painted (#19).</summary>
public sealed partial class StackProcessTests {
    static PaintCanvas CanvasBeside(string stack) {
        var file = Directory.EnumerateFiles(Path.GetDirectoryName(stack)!, "*.vxpaint").Single();
        using var stream = File.OpenRead(file);
        return PaintCanvas.Read(stream);
    }

    /// <summary>
    ///     A set made twice as large keeps its paint where it was: the canvas is not touched by the change, and the
    ///     next stroke enlarges it so that each texel painted before is a block of four the same.
    /// </summary>
    [Fact]
    public async Task A_set_made_larger_keeps_its_paint_exactly_and_paints_at_the_new_size() {
        using var deadline = Deadline(240);
        var (_, state) = await StartAsync("Grown.vxlayers", deadline.Token);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke("first", "paint", "baseColor", new(30f, 0.3f), Red, Line(100, 100, 10))
        );
        await SaveAsync(state.Id, deadline.Token);
        var before = CanvasBeside(state.Path).Channel("baseColor");
        Assert.Equal(Side, before.Width);

        using (var closed = await http.DeleteAsync($"/api/v1/stacks/{state.Id}", deadline.Token)) {
            Assert.True(closed.IsSuccessStatusCode);
        }

        var again = await OpenAsync(state.Path, deadline.Token);
        await EditAsync(again.Id, deadline.Token, new StackSetSettings(Side * 2, Side * 2));
        await SaveAsync(again.Id, deadline.Token);
        Assert.Equal(Side, CanvasBeside(state.Path).Width);

        await EditAsync(
            again.Id,
            deadline.Token,
            new StackStroke("second", "paint", "baseColor", new(30f, 0.3f), Red, Line(1600, 1600, 10))
        );
        await SaveAsync(again.Id, deadline.Token);

        var after = CanvasBeside(state.Path).Channel("baseColor");
        Assert.Equal(Side * 2, after.Width);
        var painted = 0;
        for (var y = 60; y < 140; y++) {
            for (var x = 60; x < 200; x++) {
                var texel = before.At(x, y);
                painted += texel != before.Background ? 1 : 0;
                Assert.Equal(texel, after.At(x * 2, y * 2));
                Assert.Equal(texel, after.At(x * 2 + 1, y * 2 + 1));
            }
        }

        Assert.True(painted > 100, $"{painted} texels of the first stroke were compared.");
        Assert.NotEqual(after.Background, after.At(1630, 1601));
    }

    /// <summary>
    ///     A stroke a Worker could not paint for its canvas's size is refused before it begins, so it never stands and
    ///     the stack still evaluates with nothing to undo (#241): a canvas larger than its set, and one a stroke of the
    ///     session began on before the set grew.
    /// </summary>
    [Fact]
    public async Task A_stroke_refused_for_its_canvas_size_never_stands() {
        using var deadline = Deadline(240);
        var (_, state) = await StartAsync("Refused.vxlayers", deadline.Token);
        var line = new StackStroke("first", "paint", "baseColor", new(30f, 0.3f), Red, Line(100, 100, 10));
        await EditAsync(state.Id, deadline.Token, line);
        await SaveAsync(state.Id, deadline.Token);

        // Painted at 1024 earlier in this session, so the set made larger cannot enlarge it now.
        await EditAsync(state.Id, deadline.Token, new StackSetSettings(Side * 2, Side * 2));
        await RefusedAsync(state.Id, "stroke-canvas-painted-at-size", line with { Stroke = "grown" }, deadline.Token);
        await SaveAsync(state.Id, deadline.Token);

        using (var closed = await http.DeleteAsync($"/api/v1/stacks/{state.Id}", deadline.Token)) {
            Assert.True(closed.IsSuccessStatusCode);
        }

        // Read from its file in a new session: 1024 is larger than a set made 512.
        var again = await OpenAsync(state.Path, deadline.Token);
        await EditAsync(again.Id, deadline.Token, new StackSetSettings(Side / 2, Side / 2));
        await RefusedAsync(again.Id, "stroke-canvas-larger", line with { Stroke = "shrunk" }, deadline.Token);
        await SaveAsync(again.Id, deadline.Token);
        Assert.Equal("Texture set settings", (await StateAsync(again.Id, deadline.Token)).History.UndoLabel);
        Assert.Equal(Side, CanvasBeside(state.Path).Width);
    }

    /// <summary>
    ///     A layer pasted from a stack of another size holds a canvas of this set's size (#106), and the size checks
    ///     know it (#241): a stroke on it at that size stands, a set made larger before any stroke on a paste enlarges
    ///     it and still shows, a stroke that carries the last one on goes through the same check as any other, and a
    ///     Worker that dies brings back the pastes at the sizes they were made at, not today's.
    /// </summary>
    [Fact]
    public async Task A_stroke_on_a_paste_from_another_size_is_checked_at_the_size_the_paste_left() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Wide.vxlayers", deadline.Token, out var worker);
        var line = new StackStroke("first", "paint", "baseColor", new(30f, 0.3f), Red, Line(100, 100, 10));
        await EditAsync(state.Id, deadline.Token, line);
        await PixelsAsync(state.Id, false, deadline.Token); // skips where the Worker has no device, as a copy needs one
        var clip = await ClipAsync(state.Id, ["paint"], deadline.Token);

        // Pasted twice into a set half the size: each paste is 512 across, made so from the 1024 it was copied at.
        var small = await CreateAsync("Narrow.vxlayers", deadline.Token, Side / 2);
        using var socket = await FollowAsync(instance, small, deadline.Token);
        await EditAsync(small.Id, deadline.Token, new StackRemoveLayer("paint"), new StackPasteLayers(clip.Id));
        var first = (await StateAsync(small.Id, deadline.Token)).Layers.Single(static layer => layer.Painted).Id;
        await EditAsync(small.Id, deadline.Token, new StackPasteLayers(clip.Id));
        var second = (await StateAsync(small.Id, deadline.Token)).Layers
            .Single(layer => layer.Painted && layer.Id != first)
            .Id;

        // A stroke at 512 is no stroke on a larger canvas, and it shows.
        var before = await PixelsAsync(small.Id, false, deadline.Token);
        await EditAsync(
            small.Id,
            deadline.Token,
            line with { Stroke = "on-first", Layer = first, Stamps = Line(40, 200, 10) }
        );
        Assert.NotEqual(
            Digest(before, "baseColor"),
            Digest(await PixelsAsync(small.Id, false, deadline.Token), "baseColor")
        );

        // Made 1024: the paste no stroke began on is enlarged by its first and shows, and a lost Worker brings both
        // pastes back at 512, where they were made and painted. The one painted at 512 is refused, a stroke carrying
        // the last one on as much as a new one.
        await EditAsync(small.Id, deadline.Token, new StackSetSettings(Side, Side));
        var grown = await PixelsAsync(small.Id, false, deadline.Token);
        await EditAsync(
            small.Id,
            deadline.Token,
            line with { Stroke = "on-second", Layer = second, Stamps = Line(80, 600, 10) }
        );
        var both = await PixelsAsync(small.Id, false, deadline.Token);
        Assert.NotEqual(Digest(grown, "baseColor"), Digest(both, "baseColor"));

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();
        await EvaluatedAsync(socket, (await StateAsync(small.Id, deadline.Token)).Revision, deadline.Token);
        Assert.Equal(
            Digest(both, "baseColor"),
            Digest(await PixelsAsync(small.Id, false, deadline.Token), "baseColor")
        );
        await RefusedAsync(
            small.Id,
            "stroke-canvas-painted-at-size",
            line with { Stroke = "resumed", Layer = first, Stamps = Line(100, 220, 10), Resume = [94f, 221f] },
            deadline.Token
        );

        // Back to 512: the second paste now holds 1024, larger than the set, resumed or not.
        await EditAsync(small.Id, deadline.Token, new StackSetSettings(Side / 2, Side / 2));
        await RefusedAsync(
            small.Id,
            "stroke-canvas-larger",
            line with { Stroke = "shrunk", Layer = second, Stamps = Line(40, 300, 10), Resume = [34f, 301f] },
            deadline.Token
        );
        Assert.Equal("Texture set settings", (await StateAsync(small.Id, deadline.Token)).History.UndoLabel);
        await PixelsAsync(small.Id, false, deadline.Token);
    }

    async Task RefusedAsync(string id, string code, StackCommand command, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{id}/commands",
            new([command]),
            ContractsJsonContext.Default.StackCommandBatch,
            token
        );
        var problem = await response.Content.ReadAsStringAsync(token);
        Assert.False(response.IsSuccessStatusCode, problem);
        Assert.Contains($"\"code\":\"{code}\"", problem, StringComparison.Ordinal);
    }
}
