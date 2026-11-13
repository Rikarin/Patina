using Patina.Contracts;
using Patina.Server.Sessions;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     The open stacks. One file has one session, so two clients that open it share its edits and its
///     undo.
/// </summary>
public sealed class StackSessions(ServerIdentity identity, StackClips clips) : IDisposable {
    readonly SessionTable<StackSession> table = new();

    /// <summary>The texture sets of stacks nobody has open, by file, until the file changes.</summary>
    readonly Dictionary<string, (DateTime Written, TextureSetAsset? Set)> files = new(StringComparer.Ordinal);

    string[] LibraryRoot => identity.Library is { } library ? [library] : [];

    public IReadOnlyList<StackSession> All => table.All;

    public void Dispose() => table.Dispose();

    public StackSession? Find(string id) => table.Find(id);

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
        return table.Open(
            full,
            () => {
                if (!File.Exists(full)) {
                    throw new FileNotFoundException($"There is no stack at '{full}'.", full);
                }

                StackSession session = new(full, SharedFile.ReadAllText(full), table.Wake) {
                    ImageRoots = LibraryRoot, Clips = clips, Changed = CatchUp
                };
                session.Siblings = name => Sibling(session, name);
                return session;
            }
        );
    }

    /// <summary>
    ///     Another texture set of the model, by name: a stack beside this one, as its open session has it or as its
    ///     file says. Sets of one model are documents of one project, which sit together.
    /// </summary>
    TextureSetAsset? Sibling(StackSession asking, string name) =>
        Siblings(asking).FirstOrDefault(set => set.Name == name);

    /// <summary>
    ///     The model's other texture sets: each stack beside this one, as its open session has it or as its file says,
    ///     the open ones first.
    /// </summary>
    public IEnumerable<TextureSetAsset> Siblings(StackSession asking) {
        ArgumentNullException.ThrowIfNull(asking);
        return SiblingsOf(asking);
    }

    IEnumerable<TextureSetAsset> SiblingsOf(StackSession asking) {
        foreach (var open in table.All) {
            if (open != asking && open.Folder == asking.Folder && open.Published is { } set) {
                yield return set;
            }
        }

        if (asking.Folder is not { } folder || !Directory.Exists(folder)) {
            yield break;
        }

        HashSet<string> held = new(table.All.Select(static session => session.Path), StringComparer.Ordinal) {
            asking.Path
        };
        foreach (var file in Directory.EnumerateFiles(folder, $"*{StackDocuments.Extension}")) {
            if (!held.Contains(file) && Read(file) is { } set) {
                yield return set;
            }
        }
    }

    /// <summary>A stack's set from its file, kept until the file changes.</summary>
    TextureSetAsset? Read(string file) {
        var written = File.GetLastWriteTimeUtc(file);
        lock (files) {
            if (files.TryGetValue(file, out var held) && held.Written == written) {
                return held.Set;
            }
        }

        TextureSetAsset? set;
        try {
            set = LayerStackEdit.SetFor(StackDocuments.Parse(SharedFile.ReadAllText(file)), string.Empty);
        } catch (Exception e) when (e is IOException or StackEditRefusedException) {
            set = null;
        }

        lock (files) {
            files[file] = (written, set);
        }

        return set;
    }

    /// <summary>
    ///     An edit of one set reaches every instance of its layers in another, open in a session of its own.
    /// </summary>
    void CatchUp(StackSession changed) {
        foreach (var other in table.All.Where(other => other != changed && other.ReadsOtherSets)) {
            other.Evaluate();
        }
    }

    public void Close(StackSession session) => table.Close(session);

    public IReadOnlyList<string> TakeClosed() => table.TakeClosed();

    public Task WaitAsync(CancellationToken cancellationToken) => table.WaitAsync(cancellationToken);
}
