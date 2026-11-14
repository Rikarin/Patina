using Patina.Server.Sessions;
using System.Diagnostics;

namespace Patina.Server.Tests;

/// <summary>
///     A save waits for the Worker to reach its stack (#275). On a software GPU that took longer than the patience
///     allowed, and the save failed with "No GPU process reached this stack" while one was connected and working: the
///     cause of journey's leave failing on Linux CI (#274).
/// </summary>
public sealed class WorkerWaitTests {
    static readonly TimeSpan Patience = TimeSpan.FromMilliseconds(100);

    static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_connected_worker_is_waited_for_however_long_it_takes() {
        var done = Stopwatch.StartNew();
        var slow = TimeSpan.FromTicks(Patience.Ticks * 5);

        await WorkerWait.UntilAsync(
            new Silent(),
            () => done.Elapsed < slow ? "evaluating" : null,
            static _ => Task.CompletedTask,
            Patience,
            Token
        );

        Assert.True(done.Elapsed >= slow);
    }

    [Fact]
    public async Task No_worker_for_the_whole_patience_is_refused() {
        var waited = Stopwatch.StartNew();

        await Assert.ThrowsAsync<RendererMissingException>(static () => WorkerWait.UntilAsync(
                new Silent(),
                static () => "evaluating",
                static token => Task.Delay(Timeout.InfiniteTimeSpan, token),
                Patience,
                Token
            )
        );

        Assert.True(waited.Elapsed >= Patience);
    }

    [Fact]
    public Task Nothing_pending_waits_for_no_worker() =>
        WorkerWait.UntilAsync(
            new Silent(),
            static () => null,
            static token => Task.Delay(Timeout.InfiniteTimeSpan, token),
            Patience,
            Token
        );

    /// <summary>A session that says nothing, so only the patience makes the wait look again.</summary>
    sealed class Silent : IEventSource<long> {
        public long Sequence => 0;

        public IReadOnlyList<long> After(long since) => [];

        public Task WaitAsync(long since, CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
