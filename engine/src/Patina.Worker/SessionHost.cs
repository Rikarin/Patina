using Patina.Ipc;

namespace Patina.Worker;

/// <summary>The sessions of one kind this Worker holds, all of them things the Server can send again.</summary>
/// <typeparam name="T">What a session is.</typeparam>
abstract class SessionHost<T> : IDisposable where T : class, IDisposable {
    readonly Dictionary<string, T> sessions = new(StringComparer.Ordinal);

    protected abstract string Kind { get; }

    public void Dispose() => LinkLost();

    /// <summary>Lets every session go: they were the Server's, and a Server tells a new link everything.</summary>
    public void LinkLost() {
        foreach (var session in sessions.Values) {
            session.Dispose();
        }

        sessions.Clear();
    }

    protected void Hold(string id, T session) {
        Forget(id);
        sessions[id] = session;
    }

    protected bool Holds(string id) => sessions.ContainsKey(id);

    /// <summary>Every session held.</summary>
    protected IEnumerable<T> Held => sessions.Values;

    protected T Find(string id) =>
        sessions.TryGetValue(id, out var session)
            ? session
            : throw new IpcException($"This Worker has not been told about {Kind} '{id}'.");

    protected void Forget(string id) {
        if (sessions.Remove(id, out var session)) {
            session.Dispose();
        }
    }
}
