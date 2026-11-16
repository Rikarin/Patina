using Patina.Server.Stacks;

namespace Patina.Server.Tests;

/// <summary>
///     The Server opens a set nobody has open for its own use, such as measuring it for a project's checks, and closes
///     it after, never a session a client holds (ADR 0043).
/// </summary>
public sealed class StackBorrowTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-borrow-");
    readonly StackSessions sessions;

    public StackBorrowTests() {
        ServerIdentity identity = new(null, null);
        sessions = new(identity, new(identity));
    }

    public void Dispose() {
        sessions.Dispose();
        folder.Delete(true);
    }

    string Made(string name) {
        var path = Path.Combine(folder.FullName, name);
        StackDocuments.Create(path, 64, 64);
        return path;
    }

    /// <summary>A borrowed set is open while it is measured, and closed when it is given back.</summary>
    [Fact]
    public void A_borrowed_set_is_closed_when_it_is_given_back() {
        var borrowed = sessions.Borrow(Made("Body.vxlayers"));

        Assert.NotNull(borrowed);
        Assert.Same(borrowed, Assert.Single(sessions.All));
        sessions.GiveBack(borrowed);
        Assert.Empty(sessions.All);
        Assert.Equal([borrowed.Id], sessions.TakeClosed());
    }

    /// <summary>A set a client has open is never borrowed, so giving nothing back closes nothing of theirs.</summary>
    [Fact]
    public void A_set_a_client_has_open_is_not_borrowed() {
        var path = Made("Body.vxlayers");
        var open = sessions.Open(path);

        Assert.Null(sessions.Borrow(path));
        Assert.Same(open, Assert.Single(sessions.All));
    }

    /// <summary>A client that opens a set while the Server has it borrowed keeps it after it is given back.</summary>
    [Fact]
    public void A_client_that_opens_a_borrowed_set_keeps_it() {
        var path = Made("Body.vxlayers");
        var borrowed = sessions.Borrow(path)!;

        var open = sessions.Open(path);
        sessions.GiveBack(borrowed);

        Assert.Same(borrowed, open);
        Assert.Same(open, Assert.Single(sessions.All));
        Assert.Empty(sessions.TakeClosed());
    }
}
