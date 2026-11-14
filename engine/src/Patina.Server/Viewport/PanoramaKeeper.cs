using Patina.Contracts;
using System.Threading.Channels;

namespace Patina.Server.Viewport;

/// <summary>
///     Draws a graph that lights the viewport again when it is saved, so a sky being made is seen on the model as each
///     save lands (#180). Nothing it holds is state: the display says which graph lights the model, and a save of any
///     other graph is passed by.
/// </summary>
public sealed class PanoramaKeeper(ViewportSession viewport, Thumbnails thumbnails, ILogger<PanoramaKeeper> logger)
    : BackgroundService {
    readonly Channel<string> saved = Channel.CreateUnbounded<string>(new() { SingleReader = true });

    /// <summary>Says a graph reached its file, which redraws it if it lights the viewport.</summary>
    /// <param name="file">The graph's whole path.</param>
    public void Saved(string file) {
        var lighting = viewport.State.Display.Environment;
        if (PictureFormats.IsEnvironmentGraph(lighting) && Same(lighting, file)) {
            saved.Writer.TryWrite(lighting);
        }
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
        await foreach (var file in saved.Reader.ReadAllAsync(stoppingToken)) {
            try {
                if (await thumbnails.PanoramaAsync(file, stoppingToken) is { } panorama) {
                    viewport.Relight(file, panorama);
                }
            } catch (Exception e) when (e is IOException or TimeoutException or UnauthorizedAccessException) {
                // The model stays lit as it was; choosing the graph again draws it again.
                ServerLog.ViewportFailed(logger, e.Message);
            }
        }
    }
}
