using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Server.Graphs;

/// <summary>
///     The open graphs. One file has one session, so two clients that open it share its edits and its undo.
/// </summary>
public sealed class GraphSessions(ServerIdentity identity) : IDisposable {
    readonly Lock changing = new();
    readonly Dictionary<string, GraphSession> byId = new(StringComparer.Ordinal);
    readonly List<string> closed = [];
    readonly SemaphoreSlim dirty = new(0, 1);

    public IReadOnlyList<GraphSession> All {
        get {
            lock (changing) {
                return [..byId.Values];
            }
        }
    }

    public void Dispose() => dirty.Dispose();

    public GraphSession? Find(string id) {
        lock (changing) {
            return byId.GetValueOrDefault(id);
        }
    }

    /// <summary>Opens a file, or joins the session that has it open.</summary>
    /// <param name="path">Absolute, or relative to the library.</param>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="GraphEditRefusedException">The file is not a graph.</exception>
    public GraphSession Open(string path) {
        var full = Path.GetFullPath(identity.Library is { } library ? Path.Combine(library, path) : path);
        lock (changing) {
            if (byId.Values.FirstOrDefault(s => string.Equals(s.Path, full, StringComparison.Ordinal)) is { } open) {
                return open;
            }

            if (!File.Exists(full)) {
                throw new FileNotFoundException($"There is no file at '{full}'.", full);
            }

            // The same folder `patina bake` reads, so a graph means the same in both.
            var folder = Path.GetDirectoryName(full);
            NodeTypeRegistry registry = new();
            NodeTypes.Register(registry);
            TextureCompoundLibrary.Publish(registry, folder, out _);
            var session = new GraphSession(full, File.ReadAllText(full), registry, folder, Wake);
            byId[session.Id] = session;
            Wake();
            return session;
        }
    }

    /// <summary>Ends a session. The file stays as it was last saved.</summary>
    public void Close(GraphSession session) {
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

    void Wake() {
        try {
            dirty.Release();
        } catch (SemaphoreFullException) {
            // Already awake: the loop reads every session, so one signal stands for any number of changes.
        }
    }
}
