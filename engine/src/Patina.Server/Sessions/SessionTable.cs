namespace Patina.Server.Sessions;

/// <summary>What a table needs of a session.</summary>
public interface IFileSession {
    string Id { get; }

    string Path { get; }

    void Close();
}

/// <summary>
///     The open sessions of one kind. One file has one session, so two clients that open it share its edits and its undo.
/// </summary>
/// <typeparam name="T">What a session is.</typeparam>
public sealed class SessionTable<T> : IDisposable where T : class, IFileSession {
    readonly Lock changing = new();
    readonly Dictionary<string, T> byId = new(StringComparer.Ordinal);
    readonly List<string> closed = [];
    readonly SemaphoreSlim dirty = new(0, 1);

    public IReadOnlyList<T> All {
        get {
            lock (changing) {
                return [..byId.Values];
            }
        }
    }

    public void Dispose() => dirty.Dispose();

    public T? Find(string id) {
        lock (changing) {
            return byId.GetValueOrDefault(id);
        }
    }

    /// <summary>The session that has a path open, or a new one.</summary>
    public T Open(string path, Func<T> open) {
        lock (changing) {
            if (byId.Values.FirstOrDefault(s => string.Equals(s.Path, path, StringComparison.Ordinal)) is { } held) {
                return held;
            }

            var session = open();
            byId[session.Id] = session;
            Wake();
            return session;
        }
    }

    /// <summary>Ends a session. The file stays as it was last saved.</summary>
    public void Close(T session) {
        lock (changing) {
            byId.Remove(session.Id);
            closed.Add(session.Id);
        }

        session.Close();
        Wake();
    }

    /// <summary>The sessions closed since the last call, for the Worker to forget.</summary>
    public IReadOnlyList<string> TakeClosed() {
        lock (changing) {
            string[] taken = [..closed];
            closed.Clear();
            return taken;
        }
    }

    /// <summary>Completes when something changed.</summary>
    public Task WaitAsync(CancellationToken cancellationToken) => dirty.WaitAsync(cancellationToken);

    public void Wake() {
        try {
            dirty.Release();
        } catch (SemaphoreFullException) {
            // Already awake: the loop reads every session, so one signal stands for any number of changes.
        }
    }
}
