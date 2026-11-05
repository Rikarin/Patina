using Patina.Export;
using Patina.Ipc;
using Vixen.Interchange;

namespace Patina.Worker;

/// <summary>
///     Runs a bake beside whatever else the Worker is asked, on its own threads (ADR 0008). The IPC loop only ever waits
///     for the start, so the viewport and graph evaluation go on being answered, and the link's reply timeout never
///     applies to a bake however long it takes. It holds a cancellation source and nothing else: a bake is rebuilt from
///     the Server's job, so losing the link stops it.
/// </summary>
sealed class BakeHost : IDisposable {
    /// <summary>The cores left to the viewport, graph evaluation and the IPC loop.</summary>
    const int ReservedCores = 2;

    /// <summary>How much a bake must move before it is worth telling the Server.</summary>
    const double ReportStep = 0.01;

    static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(250);

    readonly Lock changing = new();
    Running? running;

    /// <summary>Starts a bake. Refuses a second: the Server hands out one at a time.</summary>
    public IpcMessage Start(IpcMessage request, IpcConnection link) {
        var ask = request.BakeStart ?? throw new IpcException("bake.start says nothing to bake.");
        lock (changing) {
            if (running is not null) {
                return IpcMessage.Fail(request.Id, $"This Worker is already baking {running.Ticket.Job}.");
            }

            var stop = new CancellationTokenSource();
            running = new(ask.Ticket, stop);
            _ = Task.Factory.StartNew(
                () => RunAsync(ask, link, stop),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            ).Unwrap();
        }

        return IpcMessage.Ok(request.Id);
    }

    /// <summary>Stops the bake named, if it is still the one running.</summary>
    public IpcMessage Cancel(IpcMessage request) {
        lock (changing) {
            if (running is { } bake && bake.Ticket == request.BakeCancel) {
                bake.Stop.Cancel();
            }
        }

        return IpcMessage.Ok(request.Id);
    }

    /// <summary>Stops whatever is running: the link has gone, and the Server will hand the job out again.</summary>
    public void LinkLost() {
        lock (changing) {
            running?.Stop.Cancel();
        }
    }

    public void Dispose() => LinkLost();

    async Task RunAsync(BakeStart ask, IpcConnection link, CancellationTokenSource stop) {
        BakeEnding ending;
        try {
            var reporter = new Reporter(ask.Ticket, link);
            var threads = Math.Max(1, Environment.ProcessorCount - ReservedCores);
            var manifest = MeshMapBakeRun.Run(ask.Request, ask.Folder, threads, reporter.Report, stop.Token);
            ending = new(ask.Ticket, manifest, null);
        } catch (OperationCanceledException) {
            ending = new(ask.Ticket, null, null);
        } catch (Exception e) when (e is IOException
                                        or InvalidDataException
                                        or ArgumentException
                                        or UnauthorizedAccessException
                                        or ModelFormatException
                                        or OutOfMemoryException) {
            ending = new(ask.Ticket, null, e.Message);
        }

        lock (changing) {
            running = null;
        }

        stop.Dispose();
        try {
            await link.SendAsync(new() { Type = IpcTypes.BakeEnded, BakeEnded = ending }).ConfigureAwait(false);
        } catch (Exception e) when (e is IOException or IpcException or TimeoutException) {
            // The Server has gone, and it hands this job out again when it is back.
        }
    }

    sealed record Running(BakeTicket Ticket, CancellationTokenSource Stop);

    /// <summary>Tells the Server, not often, and never holding up a casting thread while it does.</summary>
    sealed class Reporter(BakeTicket ticket, IpcConnection link) {
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

            _ = SendAsync(fraction);
        }

        async Task SendAsync(double fraction) {
            try {
                await link.SendAsync(new() { Type = IpcTypes.BakeProgress, BakeProgress = new(ticket, fraction) })
                    .ConfigureAwait(false);
            } catch (Exception e) when (e is IOException or IpcException or TimeoutException) {
                // A report nobody got. The link's own loss is what stops the bake.
            }
        }
    }
}
