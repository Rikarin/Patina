using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;
using Patina.Testing;
using System.Security.Cryptography;

namespace Patina.Server.Tests;

/// <summary>What a picture the Worker would not draw leaves: nothing, unless what was drawn was why.</summary>
public sealed class ThumbnailRefusalTests : IDisposable {
    /// <summary>What the Worker says to each picture, in turn: a refusal, or null to draw it.</summary>
    static readonly string?[] Answers = ["No Vulkan device: none found", null, "It cannot be drawn.", "unasked"];

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-thumbnail-refusal-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => TempFolders.Delete(folder.FullName);

    /// <summary>
    ///     A library opened once on a machine with no GPU kept blank shelf pictures until its cache was cleared (#302):
    ///     the Worker's "No Vulkan device" was remembered as "nothing to draw". A refusal about the device is asked
    ///     again; one about the material is still remembered, so a broken material is not drawn on every visit.
    /// </summary>
    [Fact]
    public async Task A_picture_refused_for_want_of_a_device_is_drawn_once_there_is_one() {
        var root = Path.Combine(folder.FullName, "Library");
        LibraryFiles.CreateDefaultLibrary(root, "Game");
        await File.WriteAllBytesAsync(Path.Combine(root, "first.png"), [1, 2, 3], Token);
        await File.WriteAllBytesAsync(Path.Combine(root, "second.png"), [4, 5, 6], Token);
        using var library = OpenLibrary.Open(root);

        await RefusedThenDrawnAsync(root, (thumbnails, sources) => thumbnails.DrawAsync(library, sources, Token));
    }

    /// <summary>
    ///     A node's picture, for a palette or a layer's generator list, is kept under the same kind of key as a shelf
    ///     picture (#303): a Worker with no device leaves nothing behind, so the next one that has a device draws it,
    ///     and a refusal about the node itself is kept, so it is not drawn on every visit.
    /// </summary>
    [Fact]
    public async Task A_node_picture_refused_for_want_of_a_device_is_drawn_once_there_is_one() {
        var root = Path.Combine(folder.FullName, "Library");
        LibraryFiles.CreateDefaultLibrary(root, "Game");
        var types = StackNodeKinds.Types(null);
        var scope = new NodePictureScope(Path.Combine(root, "graph.ptgraph"), null, [root], types);

        await RefusedThenDrawnAsync(
            root,
            (thumbnails, sources) => thumbnails.DrawNodesAsync(scope, sources, Token),
            "Source/Noise",
            "Filters/Blur"
        );
    }

    /// <summary>
    ///     Asks for the first picture with no device and again with one, the second picture twice with a refusal about
    ///     it, and both of a new <see cref="Thumbnails" /> over the same cache, as after a restart.
    /// </summary>
    async Task RefusedThenDrawnAsync(
        string root,
        Func<Thumbnails, IReadOnlyList<string>, Task<IReadOnlyList<Thumbnail>>> draw,
        string first = "first.png",
        string second = "second.png"
    ) {
        var store = new PreviewStore(new(root, null));
        var socketPath = TempFolders.NewSocket();
        using var link = new WorkerLink(
            Options.Create(new ServerOptions { Worker = { SocketPath = socketPath } }),
            NullLogger<WorkerLink>.Instance
        );
        await link.StartAsync(Token);
        try {
            var answers = new Queue<string?>(Answers);
            var asked = 0;
            await using var worker = await WorkerAsync(
                socketPath,
                request => {
                    if (request.Type != IpcTypes.MaterialThumbnail) {
                        return IpcMessage.Ok(request.Id); // the link's health and anything else it says
                    }

                    asked++;
                    return answers.Dequeue() is { } refusal
                        ? IpcMessage.Fail(request.Id, refusal)
                        : Drawn(request, store.Root);
                }
            );
            await link.ConnectionAsync(Token);
            using var thumbnails = new Thumbnails(link, store);

            var blind = await draw(thumbnails, [first]);
            var seeing = await draw(thumbnails, [first]);
            var broken = await draw(thumbnails, [second]);
            var again = await draw(thumbnails, [second]);
            using var restarted = new Thumbnails(link, store);
            var afterRestart = await draw(restarted, [first, second]);

            Assert.Null(blind[0].Hash);
            Assert.Matches("^[0-9a-f]{64}$", seeing[0].Hash);
            Assert.Null(broken[0].Hash);
            Assert.Null(again[0].Hash);
            Assert.Equal([seeing[0].Hash, null], afterRestart.Select(static thumbnail => thumbnail.Hash));
            Assert.Equal(3, asked);
        } finally {
            await link.StopAsync(Token);
        }
    }

