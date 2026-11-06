using Patina.Contracts;
using Patina.Ipc;
using System.Diagnostics;

namespace Patina.Worker;

/// <summary>Stays connected to the Server until stopped.</summary>
sealed class WorkerLoop(
    ViewportHost viewport,
    GraphHost graphs,
    StackHost stacks,
    BakeHost bakes,
    BuildHost builds,
    DeviceHost gpu,
    DeviceWatch watch
) {
    static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    static readonly long Started = Stopwatch.GetTimestamp();

    /// <summary>
    ///     Keeps a link to the Server, reconnecting when it drops. <paramref name="log" /> gets one line per link,
    ///     which the desktop app's supervision reads.
    /// </summary>
    public async Task RunAsync(string socketPath, TextWriter log, CancellationToken cancellationToken) {
        _ = WatchAsync(log, cancellationToken);
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

    async Task WatchAsync(TextWriter log, CancellationToken cancellationToken) {
        try {
            await watch.WatchAsync(log, cancellationToken);
        } catch (OperationCanceledException) {
            // Stopping.
        }
    }

    async Task RunLinkAsync(string socketPath, TextWriter log, CancellationToken cancellationToken) {
        var stream = await IpcSocket.ConnectAsync(socketPath, ConnectTimeout, cancellationToken);
        IpcConnection? connection = null;
        connection = new(stream, (request, token) => AnswerAsync(connection!, request, token)) {
            Immediate = static type => type is IpcTypes.Health or IpcTypes.BakeCancel or IpcTypes.BuildCancel
        };
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
                builds.LinkLost();
            }
        }
    }

    ValueTask<IpcMessage> AnswerAsync(IpcConnection link, IpcMessage request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            request.Type switch {
                IpcTypes.Health => new() { Type = IpcTypes.Ok, Health = Health() },
                IpcTypes.ViewportOpen => viewport.Open(request),
                IpcTypes.ViewportMaps => viewport.SetMaps(request),
                IpcTypes.ViewportCamera => viewport.SetCamera(request),
                IpcTypes.ViewportRender => viewport.Render(request),
                IpcTypes.ViewportPick => viewport.Pick(request),
                IpcTypes.GraphLoad
                    or IpcTypes.GraphEvaluate
                    or IpcTypes.GraphImage
                    or IpcTypes.GraphClose
                    or IpcTypes.StackLoad
                    or IpcTypes.StackSync
                    or IpcTypes.StackThumbnails
                    or IpcTypes.StackEvaluate
                    or IpcTypes.StackSave
                    or IpcTypes.StackCheckpoint
                    or IpcTypes.StackPixels
                    or IpcTypes.StackClose
                    or IpcTypes.ViewportStack => OnDevice(request),
                IpcTypes.BakeStart => bakes.Start(request, link),
                IpcTypes.BakeCancel => bakes.Cancel(request),
                IpcTypes.BuildStart => builds.Start(request, link),
                IpcTypes.BuildCancel => builds.Cancel(request),
                _ => IpcMessage.Fail(request.Id, $"Unknown request '{request.Type}'.")
            }
        );

    /// <summary>What uses the device, in turn with a build's step. Health and bakes never wait for this.</summary>
    IpcMessage OnDevice(IpcMessage request) {
        lock (gpu.Gate) {
            watch.Beat();
            try {
                return Device(request);
            } finally {
                watch.Idle();
            }
        }
    }

    IpcMessage Device(IpcMessage request) =>
        request.Type switch {
                IpcTypes.ViewportStack => stacks.Follow(request),
                IpcTypes.GraphLoad => graphs.Load(request),
                IpcTypes.GraphEvaluate => graphs.Evaluate(request),
                IpcTypes.GraphImage => graphs.Image(request),
                IpcTypes.GraphClose => graphs.Close(request),
                IpcTypes.StackLoad => stacks.Load(request),
                IpcTypes.StackSync => stacks.Sync(request),
                IpcTypes.StackThumbnails => stacks.Thumbnails(request),
                IpcTypes.StackEvaluate => stacks.Evaluate(request),
                IpcTypes.StackSave or IpcTypes.StackCheckpoint => stacks.Save(request),
                IpcTypes.StackPixels => stacks.Pixels(request),
                _ => stacks.Close(request)
        };

    WorkerHealth Health() =>
        new(Stopwatch.GetElapsedTime(Started).TotalSeconds, Environment.WorkingSet, watch.Busy.TotalSeconds);
}
