using Patina.Export;
using Patina.Ipc;

namespace Patina.Server.Tests;

/// <summary>
///     A Worker for an in-memory Server that only decodes pictures (#293), with the library code the real Worker
///     answers <see cref="IpcTypes.PictureRead" /> with, so an API test of a library picture needs no GPU process.
///     Anything else it is asked is refused. <c>StackProcessTests</c> asks the real Worker.
/// </summary>
sealed class PictureWorker : IAsyncDisposable {
    readonly Stream stream;
    readonly Func<string, bool> dies;
    readonly IpcConnection connection;

    PictureWorker(Stream stream, Func<string, bool> dies) {
        this.stream = stream;
        this.dies = dies;
        connection = new(stream, (request, _) => ValueTask.FromResult(Answer(request)));
    }

    /// <summary>Connects to the Server's socket and says hello as a Worker of this protocol.</summary>
    /// <param name="socketPath">The Server's socket.</param>
    /// <param name="cancellationToken">Ends the wait.</param>
    /// <param name="dies">Which pictures this Worker goes away while reading, as a crashing decoder would.</param>
    public static async Task<PictureWorker> ConnectAsync(
        string socketPath,
        CancellationToken cancellationToken,
        Func<string, bool>? dies = null
    ) {
        var stream = await IpcSocket.ConnectAsync(socketPath, TimeSpan.FromSeconds(30), cancellationToken);
        PictureWorker worker = new(stream, dies ?? (static _ => false));
        worker.connection.Start();
        await worker.connection.SendAsync(
            new() { Type = IpcTypes.Hello, Worker = new(IpcMessage.ProtocolVersion, "test", Environment.ProcessId) },
            cancellationToken
        );
        return worker;
    }

    /// <summary>How many pictures it was asked to read.</summary>
    public int Reads { get; private set; }

    IpcMessage Answer(IpcMessage request) {
        if (request is not { Type: IpcTypes.PictureRead, Picture.Read: { } ask }) {
            return request.Type == IpcTypes.Health
                ? IpcMessage.Ok(request.Id)
                : IpcMessage.Fail(request.Id, $"A picture Worker does not answer '{request.Type}'.");
        }

        Reads++;
        if (dies(ask.File)) {
            stream.Dispose();
            return IpcMessage.Fail(request.Id, "gone");
        }

        return new() {
            Type = IpcTypes.Ok, Id = request.Id, Pictured = new(PictureReads.Read(ask.File, ask.Preview, ask.Side))
        };
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
