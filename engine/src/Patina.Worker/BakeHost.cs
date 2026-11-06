using Patina.Export;
using Patina.Ipc;

namespace Patina.Worker;

/// <summary>
///     Runs a bake beside whatever else the Worker is asked, on its own threads (ADR 0008). The IPC loop only ever
///     waits
///     for the start, so the viewport and graph evaluation go on being answered, and the link's reply timeout never
///     applies to a bake however long it takes. It holds a cancellation source and nothing else: a bake is rebuilt from
///     the Server's job, so losing the link stops it.
/// </summary>
sealed class BakeHost() : OneAtATime("baking") {
    /// <summary>The cores left to the viewport, graph evaluation and the IPC loop.</summary>
    const int ReservedCores = 2;

    /// <summary>How much a bake must move before it is worth telling the Server.</summary>
    const double ReportStep = 0.01;

    static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Starts a bake. Refuses a second: the Server hands out one at a time.</summary>
    public IpcMessage Start(IpcMessage request, IpcConnection link) {
        var ask = request.BakeStart ?? throw new IpcException("bake.start says nothing to bake.");
        return Begin(request.Id, ask.Ticket, stop => RunAsync(ask, link, stop, Closing));
    }

    async Task RunAsync(
        BakeStart ask,
        IpcConnection link,
        CancellationTokenSource stop,
        CancellationToken cancellationToken
    ) {
        BakeEnding ending;
        try {
            var reporter = new Reporter(ask.Ticket, link, cancellationToken);
            var threads = Math.Max(1, Environment.ProcessorCount - ReservedCores);
            var manifest = MeshMapBakeRun.Run(ask.Request, ask.Folder, threads, reporter.Report, stop.Token);
            ending = new(ask.Ticket, manifest, null);
        } catch (OperationCanceledException) {
            ending = new(ask.Ticket, null, null);
        } catch (Exception e) {
            // Whatever went wrong, the Server is waiting on this step and hears how it ended.
            ending = new(ask.Ticket, null, e.Message);
        }

        Ended(stop);
        try {
            await link.SendAsync(new() { Type = IpcTypes.BakeEnded, BakeEnded = ending }, cancellationToken)
                .ConfigureAwait(false);
        } catch (Exception e) when (e is IOException
                                        or IpcException
                                        or TimeoutException
                                        or OperationCanceledException) {
            // The Server has gone, and it hands this job out again when it is back.
        }
    }

    /// <summary>Tells the Server, not often, and never holding up a casting thread while it does.</summary>
    sealed class Reporter(BakeTicket ticket, IpcConnection link, CancellationToken cancellationToken) {
        readonly Lock reporting = new();
        DateTime next = DateTime.MinValue;
        double said = -1;

        public void Report(double fraction) {
            lock (reporting) {
                var now = DateTime.UtcNow;
                if (fraction - said < ReportStep || now < next) {
                    return;
                }

                said = fraction;
                next = now + ReportInterval;
            }

            _ = SendAsync(fraction, cancellationToken);
        }

        async Task SendAsync(double fraction, CancellationToken stopping) {
            try {
                await link.SendAsync(
                        new() { Type = IpcTypes.BakeProgress, BakeProgress = new(ticket, fraction) },
                        stopping
                    )
                    .ConfigureAwait(false);
            } catch (Exception e) when (e is IOException
                                            or IpcException
                                            or TimeoutException
                                            or OperationCanceledException) {
                // A report nobody got. The link's own loss is what stops the bake.
            }
        }
    }
}
