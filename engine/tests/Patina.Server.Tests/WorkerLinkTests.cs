using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Patina.Contracts;
using Patina.Ipc;

namespace Patina.Server.Tests;

public sealed class WorkerLinkTests {
    static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    ///     A job's runner and the link both wake on a connection closing, and the runner can win: it used to be handed
    ///     the dead connection, so one Worker's death cost a job two of its attempts. Asking at the moment of closing
    ///     is that race, and enough rounds of it lose it at least once.
    /// </summary>
    [Fact]
    public async Task A_closed_connection_is_never_offered_to_the_next_asker() {
        var socketPath = Patina.Testing.TempFolders.NewSocket();
        var options = new ServerOptions { Worker = { SocketPath = socketPath } };
        using var link = new WorkerLink(Options.Create(options), NullLogger<WorkerLink>.Instance);
        await link.StartAsync(Token);
        try {
            for (var round = 0; round < 200; round++) {
                var stream = await IpcSocket.ConnectAsync(socketPath, TimeSpan.FromSeconds(5), Token);
                var worker = new IpcConnection(
                    stream,
                    static (request, _) => ValueTask.FromResult(IpcMessage.Ok(request.Id))
                );
                worker.Start();
                var hello = new WorkerHello(IpcMessage.ProtocolVersion, ProductInfo.Version, Environment.ProcessId);
                await worker.SendAsync(new() { Type = IpcTypes.Hello, Worker = hello }, Token);
                var connection = await link.ConnectionAsync(Token);

                var asked = connection.Closed.ContinueWith(
                    _ => link.ConnectionAsync(Token),
                    Token,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default
                );
                await worker.DisposeAsync();

                Assert.False((await asked).IsCompleted, $"Round {round} was handed a closed connection.");
            }
        } finally {
            await link.StopAsync(Token);
        }
    }

    /// <summary>
    ///     What the Worker logs as a warning or an error reaches the Server's log, which every client reads (#297), and
    ///     a Worker's line said before its link was up waits for it.
    /// </summary>
    [Fact]
    public async Task The_worker_s_warnings_and_errors_reach_the_server_s_log() {
        var socketPath = Patina.Testing.TempFolders.NewSocket();
        var options = new ServerOptions { Worker = { SocketPath = socketPath } };
        var ring = new LogRing();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(ring));
        using var link = new WorkerLink(Options.Create(options), factory.CreateLogger<WorkerLink>());
        await link.StartAsync(Token);
        try {
            var stream = await IpcSocket.ConnectAsync(socketPath, TimeSpan.FromSeconds(5), Token);
            await using var worker = new IpcConnection(
                stream,
                static (request, _) => ValueTask.FromResult(IpcMessage.Ok(request.Id))
            );
            worker.Start();
            var hello = new WorkerHello(IpcMessage.ProtocolVersion, ProductInfo.Version, Environment.ProcessId);
            await worker.SendAsync(new() { Type = IpcTypes.Hello, Worker = hello }, Token);
            await worker.SendAsync(new() { Type = IpcTypes.WorkerLog, Log = new(false, "the device is slow") }, Token);
            await worker.SendAsync(new() { Type = IpcTypes.WorkerLog, Log = new(true, "the device was lost") }, Token);

            var lines = ring.After(0).Entries.Where(static entry => entry.Text.StartsWith("Worker: ", StringComparison.Ordinal));

            Assert.Equal(
                [("warning", "Worker: the device is slow"), ("error", "Worker: the device was lost")],
                lines.Select(static entry => (entry.Level, entry.Text))
            );
        } finally {
            await link.StopAsync(Token);
        }
    }
}
