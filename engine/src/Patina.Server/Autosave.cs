using Microsoft.Extensions.Options;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;

namespace Patina.Server;

/// <summary>
///     Keeps what is open and unsaved, so that a crash costs a minute of work. It runs here and not in a client because
///     open sessions live here, and every client gets it. It writes to the cache, never over a document.
/// </summary>
public sealed class Autosave(
    StackSessions stacks,
    GraphSessions graphs,
    StackEvaluator evaluator,
    ServerIdentity identity,
    IOptions<ServerOptions> options,
    ILogger<Autosave> logger
) : BackgroundService {
    readonly Dictionary<string, long> kept = new(StringComparer.Ordinal);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        identity.Library is { } library
            ? ServiceLoop.RunAsync(logger, nameof(Autosave), token => LoopAsync(library, token), stoppingToken)
            : Task.CompletedTask;

    async Task LoopAsync(string library, CancellationToken stoppingToken) {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(options.Value.AutosaveSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            foreach (var graph in graphs.All.Where(open => Moved(open.Path, open.Summary.Revision, open.Summary.Dirty)
                     )) {
                Recovery.Keep(library, graph.Path, graph.Text);
            }

            foreach (var stack in stacks.All.Where(open => Moved(open.Path, open.Summary.Revision, open.Summary.Dirty)
                     )) {
                await TryAsync(stack, library, stoppingToken);
            }
        }
    }

    /// <summary>A Worker that is away this minute is here the next, and a save must not wait on it.</summary>
    async Task TryAsync(StackSession stack, string library, CancellationToken cancellationToken) {
        try {
            await evaluator.KeepAsync(stack, Recovery.Folder(library, stack.Path), cancellationToken);
            Recovery.Keep(library, stack.Path, stack.Text);
        } catch (Exception e) when (e is IOException or TimeoutException or Patina.Ipc.IpcException) {
            ServerLog.AutosaveFailed(logger, stack.Path, e.Message);
        }
    }

    bool Moved(string path, long revision, bool dirty) {
        if (!dirty || kept.GetValueOrDefault(path) == revision) {
            return false;
        }

        kept[path] = revision;
        return true;
    }
}
