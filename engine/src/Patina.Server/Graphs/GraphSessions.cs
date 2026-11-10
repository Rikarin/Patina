using Patina.Contracts;
using Patina.Library;
using Patina.Server.Sessions;
using System.Runtime.CompilerServices;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Server.Graphs;

/// <summary>
///     The open graphs. One file has one session, so two clients that open it share its edits and its undo.
/// </summary>
public sealed class GraphSessions(ServerIdentity identity) : IDisposable {
    /// <summary>What a compound that ships with Patina is opened by. No file holds one.</summary>
    public const string ShippedPrefix = "shipped:";

    readonly SessionTable<GraphSession> table = new();

    string[] LibraryRoot => identity.Library is { } library ? [library] : [];

    public IReadOnlyList<GraphSession> All => table.All;

    public void Dispose() => table.Dispose();

    public GraphSession? Find(string id) => table.Find(id);

    /// <summary>Opens a file, or joins the session that has it open.</summary>
    /// <param name="path">Absolute, or relative to the library.</param>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="GraphEditRefusedException">The file is not a graph.</exception>
    public GraphSession Open(string path) {
        var shipped = path.StartsWith(ShippedPrefix, StringComparison.Ordinal);
        var full = shipped ? path : Resolve(path);
        return table.Open(full, () => shipped ? OpenShipped(path) : OpenFile(full));
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

    /// <summary>A compound was saved, so every graph that reads its folder takes the node types again.</summary>
    public void Republish(GraphSession saved) {
        if (saved.Folder is { } folder && CompoundFolders.Holds(saved.Path)) {
            Republish(folder, saved);
        }
    }

    /// <summary>A folder's compounds changed, so every graph that reads it takes the node types again.</summary>
    /// <param name="folder">The compounds folder.</param>
    /// <param name="except">A session that has already taken them.</param>
    public void Republish(string folder, GraphSession? except = null) {
        foreach (var session in All.Where(s => s != except && string.Equals(s.Folder, folder, StringComparison.Ordinal)
                 )) {
            session.Republish(Types(folder));
        }
    }

    /// <summary>Saves some of a graph's nodes as a new compound in the folder it reads.</summary>
    /// <exception cref="GraphEditRefusedException">It has no such folder, or the nodes or name will not do.</exception>
    public GraphExtraction Extract(GraphSession session, GraphExtractRequest request) {
        if (session.Folder is not { } folder) {
            throw new GraphEditRefusedException("This graph has no compounds folder to save into.");
        }

        var type = request.Name.Trim();
        RefuseShipped(type);
        var extraction = session.Extract(
            request,
            text => {
                try {
                    return (CompoundFolders.Create(folder, type, text), Types(folder));
                } catch (ProjectRefusedException e) {
                    throw new GraphEditRefusedException(e.Message, e);
                }
            }
        );
        Republish(folder, session);
        return extraction;
    }

    /// <summary>Copies a shipped compound into a folder as a file, so that it can be edited.</summary>
    /// <param name="request">Which one, and what to call the copy.</param>
    /// <param name="folder">The compounds folder that takes it.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="FileNotFoundException">No such compound ships.</exception>
    /// <exception cref="ProjectRefusedException">The name will not do, or the file is taken.</exception>
    /// <exception cref="GraphEditRefusedException">The name is a shipped compound's.</exception>
    public GraphCompound Copy(GraphCompoundCopy request, string folder) {
        var text = TextureCompoundLibrary.Shipped.Contains(request.Type)
            ? TextureCompoundLibrary.Source(request.Type)
            : null;
        if (text is null) {
            throw new FileNotFoundException($"No compound called '{request.Type}' ships with Patina.", request.Type);
        }

        var type = (request.Name ?? $"{request.Type} Copy").Trim();
        RefuseShipped(type);
        var document = GraphDocuments.Parse(text) with { Name = type.Split('/')[^1] };
        var file = CompoundFolders.Create(folder, type, GraphDocuments.Text(GraphDocuments.Canonical(document)));
        Republish(folder);
        return new(type, file, false);
    }

    /// <summary>Vixen refuses a file that shadows a shipped compound, and saying so here beats a lost node.</summary>
    static void RefuseShipped(string type) {
        if (TextureCompoundLibrary.Shipped.Contains(type)) {
            throw new GraphEditRefusedException($"'{type}' ships with Patina. A copy needs a name of its own.");
        }
    }

    string Resolve(string path) =>
        Path.GetFullPath(identity.Library is { } library ? Path.Combine(library, path) : path);

    static NodeTypeRegistry Types(string? folder) {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        PublishedWith.AddOrUpdate(registry, TextureCompoundLibrary.Publish(registry, folder, out _));
        return registry;
    }

    /// <summary>The graphs behind the compounds a registry was published with, which hold their presets.</summary>
    static readonly ConditionalWeakTable<NodeTypeRegistry, ISubGraphSource> PublishedWith = [];

    /// <summary>Each compound's presets, by node type, for the node types a session offers.</summary>
    public static IReadOnlyList<GraphPresetState> PresetsOf(NodeTypeRegistry registry, string type) =>
        PublishedWith.TryGetValue(registry, out var source) && source.TryGet(type, out var graph)
            ? [..graph.Presets.Select(GraphDocuments.State)]
            : [];

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

        // The same folder `patina bake` reads, so a graph means the same in both. A package's graph opens to be read,
        // as a shipped compound does: it is offered by its parameters, and a republish would replace any edit (ADR 0027).
        var folder = CompoundFolders.Of(full);
        var packaged = PackageManifests.PackageOf(full) is not null;
        return new(full, SharedFile.ReadAllText(full), Types(folder), folder, Wake, packaged) {
            ImageRoots = CompoundFolders.PictureRoots(full, identity.Library)
        };
    }

    /// <summary>Ends a session. The file stays as it was last saved.</summary>
    public void Close(GraphSession session) => table.Close(session);

    /// <summary>The sessions closed since the last call, for the Worker to forget.</summary>
    public IReadOnlyList<string> TakeClosed() => table.TakeClosed();

    /// <summary>Completes when something changed.</summary>
    public Task WaitAsync(CancellationToken cancellationToken) => table.WaitAsync(cancellationToken);

    void Wake() => table.Wake();
}
