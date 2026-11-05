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
        var socketPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");
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
}
