using Patina.Contracts;

namespace Patina.Library.Tests;

/// <summary>
///     A read held open for a moment, as the library scan holds a file it hashes, let go by a thread of its own. The
///     file is open before this returns, so whatever the caller does next meets the read; and the release needs no
///     thread from the pool, which a busy test run can starve for longer than a move waits.
/// </summary>
static class HeldFile {
    /// <summary>Opens a file to read and lets it go after a moment.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The thread that lets it go, to join.</returns>
    public static Thread Open(string path) {
        var reading = SharedFile.OpenRead(path);
        Thread release = new(static state => {
                Thread.Sleep(150);
                ((IDisposable)state!).Dispose();
            }
        );
        release.Start(reading);
        return release;
    }
}
