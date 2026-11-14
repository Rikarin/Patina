using Patina.Contracts;

namespace Patina.Server.Sessions;

/// <summary>Something a client can follow: numbered events, and a way to wait for the next.</summary>
/// <typeparam name="TEvent">What an event is.</typeparam>
public interface IEventSource<out TEvent> {
    /// <summary>The number of the newest event.</summary>
    long Sequence { get; }

    /// <summary>The events after a number; a single resync event when they are no longer held.</summary>
    IReadOnlyList<TEvent> After(long since);

    /// <summary>Completes when there is an event after a number.</summary>
    Task WaitAsync(long since, CancellationToken cancellationToken);
}

/// <summary>
///     The numbered events of one session, and the signal that says there is another. A client that dropped its socket
///     catches up from the number it last saw. Not thread safe: the session locks.
/// </summary>
/// <typeparam name="TEvent">What an event is.</typeparam>
public sealed class EventLog<TEvent>(Func<TEvent, long> numberOf) {
    const int Capacity = 1024;

    readonly Queue<TEvent> log = new();
    TaskCompletionSource next = NewSignal();

    /// <summary>The number of the newest event.</summary>
    public long Sequence { get; private set; }

    public void Append(TEvent item) {
        Sequence = numberOf(item);
        log.Enqueue(item);
        if (log.Count > Capacity) {
            log.Dequeue();
        }

        var signal = next;
        next = NewSignal();
        signal.TrySetResult();
    }

    /// <summary>The events after a number, or nothing when they cannot be given: the caller then says resync.</summary>
    public IReadOnlyList<TEvent>? After(long since) {
        var oldest = log.Count > 0 ? numberOf(log.Peek()) : Sequence + 1;
        return since > Sequence || since < oldest - 1 ? null : [..log.Where(e => numberOf(e) > since)];
    }

    public Task WaitAsync(long since, CancellationToken cancellationToken) =>
        Sequence > since ? Task.CompletedTask : next.Task.WaitAsync(cancellationToken);

    static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>What a session of any kind is to its followers: a lock, and numbered events under it.</summary>
/// <typeparam name="TEvent">What an event is.</typeparam>
public abstract class FollowedSession<TEvent>(Func<TEvent, long> numberOf) : IEventSource<TEvent> {
    protected Lock Changing { get; } = new();

    protected EventLog<TEvent> Log { get; } = new(numberOf);

    /// <inheritdoc />
    public long Sequence {
        get {
            lock (Changing) {
                return Log.Sequence;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<TEvent> After(long since) {
        lock (Changing) {
            return Log.After(since) ?? [Resync()];
        }
    }

    /// <inheritdoc />
    public Task WaitAsync(long since, CancellationToken cancellationToken) {
        lock (Changing) {
            return Log.WaitAsync(since, cancellationToken);
        }
    }

    /// <summary>The event that says the ones asked for are gone.</summary>
    protected abstract TEvent Resync();

    /// <summary>
    ///     Tells every follower how autosave went (#192): kept, so a crash now costs only later edits, or not, and why.
    ///     Neither changes the document or its history.
    /// </summary>
    /// <param name="failure">Why it could not keep the edits, or null when it did.</param>
    public void Autosaved(string? failure) {
        lock (Changing) {
            Log.Append(Noted(failure is null ? GraphEventKind.Autosaved : GraphEventKind.AutosaveFailed, failure));
        }
    }

    /// <summary>The next event, of a kind that says something and changes nothing, with what it says.</summary>
    protected abstract TEvent Noted(GraphEventKind kind, string? label);
}
