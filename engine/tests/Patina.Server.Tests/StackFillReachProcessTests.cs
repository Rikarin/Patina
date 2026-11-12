using Patina.Contracts;

namespace Patina.Server.Tests;

/// <summary>
///     A fill reaches what a stroke reaches, with a real Worker: what the viewport shows alone (#136, #129), and over a
///     mask that is a picture, a paint layer of the mask's own stack (#72).
/// </summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     With the housing shown alone, a click fill and an outline fill from above leave the strap isolated away
    ///     alone and land on the top it hid, as a dab does. Each keeps what was shown when it began, so showing the
    ///     strap again changes nothing, and a Worker that replays them after a crash fills the same faces.
    /// </summary>
    [Fact]
    public async Task A_fill_leaves_what_is_isolated_away_alone_and_its_replay_too() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("FillAlone.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        using var frames = await WearAsync(instance, state.Id, deadline.Token);
        var above = await AboveAsync(deadline.Token);
        await IsolateAsync(new(["housing_low"], []), deadline.Token);

        var click = new StackStroke(
            "click",
            "paint",
            "baseColor",
            new(1f),
            Red,
            [new(Picture / 2f, Picture / 2f)],
            View: above,
            Curve: "fill",
            Fill: "polygon"
        );
        const float half = 12f;
        var box = click with {
            Stroke = "box",
            Fill = "triangle",
            Outline = true,
            Stamps = [
                new(Picture / 2f - half, Picture / 2f - half),
                new(Picture / 2f + half, Picture / 2f - half),
                new(Picture / 2f + half, Picture / 2f + half),
                new(Picture / 2f - half, Picture / 2f + half)
            ]
        };

        await EditAsync(state.Id, deadline.Token, click);
        await IsolateAsync(new([], []), deadline.Token);
        var (clickTop, clickStrap) = await ChartsAsync(state.Id, "click-alone", deadline.Token);
        await ActAsync(state.Id, "undo", deadline.Token);
        await IsolateAsync(new(["housing_low"], []), deadline.Token);
        await EditAsync(state.Id, deadline.Token, box);
        await IsolateAsync(new([], []), deadline.Token);
        var (boxTop, boxStrap) = await ChartsAsync(state.Id, "box-alone", deadline.Token);
        var last = await EditAsync(state.Id, deadline.Token, click with { Stroke = "again" });
        await EvaluatedAsync(socket, last.Revision, deadline.Token);
        var painted = await PixelsAsync(state.Id, false, deadline.Token);

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();
        var replayed = await PixelsAsync(state.Id, false, deadline.Token);

        Assert.Equal(0, clickStrap);
        Assert.True(clickTop > 50, $"A click took {clickTop} texels of the top with the strap isolated away.");
        Assert.Equal(0, boxStrap);
        Assert.True(boxTop > 20, $"An outline took {boxTop} texels of the top with the strap isolated away.");
        Assert.Equal(Digest(painted, "baseColor"), Digest(replayed, "baseColor"));
    }

    /// <summary>
    ///     A fill on a mask that is a picture paints over it as a brush does (#72): the picture stays, live, beneath a
    ///     paint layer of the mask's own stack that takes the fill, and one undo leaves the mask as it was.
    /// </summary>
    [Fact]
    public async Task A_fill_on_a_mask_that_is_a_picture_paints_over_it() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("FillOverPicture.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        using var frames = await WearAsync(instance, state.Id, deadline.Token);
        var view = await AimAsync(0.5, 0.5, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", AnchorPoint: "Paint"));
        await PaintAsync(state.Id, "picture", Line(250, 280, 40), false, deadline.Token);
        var fill = await MaskedFillAsync(state.Id, new("Anchor", Anchor: new("Paint", "baseColor")), deadline.Token);
        var before = await TexelsAsync(state.Id, "before", deadline.Token);

        var white = new StackStroke(
            "white",
            fill,
            "baseColor",
            new(1f),
            [1f, 1f, 1f, 1f],
            [new(Picture / 2f, Picture / 2f)],
            View: view,
            Curve: "fill",
            Fill: "part",
            Mask: true
        );
        await EditAsync(state.Id, deadline.Token, white);
        var after = await TexelsAsync(state.Id, "after", deadline.Token);

        var mask = Layer(await StateAsync(state.Id, deadline.Token), fill).Mask;
        Assert.Equal("Anchor", mask.Source);
        var entry = Assert.Single(mask.Layers!);
        Assert.Equal(("Paint", true), (entry.Source, entry.Painted));
        Assert.NotEqual(Digest(before.Maps, "baseColor"), Digest(after.Maps, "baseColor"));

        await ActAsync(state.Id, "undo", deadline.Token);
        mask = Layer(await StateAsync(state.Id, deadline.Token), fill).Mask;
        Assert.Equal("Anchor", mask.Source);
        Assert.Empty(mask.Layers!);
        Assert.Equal(
            Digest(before.Maps, "baseColor"),
            Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor")
        );
    }
}
