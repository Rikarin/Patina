using Patina.Contracts;
using Patina.Ipc;
using System.Diagnostics;

namespace Patina.Worker;

/// <summary>Stays connected to the Server until stopped.</summary>
sealed class WorkerLoop(ViewportHost viewport) {
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
        var connection = new IpcConnection(stream, AnswerAsync);
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
            await connection.Closed.WaitAsync(cancellationToken);
            await log.WriteLineAsync("patina-worker: the Server disconnected");
        }
    }

    ValueTask<IpcMessage> AnswerAsync(IpcMessage request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            request.Type switch {
                IpcTypes.Health => new() { Type = IpcTypes.Ok, Health = Health() },
                IpcTypes.ViewportOpen => viewport.Open(request),
                IpcTypes.ViewportCamera => viewport.SetCamera(request),
                IpcTypes.ViewportRender => viewport.Render(request),
                _ => IpcMessage.Fail(request.Id, $"Unknown request '{request.Type}'.")
            }
        );

    static WorkerHealth Health() => new(Stopwatch.GetElapsedTime(Started).TotalSeconds, Environment.WorkingSet);
}
