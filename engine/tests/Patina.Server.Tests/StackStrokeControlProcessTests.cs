using Patina.Contracts;

namespace Patina.Server.Tests;

/// <summary>A lazy mouse, through a real Worker: a distance the brush trails by, whatever the pointer's rate.</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A lazy brush paints less of a drag than one that follows it, paints the same however the drag was split into
    ///     parts, and leaves a line alone: a line has to reach its last point.
    /// </summary>
    [Fact]
    public async Task A_lazy_brush_trails_by_a_distance_whatever_the_parts() {
        using var deadline = Deadline(240);
        var (_, plain) = await StartAsync("Plain.vxlayers", deadline.Token);
        var whole = await CreateAsync("Whole.vxlayers", deadline.Token);
        var parts = await CreateAsync("Parts.vxlayers", deadline.Token);
        StackBrush lazy = new(16f, Spacing: 0.25f, Lazy: 60f);
        StackBrush follows = new(16f, Spacing: 0.25f);
        var drag = Line(100, 300, 60);

        await EditAsync(plain.Id, deadline.Token, new StackStroke("a", "paint", "baseColor", follows, Red, drag));
        await EditAsync(whole.Id, deadline.Token, new StackStroke("a", "paint", "baseColor", lazy, Red, drag));
        for (var at = 0; at < drag.Count; at += 7) {
            await EditAsync(
                parts.Id,
                deadline.Token,
                new StackStroke(
                    "a",
                    "paint",
                    "baseColor",
                    lazy,
                    Red,
                    [..drag.Skip(at).Take(7)],
                    at + 7 < drag.Count
                )
            );
        }

        var trailed = Digest(await PixelsAsync(whole.Id, false, deadline.Token), "baseColor");
        Assert.NotEqual(Digest(await PixelsAsync(plain.Id, false, deadline.Token), "baseColor"), trailed);
        Assert.Equal(trailed, Digest(await PixelsAsync(parts.Id, false, deadline.Token), "baseColor"));

        var straight = await CreateAsync("Straight.vxlayers", deadline.Token);
        var lazyLine = await CreateAsync("LazyLine.vxlayers", deadline.Token);
        IReadOnlyList<StackStamp> ends = [new(100f, 600f), new(700f, 640f)];
        await EditAsync(
            straight.Id,
            deadline.Token,
            new StackStroke("l", "paint", "baseColor", follows, Red, ends, Curve: "line")
        );
        await EditAsync(
            lazyLine.Id,
            deadline.Token,
            new StackStroke("l", "paint", "baseColor", lazy, Red, ends, Curve: "line")
        );
        Assert.Equal(
            Digest(await PixelsAsync(straight.Id, false, deadline.Token), "baseColor"),
            Digest(await PixelsAsync(lazyLine.Id, false, deadline.Token), "baseColor")
        );
    }
}
