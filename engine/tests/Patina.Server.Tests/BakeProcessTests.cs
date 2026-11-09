using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Vixen.Core.Imaging;
using Vixen.NodeGraph;

namespace Patina.Server.Tests;

/// <summary>
///     Bakes as a client that is not the editor sees them: the real Server and Worker as processes, paths in and paths
///     out. S5's kill lands here, in the middle of a bake.
/// </summary>
public sealed partial class BakeProcessTests : IDisposable {
    const int ViewportFrameHeader = 32;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-bake-");
    readonly string socketPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");
    readonly List<Process> started = [];
    readonly HttpClient http = new();
    readonly HttpClient restarted = new();

    string Library => Directory.CreateDirectory(Path.Combine(folder.FullName, "Game.ptlib")).FullName;

    string Meshes => Path.Combine(folder.FullName, "meshes");

    string Output => Path.Combine(folder.FullName, "out");

    string DiscoveryFile => Path.Combine(folder.FullName, "servers.json");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() {
        foreach (var process in started) {
            if (!process.HasExited) {
                process.Kill(true);
                process.WaitForExit();
            }

            process.Dispose();
        }

        http.Dispose();
        restarted.Dispose();
        File.Delete(socketPath);
        try {
            folder.Delete(true);
        } catch (IOException) {
            // A Worker's last write can land while the folder is going, and a temp folder left behind is no failure.
        }
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
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        started.Add(process);
        return process;
    }

    Process StartWorker() =>
        Start(Engine.Dll("Patina.Worker", "patina-worker"), "--socket", socketPath, "--watch-stdin");

    Process StartServer() =>
        Start(
            Engine.Dll("Patina.Server", "patina-server"),
            "--library",
            Library,
            $"--Patina:DiscoveryFile={DiscoveryFile}",
            $"--Patina:Worker:SocketPath={socketPath}",
            "--Patina:WatchStdin=true"
        );

