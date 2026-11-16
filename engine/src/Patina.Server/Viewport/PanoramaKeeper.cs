using Patina.Contracts;
using Patina.Ipc;
using System.Threading.Channels;
using Vixen.NodeGraph;

namespace Patina.Server.Viewport;

/// <summary>
///     Draws a graph that lights the viewport again when it is saved or evaluated after an edit, so a sky being made is
///     seen on the model as it is edited (#180, #300). Nothing it holds is state: the display says which graph lights
///     the model, a change to any other graph is passed by, and only the newest change waits to be drawn, so a drag
///     costs a drawing at a time rather than one per step.
/// </summary>
public sealed class PanoramaKeeper(ViewportSession viewport, Thumbnails thumbnails, ILogger<PanoramaKeeper> logger)
    : BackgroundService {
    readonly Channel<bool> wake = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true }
    );

    readonly Lock changing = new();

    /// <summary>The newest change waiting to be drawn: its file, and its document unless the file holds it.</summary>
    (string File, NodeGraphAsset? Document)? next;

    /// <summary>Says a graph reached its file, which redraws it if it lights the viewport.</summary>
    /// <param name="file">The graph's whole path.</param>
    public void Saved(string file) => Ask(file, null);

    /// <summary>
    ///     Says an open graph evaluated a document, saved or not, which redraws it from that document if it lights the
    ///     viewport.
    /// </summary>
    /// <param name="file">The graph's whole path.</param>
    /// <param name="document">The document it evaluated.</param>
    public void Evaluated(string file, NodeGraphAsset document) {
        ArgumentNullException.ThrowIfNull(document);
        Ask(file, document);
    }

    void Ask(string file, NodeGraphAsset? document) {
        var lighting = viewport.State.Display.Environment;
        // A graph that ships with Patina has no path of its own, and lights nothing.
        if (!PictureFormats.IsEnvironmentGraph(lighting)
            || !Path.IsPathFullyQualified(file)
            || !Same(lighting, file)) {
            return;
        }

        lock (changing) {
            next = (lighting, document);
        }

        wake.Writer.TryWrite(true);
    }

    /// <summary>Whether two names are one file, as the machine's own file system compares them.</summary>
    static bool Same(string one, string other) =>
        string.Equals(
            Path.GetFullPath(one),
            Path.GetFullPath(other),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
        );

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ServiceLoop.RunAsync(logger, nameof(PanoramaKeeper), LoopAsync, stoppingToken);

    async Task LoopAsync(CancellationToken stoppingToken) {
        await foreach (var _ in wake.Reader.ReadAllAsync(stoppingToken)) {
            (string File, NodeGraphAsset? Document)? taken;
            lock (changing) {
                taken = next;
                next = null;
            }

            if (taken is not var (file, document)) {
                continue;
            }

            try {
                var panorama = document is null
                    ? await thumbnails.PanoramaAsync(file, stoppingToken)
                    : await thumbnails.PanoramaAsync(file, document, stoppingToken);
                if (panorama is not null) {
                    viewport.Relight(file, panorama);
                }
            } catch (Exception e) when (e is IOException
                                            or TimeoutException
                                            or UnauthorizedAccessException
                                            or IpcException) {
                // The model stays lit as it was; the next change or choosing the graph again draws it again.
                ServerLog.ViewportFailed(logger, e.Message);
            }
        }
    }
}
