using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Graphs;
using Patina.Ipc;

namespace Patina.Worker;

/// <summary>
///     Runs one step of a build at a time, on a thread of its own, as <see cref="BakeHost" /> runs a bake: a block
///     compression can outlast any reply, so the IPC loop waits only for the start. The GPU part takes the device's gate,
///     and everything else is plain threads. Holds nothing a Server's plan cannot make again.
/// </summary>
sealed class BuildHost : IDisposable {
    const int ReservedCores = 2;

    readonly Lock changing = new();
    readonly CancellationTokenSource closing = new();
    readonly OutputBuilders builders;
    Running? running;

    public BuildHost(DeviceHost gpu) {
        builders = new(
            () => gpu.Evaluator,
            work => {
                lock (gpu.Gate) {
                    return work();
                }
            }
        );
    }

    /// <summary>Starts a step. Refuses a second: the Server hands out one at a time.</summary>
    public IpcMessage Start(IpcMessage request, IpcConnection link) {
        var ask = request.BuildStart ?? throw new IpcException("build.start says nothing to do.");
        lock (changing) {
            if (running is not null) {
                return IpcMessage.Fail(request.Id, $"This Worker is already building for {running.Ticket.Job}.");
            }

            var stop = new CancellationTokenSource();
            running = new(ask.Ticket, stop);
            _ = Task.Factory.StartNew(
                    () => RunAsync(ask, link, stop, closing.Token),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default
                )
                .Unwrap();
        }

        return IpcMessage.Ok(request.Id);
    }

    /// <summary>Stops the step named, if it is still the one running.</summary>
    public IpcMessage Cancel(IpcMessage request) {
        lock (changing) {
            if (running is { } step && step.Ticket == request.BakeCancel) {
                step.Stop.Cancel();
            }
        }

        return IpcMessage.Ok(request.Id);
    }

    /// <summary>Stops whatever is running: the link has gone, and the Server plans again when it is back.</summary>
    public void LinkLost() {
        lock (changing) {
            running?.Stop.Cancel();
        }
    }

    public void Dispose() {
        LinkLost();
        closing.Cancel();
        closing.Dispose();
    }

    async Task RunAsync(
        BuildStart ask,
        IpcConnection link,
        CancellationTokenSource stop,
        CancellationToken cancellationToken
    ) {
        BuildEnding ending;
        try {
            var threads = Math.Max(1, Environment.ProcessorCount - ReservedCores);
            ending = ask switch {
                { Output: { } output } => new(ask.Ticket, builders.Build(output, threads, stop.Token), null, null),
                { Export: { } export } => new(ask.Ticket, null, OutputBuilders.Export(export, stop.Token), null),
                _ => new(ask.Ticket, null, null, "build.start says nothing to do.")
            };
        } catch (OperationCanceledException) {
            ending = new(ask.Ticket, null, null, null);
        } catch (Exception e) when (e is BuildStepFailedException
                                        or IOException
                                        or UnauthorizedAccessException
                                        or OutOfMemoryException
                                        or IpcException
                                       ) {
            ending = new(ask.Ticket, null, null, e.Message);
        }

        lock (changing) {
            running = null;
        }

        stop.Dispose();
        try {
            await link.SendAsync(new() { Type = IpcTypes.BuildEnded, BuildEnded = ending }, cancellationToken)
                .ConfigureAwait(false);
        } catch (Exception e) when (e is IOException
                                        or IpcException
                                        or TimeoutException
                                        or OperationCanceledException) {
            // The Server has gone, and it plans again when it is back.
        }
    }

    sealed record Running(BakeTicket Ticket, CancellationTokenSource Stop);
}
