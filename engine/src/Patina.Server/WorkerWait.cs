using Patina.Server.Sessions;

namespace Patina.Server;

/// <summary>
///     How long a wait on the Worker lasts (#275). A Worker that is connected is working through what it was sent,
///     however slow its device: a software GPU takes minutes over a large stack, and one whose device stops moving ends
///     itself (<see cref="Patina.Ipc.DeviceWatch" />). So a wait gives up only when no Worker has been connected for the
///     whole of its patience, never because the one connected is slow.
/// </summary>
public static class WorkerWait {
    /// <summary>Waits until nothing is pending, looking again at each of the session's events.</summary>
    /// <param name="session">What is waited on, whose events say something may have changed.</param>
    /// <param name="pending">What is still to be done, or null once it is.</param>
    /// <param name="connected">Completes when a Worker is connected, at once when one already is.</param>
    /// <param name="patience">How long to wait for a Worker, and how often to ask again whether one is there.</param>
    /// <param name="cancellationToken">Ends the wait.</param>
    /// <typeparam name="TEvent">What the session's events are.</typeparam>
    /// <exception cref="RendererMissingException">No Worker was connected for the whole of the patience.</exception>
    public static async Task UntilAsync<TEvent>(
        IEventSource<TEvent> session,
        Func<object?> pending,
        Func<CancellationToken, Task> connected,
        TimeSpan patience,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(connected);
        var sequence = session.Sequence;
        while (pending() is not null) {
            using var turn = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            turn.CancelAfter(patience);
            try {
                await connected(turn.Token);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                throw new RendererMissingException("No GPU process came to reach this.");
            }

            try {
                await session.WaitAsync(sequence, turn.Token);
                sequence = session.Sequence;
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                // Nothing moved in a while: look again, and ask again whether a Worker is there.
            }
        }
    }
}
