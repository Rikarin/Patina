using Patina.Export.Builds;
using Patina.Graphs;
using Patina.Ipc;

namespace Patina.Worker;

/// <summary>
///     Runs one step of a build at a time, on a thread of its own, as <see cref="BakeHost" /> runs a bake: a block
///     compression can outlast any reply, so the IPC loop waits only for the start. The GPU part takes the device's
///     gate, and everything else is plain threads. Holds nothing a Server's plan cannot make again.
/// </summary>
sealed class BuildHost(DeviceHost gpu) : OneAtATime("building") {
    const int ReservedCores = 2;

    readonly OutputBuilders builders = new(
        () => gpu.Evaluator,
        work => {
            lock (gpu.Gate) {
                return work();
            }
        }
    );

    /// <summary>Starts a step. Refuses a second: the Server hands out one at a time.</summary>
    public IpcMessage Start(IpcMessage request, IpcConnection link) {
        var ask = request.BuildStart ?? throw new IpcException("build.start says nothing to do.");
        return Begin(request.Id, ask.Ticket, stop => RunAsync(ask, link, stop, Closing));
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
                { Export: { } export } => new(
                    ask.Ticket,
                    null,
                    ExportSteps.Run(export, (fraction, file) => Tell(link, ask.Ticket, fraction, file), stop.Token),
                    null
                ),
                _ => new(ask.Ticket, null, null, "build.start says nothing to do.")
            };
        } catch (OperationCanceledException) {
            ending = new(ask.Ticket, null, null, null);
        } catch (Exception e) {
            // Whatever went wrong, the Server is waiting on this step and hears how it ended.
            ending = new(ask.Ticket, null, null, e.Message);
        }

        Ended(stop);
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

    /// <summary>
    ///     Says how far a step is, without waiting for the answer: a step must not stop because the Server is slow to
    ///     hear it, and a report nobody got changes nothing.
    /// </summary>
    static void Tell(IpcConnection link, BakeTicket ticket, double fraction, string file) =>
        _ = SendAsync(link, new() { Type = IpcTypes.BuildProgress, BuildProgress = new(ticket, fraction, file) });

    static async Task SendAsync(IpcConnection link, IpcMessage message) {
        try {
            await link.SendAsync(message, CancellationToken.None).ConfigureAwait(false);
        } catch (Exception e) when (e is IOException
                                        or IpcException
                                        or TimeoutException
                                        or OperationCanceledException) {
            // A report nobody got. The link's own loss is what stops the step.
        }
    }
}
