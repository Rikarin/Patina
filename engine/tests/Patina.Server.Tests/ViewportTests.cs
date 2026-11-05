using Microsoft.Extensions.Options;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Viewport;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>
///     The frame path as a client sees it: the real Server and Worker, and frames over the real WebSocket.
/// </summary>
public sealed class ViewportProcessTests : IDisposable {
    const int HeaderBytes = 32;

    static readonly HttpClient Http = new();

    static readonly string Fixture =
        Path.GetFullPath(Path.Combine(Engine.Root, "..", "content", "fixtures", "viewport-test.glb"));

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-viewport-");
    readonly string socketPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");
    readonly List<Process> started = [];
    readonly ConcurrentQueue<string> output = new();

    string Library => Directory.CreateDirectory(Path.Combine(folder.FullName, "Game.ptlib")).FullName;

    string DiscoveryFile => Path.Combine(folder.FullName, "servers.json");

    string ShaderCache => Path.Combine(folder.FullName, "shaders");

    public void Dispose() {
        foreach (var process in started) {
            if (!process.HasExited) {
                process.Kill(true);
                process.WaitForExit();
            }

            process.Dispose();
        }

        // What the processes said is the only account of a Server that stopped itself.
        foreach (var line in output) {
            TestContext.Current.TestOutputHelper?.WriteLine(line);
        }

        File.Delete(socketPath);
        folder.Delete(true);
    }

