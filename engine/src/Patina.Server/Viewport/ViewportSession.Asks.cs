using Patina.Contracts;
using Patina.Ipc;

namespace Patina.Server.Viewport;

/// <summary>What the viewport asks the Worker about the open mesh without drawing: picks and casts.</summary>
public sealed partial class ViewportSession {
    /// <summary>What is under a position on the picture, through the Worker.</summary>
    /// <exception cref="IOException">The Worker went.</exception>
    /// <exception cref="IpcException">No mesh is open, or it cannot be read.</exception>
    public async Task<ViewportPick> PickAsync(ViewportPickRequest request, CancellationToken cancellationToken) {
        ViewportCamera look;
        int across;
        int down;
        string path;
        Dictionary<string, int>? laidOut;
        lock (changing) {
            path = mesh ?? throw new IpcException("Open a mesh first: there is nothing to pick.");
            look = Lens(request.Camera ?? camera, display.Display);
            across = request.Width ?? width;
            down = request.Height ?? height;
            laidOut = layouts.Count == 0 ? null : new(layouts, StringComparer.Ordinal);
        }

        var reply = await AskWorkerAsync(
            new() { Type = IpcTypes.ViewportPick, Pick = new(request.X, request.Y, look, across, down, path, laidOut) },
            cancellationToken
        );
        return reply.Picked!;
    }

    /// <summary>Where rays along one direction meet the open mesh, through the Worker, at once (#263).</summary>
    public async Task<ViewportCast> CastAsync(ViewportCastRequest request, CancellationToken cancellationToken) {
        string path;
        lock (changing) {
            path = mesh ?? throw new IpcException("Open a mesh first: there is nothing to cast at.");
        }

        var cast = new IpcMessage {
            Type = IpcTypes.ViewportCast, Cast = new(request.Origins, request.Direction, path)
        };
        return (await AskWorkerAsync(cast, cancellationToken)).Casted!;
    }

    /// <summary>A question for the Worker that reads the mesh and no device: a pick or a cast.</summary>
    /// <exception cref="RendererMissingException">No Worker came in time.</exception>
    async Task<IpcMessage> AskWorkerAsync(IpcMessage request, CancellationToken cancellationToken) {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(PickPatience);
        IpcConnection connection;
        try {
            connection = await worker.ConnectionAsync(patience.Token);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new RendererMissingException();
        }

        return await connection.SendAsync(request, cancellationToken);
    }
}
