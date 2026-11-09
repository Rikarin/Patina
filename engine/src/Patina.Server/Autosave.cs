using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;

namespace Patina.Server;

/// <summary>
///     Keeps what is open and unsaved, so that a crash, a kill or a restart costs seconds of work. It runs here and not
///     in a client because open sessions live here, and every client gets it. It writes to the cache, never over a
///     document. A document is kept when its edits stop for a moment (the idle wait), and at the latest every interval
///     while they go on, and the last few are kept (<see cref="Preferences" />).
/// </summary>
public sealed class Autosave(
    StackSessions stacks,
    GraphSessions graphs,
    StackEvaluator evaluator,
    ServerIdentity identity,
    Preferences preferences,
    AutosaveGate gate,
    ILogger<Autosave> logger,
    TimeProvider? clock = null
) : BackgroundService {
    /// <summary>How often open documents are looked at. Every wait is rounded up to it.</summary>
    static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(500);

    readonly TimeProvider time = clock ?? TimeProvider.System;
    readonly Dictionary<string, Watch> watched = new(StringComparer.Ordinal);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        identity.Library is { } library
            ? ServiceLoop.RunAsync(logger, nameof(Autosave), token => LoopAsync(library, token), stoppingToken)
            : Task.CompletedTask;

    async Task LoopAsync(string library, CancellationToken stoppingToken) {
        using PeriodicTimer timer = new(Tick, time);
        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            var settings = preferences.Autosave;
            if (settings.IntervalSeconds == 0) {
                continue;
            }

            var now = time.GetUtcNow();
            // Before what is open is looked at: a document closed or saved after this has nothing kept from here on.
            var mark = gate.Mark();
            HashSet<string> open = new(StringComparer.Ordinal);
            foreach (var graph in graphs.All) {
                open.Add(graph.Path);
                if (Due(graph.Path, graph.Summary.Revision, graph.Summary.Dirty, now, settings)) {
                    var generation = Recovery.Begin(library, graph.Path);
                    Finished(graph.Path, gate.Finish(library, graph.Path, generation, mark, graph.Text, settings.Keep));
                }
            }

            foreach (var stack in stacks.All) {
                open.Add(stack.Path);
                if (Due(stack.Path, stack.Summary.Revision, stack.Summary.Dirty, now, settings)) {
                    await TryAsync(stack, library, mark, settings.Keep, stoppingToken);
                }
            }

            Forget(open);
        }
    }

    /// <summary>Lets go of what autosave knew of documents that have closed.</summary>
    void Forget(HashSet<string> open) {
        foreach (var path in watched.Keys.Except(open, StringComparer.Ordinal).ToList()) {
            watched.Remove(path);
        }
    }

    /// <summary>A Worker that is away this moment is here the next, and a save must not wait on it.</summary>
    async Task TryAsync(StackSession stack, string library, long mark, int keep, CancellationToken cancellationToken) {
        string? generation = null;
        try {
            generation = Recovery.Begin(library, stack.Path);
            await evaluator.KeepAsync(stack, generation, cancellationToken);
            Finished(stack.Path, gate.Finish(library, stack.Path, generation, mark, stack.Text, keep));
        } catch (Exception e) when (e is IOException or TimeoutException or Patina.Ipc.IpcException) {
            ServerLog.AutosaveFailed(logger, stack.Path, e.Message);
            if (generation is not null) {
                Abandon(stack.Path, library, generation, mark);
            }
        }
    }

    /// <summary>
    ///     An autosave a save let go of as it landed is not one kept: edits after that save are kept when next due.
    /// </summary>
    void Finished(string path, bool kept) {
        if (!kept && watched.TryGetValue(path, out var watch)) {
            watch.Kept = long.MinValue;
        }
    }

    /// <summary>What a Worker that failed left of an autosave of a document since let go goes too.</summary>
    void Abandon(string path, string library, string generation, long mark) {
        try {
            gate.Abandon(library, path, generation, mark);
        } catch (IOException e) {
            ServerLog.AutosaveFailed(logger, path, e.Message);
        }
    }

    /// <summary>
    ///     Whether a document has unsaved edits that have not been kept, and has either been still for the idle wait or
    ///     waited the interval since it was last kept.
    /// </summary>
    bool Due(string path, long revision, bool dirty, DateTimeOffset now, AutosavePreferences settings) {
        if (!watched.TryGetValue(path, out var watch)) {
            // A document opens with nothing to keep, since what it opened with is its file. One already edited when
            // autosave first sees it, as when autosave was off, is kept once it has been still or waited.
            watch = new() { Seen = revision, Kept = dirty ? long.MinValue : revision, Changed = now, KeptAt = now };
            watched[path] = watch;
        }

        if (revision != watch.Seen) {
            watch.Seen = revision;
            watch.Changed = now;
        }

        if (!dirty || revision == watch.Kept) {
            return false;
        }

        var still = settings.IdleSeconds > 0 && now - watch.Changed >= TimeSpan.FromSeconds(settings.IdleSeconds);
        var waited = now - watch.KeptAt >= TimeSpan.FromSeconds(settings.IntervalSeconds);
        if (!still && !waited) {
            return false;
        }

        watch.Kept = revision;
        watch.KeptAt = now;
        return true;
    }

    /// <summary>What autosave knows of one open document.</summary>
    sealed class Watch {
        /// <summary>The revision last seen, and when it was first seen.</summary>
        public long Seen { get; set; }

        public DateTimeOffset Changed { get; set; }

        /// <summary>The revision last kept, and when.</summary>
        public long Kept { get; set; }

        public DateTimeOffset KeptAt { get; set; }
    }
}