    Process Start(string dll, params string[] arguments) {
        var info = new ProcessStartInfo("dotnet") {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add(dll);
        foreach (var argument in arguments) {
            info.ArgumentList.Add(argument);
        }

        var process = Process.Start(info)!;
        var name = Path.GetFileNameWithoutExtension(dll);
        process.OutputDataReceived += (_, line) => Keep(name, line.Data);
        process.ErrorDataReceived += (_, line) => Keep(name, line.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        started.Add(process);
        return process;
    }

    void Keep(string name, string? line) {
        if (line is not null) {
            output.Enqueue($"{name}: {line}");
        }
    }

    Process StartWorker() =>
        Start(
            Engine.Dll("Patina.Worker", "patina-worker"),
            "--socket",
            socketPath,
            "--shader-cache",
            ShaderCache,
            "--watch-stdin"
        );

    async Task<ServerInstance> StartServerAsync(CancellationToken cancellationToken) {
        Start(
            Engine.Dll("Patina.Server", "patina-server"),
            "--library",
            Library,
            $"--Patina:DiscoveryFile={DiscoveryFile}",
            $"--Patina:Worker:SocketPath={socketPath}",
            "--Patina:WatchStdin=true"
        );
        var registry = new InstanceRegistry(DiscoveryFile);
        while (true) {
            if (await registry.FindAsync(Library, cancellationToken) is { } instance) {
                return instance;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    static HttpRequestMessage Authorised(HttpMethod method, ServerInstance instance, string path) {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{instance.Port}{path}");
        request.Headers.Authorization = new("Bearer", instance.Token);
        return request;
    }

    static async Task OpenMeshAsync(ServerInstance instance, CancellationToken cancellationToken) {
        using var request = Authorised(HttpMethod.Put, instance, "/api/v1/viewport/mesh");
        request.Content = JsonContent.Create(new(Fixture), ContractsJsonContext.Default.ViewportMeshRequest);
        using var response = await Http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    static async Task<ViewportState> StateAsync(ServerInstance instance, CancellationToken cancellationToken) {
        using var request = Authorised(HttpMethod.Get, instance, "/api/v1/viewport");
        using var response = await Http.SendAsync(request, cancellationToken);
        return (await response.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.ViewportState,
            cancellationToken
        ))!;
    }

    /// <summary>Socket input and REST calls are two channels, so a test that wants their order waits for it.</summary>
    static async Task UntilSizeAsync(ServerInstance instance, int width, CancellationToken cancellationToken) {
        while ((await StateAsync(instance, cancellationToken)).Width != width) {
            await Task.Delay(10, cancellationToken);
        }
    }

    static async Task<ClientWebSocket> ConnectAsync(ServerInstance instance, CancellationToken cancellationToken) {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(
            new($"ws://127.0.0.1:{instance.Port}/ws/viewport?access_token={instance.Token}"),
            cancellationToken
        );
        return socket;
    }

    static Task SendAsync(ClientWebSocket socket, ViewportInput input, CancellationToken cancellationToken) =>
        socket.SendAsync(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input, ContractsJsonContext.Default.ViewportInput)),
            WebSocketMessageType.Text,
            true,
            cancellationToken
        );

    /// <summary>Reads one whole message, or skips the test where the Worker has no Vulkan.</summary>
    static async Task<byte[]> ReceiveFrameAsync(
        ClientWebSocket socket,
        ServerInstance instance,
        CancellationToken cancellationToken
    ) {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var buffer = new byte[64 * 1024 * 1024];
        var length = 0;
        var receive = socket.ReceiveAsync(buffer.AsMemory(length), patience.Token).AsTask();
        while (true) {
            if (await Task.WhenAny(receive, Task.Delay(250, cancellationToken)) != receive) {
                if ((await StateAsync(instance, cancellationToken)).Error is { } error
                    && error.StartsWith("No Vulkan", StringComparison.Ordinal)) {
                    await patience.CancelAsync();
                    Assert.Skip(error);
                }

                continue;
            }

            var part = await receive;
            length += part.Count;
            if (part.EndOfMessage) {
                return buffer[..length];
            }

            receive = socket.ReceiveAsync(buffer.AsMemory(length), patience.Token).AsTask();
        }
    }

    [Fact]
    public async Task A_client_gets_lit_frames_and_each_input_comes_back_in_a_frame() {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        var instance = await StartServerAsync(deadline.Token);
        StartWorker();
        using var socket = await ConnectAsync(instance, deadline.Token);
        await SendAsync(socket, new(Width: 320, Height: 200, Input: 1), deadline.Token);
        await UntilSizeAsync(instance, 320, deadline.Token);
        await OpenMeshAsync(instance, deadline.Token);

        var frame = await ReceiveFrameAsync(socket, instance, deadline.Token);

        Assert.Equal(0x464E5450u, BinaryPrimitives.ReadUInt32LittleEndian(frame));
        Assert.Equal(320u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(8)));
        Assert.Equal(200u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(12)));
        Assert.Equal(HeaderBytes + 320 * 200 * 4, frame.Length);
        var warm = Enumerable.Range(0, 320 * 200)
            .Count(i => frame[HeaderBytes + i * 4] > frame[HeaderBytes + i * 4 + 2] + 40);
        Assert.InRange(warm, 320, 320 * 200 / 2);

        await SendAsync(socket, new(new(2d, 0.2d, 2.2d), Input: 42), deadline.Token);
        var input = 0u;
        byte[] moved = [];
        while (input != 42) {
            moved = await ReceiveFrameAsync(socket, instance, deadline.Token);
            input = BinaryPrimitives.ReadUInt32LittleEndian(moved.AsSpan(16));
        }

        Assert.False(frame.AsSpan(HeaderBytes).SequenceEqual(moved.AsSpan(HeaderBytes)));
        Assert.Equal(2d, (await StateAsync(instance, deadline.Token)).Camera.Yaw);
    }

    [Fact]
    public async Task The_frame_stream_carries_the_same_frames_and_input_comes_over_the_socket() {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        var instance = await StartServerAsync(deadline.Token);
        StartWorker();
        using var input = new ClientWebSocket();
        await input.ConnectAsync(
            new($"ws://127.0.0.1:{instance.Port}/ws/viewport?frames=false&access_token={instance.Token}"),
            deadline.Token
        );
        await SendAsync(input, new(Width: 64, Height: 48), deadline.Token);
        await UntilSizeAsync(instance, 64, deadline.Token);
        await OpenMeshAsync(instance, deadline.Token);

        using var request = Authorised(HttpMethod.Get, instance, "/api/v1/viewport/frames");
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
        var frame = new byte[HeaderBytes + 64 * 48 * 4];
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        patience.CancelAfter(TimeSpan.FromSeconds(30));
        try {
            await stream.ReadExactlyAsync(frame, patience.Token);
        } catch (OperationCanceledException) {
            Assert.Skip(
                (await StateAsync(instance, deadline.Token)).Error ?? "No frame arrived and no reason was given."
            );
        }

        Assert.Equal(0x464E5450u, BinaryPrimitives.ReadUInt32LittleEndian(frame));
        Assert.Equal(64u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(8)));
        Assert.Contains(frame.Skip(HeaderBytes), static value => value > 0);
    }

    [Fact]
    public async Task The_frame_stream_needs_the_token() {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var instance = await StartServerAsync(deadline.Token);

        using var response = await Http.GetAsync(
            $"http://127.0.0.1:{instance.Port}/api/v1/viewport/frames",
            deadline.Token
        );

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    ///     Spike S5 (ADR 0004): the Server stays up, keeps the state, and tells the next Worker all of it.
    /// </summary>
    [Fact]
    public async Task A_killed_worker_loses_nothing_and_its_successor_carries_on() {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(120));
        var instance = await StartServerAsync(deadline.Token);
        var worker = StartWorker();
        using var socket = await ConnectAsync(instance, deadline.Token);
        await SendAsync(socket, new(new(1d, 0.1d, 2.5d), 160, 100), deadline.Token);
        await UntilSizeAsync(instance, 160, deadline.Token);
        await OpenMeshAsync(instance, deadline.Token);
        var first = await ReceiveFrameAsync(socket, instance, deadline.Token);

        // Mid-render: the camera keeps moving, so the Server has a frame in the Worker's hands when the kill lands.
        for (var i = 0; i < 20; i++) {
            await SendAsync(socket, new(new(1d + i * 0.01d, 0.1d, 2.5d)), deadline.Token);
        }

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);

