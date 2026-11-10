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

    /// <summary>
    ///     A stroke that carries the last one on starts where the brush stood: a lazy one put down within its string of
    ///     there paints nothing until it is pulled past it, where a fresh stroke lays a stamp under the pointer at once.
    ///     Undone, it takes nothing of the stroke before it.
    /// </summary>
    [Fact]
    public async Task A_stroke_carried_on_starts_where_the_brush_stood() {
        using var deadline = Deadline(240);
        var (_, first) = await StartAsync("First.vxlayers", deadline.Token);
        var carried = await CreateAsync("Carried.vxlayers", deadline.Token);
        var fresh = await CreateAsync("Fresh.vxlayers", deadline.Token);
        StackBrush lazy = new(16f, Spacing: 0.25f, Lazy: 60f, Resume: true);
        var drag = Line(100, 300, 40);
        var stood = new StackStamp(300f, 300f);
        IReadOnlyList<StackStamp> within = [new(320f, 310f), new(330f, 300f)];

        foreach (var stack in new[] { first.Id, carried.Id, fresh.Id }) {
            await EditAsync(stack, deadline.Token, new StackStroke("a", "paint", "baseColor", lazy, Red, drag));
        }

        await EditAsync(
            carried.Id,
            deadline.Token,
            new StackStroke("b", "paint", "baseColor", lazy, Red, within, Resume: [stood.X, stood.Y])
        );
        await EditAsync(fresh.Id, deadline.Token, new StackStroke("b", "paint", "baseColor", lazy, Red, within));

        var before = Digest(await PixelsAsync(first.Id, false, deadline.Token), "baseColor");
        Assert.Equal(before, Digest(await PixelsAsync(carried.Id, false, deadline.Token), "baseColor"));
        Assert.NotEqual(before, Digest(await PixelsAsync(fresh.Id, false, deadline.Token), "baseColor"));

        // Pulled past the string, it paints on from where it stood.
        await EditAsync(
            carried.Id,
            deadline.Token,
            new StackStroke("c", "paint", "baseColor", lazy, Red, [new(500f, 300f)], Resume: [stood.X, stood.Y])
        );
        Assert.NotEqual(before, Digest(await PixelsAsync(carried.Id, false, deadline.Token), "baseColor"));
    }
}
