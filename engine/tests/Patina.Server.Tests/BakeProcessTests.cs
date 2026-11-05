using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Vixen.NodeGraph;

namespace Patina.Server.Tests;

/// <summary>
///     Bakes as a client that is not the editor sees them: the real Server and Worker as processes, paths in and paths
///     out. S5's kill lands here, in the middle of a bake.
/// </summary>
public sealed class BakeProcessTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-bake-");
    readonly string socketPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");
    readonly List<Process> started = [];
    readonly HttpClient http = new();

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
        return new(
            Path.Combine(Meshes, BakeFixture.HighFile),
            Path.Combine(Meshes, BakeFixture.LowFile),
            Output,
            Resolution: resolution,
            Maps: ["ao"],
            OcclusionRays: rays,
            SearchRadius: 0.05
        );
    }

    /// <summary>A bake that takes long enough to be interrupted on a loaded machine and still finishes.</summary>
    BakeRequest Slow() => Request(1024, 256);

    async Task<JobState> StartAsync(BakeRequest request, CancellationToken cancellationToken) {
        using var response = await http.PostAsJsonAsync("/api/v1/jobs/bakes", request, ContractsJsonContext.Default.BakeRequest, cancellationToken);
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

        var again = await StartAsync(Request() with { Output = Path.Combine(Output, "elsewhere"), Name = "other" }, deadline.Token);

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
                using var client = new HttpClient { BaseAddress = new($"http://127.0.0.1:{instance.Port}") };
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
    ///     every five seconds and costs it the link when unanswered, and reports at the same time. Waiting out two probes
    ///     mid-bake is the assertion: the reply is a fresh uptime while the same job is still running on the same attempt.
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
        var job = await UntilAsync((await StartAsync(Request(128, 8), deadline.Token)).Id, Over, deadline.Token);
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
        Assert.Contains(unbound.Diagnostics, static d => d.Message.Contains("no bake has been chosen", StringComparison.Ordinal));

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

    static async Task<ClientWebSocket> FollowAsync(
        ServerInstance instance,
        GraphState state,
        CancellationToken cancellationToken
    ) {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(
            new($"ws://127.0.0.1:{instance.Port}/ws/graphs/{state.Id}?since={state.Sequence}&access_token={instance.Token}"),
            cancellationToken
        );
        return socket;
    }

    /// <summary>The next evaluation of a revision or later, or skips the test where the Worker has no Vulkan.</summary>
    static async Task<GraphEvaluation> NextAsync(ClientWebSocket socket, long revision, CancellationToken cancellationToken) {
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
                    Assert.Skip(error);
                }

                return evaluation;
            }
        }
    }

    static async Task<GraphEvaluation> EvaluatedAsync(ClientWebSocket socket, long revision, CancellationToken cancellationToken) {
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
        await socket.ConnectAsync(new($"ws://127.0.0.1:{instance.Port}/ws/jobs?since=0&access_token={instance.Token}"), deadline.Token);
        StartWorker();

        await StartAsync(Request(128, 8), deadline.Token);

        var buffer = new byte[1 << 20];
        var numbers = new List<long>();
        while (true) {
            var received = await socket.ReceiveAsync(buffer, deadline.Token);
            var change = JsonSerializer.Deserialize(buffer.AsSpan(0, received.Count), ContractsJsonContext.Default.JobEvent)!;
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

        var missing = new BakeRequest("/nowhere/high.glb", "/nowhere/low.glb", Output);
        using var notFound = await http.PostAsJsonAsync("/api/v1/jobs/bakes", missing, ContractsJsonContext.Default.BakeRequest, deadline.Token);
        using var refused = await http.PostAsJsonAsync("/api/v1/jobs/bakes", Request() with { Format = "tga" }, ContractsJsonContext.Default.BakeRequest, deadline.Token);

        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Empty((await http.GetFromJsonAsync("/api/v1/jobs", ContractsJsonContext.Default.JobPage, deadline.Token))!.Jobs);
    }
}
