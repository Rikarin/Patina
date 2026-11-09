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
    ///     A set made twice as large keeps its paint where it was: the canvas is not touched by the change, and the next
    ///     stroke enlarges it so that each texel painted before is a block of four the same.
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
        await EditAsync(again.Id, deadline.Token, new StackSetSettings(Width: Side * 2, Height: Side * 2));
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
}
