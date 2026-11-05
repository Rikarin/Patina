using Patina.Contracts;
using Patina.Ipc;
using System.Diagnostics;

namespace Patina.Worker;

/// <summary>Stays connected to the Server until stopped.</summary>
sealed class WorkerLoop(ViewportHost viewport, GraphHost graphs, BakeHost bakes) {
    static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    static readonly long Started = Stopwatch.GetTimestamp();

    /// <summary>
    ///     Keeps a link to the Server, reconnecting when it drops. <paramref name="log" /> gets one line per link,
    ///     which the desktop app's supervision reads.
    /// </summary>
    public async Task RunAsync(string socketPath, TextWriter log, CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            try {
                await RunLinkAsync(socketPath, log, cancellationToken);
            } catch (Exception e) when (e is IOException or IpcException or System.Net.Sockets.SocketException) {
                await log.WriteLineAsync($"patina-worker: {e.Message}");
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                return;
            }

            try {
                await Task.Delay(250, cancellationToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    async Task RunLinkAsync(string socketPath, TextWriter log, CancellationToken cancellationToken) {
        var stream = await IpcSocket.ConnectAsync(socketPath, ConnectTimeout, cancellationToken);
        IpcConnection? connection = null;
        connection = new(stream, (request, token) => AnswerAsync(connection!, request, token));
        await using (connection) {
            connection.Start();
            await connection.SendAsync(
                new() {
                    Type = IpcTypes.Hello,
                    Worker = new(IpcMessage.ProtocolVersion, ProductInfo.Version, Environment.ProcessId)
                },
                cancellationToken
            );
            await log.WriteLineAsync($"patina-worker {ProductInfo.Version} connected to {socketPath}");
            try {
                await connection.Closed.WaitAsync(cancellationToken);
                await log.WriteLineAsync("patina-worker: the Server disconnected");
            } finally {
                bakes.LinkLost();
            }
        }
    }

    ValueTask<IpcMessage> AnswerAsync(IpcConnection link, IpcMessage request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            request.Type switch {
                IpcTypes.Health => new() { Type = IpcTypes.Ok, Health = Health() },
                IpcTypes.ViewportOpen => viewport.Open(request),
                IpcTypes.ViewportCamera => viewport.SetCamera(request),
                IpcTypes.ViewportRender => viewport.Render(request),
                IpcTypes.GraphLoad => graphs.Load(request),
                IpcTypes.GraphEvaluate => graphs.Evaluate(request),
                IpcTypes.GraphImage => graphs.Image(request),
                IpcTypes.GraphClose => graphs.Close(request),
                IpcTypes.BakeStart => bakes.Start(request, link),
                IpcTypes.BakeCancel => bakes.Cancel(request),
                _ => IpcMessage.Fail(request.Id, $"Unknown request '{request.Type}'.")
            }
        );

    static WorkerHealth Health() => new(Stopwatch.GetElapsedTime(Started).TotalSeconds, Environment.WorkingSet);
}
