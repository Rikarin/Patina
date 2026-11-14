using Patina.Contracts;

namespace Patina.Server.Tests;

/// <summary>A Server that dies leaves the Worker, which carries on with the next Server.</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A document the next Server opens again is its file, without the stroke the dead Server never saved, and
    ///     takes a stroke of its own. The Worker kept the dead Server's sessions and lent their unsaved canvases to the
    ///     new ones (#108's sharing), so the lost stroke showed; and the new session's first stroke of its own went
    ///     into the lent canvas, which the next evaluation dropped for the file, so the stroke never showed.
    /// </summary>
    [Fact]
    public async Task A_document_opened_by_the_next_server_is_its_file_and_takes_a_stroke() {
        using var deadline = Deadline(300);
        var (_, first) = await StartAsync("Restarted.vxlayers", deadline.Token, out var worker);
        await EditAsync(first.Id, deadline.Token, Dab("saved", 100));
        await SaveAsync(first.Id, deadline.Token);
        var saved = Digest(await PixelsAsync(first.Id, false, deadline.Token), "baseColor");
        await EditAsync(first.Id, deadline.Token, Dab("lost", 400));
        Assert.NotEqual(saved, Digest(await PixelsAsync(first.Id, false, deadline.Token), "baseColor"));

        var server = started.Last(process => !ReferenceEquals(process, worker));
        server.Kill();
        await server.WaitForExitAsync(deadline.Token);
        File.Delete(DiscoveryFile);
        await AttachAsync("Other.vxlayers", deadline.Token);
        Assert.False(worker.HasExited);

        var again = await OpenAsync(first.Path, deadline.Token);
        Assert.Equal(saved, Digest(await PixelsAsync(again.Id, false, deadline.Token), "baseColor"));

        await EditAsync(again.Id, deadline.Token, Dab("kept", 700));
        var painted = Digest(await PixelsAsync(again.Id, false, deadline.Token), "baseColor");
        Assert.NotEqual(saved, painted);
        Assert.Equal(Digest(await PixelsAsync(again.Id, true, deadline.Token), "baseColor"), painted);
    }

    static StackStroke Dab(string id, float at) => new(id, "paint", "baseColor", new(30f, 0.3f), Red, Line(at, at, 10));
}
