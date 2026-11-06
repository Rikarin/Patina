using Patina.Graphs;
using Patina.Ipc;

namespace Patina.Worker;

/// <summary>
///     Runs one step of a build at a time, on a thread of its own, as <see cref="BakeHost" /> runs a bake: a block
///     compression can outlast any reply, so the IPC loop waits only for the start. The GPU part takes the device's
///     gate, and everything else is plain threads. Holds nothing a Server's plan cannot make again.
/// </summary>
sealed class BuildHost : OneAtATime {
    const int ReservedCores = 2;

    readonly OutputBuilders builders;

    public BuildHost(DeviceHost gpu) : base("building") {
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
                { Export: { } export } => new(ask.Ticket, null, OutputBuilders.Export(export, stop.Token), null),
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
}
