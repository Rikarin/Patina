using Microsoft.Extensions.Logging;
using Patina.Contracts;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>The Server's log as a client's log pane reads it (#192).</summary>
public sealed class LogRingTests {
    CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Patina's lines are kept from information up and the framework's from warnings up, numbered from one, and a
    ///     reader follows along by asking after the last number it saw.
    /// </summary>
    [Fact]
    public void Lines_are_numbered_and_followed_and_requests_are_not_lines() {
        using var ring = new LogRing();
        Say(ring, "Patina.Server.Autosave", LogLevel.Information, "Kept crate.vxlayers");
        Say(ring, "Microsoft.AspNetCore.Routing", LogLevel.Information, "Executing endpoint");
        Say(ring, "Microsoft.AspNetCore.Server", LogLevel.Warning, "Slow");
        Say(ring, "Patina.Server.WorkerLink", LogLevel.Debug, "Heard a beat");

        var first = ring.After(0);

        Assert.Equal(
            [(1L, "information", "Kept crate.vxlayers"), (2L, "warning", "Slow")],
            first.Entries.Select(static entry => (entry.Sequence, entry.Level, entry.Text))
        );
        Assert.Equal((2, false), (first.Next, first.Dropped));
        Say(ring, "Patina.Server.Autosave", LogLevel.Error, "Could not keep it.", new IOException("The disk is full."));
        var next = ring.After(first.Next);
        Assert.Equal("Could not keep it. The disk is full.", Assert.Single(next.Entries).Text);
        Assert.Empty(ring.After(next.Next).Entries);
        Assert.Equal(next.Next, ring.After(next.Next).Next);
    }

    /// <summary>
    ///     Only the latest are kept, and a reader that fell behind them, or follows a Server since restarted, hears
    ///     that lines are gone and starts again from the oldest kept.
    /// </summary>
    [Fact]
    public void A_reader_that_fell_behind_hears_that_lines_are_gone() {
        using var ring = new LogRing();
        for (var line = 0; line < ServerLogPage.Most + 5; line++) {
            Say(ring, "Patina.Server.Jobs", LogLevel.Information, "Line");
        }

        var behind = ring.After(2);
        Assert.True(behind.Dropped);
        Assert.Equal(6, behind.Entries[0].Sequence);
        Assert.Equal(ServerLogPage.Most, behind.Entries.Count);

        var restarted = ring.After(ServerLogPage.Most * 10);
        Assert.True(restarted.Dropped);
        Assert.Equal(6, restarted.Entries[0].Sequence);
    }

    [Fact]
    public async Task Any_client_reads_the_log() {
        await using ServerFixture server = new();
        using var client = server.AuthorisedClient();

        using var answer = await client.GetAsync("/api/v1/log?after=0", Token);

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        var page = await answer.Content.ReadFromJsonAsync<ServerLogPage>(Token);
        Assert.True(page!.Next >= 0);
    }

    static void Say(LogRing ring, string source, LogLevel level, string text, Exception? exception = null) =>
        ring.CreateLogger(source).Log(level, default, text, exception, static (said, _) => said);
}
