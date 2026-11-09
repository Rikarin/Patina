using Patina.Contracts;
using Patina.Ipc;

namespace Patina.Server.Viewport;

/// <summary>
///     The bake setup the viewport shows (#149), for every client and for a new Worker. The Server holds what was asked
///     and the key the Worker answered with; the Worker holds the shapes, which it makes again when it is told again.
/// </summary>
/// <remarks>
///     Its own state beside <see cref="ViewportSession" /> rather than inside it: a preview lies over whatever the
///     viewport shows and changes nothing about it, so the session only has to tell a new Worker and draw again.
/// </remarks>
public sealed class BakePreviewSession(WorkerLink worker) : IDisposable {
    static readonly TimeSpan WorkerPatience = TimeSpan.FromSeconds(10);

    readonly Lock changing = new();
    readonly SemaphoreSlim telling = new(1, 1);
    ViewportBakePreview? preview;
    BakePreviewState state = BakePreviewState.Off;
    long version;
    (IpcConnection? Connection, long Version) told;

    public void Dispose() => telling.Dispose();

    /// <summary>What is shown, and the key to it.</summary>
    public BakePreviewState State {
        get {
            lock (changing) {
                return state;
            }
        }
    }

    /// <summary>Shows a bake's setup, or takes it off for an empty low mesh, and answers with the key.</summary>
    /// <exception cref="RendererMissingException">No GPU process connected in time.</exception>
    /// <exception cref="IpcException">The Worker could not read the meshes or show them.</exception>
    public async Task<BakePreviewState> ShowAsync(ViewportBakePreview asked, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(asked);
        lock (changing) {
            preview = asked.Low.Length == 0 ? null : asked;
            version++;
            if (preview is null) {
                state = BakePreviewState.Off;
            }
        }

        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(WorkerPatience);
        IpcConnection connection;
        try {
            connection = await worker.ConnectionAsync(patience.Token);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new RendererMissingException();
        }

        await TellAsync(connection, true, cancellationToken);
        return State;
    }

    /// <summary>Tells a Worker the preview when it has not heard this one: a new Worker has heard nothing.</summary>
    /// <param name="connection">The Worker's link.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <remarks>A Worker that cannot show it costs the preview and not the frame: the reason goes in the key.</remarks>
    public Task TellAsync(IpcConnection connection, CancellationToken cancellationToken) =>
        TellAsync(connection, false, cancellationToken);

    async Task TellAsync(IpcConnection connection, bool throwing, CancellationToken cancellationToken) {
        await telling.WaitAsync(cancellationToken);
        try {
            ViewportBakePreview? showing;
            long asked;
            lock (changing) {
                (showing, asked) = (preview, version);
                // A Worker that never heard of a preview has none to take off.
                if (told == (connection, asked) || (showing is null && told.Connection != connection && asked == 0)) {
                    return;
                }
            }

            try {
                var reply = await connection.SendAsync(
                    new() { Type = IpcTypes.ViewportBakePreview, BakePreview = showing ?? new() },
                    cancellationToken
                );
                lock (changing) {
                    if (version == asked) {
                        state = reply.BakePreviewShown ?? BakePreviewState.Off;
                    }
                }
                told = (connection, asked);
            } catch (Exception e) when (e is IpcException || (e is TimeoutException && !throwing)) {
                // ⚠ A Worker too slow to answer is told once and not on every frame, or the viewport would wait on
                // the same meshes for ever; a client asking again tells it again.
                lock (changing) {
                    state = new(showing, [], [e.Message]);
                }

                told = (connection, asked);
                if (throwing) {
                    throw;
                }
            }
        } finally {
            telling.Release();
        }
    }
}