    /// <summary>
    ///     A picture the connected Worker takes longer over than the patience is waited for (#284): the patience
    ///     bounded the drawing as well as the wait for a Worker, so a panorama on a busy GPU was answered 503, "No GPU
    ///     process is connected", while one was connected and drawing. With no Worker at all, the patience still runs
    ///     out.
    /// </summary>
    [Fact]
    public async Task A_picture_the_Worker_is_slow_over_is_waited_for_and_no_Worker_is_not() {
        var root = Path.Combine(folder.FullName, "Library");
        LibraryFiles.CreateDefaultLibrary(root, "Game");
        await File.WriteAllBytesAsync(Path.Combine(root, "slow.png"), [1, 2, 3], Token);
        using var library = OpenLibrary.Open(root);
        var store = new PreviewStore(new(root, null));
        var socketPath = TempFolders.NewSocket();
        using var link = new WorkerLink(
            Options.Create(new ServerOptions { Worker = { SocketPath = socketPath } }),
            NullLogger<WorkerLink>.Instance
        );
        var patience = TimeSpan.FromMilliseconds(200);
        await link.StartAsync(Token);
        try {
            using var thumbnails = new Thumbnails(link, store) { WorkerPatience = patience };
            await Assert.ThrowsAsync<RendererMissingException>(() =>
                thumbnails.DrawAsync(library, ["slow.png"], Token)
            );

            await using var worker = await WorkerAsync(
                socketPath,
                async request => {
                    if (request.Type != IpcTypes.MaterialThumbnail) {
                        return IpcMessage.Ok(request.Id);
                    }

                    await Task.Delay(patience * 5, Token);
                    return Drawn(request, store.Root);
                }
            );
            await link.ConnectionAsync(Token);

            var drawn = await thumbnails.DrawAsync(library, ["slow.png"], Token);

            Assert.Matches("^[0-9a-f]{64}$", drawn[0].Hash);
        } finally {
            await link.StopAsync(Token);
        }
    }

    /// <summary>A Worker's picture: any bytes, written under their hash into the cache it was told of.</summary>
    static IpcMessage Drawn(IpcMessage request, string cache) {
        byte[] pixels = [..Guid.NewGuid().ToByteArray()];
        var hash = Convert.ToHexStringLower(SHA256.HashData(pixels));
        var path = GraphPreviewFiles.PathOf(cache, hash);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, pixels);
        return new() { Type = IpcTypes.Ok, Id = request.Id, ThumbnailHash = hash };
    }

    /// <summary>A Worker that says hello and answers every request with <paramref name="answer" />.</summary>
    static Task<IpcConnection> WorkerAsync(string socketPath, Func<IpcMessage, IpcMessage> answer) =>
        WorkerAsync(socketPath, request => Task.FromResult(answer(request)));

    /// <summary>A Worker that says hello and answers with <paramref name="answer" />, in its own time.</summary>
    static async Task<IpcConnection> WorkerAsync(
        string socketPath,
        Func<IpcMessage, Task<IpcMessage>> answer
    ) {
        var stream = await IpcSocket.ConnectAsync(socketPath, TimeSpan.FromSeconds(5), Token);
        var worker = new IpcConnection(stream, async (request, _) => await answer(request));
        worker.Start();
        var hello = new WorkerHello(IpcMessage.ProtocolVersion, ProductInfo.Version, Environment.ProcessId);
        await worker.SendAsync(new() { Type = IpcTypes.Hello, Worker = hello }, Token);
        return worker;
    }
}
