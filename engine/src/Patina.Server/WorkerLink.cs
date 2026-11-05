using Microsoft.Extensions.Options;
using Patina.Contracts;
using Patina.Ipc;
using System.Net.Sockets;

namespace Patina.Server;

/// <summary>
///     The Server's end of the Worker link. It listens on the socket and serves one Worker at a time, asking for its
///     health every few seconds; a second Worker waits in the socket's backlog until the first has gone. The Worker can
///     die at any moment and the Server carries on (plan §3.1), so a lost link only changes <see cref="Status" />.
/// </summary>
public sealed class WorkerLink(IOptions<ServerOptions> options, ILogger<WorkerLink> logger) : BackgroundService {
    static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(10);
    static readonly TimeSpan HealthInterval = TimeSpan.FromSeconds(5);
    static readonly WorkerStatus Disconnected = new(false);

    readonly Lock changing = new();
    TaskCompletionSource<IpcConnection> nextWorker = NewWaiter();
    volatile WorkerStatus status = Disconnected;
    volatile IpcConnection? current;

    public WorkerStatus Status => status;

    /// <summary>
    ///     Something the Worker said that nobody asked it: how a job is going. Raised on the link's own loop, so a
    ///     handler
    ///     returns at once.
    /// </summary>
    public event Action<IpcMessage>? Reported;

    /// <summary>
    ///     The Worker's connection now, or waits for one: after a crash, that is the next Worker's hello. A closed
    ///     connection is not one, though <see cref="current" /> can keep it until the health loop next wakes: a job
    ///     handed it would spend an attempt on a Worker that had already gone.
    /// </summary>
    public Task<IpcConnection> ConnectionAsync(CancellationToken cancellationToken) {
        lock (changing) {
            return current is { Closed.IsCompleted: false } connection
                ? Task.FromResult(connection)
                : nextWorker.Task.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    ///     A connection and some work, if there is work. Waiting for a Worker can take seconds, and only what is newest
    ///     when one arrives counts, so the work is asked for on both sides of the wait.
    /// </summary>
    public async Task<(IpcConnection Connection, TWork Work)?> WhenThereIsWorkAsync<TWork>(
        Func<TWork?> pending,
        CancellationToken cancellationToken
    ) where TWork : class {
        if (pending() is null) {
            return null;
        }

        var connection = await ConnectionAsync(cancellationToken);
        return pending() is { } work ? (connection, work) : null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var socketPath = options.Value.Worker.SocketPath;
        using var listener = IpcSocket.Listen(socketPath);
        try {
            while (!stoppingToken.IsCancellationRequested) {
                var socket = await listener.AcceptAsync(stoppingToken);
                await ServeAsync(new NetworkStream(socket, true), stoppingToken);
            }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // Stopping: nothing to report.
        } finally {
            File.Delete(socketPath);
        }
    }

    async Task ServeAsync(Stream stream, CancellationToken cancellationToken) {
        var hello = new TaskCompletionSource<WorkerHello>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new IpcConnection(stream, (request, _) => ValueTask.FromResult(Answer(request, hello)));
        await using (connection) {
            connection.Start();
            try {
                await ProbeAsync(connection, hello.Task, cancellationToken);
            } catch (Exception e) when (e is IOException or IpcException or TimeoutException) {
                ServerLog.WorkerLost(logger, e.Message);
            } finally {
                lock (changing) {
                    current = null;
                }

                status = Disconnected;
            }
        }
    }

    static TaskCompletionSource<IpcConnection> NewWaiter() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    IpcMessage Answer(IpcMessage request, TaskCompletionSource<WorkerHello> hello) {
        if (request.Type is IpcTypes.BakeProgress or IpcTypes.BakeEnded) {
            Reported?.Invoke(request);
            return IpcMessage.Ok(request.Id);
        }

        return Greet(request, hello);
    }

    /// <summary>A Worker's hello, refused if it speaks another protocol.</summary>
    static IpcMessage Greet(IpcMessage request, TaskCompletionSource<WorkerHello> hello) {
        if (request is not { Type: IpcTypes.Hello, Worker: { } worker }) {
            return IpcMessage.Fail(request.Id, $"Unexpected request '{request.Type}'.");
        }

        if (worker.Protocol != IpcMessage.ProtocolVersion) {
            return IpcMessage.Fail(
                request.Id,
                $"The Server speaks protocol {IpcMessage.ProtocolVersion}, the Worker {worker.Protocol}."
            );
        }

        hello.TrySetResult(worker);
        return IpcMessage.Ok(request.Id);
    }

    async Task ProbeAsync(IpcConnection connection, Task<WorkerHello> hello, CancellationToken cancellationToken) {
        var first = await Task.WhenAny(hello, connection.Closed).WaitAsync(HelloTimeout, cancellationToken);
        if (first != hello) {
            return;
        }

        var worker = await hello;
        ServerLog.WorkerConnected(logger, worker.Version, worker.ProcessId);
        lock (changing) {
            current = connection;
            nextWorker.TrySetResult(connection);
            nextWorker = NewWaiter();
        }

        while (!connection.Closed.IsCompleted) {
            var reply = await connection.SendAsync(new() { Type = IpcTypes.Health }, cancellationToken);
            status = new(
                true,
                worker.Version,
                worker.ProcessId,
                reply.Health?.UptimeSeconds,
                reply.Health?.WorkingSetBytes
            );
            await Task.WhenAny(connection.Closed, Task.Delay(HealthInterval, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
