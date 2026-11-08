using Patina.Contracts;

namespace Patina.Server.Tests;

/// <summary>Named anchor points with a real Worker: what a client is told, and what a mask reads.</summary>
public sealed partial class StackProcessTests {
    /// <summary>The list of anchor points follows every edit, and a mask reading one names its channel.</summary>
    [Fact]
    public async Task An_anchor_point_is_listed_with_who_reads_it_after_each_edit() {
        using var deadline = Deadline(180);
        var (instance, state) = await StartAsync("Anchors.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);

        await EditAsync(state.Id, deadline.Token, new StackSetLayer("base", AnchorPoint: "Ground"));
        Assert.Equal("Ground", Assert.Single((await StateAsync(state.Id, deadline.Token)).Anchors!).Name);

        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", Mask: new("Constant", 1f)));
        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", Mask: new("Anchor", 1f)));
        Assert.Equal("Ground", Assert.Single((await StateAsync(state.Id, deadline.Token)).Anchors!).Name);

        var read = await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer("paint", Mask: new("Anchor", 1f, Anchor: new("Ground", "roughness")))
        );
        var anchor = Assert.Single((await StateAsync(state.Id, deadline.Token)).Anchors!);
        Assert.Equal([new StackAnchorUse("paint", "mask", "roughness")], anchor.Uses);
        var evaluation = await EvaluatedAsync(socket, read.Revision, deadline.Token);
        Assert.DoesNotContain(evaluation.Problems, static problem => problem.Severity == "Error");
    }
}
