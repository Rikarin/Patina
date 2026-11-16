using Patina.Contracts;
using Patina.Ipc;
using Patina.Server.Viewport;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>
///     A bake's preview while no GPU engine is connected (#310): the setup asked for is kept, but the key the last
///     engine drew is not the answer, and a new engine hears the preview when it is asked for.
/// </summary>
public sealed class BakePreviewWaitTests : IAsyncDisposable {
    readonly ServerFixture server = new();

    CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => server.DisposeAsync();

    [Fact]
    public async Task A_preview_with_no_engine_waits_for_one_and_a_new_one_hears_it_when_asked() {
        using var client = server.AuthorisedClient();
        var low = Path.Combine(server.Library, "low.fbx");
        File.WriteAllBytes(low, []);
        var first = await PreviewWorker.ConnectAsync(server.SocketPath, Token);
        using var shown = await client.PutAsJsonAsync(
            "/api/v1/viewport/bake-preview",
            new ViewportBakePreview { Low = low, High = [low] },
            ContractsJsonContext.Default.ViewportBakePreview,
            Token
        );
        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        Assert.Single((await StateAsync(client)).Parts);

        await first.DisposeAsync();
        var waiting = await StateAsync(client);
        while (waiting.Parts.Count > 0) {
            await Task.Delay(10, Token);
            waiting = await StateAsync(client);
        }

        Assert.Equal(low, waiting.Preview?.Low);
        Assert.Equal([BakePreviewSession.WaitsForEngine], waiting.Warnings);

        await using var second = await PreviewWorker.ConnectAsync(server.SocketPath, Token);
        var again = await StateAsync(client);
        while (again.Warnings.Count > 0) {
            await Task.Delay(10, Token);
            again = await StateAsync(client);
        }

        Assert.Single(again.Parts);
        Assert.Equal(1, second.Told);
    }

    async Task<BakePreviewState> StateAsync(HttpClient client) =>
        (await client.GetFromJsonAsync(
            "/api/v1/viewport/bake-preview",
            ContractsJsonContext.Default.BakePreviewState,
            Token
        ))!;

    /// <summary>A Worker that draws a bake's preview as one part, and answers nothing else but its health.</summary>
    sealed class PreviewWorker : IAsyncDisposable {
        readonly IpcConnection connection;

        PreviewWorker(Stream stream) => connection = new(stream, (request, _) => ValueTask.FromResult(Answer(request)));

        /// <summary>How many previews it was told.</summary>
        public int Told { get; private set; }

        public static async Task<PreviewWorker> ConnectAsync(string socketPath, CancellationToken cancellationToken) {
            var stream = await IpcSocket.ConnectAsync(socketPath, TimeSpan.FromSeconds(30), cancellationToken);
            PreviewWorker worker = new(stream);
            worker.connection.Start();
            await worker.connection.SendAsync(
                new() {
                    Type = IpcTypes.Hello, Worker = new(IpcMessage.ProtocolVersion, "test", Environment.ProcessId)
                },
                cancellationToken
            );
            return worker;
        }

        IpcMessage Answer(IpcMessage request) {
            if (request is not { Type: IpcTypes.ViewportBakePreview, BakePreview: { } preview }) {
                return request.Type == IpcTypes.Health
                    ? IpcMessage.Ok(request.Id)
                    : IpcMessage.Fail(request.Id, $"A preview Worker does not answer '{request.Type}'.");
            }

            Told++;
            BakePreviewState shown = preview.Low.Length == 0
                ? BakePreviewState.Off
                : new(preview, [new("Box", 12, 40, "#ff8000")], []);
            return new() { Type = IpcTypes.Ok, Id = request.Id, BakePreviewShown = shown };
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
