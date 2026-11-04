using Patina.Library;

namespace Patina.Server;

/// <summary>
///     The Server's open library. It opens at once and rescans behind the API, so a client gets the index as it
///     stands from the first request, and <c>indexing</c> says when that may lag the files. A folder without a
///     <c>library.yaml</c> is not an error: the Server still answers everything that needs no library.
/// </summary>
public sealed class LibraryHost(ServerIdentity identity, ILogger<LibraryHost> logger) : BackgroundService {
    readonly Lazy<OpenLibrary?> library = new(() => Open(identity.Library, logger));

    /// <summary>The open library, or null when there is none.</summary>
    public OpenLibrary? Library => library.Value;

    public override void Dispose() {
        if (library.IsValueCreated) {
            library.Value?.Dispose();
        }

        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        await Task.Yield(); // Start-up does not wait for a scan.
        if (Library is not { } open) {
            return;
        }

        try {
            var scan = await Task.Run(() => open.Rescan(stoppingToken), stoppingToken);
            ServerLog.LibraryIndexed(logger, scan.Files, scan.Changed, scan.Elapsed.TotalSeconds);
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // Shutting down: the transaction rolls back and the last good index stays.
        }
    }

    static OpenLibrary? Open(string? root, ILogger logger) {
        if (root is null || !OpenLibrary.Exists(root)) {
            return null;
        }

        try {
            return OpenLibrary.Open(root);
        } catch (LibraryFormatException e) {
            ServerLog.LibraryUnreadable(logger, e.Message);
            return null;
        }
    }
}
