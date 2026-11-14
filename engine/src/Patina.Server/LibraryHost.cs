using Patina.Export;
using Patina.Library;
using System.Threading.Channels;

namespace Patina.Server;

/// <summary>
///     The Server's open library. It opens at once and rescans behind the API, so a client gets the index as it
///     stands from the first request, and <c>indexing</c> says when that may lag the files. A folder without a
///     <c>library.yaml</c> is not an error: the Server still answers everything that needs no library.
///     A write through the API asks for another scan the same way: the files change first, and the index catches up
///     behind it.
/// </summary>
public sealed class LibraryHost(ServerIdentity identity, ILogger<LibraryHost> logger, MeshSetCache meshes)
    : BackgroundService {
    readonly Lazy<OpenLibrary?> library = new(() => Open(identity.Library, logger, meshes));

    // One waiting request is enough, whatever else arrives: the scan it starts sees every write made before it starts.
    readonly Channel<bool> rescans = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true }
    );

    /// <summary>The open library, or null when there is none.</summary>
    public OpenLibrary? Library => library.Value;

    /// <summary>Asks the background scan to bring the index up to the files again.</summary>
    public void RequestRescan() => rescans.Writer.TryWrite(true);

    public override void Dispose() {
        if (library.IsValueCreated) {
            library.Value?.Dispose();
        }

        base.Dispose();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ServiceLoop.RunAsync(logger, nameof(LibraryHost), ScanningAsync, stoppingToken);

    async Task ScanningAsync(CancellationToken stoppingToken) {
        await Task.Yield(); // Start-up does not wait for a scan.
        if (Library is not { } open) {
            return;
        }

        await ScanAsync(open, stoppingToken);
        await foreach (var _ in rescans.Reader.ReadAllAsync(stoppingToken)) {
            await ScanAsync(open, stoppingToken);
        }
    }

    async Task ScanAsync(OpenLibrary open, CancellationToken stoppingToken) {
        var scan = await Task.Run(() => open.Rescan(stoppingToken), stoppingToken);
        ServerLog.LibraryIndexed(logger, scan.Files, scan.Changed, scan.Elapsed.TotalSeconds);
    }

    static OpenLibrary? Open(string? root, ILogger logger, MeshSetCache meshes) {
        if (root is null || !OpenLibrary.Exists(root)) {
            return null;
        }

        try {
            var library = OpenLibrary.Open(root);
            // The tiles are what the Worker read of each mesh: the Server reads none (#285).
            library.Tiles = (mesh, set, uvSet) => SetUdim.Of(meshes, mesh, set, uvSet);
            return library;
        } catch (LibraryFormatException e) {
            ServerLog.LibraryUnreadable(logger, e.Message);
            return null;
        }
    }
}
