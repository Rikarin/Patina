using Patina.Ipc;

namespace Patina.Worker;

/// <summary>
///     What a bake and a build's step have in common: one at a time, on a thread of its own, so the IPC loop only ever
///     waits for the start, and stopped by a cancellation source, which is all that is held. The work is rebuilt from
///     what the Server has, so losing the link stops it.
/// </summary>
/// <param name="doing">What a refusal says this Worker is at.</param>
abstract class OneAtATime(string doing) : IDisposable {
    readonly Lock changing = new();
    readonly CancellationTokenSource closing = new();
    Running? running;

    /// <summary>Stops the sending of a last word when the Worker is going.</summary>
    protected CancellationToken Closing => closing.Token;

    /// <summary>Starts work, or refuses if some is running. The work must call <see cref="Ended" />.</summary>
    protected IpcMessage Begin(long request, BakeTicket ticket, Func<CancellationTokenSource, Task> work) {
        lock (changing) {
            if (running is not null) {
                return IpcMessage.Fail(request, $"This Worker is already {doing} {running.Ticket.Job}.");
            }

            var stop = new CancellationTokenSource();
            running = new(ticket, stop);
            _ = Task.Factory.StartNew(
                    () => work(stop),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default
                )
                .Unwrap();
        }

        return IpcMessage.Ok(request);
    }

    /// <summary>Stops the work named, if it is still what is running.</summary>
    public IpcMessage Cancel(IpcMessage request) {
        lock (changing) {
            if (running is { } work && work.Ticket == request.BakeCancel) {
                work.Stop.Cancel();
            }
        }

        return IpcMessage.Ok(request.Id);
    }

    /// <summary>Stops whatever is running.</summary>
    public void LinkLost() {
        lock (changing) {
            running?.Stop.Cancel();
        }
    }

    /// <summary>Says the work is over, so that the next can start.</summary>
    protected void Ended(CancellationTokenSource stop) {
        ArgumentNullException.ThrowIfNull(stop);
        lock (changing) {
            running = null;
        }

        stop.Dispose();
    }

    public void Dispose() {
        LinkLost();
        closing.Cancel();
        closing.Dispose();
        GC.SuppressFinalize(this);
    }

    sealed record Running(BakeTicket Ticket, CancellationTokenSource Stop);
}