    async Task DiscoverAsync(CancellationToken cancellationToken) {
        var registry = new InstanceRegistry(DiscoveryFile);
        while (true) {
            if (await registry.FindAsync(Library, cancellationToken) is { } instance) {
                http.BaseAddress = new($"http://127.0.0.1:{instance.Port}");
                http.DefaultRequestHeaders.Authorization = new("Bearer", instance.Token);
                return;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    static CancellationTokenSource Deadline(int seconds) {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
        return deadline;
    }

    BakeRequest Request(int resolution = 64, int rays = 8) {
        BakeFixture.Write(Meshes, 0.02f);
        return new() {
            High = [Path.Combine(Meshes, BakeFixture.HighFile)],
            Low = Path.Combine(Meshes, BakeFixture.LowFile),
            Output = Output,
            Options = new() {
                Resolution = resolution, Maps = ["normal", "height", "ao"], Occlusion = new() { Rays = rays }
            }
        };
    }

    /// <summary>A bake that takes long enough to be interrupted on a loaded machine and still finishes.</summary>
    BakeRequest Slow() => Request(1024, 256);

    async Task<JobState> StartAsync(BakeRequest request, CancellationToken cancellationToken) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/jobs/bakes",
            request,
            ContractsJsonContext.Default.BakeRequest,
            cancellationToken
        );
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.JobState, cancellationToken))!;
    }

    async Task<JobState> GetAsync(string id, CancellationToken cancellationToken) =>
        (await http.GetFromJsonAsync($"/api/v1/jobs/{id}", ContractsJsonContext.Default.JobState, cancellationToken))!;

    async Task<JobState> UntilAsync(string id, Func<JobState, bool> wanted, CancellationToken cancellationToken) {
        while (true) {
            var job = await GetAsync(id, cancellationToken);
            if (wanted(job)) {
                return job;
            }

            await Task.Delay(20, cancellationToken);
        }
    }

    static bool Over(JobState job) => job.State is JobStates.Done or JobStates.Failed or JobStates.Cancelled;

    /// <summary>Paths in, paths out, and every path a real file, with no pixel in the reply.</summary>
    [Fact]
    public async Task A_bake_runs_on_the_worker_and_its_results_are_files() {
        using var deadline = Deadline(120);
        StartServer();
        await DiscoverAsync(deadline.Token);
        StartWorker();

        var job = await StartAsync(Request(), deadline.Token);
        var done = await UntilAsync(job.Id, Over, deadline.Token);

        Assert.Equal(JobStates.Done, done.State);
        Assert.Equal(1, done.Attempts);
        Assert.False(done.Result!.Cached);
        Assert.Equal(["normal", "height", "ao"], done.Result.Maps.Select(static map => map.Map));
        Assert.All(done.Result.Maps, static map => Assert.True(new FileInfo(map.Path).Length > 0, map.Path));
        Assert.True(File.Exists(Path.Combine(Output, "housing_low.bake.json")));

        using var preview = await http.GetAsync($"/api/v1/jobs/{job.Id}/previews/normal", deadline.Token);
        Assert.Equal("image/png", preview.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>The key's whole point: the same bake again runs nothing, and needs no Worker to say so.</summary>
    [Fact]
    public async Task The_same_bake_again_comes_from_the_cache() {
        using var deadline = Deadline(120);
        StartServer();
        await DiscoverAsync(deadline.Token);
        var worker = StartWorker();

        var first = await UntilAsync((await StartAsync(Request(), deadline.Token)).Id, Over, deadline.Token);
        Assert.Equal(JobStates.Done, first.State);

        worker.Kill(true);
        await worker.WaitForExitAsync(deadline.Token);

        var again = await StartAsync(
            Request() with { Output = Path.Combine(Output, "elsewhere"), Name = "other" },
            deadline.Token
        );

        Assert.Equal(JobStates.Done, again.State);
        Assert.True(again.Result!.Cached);
        Assert.True(File.Exists(Path.Combine(Output, "elsewhere", "other_normal.png")));
    }

    /// <summary>S5's bake half: a Worker killed in a bake costs an attempt and nothing else.</summary>
    [Fact]
    public async Task A_killed_worker_loses_no_job_and_its_successor_runs_it() {
        using var deadline = Deadline(300);
        StartServer();
        await DiscoverAsync(deadline.Token);
        var worker = StartWorker();

        var job = await StartAsync(Slow(), deadline.Token);
        await UntilAsync(job.Id, static j => j is { State: JobStates.Running, Progress: > 0.1 }, deadline.Token);

        worker.Kill(true);
        await worker.WaitForExitAsync(deadline.Token);
        var back = await UntilAsync(job.Id, static j => j.State == JobStates.Queued, deadline.Token);
        Assert.Equal(0, back.Progress);

        var clock = Stopwatch.StartNew();
        StartWorker();
        var running = await UntilAsync(job.Id, static j => j.Attempts == 2, deadline.Token);
        Assert.Equal(JobStates.Running, running.State);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"The job waited {clock.Elapsed} for a new Worker.");

        var done = await UntilAsync(job.Id, Over, deadline.Token);
        Assert.Equal(JobStates.Done, done.State);
        Assert.Equal(2, done.Attempts);
    }

    /// <summary>The Server owns the job, and the file it keeps it in outlives it.</summary>
    [Fact]
    public async Task A_killed_server_comes_back_to_its_queue() {
        using var deadline = Deadline(300);
        var server = StartServer();
        await DiscoverAsync(deadline.Token);
        StartWorker();

        var job = await StartAsync(Slow(), deadline.Token);
        await UntilAsync(job.Id, static j => j is { State: JobStates.Running, Progress: > 0.1 }, deadline.Token);

        server.Kill(true);
        await server.WaitForExitAsync(deadline.Token);
        http.CancelPendingRequests();
        File.Delete(DiscoveryFile);
        File.Delete(socketPath);

        var address = http.BaseAddress;
        StartServer();
        while (true) {
            var instance = await new InstanceRegistry(DiscoveryFile).FindAsync(Library, deadline.Token);
            if (instance is not null && new Uri($"http://127.0.0.1:{instance.Port}") != address) {
                var client = restarted;
                client.BaseAddress = new($"http://127.0.0.1:{instance.Port}");
                client.DefaultRequestHeaders.Authorization = new("Bearer", instance.Token);

                // The old Worker reconnects by itself and is handed the job from the start.
                while (true) {
                    var seen = await client.GetFromJsonAsync(
                        $"/api/v1/jobs/{job.Id}",
                        ContractsJsonContext.Default.JobState,
                        deadline.Token
                    );
                    if (Over(seen!)) {
                        Assert.Equal(JobStates.Done, seen!.State);
                        Assert.True(seen.Attempts >= 2);
                        return;
                    }

                    await Task.Delay(50, deadline.Token);
                }
            }

            await Task.Delay(50, deadline.Token);
        }
    }

    /// <summary>A stop lands between rows of the atlas, not at the end of the bake.</summary>
    [Fact]
    public async Task A_cancelled_bake_stops_and_leaves_no_files() {
        using var deadline = Deadline(120);
        StartServer();
        await DiscoverAsync(deadline.Token);
        StartWorker();

        var job = await StartAsync(Slow(), deadline.Token);
        await UntilAsync(job.Id, static j => j is { State: JobStates.Running, Progress: > 0.1 }, deadline.Token);

        using var response = await http.PostAsync($"/api/v1/jobs/{job.Id}/cancel", null, deadline.Token);
        var stopped = await UntilAsync(job.Id, Over, deadline.Token);

        Assert.Equal(JobStates.Cancelled, stopped.State);
        Assert.True(stopped.Progress < 1, "It ran to the end first.");
        Assert.False(Directory.Exists(Output));
    }

    /// <summary>
    ///     ADR 0008: a long bake holds up nothing. The Worker goes on answering the Server's health probe, which comes
    ///     every five seconds and costs it the link when unanswered, and reports at the same time. Waiting out two
    ///     probes
    ///     mid-bake is the assertion: the reply is a fresh uptime while the same job is still running on the same
    ///     attempt.
    /// </summary>
    [Fact]
    public async Task The_worker_keeps_answering_through_a_bake() {
        using var deadline = Deadline(300);
        StartServer();
        await DiscoverAsync(deadline.Token);
        StartWorker();

        var job = await StartAsync(Request(2048, 256), deadline.Token);
        await UntilAsync(job.Id, static j => j is { State: JobStates.Running, Progress: > 0.06 }, deadline.Token);

        var before = await SystemAsync(deadline.Token);
        while ((await SystemAsync(deadline.Token)).Worker.UptimeSeconds <= before.Worker.UptimeSeconds + 4) {
            await Task.Delay(100, deadline.Token);
        }

        var during = await GetAsync(job.Id, deadline.Token);
        Assert.Equal(JobStates.Running, during.State);
        Assert.Equal(1, during.Attempts);
        Assert.True((await SystemAsync(deadline.Token)).Worker.Connected);

        await http.PostAsync($"/api/v1/jobs/{job.Id}/cancel", null, deadline.Token);
    }

    /// <summary>
    ///     ADR 0008 with the GPU in it: a graph edit made while a long bake runs is evaluated while it runs. The job is
    ///     looked at after the evaluation arrives, which is what makes this an order and not a timing.
    /// </summary>
    [Fact]
    public async Task A_graph_evaluates_while_a_bake_runs() {
        using var deadline = Deadline(300);
        StartServer();
        await DiscoverAsync(deadline.Token);
        var instance = (await new InstanceRegistry(DiscoveryFile).FindAsync(Library, deadline.Token))!;
        var state = await http.OpenAsync(GraphTesting.Write(Library, GraphTesting.Sample()), deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        StartWorker();
        await EvaluatedAsync(socket, state.Revision, deadline.Token);

        var job = await StartAsync(Request(2048, 256), deadline.Token);
        await UntilAsync(job.Id, static j => j is { State: JobStates.Running, Progress: > 0.06 }, deadline.Token);

        var edit = await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(2, "Hue", [0.25f]));
        var evaluation = await EvaluatedAsync(socket, edit.Revision, deadline.Token);
        var during = await GetAsync(job.Id, deadline.Token);

        Assert.True(evaluation.Ran > 0);
        Assert.Equal(JobStates.Running, during.State);
        Assert.Equal(1, during.Attempts);

        await http.PostAsync($"/api/v1/jobs/{job.Id}/cancel", null, deadline.Token);
    }

    /// <summary>A baked map is a graph input: chosen once, remembered beside the file, and read by the node.</summary>
    [Fact]
    public async Task A_mesh_map_node_reads_a_finished_bake() {
        using var deadline = Deadline(120);
        StartServer();
        await DiscoverAsync(deadline.Token);
        var instance = (await new InstanceRegistry(DiscoveryFile).FindAsync(Library, deadline.Token))!;
        StartWorker();
        var job = await UntilAsync((await StartAsync(Request(128), deadline.Token)).Id, Over, deadline.Token);
        Assert.Equal(JobStates.Done, job.State);

        var asset = new NodeGraphAsset {
            Name = "Worn",
            Nodes = [
                new() { Id = 1, Type = "Source/Mesh Map", Texts = new() { ["Map"] = "ao" } },
                new() { Id = 2, Type = "Output/Output", X = 300, Texts = new() { ["Usage"] = "baseColor" } }
            ],
            Edges = [new() { FromNode = 1, FromPort = "Out", ToNode = 2, ToPort = "Input" }],
            Settings = new() { ["baseWidth"] = "64", ["baseHeight"] = "64" }
        };
        var path = GraphTesting.Write(Library, asset, "worn.vxtexgraph");
        var state = await http.OpenAsync(path, deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);

        // Nothing chosen yet, and the graph says so rather than reading black.
        var unbound = await NextAsync(socket, state.Revision, deadline.Token);
        Assert.Contains(
            unbound.Diagnostics,
            static d => d.Message.Contains("no bake has been chosen", StringComparison.Ordinal)
        );

        using var chosen = await http.PutAsJsonAsync(
            $"/api/v1/graphs/{state.Id}/mesh-maps",
            new(Output),
            ContractsJsonContext.Default.GraphMeshMapsRequest,
            deadline.Token
        );
        Assert.Equal(HttpStatusCode.NoContent, chosen.StatusCode);

        var bound = await NextAsync(socket, state.Revision, deadline.Token);
        Assert.DoesNotContain(bound.Diagnostics, static d => d.Severity == "Error");
        Assert.NotEmpty(bound.Previews);
        Assert.True(File.Exists($"{path}.bake"));
    }

    /// <summary>A generator on a stack: baked mesh maps through a graph, as a mask, from a folder it chose.</summary>
    [Fact]
    public async Task A_generator_mask_reads_the_bake_a_stack_chose() {
        using var deadline = Deadline(120);
        StartServer();
        await DiscoverAsync(deadline.Token);
        StartWorker();
        var request = Request(128);
        request = request with { Options = request.Options with { Maps = ["normal", "height", "ao", "curvature"] } };
        var job = await UntilAsync((await StartAsync(request, deadline.Token)).Id, Over, deadline.Token);
        Assert.Equal(JobStates.Done, job.State);

        using var created = await http.PostAsJsonAsync(
            "/api/v1/stacks/new",
            new(Path.Combine(folder.FullName, "Worn.vxlayers"), 128, 128),
            ContractsJsonContext.Default.StackCreateRequest,
            deadline.Token
        );
        created.EnsureSuccessStatusCode();
        var stack = (await created.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackState, deadline.Token))!;
        using var chosen = await http.PutAsJsonAsync(
            $"/api/v1/stacks/{stack.Id}/mesh-maps",
            new(Output),
            ContractsJsonContext.Default.StackMeshMapsRequest,
            deadline.Token
        );
        Assert.Equal(HttpStatusCode.NoContent, chosen.StatusCode);

        using var masked = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{stack.Id}/commands",
            new([new StackSetLayer("paint", Mask: new("Generator", Generator: "Generators/Dirt"))]),
            ContractsJsonContext.Default.StackCommandBatch,
            deadline.Token
        );
        Assert.True(masked.IsSuccessStatusCode, await masked.Content.ReadAsStringAsync(deadline.Token));

        using var pixels = await http.GetAsync($"/api/v1/stacks/{stack.Id}/pixels", deadline.Token);
        if (pixels.StatusCode == HttpStatusCode.UnprocessableEntity) {
            Gpu.Skip(await pixels.Content.ReadAsStringAsync(deadline.Token));
        }

        pixels.EnsureSuccessStatusCode();
        Assert.True(File.Exists($"{stack.Path}.bake"));
    }

    /// <summary>The farm's path: mesh maps, cage and skew map with no Server and no Worker, file to file.</summary>
    [Fact]
    public async Task The_command_line_bakes_with_a_cage_and_a_skew_map_and_a_graph_reads_the_result() {
        using var deadline = Deadline(120);
        var request = Request();
        var skew = Path.Combine(folder.FullName, "skew.png");
        await File.WriteAllBytesAsync(skew, PngCodec.Encode(new(1, 1, [128, 128, 128, 255])), deadline.Token);

        var (baked, said) = await CommandAsync(
            deadline.Token,
            "bake-maps",
            "--high",
            request.High[0],
            "--low",
            request.Low,
            "--cage",
            request.Low,
            "--skew",
            "0.5",
            "--skew-map",
            skew,
            "--maps",
            "ao",
            "--resolution",
            "64",
            "--rays",
            "4",
            "--out",
            Output
        );
        Assert.True(baked == 0, said);
        Assert.True(File.Exists(Path.Combine(Output, "housing_low_ao.png")));

        var asset = new NodeGraphAsset {
            Name = "Worn",
            Nodes = [
                new() { Id = 1, Type = "Source/Mesh Map", Texts = new() { ["Map"] = "ao" } },
                new() { Id = 2, Type = "Output/Output", X = 300, Texts = new() { ["Usage"] = "baseColor" } }
            ],
            Edges = [new() { FromNode = 1, FromPort = "Out", ToNode = 2, ToPort = "Input" }],
            Settings = new() { ["baseWidth"] = "64", ["baseHeight"] = "64" }
        };
        var graph = GraphTesting.Write(Library, asset, "worn.vxtexgraph");
        var pictures = Path.Combine(folder.FullName, "pictures");

        var (unbound, refusal) = await CommandAsync(deadline.Token, "bake", graph, pictures);
        var (bound, reason) = await CommandAsync(deadline.Token, "bake", graph, pictures, "--mesh-maps", Output);

        if (reason.Contains("no Vulkan device", StringComparison.Ordinal)) {
            Gpu.Skip(reason);
        }

        Assert.NotEqual(0, unbound);
        Assert.Contains("no bake has been chosen", refusal, StringComparison.Ordinal);
        Assert.True(bound == 0, reason);
        Assert.True(File.Exists(Path.Combine(pictures, "baseColor.png")));
    }

    static async Task<(int Exit, string Said)> CommandAsync(
        CancellationToken cancellationToken,
        params string[] arguments
    ) {
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Engine.Dll("Patina.Cli", "patina"));
        foreach (var argument in arguments) {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await output + await errors);
    }

    async Task<ClientWebSocket> ViewportAsync(CancellationToken cancellationToken) {
        var instance = (await new InstanceRegistry(DiscoveryFile).FindAsync(Library, cancellationToken))!;
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

    async Task<ViewportState> ViewportStateAsync(CancellationToken cancellationToken) =>
        (await http.GetFromJsonAsync(
            "/api/v1/viewport",
            ContractsJsonContext.Default.ViewportState,
            cancellationToken
        ))!;

    /// <summary>One whole frame, or skips the test where the Worker has no Vulkan.</summary>
    async Task<byte[]> FrameAsync(ClientWebSocket socket, CancellationToken cancellationToken) {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var buffer = new byte[16 * 1024 * 1024];
        var length = 0;
        var receive = socket.ReceiveAsync(buffer.AsMemory(length), patience.Token).AsTask();
        while (true) {
            if (await Task.WhenAny(receive, Task.Delay(250, cancellationToken)) != receive) {
                if ((await ViewportStateAsync(cancellationToken)).Error is { } error
                    && error.StartsWith("No Vulkan", StringComparison.Ordinal)) {
                    await patience.CancelAsync();
                    Gpu.Skip(error);
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

    static bool SamePicture(byte[] one, byte[] two) =>
        one.Length == two.Length
        && Enumerable.Range(ViewportFrameHeader, one.Length - ViewportFrameHeader)
            .All(at => Math.Abs(one[at] - two[at]) <= 3);

    /// <summary>
    ///     What an artist judges a bake on, by a client that is not the editor: choose the bake and the low mesh shows
    ///     it. The Server holds the choice, so the Worker that dies with it loses nothing.
    /// </summary>
    [Fact]
    public async Task A_bake_shows_on_its_low_mesh_and_outlives_the_worker() {
        using var deadline = Deadline(180);
        StartServer();
        await DiscoverAsync(deadline.Token);
        var worker = StartWorker();
        var job = await UntilAsync((await StartAsync(Request(256), deadline.Token)).Id, Over, deadline.Token);
        Assert.Equal(JobStates.Done, job.State);

        using var socket = await ViewportAsync(deadline.Token);
        await SendAsync(socket, new(Width: 160, Height: 100, Input: 1), deadline.Token);
        while ((await ViewportStateAsync(deadline.Token)).Width != 160) {
            await Task.Delay(10, deadline.Token);
        }

        using var opened = await http.PutAsJsonAsync(
            "/api/v1/viewport/mesh",
            new(job.Bake!.Low),
            ContractsJsonContext.Default.ViewportMeshRequest,
            deadline.Token
        );
        opened.EnsureSuccessStatusCode();
        var bare = await FrameAsync(socket, deadline.Token);

        using var shown = await http.PutAsJsonAsync(
            "/api/v1/viewport/bake",
            new(job.Id),
            ContractsJsonContext.Default.ViewportBakeRequest,
            deadline.Token
        );
        shown.EnsureSuccessStatusCode();
        var state = (await shown.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.ViewportState,
            deadline.Token
        ))!;
        Assert.Equal(job.Bake.Low, state.Mesh);
        Assert.EndsWith("_normal.png", state.Maps.Normal, StringComparison.Ordinal);
        Assert.EndsWith("_ao.png", state.Maps.Occlusion, StringComparison.Ordinal);

        var baked = await FrameAsync(socket, deadline.Token);
        Assert.False(SamePicture(bare, baked));

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        await SendAsync(socket, new(Input: 9), deadline.Token);
        await Task.Delay(100, deadline.Token);
        StartWorker();

        var again = await FrameAsync(socket, deadline.Token);
        while (BinaryPrimitives.ReadUInt32LittleEndian(again.AsSpan(16)) != 9) {
            again = await FrameAsync(socket, deadline.Token);
        }

        Assert.True(SamePicture(baked, again), "A new Worker drew something else.");
    }

    /// <summary>Shows a graph in the viewport, once its first evaluation holds a picture.</summary>
    async Task<ViewportState> ShowGraphAsync(string graph, CancellationToken cancellationToken) {
        while (true) {
            using var response = await http.PutAsJsonAsync(
                "/api/v1/viewport/graph",
                new(graph),
                ContractsJsonContext.Default.ViewportGraphRequest,
                cancellationToken
            );
            if (response.IsSuccessStatusCode) {
                return (await response.Content.ReadFromJsonAsync(
                    ContractsJsonContext.Default.ViewportState,
                    cancellationToken
                ))!;
            }

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(100, cancellationToken);
        }
    }

    [Fact]
    public async Task A_graph_s_outputs_lie_on_the_open_mesh() {
        using var deadline = Deadline(120);
        StartServer();
        await DiscoverAsync(deadline.Token);
        StartWorker();
        var request = Request();
        var asset = new NodeGraphAsset {
            Name = "Painted",
            Nodes = [
                new() { Id = 1, Type = "Source/Gradient" },
                new() { Id = 2, Type = "Output/Output", X = 300, Texts = new() { ["Usage"] = "roughness" } },
                new() { Id = 3, Type = "Output/Output", X = 300, Texts = new() { ["Usage"] = "emissive" } }
            ],
            Edges = [
                new() { FromNode = 1, FromPort = "Out", ToNode = 2, ToPort = "Input" },
                new() { FromNode = 1, FromPort = "Out", ToNode = 3, ToPort = "Input" }
            ],
            Settings = new() { ["baseWidth"] = "64", ["baseHeight"] = "64" }
        };
        var graph = await http.OpenAsync(GraphTesting.Write(Library, asset, "painted.vxtexgraph"), deadline.Token);

        // A machine with no Vulkan never holds a picture to show, so this is where it skips.
        var instance = (await new InstanceRegistry(DiscoveryFile).FindAsync(Library, deadline.Token))!;
        using var evaluations = await FollowAsync(instance, graph, deadline.Token);
        await NextAsync(evaluations, graph.Revision, deadline.Token);

        using var socket = await ViewportAsync(deadline.Token);

        // With no mesh open, a graph is seen on a shape.
        var onShape = await ShowGraphAsync(graph.Id, deadline.Token);
        Assert.EndsWith("Sphere.glb", onShape.Mesh, StringComparison.Ordinal);

        using var opened = await http.PutAsJsonAsync(
            "/api/v1/viewport/mesh",
            new(request.Low),
            ContractsJsonContext.Default.ViewportMeshRequest,
            deadline.Token
        );
        opened.EnsureSuccessStatusCode();
        await FrameAsync(socket, deadline.Token);

        var shown = await ShowGraphAsync(graph.Id, deadline.Token);

        Assert.True(File.Exists(shown.Maps.Roughness), shown.Maps.Roughness);
        Assert.Null(shown.Maps.BaseColor);
        await FrameAsync(socket, deadline.Token);
        Assert.Null((await ViewportStateAsync(deadline.Token)).Error);
    }

    [Fact]
    public async Task A_picture_the_worker_cannot_read_costs_the_picture_and_not_the_view() {
        using var deadline = Deadline(120);
        StartServer();
        await DiscoverAsync(deadline.Token);
        StartWorker();
        var request = Request();
        using var socket = await ViewportAsync(deadline.Token);
        using var opened = await http.PutAsJsonAsync(
            "/api/v1/viewport/mesh",
            new(request.Low),
            ContractsJsonContext.Default.ViewportMeshRequest,
            deadline.Token
        );
        opened.EnsureSuccessStatusCode();
        await FrameAsync(socket, deadline.Token);

        var broken = Path.Combine(folder.FullName, "broken.png");
        await File.WriteAllBytesAsync(broken, [1, 2, 3], deadline.Token);
        using var laid = await http.PutAsJsonAsync(
            "/api/v1/viewport/maps",
            new(broken),
            ContractsJsonContext.Default.ViewportMapPaths,
            deadline.Token
        );
        laid.EnsureSuccessStatusCode();
        await SendAsync(socket, new(new(1d, 0.2d, 2.5d)), deadline.Token);
        await FrameAsync(socket, deadline.Token);

        Assert.NotNull((await ViewportStateAsync(deadline.Token)).Error);
    }

    [Fact]
    public async Task What_cannot_be_shown_is_refused_and_says_why() {
        using var deadline = Deadline(120);
        StartServer();
        await DiscoverAsync(deadline.Token);

        async Task<HttpStatusCode> ShowAsync(string id) {
            using var response = await http.PutAsJsonAsync(
                "/api/v1/viewport/bake",
                new(id),
                ContractsJsonContext.Default.ViewportBakeRequest,
                deadline.Token
            );
            return response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.NotFound, await ShowAsync("nothing"));

        // No Worker, so this job stays queued.
        var queued = await StartAsync(Request(), deadline.Token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await ShowAsync(queued.Id));

        using var missing = await http.PutAsJsonAsync(
            "/api/v1/viewport/maps",
            new(Normal: Path.Combine(folder.FullName, "gone.png")),
            ContractsJsonContext.Default.ViewportMapPaths,
            deadline.Token
        );
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // A preview of a bake that could not run is refused before anything is read.
        using var gone = await PreviewAsync(
            new() { High = queued.Bake!.High, Low = Path.Combine(folder.FullName, "gone.glb") },
            deadline.Token
        );
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        using var reachless = await PreviewAsync(
            new() {
                High = queued.Bake.High, Low = queued.Bake.Low, Options = new() { Rays = new() { Front = 0, Rear = 0 } }
            },
            deadline.Token
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reachless.StatusCode);
    }

    static async Task<ClientWebSocket> FollowAsync(
        ServerInstance instance,
        GraphState state,
        CancellationToken cancellationToken
    ) {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(
            new(
                $"ws://127.0.0.1:{instance.Port}/ws/graphs/{state.Id}"
                + $"?since={state.Sequence}&access_token={instance.Token}"
            ),
            cancellationToken
        );
        return socket;
    }

    /// <summary>The next evaluation of a revision or later, or skips the test where the Worker has no Vulkan.</summary>
    static async Task<GraphEvaluation> NextAsync(
        ClientWebSocket socket,
        long revision,
        CancellationToken cancellationToken
    ) {
        var buffer = new byte[1 << 20];
        while (true) {
            var length = 0;
            ValueWebSocketReceiveResult part;
            do {
                part = await socket.ReceiveAsync(buffer.AsMemory(length), cancellationToken);
                length += part.Count;
            } while (!part.EndOfMessage);

            var item = JsonSerializer.Deserialize(buffer.AsSpan(0, length), ContractsJsonContext.Default.GraphEvent)!;
            if (item.Evaluation is { } evaluation && evaluation.Revision >= revision) {
                if (evaluation.Error is { } error && error.StartsWith("No Vulkan", StringComparison.Ordinal)) {
                    Gpu.Skip(error);
                }

                return evaluation;
            }
        }
    }

    static async Task<GraphEvaluation> EvaluatedAsync(
        ClientWebSocket socket,
        long revision,
        CancellationToken cancellationToken
    ) {
        var evaluation = await NextAsync(socket, revision, cancellationToken);
        Assert.Null(evaluation.Error);
        return evaluation;
    }

    async Task<SystemInfo> SystemAsync(CancellationToken cancellationToken) =>
        (await http.GetFromJsonAsync("/api/v1/system", ContractsJsonContext.Default.SystemInfo, cancellationToken))!;

    /// <summary>Progress reaches a client over the socket, numbered, ending in the result.</summary>
    [Fact]
    public async Task A_client_follows_a_bake_over_the_socket() {
        using var deadline = Deadline(120);
        StartServer();
        await DiscoverAsync(deadline.Token);
        var instance = (await new InstanceRegistry(DiscoveryFile).FindAsync(Library, deadline.Token))!;
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(
            new($"ws://127.0.0.1:{instance.Port}/ws/jobs?since=0&access_token={instance.Token}"),
            deadline.Token
        );
        StartWorker();

        await StartAsync(Request(128), deadline.Token);

        var buffer = new byte[1 << 20];
        var numbers = new List<long>();
        while (true) {
            var received = await socket.ReceiveAsync(buffer, deadline.Token);
            var change = JsonSerializer.Deserialize(
                buffer.AsSpan(0, received.Count),
                ContractsJsonContext.Default.JobEvent
            )!;
            numbers.Add(change.Sequence);
            if (Over(change.Job)) {
                Assert.Equal(JobStates.Done, change.Job.State);
                break;
            }
        }

        Assert.Equal([..numbers.Order()], numbers);
        Assert.True(numbers.Count > 2);
    }

    /// <summary>A refusal says why, and a missing file is a 404 and not a job that fails later.</summary>
    [Fact]
    public async Task A_bad_bake_is_refused_before_it_is_queued() {
        using var deadline = Deadline(60);
        StartServer();
        await DiscoverAsync(deadline.Token);

        var missing = new BakeRequest { High = ["/nowhere/high.glb"], Low = "/nowhere/low.glb", Output = Output };
        using var notFound = await http.PostAsJsonAsync(
            "/api/v1/jobs/bakes",
            missing,
            ContractsJsonContext.Default.BakeRequest,
            deadline.Token
        );
        using var refused = await http.PostAsJsonAsync(
            "/api/v1/jobs/bakes",
            Request() with { Options = new() { Format = "tga" } },
            ContractsJsonContext.Default.BakeRequest,
            deadline.Token
        );

        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Empty(
            (await http.GetFromJsonAsync("/api/v1/jobs", ContractsJsonContext.Default.JobPage, deadline.Token))!.Jobs
        );
    }
}
