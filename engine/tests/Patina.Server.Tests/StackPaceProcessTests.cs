using Patina.Contracts;
using System.Net;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>A big stack stays quick to set up: evaluation pauses, and a proxy composites it smaller (#208).</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A paused stack takes edits without evaluating them, yet what a client reads of its pixels is every edit, and
    ///     a resume evaluates them all at once.
    /// </summary>
    [Fact]
    public async Task A_paused_stack_waits_to_evaluate_but_reads_every_edit() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Paused.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        var first = await PaintAsync(state.Id, "a", Line(80, 300, 30), false, deadline.Token);
        await EvaluatedAsync(socket, first.Revision, deadline.Token);
        var before = await PixelsAsync(state.Id, false, deadline.Token);

        Assert.Equal(new(true, 1), await PaceAsync(state.Id, new(Paused: true), deadline.Token));
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Top", Tint));
        var edited = await PaintAsync(state.Id, "b", Line(600, 700, 30), false, deadline.Token);

        // Nothing is evaluated while it is paused, however long the edits wait.
        await Task.Delay(TimeSpan.FromSeconds(1), deadline.Token);
        var paused = await StateAsync(state.Id, deadline.Token);
        Assert.True(paused.Pace!.Paused);
        Assert.True(paused.Settled < edited.Revision, $"Settled at {paused.Settled} of {edited.Revision}.");

        // A read is never a pause's: it is evaluated for, and it is what an export writes.
        var read = await PixelsAsync(state.Id, false, deadline.Token);
        Assert.NotEqual(Digest(before, "baseColor"), Digest(read, "baseColor"));
        var exported = await ExportAsync(state.Id, Path.Combine(folder.FullName, "paused"), deadline.Token);
        Assert.All(exported, map => Assert.Equal(map.Digest, Digest(read, map.Usage)));

        await PaceAsync(state.Id, new(Paused: false), deadline.Token);
        var latest = await StateAsync(state.Id, deadline.Token);
        await EvaluatedAsync(socket, latest.Revision, deadline.Token);
        Assert.False((await StateAsync(state.Id, deadline.Token)).Pace!.Paused);
    }

    /// <summary>
    ///     At a proxy the previews are smaller, while the pixels, an export and a save are at full size and the same as
    ///     with none; a proxy no size is divided by is refused with its code.
    /// </summary>
    [Fact]
    public async Task A_proxy_previews_smaller_and_exports_at_full_size() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Proxy.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        var first = await PaintAsync(state.Id, "a", Line(80, 300, 30), false, deadline.Token);
        await EvaluatedAsync(socket, first.Revision, deadline.Token);
        var full = await PixelsAsync(state.Id, false, deadline.Token);

        Assert.Equal(new(false, 4), await PaceAsync(state.Id, new(Proxy: 4), deadline.Token));
        var second = await PaintAsync(state.Id, "b", Line(600, 700, 30), false, deadline.Token);
        var small = await EvaluatedAsync(socket, second.Revision, deadline.Token);
        Assert.NotEmpty(small.Previews);
        Assert.All(small.Previews, static preview => Assert.True(preview.Width <= Side / 4, $"{preview.Width}"));

        var proxied = await PixelsAsync(state.Id, false, deadline.Token);
        Assert.All(proxied, static map => Assert.Equal(Side, map.Width));
        Assert.NotEqual(Digest(full, "baseColor"), Digest(proxied, "baseColor"));
        var exported = await ExportAsync(state.Id, Path.Combine(folder.FullName, "proxy"), deadline.Token);
        Assert.All(exported, map => Assert.Equal(map.Digest, Digest(proxied, map.Usage)));

        // Back at full size the picture is the one a proxy read: the canvas was never smaller.
        await PaceAsync(state.Id, new(Proxy: 1), deadline.Token);
        var clean = await PixelsAsync(state.Id, true, deadline.Token);
        Assert.All(clean, map => Assert.Equal(map.Digest, Digest(proxied, map.Usage)));

        using var refused = await http.PutAsJsonAsync(
            $"/api/v1/stacks/{state.Id}/pace",
            new StackPaceChange(Proxy: 3),
            ContractsJsonContext.Default.StackPaceChange,
            deadline.Token
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains("stack-proxy-refused", await refused.Content.ReadAsStringAsync(deadline.Token));
        Assert.Equal(1, (await StateAsync(state.Id, deadline.Token)).Pace!.Proxy);
    }

    async Task<StackPace> PaceAsync(string id, StackPaceChange change, CancellationToken token) {
        using var response = await http.PutAsJsonAsync(
            $"/api/v1/stacks/{id}/pace",
            change,
            ContractsJsonContext.Default.StackPaceChange,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackPace, token))!;
    }
}
