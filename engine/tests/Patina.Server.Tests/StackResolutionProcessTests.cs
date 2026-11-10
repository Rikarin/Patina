using Patina.Contracts;
using System.Net.Http.Json;
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
