using Patina.Contracts;
using Patina.Ipc;

namespace Patina.Server.Viewport;

/// <summary>What the viewport asks the Worker about the open mesh without drawing: picks, marquees and casts.</summary>
public sealed partial class ViewportSession {
    /// <summary>What is under a position on the picture, through the Worker.</summary>
    /// <exception cref="IOException">The Worker went.</exception>
    /// <exception cref="ViewportNoMeshException">No mesh is open.</exception>
    /// <exception cref="IpcException">The mesh cannot be read.</exception>
    public async Task<ViewportPick> PickAsync(ViewportPickRequest request, CancellationToken cancellationToken) {
        var ask = Ask(request.X, request.Y, request.Camera, request.Width, request.Height, "pick");
        var reply = await AskWorkerAsync(new() { Type = IpcTypes.ViewportPick, Pick = ask }, cancellationToken);
        return reply.Picked!;
    }

    /// <summary>The named meshes that show inside a rectangle on the picture, through the Worker (#110).</summary>
    /// <exception cref="IOException">The Worker went.</exception>
    /// <exception cref="ViewportNoMeshException">No mesh is open.</exception>
    /// <exception cref="IpcException">The mesh cannot be read.</exception>
    public async Task<ViewportMarquee> MarqueeAsync(
        ViewportMarqueeRequest request,
        CancellationToken cancellationToken
    ) {
        var ask = Ask(request.X, request.Y, request.Camera, request.Width, request.Height, "select") with {
            To = new(request.ToX, request.ToY)
        };
        var reply = await AskWorkerAsync(new() { Type = IpcTypes.ViewportMarquee, Pick = ask }, cancellationToken);
        return reply.Marqueed!;
    }

    /// <summary>A question about a place on the picture, through the camera asked about or the viewport's.</summary>
    /// <exception cref="ViewportNoMeshException">No mesh is open.</exception>
    ViewportPickAsk Ask(double x, double y, ViewportCameraState? asked, int? across, int? down, string verb) {
        lock (changing) {
            var path = mesh ?? throw new ViewportNoMeshException($"Open a mesh first: there is nothing to {verb}.");
            var laidOut = layouts.Count == 0 ? null : new Dictionary<string, int>(layouts, StringComparer.Ordinal);
            return new(x, y, Lens(asked ?? camera, display.Display), across ?? width, down ?? height, path, laidOut);
        }
    }

    /// <summary>Where rays along one direction meet the open mesh, through the Worker, at once (#263).</summary>
    public async Task<ViewportCast> CastAsync(ViewportCastRequest request, CancellationToken cancellationToken) {
        string path;
        lock (changing) {
            path = mesh ?? throw new ViewportNoMeshException("Open a mesh first: there is nothing to cast at.");
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
