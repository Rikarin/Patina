using Patina.Contracts;
using Patina.Jobs;
using Patina.Library;

namespace Patina.Server;

/// <summary>
///     Holds the open library's cache to the room the person allows it (#189,
///     <see cref="ServerPreferences.CacheBudget" />): every few minutes, while no bake or build is queued or running,
///     the built objects, bakes and reports used longest ago go until the cache fits, as Prune would take them. A
///     later build makes them again.
/// </summary>
public sealed class CacheBudget(
    LibraryHost host,
    Preferences preferences,
    JobQueue jobs,
    ILogger<CacheBudget> logger
) : BackgroundService {
    static readonly TimeSpan Every = TimeSpan.FromMinutes(5);

    /// <summary>Brings the cache within its budget now, if one is set and nothing is building.</summary>
    /// <returns>What went, or null when nothing was looked at.</returns>
    public LibraryTidied? KeepOnce() {
        if (host.Library is not { } library || preferences.Current.CacheBudget is not { } budget) {
            return null;
        }

        // A build may be about to read an object it found a moment ago, and a bake writes into the cache.
        if (jobs.Page().Jobs.Any(static job => job.State is JobStates.Queued or JobStates.Running)) {
            return null;
        }

        var (removed, bytes) = CacheSweep.Trim(library.RootPath, budget);
        if (removed > 0) {
            ServerLog.CacheTrimmed(logger, removed, bytes);
        }

        return new(removed, bytes);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ServiceLoop.RunAsync(
            logger,
            nameof(CacheBudget),
            async token => {
                using PeriodicTimer timer = new(Every);
                while (await timer.WaitForNextTickAsync(token)) {
                    KeepOnce();
                }
            },
            stoppingToken
        );
}
