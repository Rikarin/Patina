using Patina.Contracts;
using Vixen.Texturing.Painting;

namespace Patina.Server.Tests;

/// <summary>A channel painted wider than eight bits a lane (#19), with a real Worker.</summary>
public sealed partial class StackProcessTests {
    /// <summary>A value between two eight-bit levels: 76.5 of 255.</summary>
    static readonly IReadOnlyList<float> Between = [0.3f, 0.3f, 0.3f, 1f];

    static StackStroke Dab(string name, string channel, float x, float y) =>
        new(name, "paint", channel, new(16f, Falloff: 0f), Between, [new(x, y), new(x + 4f, y)]);

    /// <summary>
    ///     A height channel at 16-bit float keeps a value eight bits round away: the stroke lays 0.3 where eight bits
    ///     would hold 77 of 255, the file holds the channel at its width, and a Worker lost and replaced paints the same.
    /// </summary>
    [Fact]
    public async Task A_channel_at_sixteen_bits_keeps_what_eight_bits_round_away() {
        using var deadline = Deadline(240);
        var (_, state) = await StartAsync("Deep.vxlayers", deadline.Token, out var worker);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddChannel("height"),
            new StackSetChannelFormat("height", "Rgba16Float")
        );
        var channel = (await StateAsync(state.Id, deadline.Token)).Channels.Single(static held => held.Usage == "height");
        Assert.Equal("Rgba16Float", channel.Format);

        await EditAsync(state.Id, deadline.Token, Dab("deep", "height", 200f, 200f), Dab("flat", "baseColor", 400f, 400f));
        var painted = Digest(await PixelsAsync(state.Id, false, deadline.Token), "height");

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();
        Assert.Equal(painted, Digest(await PixelsAsync(state.Id, false, deadline.Token), "height"));

        await SaveAsync(state.Id, deadline.Token);
        var canvas = CanvasBeside(state.Path);
        var height = canvas.Channel("height");
        Assert.Equal(PaintTexelFormat.Rgba16Float, height.Format);
        Assert.InRange(height.Read(202, 200).X, 0.2995f, 0.3005f);
        Assert.Equal(PaintTexelFormat.Rgba8, canvas.Channel("baseColor").Format);
        Assert.Equal(PaintImage.Pack(0.3f, 0.3f, 0.3f, 1f), canvas.Channel("baseColor").At(402, 400));
    }

    /// <summary>
    ///     A channel painted at eight bits and made wider is widened, exactly, by its next stroke in a new session, and
    ///     one a stroke of this session began on keeps its width until it is opened again, with nothing refused.
    /// </summary>
    [Fact]
    public async Task A_channel_painted_before_is_widened_exactly_by_its_next_stroke() {
        using var deadline = Deadline(240);
        var (_, state) = await StartAsync("Widened.vxlayers", deadline.Token);
        await EditAsync(state.Id, deadline.Token, Dab("first", "baseColor", 200f, 200f));
        await EditAsync(state.Id, deadline.Token, new StackSetChannelFormat("baseColor", "Rgba32Float"));
        await EditAsync(state.Id, deadline.Token, Dab("same", "baseColor", 300f, 300f));
        await SaveAsync(state.Id, deadline.Token);
        var narrow = CanvasBeside(state.Path).Channel("baseColor");
        Assert.Equal(PaintTexelFormat.Rgba8, narrow.Format);

        using (var closed = await http.DeleteAsync($"/api/v1/stacks/{state.Id}", deadline.Token)) {
            Assert.True(closed.IsSuccessStatusCode);
        }

        var again = await OpenAsync(state.Path, deadline.Token);
        await EditAsync(again.Id, deadline.Token, Dab("wide", "baseColor", 600f, 600f));
        await SaveAsync(again.Id, deadline.Token);

        var wide = CanvasBeside(state.Path).Channel("baseColor");
        Assert.Equal(PaintTexelFormat.Rgba32Float, wide.Format);
        Assert.Equal(narrow.Read(202, 200), wide.Read(202, 200));
        Assert.Equal(narrow.Read(302, 300), wide.Read(302, 300));
        Assert.Equal(0.3f, wide.Read(602, 600).X, 6);
    }

    [Fact]
    public async Task A_depth_that_is_none_is_refused() {
        using var deadline = Deadline(120);
        var (_, state) = await StartAsync("Depthless.vxlayers", deadline.Token);

        await RefusedAsync(state.Id, "channel-format-unknown", new StackSetChannelFormat("baseColor", "Rgb10"), deadline.Token);
    }
}
