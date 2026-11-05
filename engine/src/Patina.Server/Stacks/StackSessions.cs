using Patina.Library;

namespace Patina.Server.Stacks;

/// <summary>The open stacks. One file has one session, so two clients that open it share its edits and its undo.</summary>
public sealed class StackSessions(ServerIdentity identity) : IDisposable {
    readonly Lock changing = new();
    readonly Dictionary<string, StackSession> byId = new(StringComparer.Ordinal);
    readonly List<string> closed = [];
    readonly SemaphoreSlim dirty = new(0, 1);

    string[] LibraryRoot => identity.Library is { } library ? [library] : [];

    public IReadOnlyList<StackSession> All {
        get {
            lock (changing) {
                return [..byId.Values];
            }
        }
    }

    public void Dispose() => dirty.Dispose();

    public StackSession? Find(string id) {
        lock (changing) {
            return byId.GetValueOrDefault(id);
        }
    }

    /// <summary>A path a client gave, as a file.</summary>
    /// <exception cref="StackEditRefusedException">It is relative and no library holds it.</exception>
    public string Resolve(string path) {
        if (Path.IsPathRooted(path)) {
            return Path.GetFullPath(path);
        }

        return identity.Library is { } library
            ? Path.GetFullPath(path, library)
            : throw new StackEditRefusedException("A relative path needs a library to be relative to.");
    }

    /// <summary>Opens a file, or joins the session that has it open.</summary>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="StackEditRefusedException">The file is not a stack.</exception>
    public StackSession Open(string path) {
        var full = Resolve(path);
        lock (changing) {
            if (byId.Values.FirstOrDefault(s => string.Equals(s.Path, full, StringComparison.Ordinal)) is { } open) {
                return open;
            }

            if (!File.Exists(full)) {
                throw new FileNotFoundException($"There is no stack at '{full}'.", full);
            }

            var session = new StackSession(full, File.ReadAllText(full), Wake) { ImageRoots = LibraryRoot };
            byId[session.Id] = session;
            Wake();
            return session;
        }
    }

    public void Close(StackSession session) {
        session.Close();
        lock (changing) {
            byId.Remove(session.Id);
            closed.Add(session.Id);
        }

        Wake();
    }

    public IReadOnlyList<string> TakeClosed() {
        lock (changing) {
            string[] taken = [..closed];
            closed.Clear();
            return taken;
        }
    }

    public Task WaitAsync(CancellationToken cancellationToken) => dirty.WaitAsync(cancellationToken);

    void Wake() {
        if (dirty.CurrentCount == 0) {
            try {
                dirty.Release();
            } catch (SemaphoreFullException) {
                // Two wakes at once: the loop will look either way.
            }
        }
    }
}
