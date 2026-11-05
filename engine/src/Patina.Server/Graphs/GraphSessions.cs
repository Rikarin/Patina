using Patina.Contracts;
using Patina.Library;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Server.Graphs;

/// <summary>
///     The open graphs. One file has one session, so two clients that open it share its edits and its undo.
/// </summary>
public sealed class GraphSessions(ServerIdentity identity) : IDisposable {
    /// <summary>What a compound that ships with Patina is opened by. No file holds one.</summary>
    public const string ShippedPrefix = "shipped:";

    readonly Lock changing = new();
    readonly Dictionary<string, GraphSession> byId = new(StringComparer.Ordinal);
    readonly List<string> closed = [];
    readonly SemaphoreSlim dirty = new(0, 1);

    string[] LibraryRoot => identity.Library is { } library ? [library] : [];

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
        var shipped = path.StartsWith(ShippedPrefix, StringComparison.Ordinal);
        var full = shipped ? path : Resolve(path);
        lock (changing) {
            if (byId.Values.FirstOrDefault(s => string.Equals(s.Path, full, StringComparison.Ordinal)) is { } open) {
                return open;
            }

            var session = shipped ? OpenShipped(path) : OpenFile(full);
            byId[session.Id] = session;
            Wake();
            return session;
        }
    }

    /// <summary>What a graph could hold: every shipped compound and its own folder's, as a client opens them.</summary>
    public static IReadOnlyList<GraphCompound> Compounds(string? folder) {
        List<GraphCompound> compounds = [
            ..TextureCompoundLibrary.Shipped.Select(static type => new GraphCompound(type, ShippedPrefix + type, true))
        ];
        HashSet<string> shipped = [..TextureCompoundLibrary.Shipped];
        if (folder is not null && Directory.Exists(folder)) {
            foreach (var file in Directory.EnumerateFiles(
                         folder,
                         $"*{TextureCompoundLibrary.Extension}",
                         SearchOption.AllDirectories
                     )) {
                var type = Path.ChangeExtension(Path.GetRelativePath(folder, file), null)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (!shipped.Contains(type)) {
                    compounds.Add(new(type, file, false));
                }
            }
        }

        compounds.Sort(static (a, b) => string.CompareOrdinal(a.Type, b.Type));
        return compounds;
    }

    /// <summary>Gives a document's boundary nodes their ports, which only its own interface declares.</summary>
    public static void AddBoundaries(NodeTypeRegistry registry, NodeGraphAsset document) {
        if (document.Interface.Length == 0) {
            return;
        }

        var graph = NodeGraphDocument.Load(document with { Nodes = [], Edges = [], Groups = [] }, out _);
        foreach (var type in new[] { SubGraphs.InputType, SubGraphs.OutputType }) {
            if (!registry.TryGet(type, out _)) {
                registry.Add(SubGraphs.Boundary(graph, type));
            }
        }
    }

    /// <summary>A compound was saved, so every graph that reads its folder takes the node types again.</summary>
    public void Republish(GraphSession saved) {
        if (saved.Folder is not { } folder || !CompoundFolders.Holds(saved.Path)) {
            return;
        }

        foreach (var session in All.Where(s => s != saved && string.Equals(s.Folder, folder, StringComparison.Ordinal)
                 )) {
            session.Republish(Types(folder));
        }
    }

    string Resolve(string path) =>
        Path.GetFullPath(identity.Library is { } library ? Path.Combine(library, path) : path);

    static NodeTypeRegistry Types(string? folder) {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        TextureCompoundLibrary.Publish(registry, folder, out _);
        return registry;
    }

    GraphSession OpenShipped(string path) {
        var type = path[ShippedPrefix.Length..];
        var text = TextureCompoundLibrary.Shipped.Contains(type) ? TextureCompoundLibrary.Source(type) : null;
        return text is null
            ? throw new FileNotFoundException($"No compound called '{type}' ships with Patina.", path)
            : new(path, text, Types(null), null, Wake, true) { ImageRoots = LibraryRoot };
    }

    GraphSession OpenFile(string full) {
        if (!File.Exists(full)) {
            throw new FileNotFoundException($"There is no file at '{full}'.", full);
        }

        // The same folder `patina bake` reads, so a graph means the same in both.
        var folder = CompoundFolders.Of(full);
        return new(full, File.ReadAllText(full), Types(folder), folder, Wake) {
            ImageRoots = CompoundFolders.PictureRoots(full, identity.Library)
        };
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