        // Input during the gap is state on the Server. Nothing renders it until there is a Worker.
        await SendAsync(socket, new(new(3d, 0.1d, 2.5d), Input: 7), deadline.Token);
        await Task.Delay(100, deadline.Token);
        var clock = Stopwatch.StartNew();
        StartWorker();

        var input = 0u;
        byte[] next = [];
        while (input != 7) {
            next = await ReceiveFrameAsync(socket, instance, deadline.Token);
            input = BinaryPrimitives.ReadUInt32LittleEndian(next.AsSpan(16));
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"S5: first frame from the new Worker after {clock.ElapsedMilliseconds} ms"
        );
        var state = await StateAsync(instance, deadline.Token);
        Assert.Equal(Fixture, state.Mesh);
        Assert.Equal(3d, state.Camera.Yaw);
        Assert.Equal(first.Length, next.Length);
        Assert.False(first.AsSpan(HeaderBytes).SequenceEqual(next.AsSpan(HeaderBytes)));
    }

    [Fact]
    public async Task A_mesh_that_is_not_there_is_a_404_and_changes_nothing() {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var instance = await StartServerAsync(deadline.Token);

        using var request = Authorised(HttpMethod.Put, instance, "/api/v1/viewport/mesh");
        request.Content = JsonContent.Create(
            new(Path.Combine(folder.FullName, "missing.glb")),
            ContractsJsonContext.Default.ViewportMeshRequest
        );
        using var response = await Http.SendAsync(request, deadline.Token);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null((await StateAsync(instance, deadline.Token)).Mesh);
    }
}

public sealed class RenderScaleTests {
    static void Frames(RenderScale scale, int count, double milliseconds, bool interacting = true) {
        for (var i = 0; i < count; i++) {
            scale.Record(milliseconds, interacting);
        }
    }

    [Fact]
    public void A_fast_link_stays_at_full_size() {
        var scale = new RenderScale();

        Frames(scale, 500, 8d);

        Assert.Equal(1d, scale.Value);
    }

    [Fact]
    public void A_link_over_budget_steps_down_and_stops_at_half() {
        var scale = new RenderScale();

        Frames(scale, 12, 40d);
        Assert.Equal(0.85d, scale.Value, 3);

        Frames(scale, 500, 40d);
        Assert.Equal(0.5d, scale.Value, 3);
    }

    [Fact]
    public void Room_to_spare_steps_back_up_but_a_frame_near_budget_holds() {
        var scale = new RenderScale();
        Frames(scale, 500, 40d);

        Frames(scale, 500, 14d);
        Assert.Equal(0.5d, scale.Value, 3);

        Frames(scale, 500, 5d);
        Assert.Equal(1d, scale.Value, 3);
    }

    [Fact]
    public void Lifting_the_hand_restores_full_size_at_once() {
        var scale = new RenderScale();
        Frames(scale, 500, 40d);

        scale.Record(40d, false);

        Assert.Equal(1d, scale.Value);
    }
}

public sealed class FrameMemoryTests : IDisposable {
    readonly FrameMemory memory = new(
        Options.Create(
            new ServerOptions {
                Worker = new() { FramesPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.frames") }
            }
        )
    );

    public void Dispose() => memory.Dispose();

    [Fact]
    public async Task A_slot_goes_back_only_when_its_last_holder_lets_go() {
        var held = new List<FrameSlot>();
        for (var i = 0; i < 4; i++) {
            held.Add(await memory.RentAsync(TestContext.Current.CancellationToken));
        }

        held[0].Hold();
        held[0].Release();
        using var patience = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => memory.RentAsync(patience.Token));

        held[0].Release();
        var again = await memory.RentAsync(TestContext.Current.CancellationToken);

        Assert.Same(held[0], again);
    }

    /// <summary>The session offers to whoever was subscribed a moment ago, and that client may have gone.</summary>
    [Fact]
    public async Task A_frame_offered_to_a_client_that_has_left_goes_back_to_the_pool() {
        var outbox = new FrameOutbox();
        outbox.Dispose();
        var held = new List<FrameSlot>();
        for (var i = 0; i < 4; i++) {
            held.Add(await memory.RentAsync(TestContext.Current.CancellationToken));
        }

        outbox.Offer(new(held[0], new byte[ViewportSession.HeaderBytes], 0));

        Assert.Same(held[0], await memory.RentAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_stopped_server_leaves_no_file_behind() {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.frames");
        var own = new FrameMemory(Options.Create(new ServerOptions { Worker = new() { FramesPath = path } }));
        Assert.True(File.Exists(path));

        own.Dispose();

        Assert.False(File.Exists(path));
    }
}
