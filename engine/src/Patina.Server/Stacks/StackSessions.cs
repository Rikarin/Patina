using Patina.Server.Sessions;

namespace Patina.Server.Stacks;

/// <summary>
///     The open stacks. One file has one session, so two clients that open it share its edits and its
///     undo.
/// </summary>
public sealed class StackSessions(ServerIdentity identity, StackClips clips) : IDisposable {
    readonly SessionTable<StackSession> table = new();

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
            () => File.Exists(full)
                ? new StackSession(full, File.ReadAllText(full), table.Wake) { ImageRoots = LibraryRoot, Clips = clips }
                : throw new FileNotFoundException($"There is no stack at '{full}'.", full)
        );
    }

    public void Close(StackSession session) => table.Close(session);

    public IReadOnlyList<string> TakeClosed() => table.TakeClosed();

    public Task WaitAsync(CancellationToken cancellationToken) => table.WaitAsync(cancellationToken);
}
