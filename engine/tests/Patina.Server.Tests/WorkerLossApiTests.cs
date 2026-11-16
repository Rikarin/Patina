using Patina.Contracts;
using Patina.Ipc;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Patina.Server.Tests;

/// <summary>
///     A Worker that dies while it answers is the engine gone, never a fault of the Server's or a file's (#309): each
///     route that asks the Worker answers 503 with <c>engine-unavailable</c>, as a client shows "the GPU engine
///     stopped", and the next Worker answers again.
/// </summary>
public sealed partial class WorkerLossApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() {
        Patina.Library.LibraryFiles.CreateDefaultLibrary(server.Library, "Game");
        client = server.AuthorisedClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    /// <summary>
    ///     A graph lighting the viewport, a mesh's facts and a library graph's thumbnail each answered 500 when the
    ///     Worker went mid-answer: <c>IOException</c> was caught nowhere on their way.
    /// </summary>
    [Fact]
    public async Task A_Worker_that_dies_mid_answer_is_the_engine_unavailable() {
        var shared = Directory.CreateDirectory(Path.Combine(server.Library, "shared")).FullName;
        var sky = GraphTesting.Write(shared, GraphTesting.Sample(), "sky.vxtexgraph");
        var mesh = Path.Combine(shared, "crate.glb");
        await File.WriteAllBytesAsync(mesh, [1, 2, 3], Token);

        await AssertGoneAsync(
            IpcTypes.MaterialThumbnail,
            () => client.PutAsJsonAsync(
                "/api/v1/viewport/display",
                new ViewportDisplay { Environment = sky },
                ContractsJsonContext.Default.ViewportDisplay,
                Token
            )
        );
        await AssertGoneAsync(
            IpcTypes.MeshFacts,
            () => client.GetAsync("/api/v1/library/mesh?path=shared/crate.glb", Token)
        );
        await AssertGoneAsync(
            IpcTypes.MaterialThumbnail,
            () => client.PostAsJsonAsync(
                "/api/v1/library/thumbnails",
                new ThumbnailRequest(["shared/sky.vxtexgraph"]),
                Token
            )
        );
    }

    /// <summary>
    ///     Nothing in the Server waits for the Worker's connection but through <see cref="WorkerWait" />, so no wait's
    ///     patience bounds what is then asked (#284, #309): a patience that did told a client no GPU process was
    ///     connected while one was drawing.
    /// </summary>
    [Fact]
    public void Nothing_in_the_Server_waits_for_a_Worker_but_through_WorkerWait() {
        var server = Path.Combine(EngineRoot(), "src", "Patina.Server");
        List<string> found = [];
        foreach (var file in Directory.EnumerateFiles(server, "*.cs", SearchOption.AllDirectories)
                     .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                     .Where(static f => Path.GetFileName(f) is not ("WorkerWait.cs" or "WorkerLink.cs"))
                     .Order(StringComparer.Ordinal)) {
            var lines = File.ReadAllLines(file);
            for (var line = 0; line < lines.Length; line++) {
                if (OwnWait().IsMatch(lines[line]) || BoundedAnswer().IsMatch(lines[line])) {
                    found.Add($"{Path.GetRelativePath(server, file)}:{line + 1}: {lines[line].Trim()}");
                }
            }
        }

        Assert.True(found.Count == 0, "Wait through WorkerWait.ConnectionAsync instead:\n" + string.Join('\n', found));
    }

    /// <summary>
    ///     Asks with a Worker connected that goes away when it is asked <paramref name="type" />, and expects the
    ///     engine unavailable.
    /// </summary>
    async Task AssertGoneAsync(string type, Func<Task<HttpResponseMessage>> ask) {
        await using var worker = await DyingWorker.ConnectAsync(server.SocketPath, type, Token);

        using var reply = await ask();

        var problem = await reply.Content.ReadAsStringAsync(Token);
        Assert.True(reply.StatusCode == HttpStatusCode.ServiceUnavailable, $"{type}: {(int)reply.StatusCode} {problem}");
        Assert.Contains("engine-unavailable", problem, StringComparison.Ordinal);
        Assert.True(worker.Asked, $"{type} was never asked");
    }

    /// <summary>A wait for the Worker's connection of its own, which only <see cref="WorkerWait" /> may make.</summary>
    [GeneratedRegex(@"(?<!WorkerWait)\.ConnectionAsync\s*\(")]
    private static partial Regex OwnWait();

    /// <summary>A question to the Worker whose answer a patience bounds.</summary>
    [GeneratedRegex(@"\.SendAsync\(.*(?i:patience|waiting)", RegexOptions.None)]
    private static partial Regex BoundedAnswer();

    static string EngineRoot() {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent) {
            if (File.Exists(Path.Combine(dir.FullName, "Patina.slnx"))) {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("No Patina.slnx above the tests.");
    }

    /// <summary>A Worker that answers everything but one kind of question, at which its process ends.</summary>
    sealed class DyingWorker : IAsyncDisposable {
        readonly Stream stream;
        readonly string dies;
        readonly IpcConnection connection;

        DyingWorker(Stream stream, string dies) {
            this.stream = stream;
            this.dies = dies;
            connection = new(stream, (request, _) => ValueTask.FromResult(Answer(request)));
        }

        /// <summary>Whether it was asked what it dies at.</summary>
        public bool Asked { get; private set; }

        public static async Task<DyingWorker> ConnectAsync(string socketPath, string dies, CancellationToken token) {
            var stream = await IpcSocket.ConnectAsync(socketPath, TimeSpan.FromSeconds(30), token);
            DyingWorker worker = new(stream, dies);
            worker.connection.Start();
            await worker.connection.SendAsync(
                new() {
                    Type = IpcTypes.Hello, Worker = new(IpcMessage.ProtocolVersion, "test", Environment.ProcessId)
                },
                token
            );
            return worker;
        }

        IpcMessage Answer(IpcMessage request) {
            if (request.Type != dies) {
                return IpcMessage.Ok(request.Id);
            }

            Asked = true;
            stream.Dispose();
            return IpcMessage.Fail(request.Id, "gone");
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
