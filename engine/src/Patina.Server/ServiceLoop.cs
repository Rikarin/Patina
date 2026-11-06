namespace Patina.Server;

/// <summary>
///     Keeps a background service alive. By default one exception out of a service stops the whole Server, and open
///     stacks and graph histories are in memory, so a fault in one job or one stack must never cost the rest.
/// </summary>
static class ServiceLoop {
    static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    /// <summary>Runs a service until it is stopped or finishes, starting it again after anything unexpected.</summary>
    public static async Task RunAsync(
        ILogger logger,
        string name,
        Func<CancellationToken, Task> body,
        CancellationToken stoppingToken
    ) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await body(stoppingToken);
                return;
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                return;
            } catch (Exception e) {
                ServerLog.ServiceFailed(logger, name, e.Message, e);
                await Task.Delay(Pause, CancellationToken.None);
            }
        }
    }
}
