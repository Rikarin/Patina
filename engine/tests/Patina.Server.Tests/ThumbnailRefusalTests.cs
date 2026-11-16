using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Testing;
using System.Security.Cryptography;

namespace Patina.Server.Tests;

/// <summary>What a shelf picture the Worker would not draw leaves behind: nothing, unless the material was the reason.</summary>
public sealed class ThumbnailRefusalTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-thumbnail-refusal-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => TempFolders.Delete(folder.FullName);

    /// <summary>
    ///     A library opened once on a machine with no GPU kept blank shelf pictures until its cache was cleared (#302):
    ///     the Worker's "No Vulkan device" was remembered as "nothing to draw". A refusal about the device is asked again;
    ///     one about the material is still remembered, so a broken material is not drawn on every visit.
    /// </summary>
    [Fact]
    public async Task A_picture_refused_for_want_of_a_device_is_drawn_once_there_is_one() {
        var root = Path.Combine(folder.FullName, "Library");
        LibraryFiles.CreateDefaultLibrary(root, "Game");
        await File.WriteAllBytesAsync(Path.Combine(root, "first.png"), [1, 2, 3], Token);
        await File.WriteAllBytesAsync(Path.Combine(root, "second.png"), [4, 5, 6], Token);
        using var library = OpenLibrary.Open(root);
        var store = new PreviewStore(new(root, null));
        var socketPath = TempFolders.NewSocket();
        using var link = new WorkerLink(
            Options.Create(new ServerOptions { Worker = { SocketPath = socketPath } }),
            NullLogger<WorkerLink>.Instance
        );
        await link.StartAsync(Token);
        try {
            var answers = new Queue<string?>(
                ["No Vulkan device: none found", null, "The picture cannot be read.", "unasked"]
            );
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

            var blind = await thumbnails.DrawAsync(library, ["first.png"], Token);
            var seeing = await thumbnails.DrawAsync(library, ["first.png"], Token);
            var broken = await thumbnails.DrawAsync(library, ["second.png"], Token);
            var again = await thumbnails.DrawAsync(library, ["second.png"], Token);
            using var restarted = new Thumbnails(link, store);
            var afterRestart = await restarted.DrawAsync(library, ["first.png", "second.png"], Token);

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

    /// <summary>A Worker's picture: any bytes, written under their hash into the cache it was told of.</summary>
    static IpcMessage Drawn(IpcMessage request, string cache) {
        byte[] pixels = [.. Guid.NewGuid().ToByteArray()];
        var hash = Convert.ToHexStringLower(SHA256.HashData(pixels));
        var path = GraphPreviewFiles.PathOf(cache, hash);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, pixels);
        return new() { Type = IpcTypes.Ok, Id = request.Id, ThumbnailHash = hash };
    }

    /// <summary>A Worker that says hello and answers every request it is asked with <paramref name="answer" />.</summary>
    static async Task<IpcConnection> WorkerAsync(string socketPath, Func<IpcMessage, IpcMessage> answer) {
        var stream = await IpcSocket.ConnectAsync(socketPath, TimeSpan.FromSeconds(5), Token);
        var worker = new IpcConnection(stream, (request, _) => ValueTask.FromResult(answer(request)));
        worker.Start();
        var hello = new WorkerHello(IpcMessage.ProtocolVersion, ProductInfo.Version, Environment.ProcessId);
        await worker.SendAsync(new() { Type = IpcTypes.Hello, Worker = hello }, Token);
        return worker;
    }
}
