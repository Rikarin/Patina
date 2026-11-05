using Patina.Contracts;
using Patina.Library;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Vixen.NodeGraph;

namespace Patina.Server.Tests;

/// <summary>
///     The evaluation path as a client sees it: the real Server and Worker, and events over the real WebSocket.
/// </summary>
public sealed class GraphProcessTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-graph-");
    readonly string socketPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");
    readonly List<Process> started = [];
    readonly HttpClient http = new();

    string Library => Directory.CreateDirectory(Path.Combine(folder.FullName, "Game.ptlib")).FullName;

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
                http.BaseAddress = new($"http://127.0.0.1:{instance.Port}");
                http.DefaultRequestHeaders.Authorization = new("Bearer", instance.Token);
                return instance;
            }

            await Task.Delay(50, cancellationToken);
        }
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

    static async Task<GraphEvent> ReceiveAsync(
        ClientWebSocket socket,
        byte[] buffer,
        CancellationToken cancellationToken
    ) {
        var length = 0;
        while (true) {
            var part = await socket.ReceiveAsync(buffer.AsMemory(length), cancellationToken);
            length += part.Count;
            if (part.EndOfMessage) {
                return JsonSerializer.Deserialize(buffer.AsSpan(0, length), ContractsJsonContext.Default.GraphEvent)!;
            }
        }
    }

    /// <summary>
    ///     The first evaluation of a revision or later, or skips the test where the Worker has no Vulkan.
    /// </summary>
    static async Task<GraphEvaluation> EvaluatedAsync(
        ClientWebSocket socket,
        long revision,
        CancellationToken cancellationToken
    ) {
        var buffer = new byte[1024 * 1024];
        while (true) {
            var item = await ReceiveAsync(socket, buffer, cancellationToken);
            if (item.Evaluation is not { } evaluation || evaluation.Revision < revision) {
                continue;
            }

            if (evaluation.Error is { } error) {
                if (error.StartsWith("No Vulkan", StringComparison.Ordinal)) {
                    Assert.Skip(error);
                }

                Assert.Fail(error);
            }

            return evaluation;
        }
    }

    static CancellationTokenSource Deadline(int seconds) {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
        return deadline;
    }

    async Task<
        (ServerInstance Instance, GraphState State, ClientWebSocket Socket, GraphEvaluation First)> OpenSampleAsync(
        NodeGraphAsset asset,
        CancellationToken cancellationToken
    ) {
        var instance = await StartServerAsync(cancellationToken);
        var state = await http.OpenAsync(GraphTesting.Write(Library, asset), cancellationToken);
        var socket = await FollowAsync(instance, state, cancellationToken);
        StartWorker();
        return (instance, state, socket, await EvaluatedAsync(socket, state.Revision, cancellationToken));
    }

    [Fact]
    public async Task Every_node_gets_a_png_by_hash_and_the_pixels_stay_off_the_socket() {
        using var deadline = Deadline(120);
        var (_, state, socket, first) = await OpenSampleAsync(GraphTesting.Sample(), deadline.Token);
        using (socket) {
            Assert.True(first.Compiled);
            Assert.True(first.Ran > 0);
            Assert.DoesNotContain(first.Diagnostics, static d => d.Severity == "Error");
            Assert.Equal([1, 2, 3, 4, 5], first.Previews.Select(static p => p.Node).Distinct().Order());
            Assert.Contains(first.Previews, static p => p is { Node: 4, Port: "baseColor" });

            foreach (var preview in first.Previews) {
                var bytes = await http.GetByteArrayAsync($"/api/v1/previews/{preview.Hash}", deadline.Token);
                Assert.Equal((byte)0x89, bytes[0]);
                Assert.Equal(preview.Hash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
                Assert.Equal((128, 128), (preview.Width, preview.Height));
                Assert.True(
                    File.Exists(
                        Path.Combine(LibraryLayout.PreviewsPath(Library), preview.Hash[..2], preview.Hash + ".png")
                    )
                );
            }

            // Noise on its own is not a flat picture, and the blur differs from it.
            var noise = first.Previews.Single(static p => p.Node == 1);
            var blur = first.Previews.Single(static p => p.Node == 5);
            Assert.NotEqual(noise.Hash, blur.Hash);
            Assert.Equal(first.Previews.Count, (await http.StateAsync(state.Id, deadline.Token)).Previews.Count);
        }
    }

    [Fact]
    public async Task A_big_graph_gets_small_previews_of_the_real_image() {
        using var deadline = Deadline(120);
        var (_, _, socket, first) = await OpenSampleAsync(GraphTesting.Sample(1024), deadline.Token);
        using (socket) {
            Assert.All(first.Previews, static preview => Assert.Equal((256, 256), (preview.Width, preview.Height)));
        }
    }

    /// <summary>
    ///     Plan §3.2: moving a slider writes a uniform. Nothing upstream of it runs, and nothing is compiled.
    /// </summary>
    [Fact]
    public async Task A_slider_edit_does_not_recompile_and_runs_only_what_depends_on_it() {
        using var deadline = Deadline(120);
        var (_, state, socket, first) = await OpenSampleAsync(GraphTesting.Sample(), deadline.Token);
        using (socket) {
            var result = await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(2, "Hue", [0.25f]));
            var slid = await EvaluatedAsync(socket, result.Revision, deadline.Token);

            Assert.False(slid.Compiled);
            Assert.Equal(
                [2, 3, 4],
                slid.Previews.Select(static p => p.Node).Distinct().Order()
            );
            // HSL and Invert. The noise and the blur did not run, and a 128 graph needs no reductions.
            Assert.Equal(2, slid.Ran);
            Assert.True(slid.Reused >= 2, $"{slid.Reused} reused");

            // Every number a slider sends is its own kind of edit, and none of them compiles either.
            var again = await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(2, "Hue", [0.5f]));
            Assert.False((await EvaluatedAsync(socket, again.Revision, deadline.Token)).Compiled);

            // A text is a structural edit.
            var text = await http.EditAsync(state.Id, deadline.Token, new GraphSetText(4, "Usage", "normal"));
            Assert.True((await EvaluatedAsync(socket, text.Revision, deadline.Token)).Compiled);
        }
    }

    [Fact]
    public async Task Undoing_a_slider_edit_finds_the_old_picture_without_running_anything() {
        using var deadline = Deadline(120);
        var (_, state, socket, first) = await OpenSampleAsync(GraphTesting.Sample(), deadline.Token);
        using (socket) {
            var hsl = first.Previews.Single(static p => p.Node == 2);
            var edit = await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(2, "Hue", [0.25f]));
            var slid = await EvaluatedAsync(socket, edit.Revision, deadline.Token);
            Assert.NotEqual(hsl.Hash, slid.Previews.Single(static p => p.Node == 2).Hash);

            var undone = await http.ActAsync(state.Id, "undo", deadline.Token);
            var back = await EvaluatedAsync(socket, undone.Revision, deadline.Token);

            Assert.False(back.Compiled);
            Assert.Equal(0, back.Ran);
            Assert.Equal(hsl.Hash, back.Previews.Single(static p => p.Node == 2).Hash);
        }
    }

    [Fact]
    public async Task A_move_evaluates_nothing() {
        using var deadline = Deadline(120);
        var (_, state, socket, _) = await OpenSampleAsync(GraphTesting.Sample(), deadline.Token);
        using (socket) {
            await http.EditAsync(state.Id, deadline.Token, new GraphMoveNodes([new(1, 500, 500)]));
            var value = await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(2, "Hue", [0.25f]));

            var evaluation = await EvaluatedAsync(socket, value.Revision - 1, deadline.Token);

            Assert.Equal(value.Revision, evaluation.Revision);
        }
    }

    [Fact]
    public async Task Edits_while_the_graph_does_not_compile_report_diagnostics_and_recover() {
        using var deadline = Deadline(120);
        var asset = GraphTesting.Sample() with {
            Nodes = [..GraphTesting.Sample().Nodes, new() { Id = 9, Type = "Plugin/Gone" }]
        };
        var (_, state, socket, first) = await OpenSampleAsync(asset, deadline.Token);
        using (socket) {
            Assert.Contains(first.Diagnostics, static d => d.Severity == "Error");
            Assert.Empty(first.Previews);

            var removed = await http.EditAsync(state.Id, deadline.Token, new GraphRemoveNodes([9]));
            var fixedUp = await EvaluatedAsync(socket, removed.Revision, deadline.Token);

            Assert.DoesNotContain(fixedUp.Diagnostics, static d => d.Severity == "Error");
            Assert.NotEmpty(fixedUp.Previews);
        }
    }

    /// <summary>The rule holds through a compound: a number on one reaches its inner nodes without a compile (plan §3.2).</summary>
    [Fact]
    public async Task A_compound_gets_one_preview_per_port_and_a_number_on_it_does_not_recompile() {
        using var deadline = Deadline(120);
        var (_, state, socket, _) = await OpenSampleAsync(GraphTesting.Sample(), deadline.Token);
        using (socket) {
            var types = (await http.GetFromJsonAsync(
                $"/api/v1/graphs/{state.Id}/node-types",
                ContractsJsonContext.Default.IReadOnlyListGraphNodeType,
                deadline.Token
            ))!;
            var cells = types.Single(static type => type.Path == "Patterns/Cells");
            var number = cells.Ports.First(static port => port is { Direction: "Input", Kind: "Float" });
            var output = cells.Ports.First(static port => port.Direction == "Output");
            var added = await http.EditAsync(
                state.Id,
                deadline.Token,
                new GraphAddNode(cells.Path, 0, 500, -1)
            );
            var node = Assert.Single(added.Created).Node;
            var first = await EvaluatedAsync(socket, added.Revision, deadline.Token);
            Assert.Contains(first.Previews, p => p.Node == node && p.Port == output.Name);

            var edit = await http.EditAsync(
                state.Id,
                deadline.Token,
                new GraphSetValue(node, number.Name, [number.Default[0] + 1f])
            );
            var evaluated = await EvaluatedAsync(socket, edit.Revision, deadline.Token);
            Assert.False(evaluated.Compiled);
            Assert.DoesNotContain(evaluated.Previews, static p => p.Node is 1 or 2 or 3 or 5);
        }
    }

    /// <summary>
    ///     Spike S5 for graphs (ADR 0004): the Server has the document, and the next Worker is told all of it.
    /// </summary>
    [Fact]
    public async Task A_killed_worker_loses_nothing_and_its_successor_carries_on() {
        using var deadline = Deadline(180);
        var instance = await StartServerAsync(deadline.Token);
        var state = await http.OpenAsync(GraphTesting.Write(Library, GraphTesting.Sample(512)), deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        var worker = StartWorker();
        var first = await EvaluatedAsync(socket, state.Revision, deadline.Token);
        var original = first.Previews.Single(static p => p.Node == 2).Hash;

        // Mid-evaluation: a structural edit makes it compile, and the kill lands while the Worker has it.
        await http.EditAsync(state.Id, deadline.Token, new GraphSetText(4, "Usage", "normal"));
        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);

        // An edit during the gap is state on the Server, and undo still works on what came before the kill.
        var gap = await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(2, "Hue", [0.25f]));
        await Task.Delay(100, deadline.Token);
        var clock = Stopwatch.StartNew();
        StartWorker();

        var after = await EvaluatedAsync(socket, gap.Revision, deadline.Token);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Graph S5: first evaluation from the new Worker after {clock.ElapsedMilliseconds} ms"
        );
        Assert.True(after.Compiled);
        Assert.NotEqual(original, after.Previews.Single(static p => p.Node == 2).Hash);
        var held = await http.StateAsync(state.Id, deadline.Token);
        Assert.Equal(0.25f, Assert.Single(held.Nodes[1].Values["Hue"]));
        Assert.Equal("normal", held.Nodes[3].Texts["Usage"]);

        // The same document gives the same picture from a Worker that never saw it.
        await http.ActAsync(state.Id, "undo", deadline.Token);
        var undoneText = await http.ActAsync(state.Id, "undo", deadline.Token);
        var back = await EvaluatedAsync(socket, undoneText.Revision, deadline.Token);
        Assert.Equal(
            original,
            (await http.StateAsync(state.Id, deadline.Token)).Previews.Single(static p => p.Node == 2).Hash
        );
        Assert.Equal(undoneText.Revision, back.Revision);
    }

    [Fact]
    public async Task Deleting_the_cache_costs_only_a_re_evaluation() {
        using var deadline = Deadline(120);
        var (_, state, socket, first) = await OpenSampleAsync(GraphTesting.Sample(), deadline.Token);
        using (socket) {
            var preview = first.Previews[0];
            Directory.Delete(LibraryLayout.PreviewsPath(Library), true);
            using var gone = await http.GetAsync($"/api/v1/previews/{preview.Hash}", deadline.Token);
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

            using var asked = await http.PostAsync($"/api/v1/graphs/{state.Id}/evaluate", null, deadline.Token);
            Assert.Equal(HttpStatusCode.Accepted, asked.StatusCode);
            await EvaluatedAsync(socket, state.Revision, deadline.Token);

            using var back = await http.GetAsync($"/api/v1/previews/{preview.Hash}", deadline.Token);
            Assert.Equal(HttpStatusCode.OK, back.StatusCode);
            Assert.Equal(
                "immutable",
                back.Headers.CacheControl?.Extensions.Single(static e => e.Name == "immutable").Name
            );
        }
    }

    /// <summary>
    ///     The plan's P2 target. From the REST call to the preview hash in the event, on a 2K, 40-node graph, for a
    ///     slider at the head of the graph, where everything runs, and at its tail. With <c>PATINA_BENCH_OUT</c> set,
    ///     in Release, it takes enough samples to quote (ADR 0005). Otherwise a few, since lavapipe runs this on a CPU.
    /// </summary>
    [Fact]
    public async Task A_slider_on_a_2K_40_node_graph_is_measured_end_to_end() {
        using var deadline = Deadline(300);
        var instance = await StartServerAsync(deadline.Token);
        var state = await http.OpenAsync(GraphTesting.Write(Library, Forty()), deadline.Token);
        Assert.Equal(40, state.Nodes.Count);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        StartWorker();
        var clock = Stopwatch.StartNew();
        var first = await EvaluatedAsync(socket, state.Revision, deadline.Token);
        Report($"cold: {clock.ElapsedMilliseconds} ms, {first.Ran} ops, compiled {first.Compiled}");
        var samples = Environment.GetEnvironmentVariable("PATINA_BENCH_OUT") is { Length: > 0 } ? 44 : 5;

        foreach (var (name, node, ops) in new[] { ("head", 2, 86), ("middle", 20, 46), ("tail", 38, 6) }) {
            List<double> times = [];
            List<double> worker = [];
            for (var i = 0; i < samples; i++) {
                var start = Stopwatch.GetTimestamp();
                var edit = await http.EditAsync(
                    state.Id,
                    deadline.Token,
                    new GraphSetValue(node, "Hue", [(i + 1) * 0.01f + node * 0.001f])
                );
                var evaluation = await EvaluatedAsync(socket, edit.Revision, deadline.Token);
                var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                Assert.False(evaluation.Compiled);
                if (i > 3) {
                    times.Add(elapsed);
                    worker.Add(evaluation.Milliseconds);
                }

                Assert.InRange(evaluation.Ran, 1, ops);
            }

            times.Sort();
            worker.Sort();
            Report(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name} (node {node}): median {times[times.Count / 2]:F1} ms, "
                    + $"p95 {times[(int)(times.Count * 0.95)]:F1} ms, "
                    + $"Worker median {worker[worker.Count / 2]:F1} ms"
                )
            );
        }
    }

    static void Report(string line) {
        TestContext.Current.TestOutputHelper?.WriteLine(line);
        if (Environment.GetEnvironmentVariable("PATINA_BENCH_OUT") is { Length: > 0 } path) {
            File.AppendAllLines(path, [line]);
        }
    }

    /// <summary>
    ///     A noise, then a chain of 37 adjustments with a blur every fourth node, then an Output and a side branch.
    /// </summary>
    static NodeGraphAsset Forty() {
        List<GraphNodeAsset> nodes = [
            new() { Id = 1, Type = "Source/Noise", Values = new() { ["Scale"] = [8f] } }
        ];
        List<GraphEdgeAsset> edges = [];
        for (var id = 2; id <= 38; id++) {
            nodes.Add(
                id % 4 == 0 && id != 20 && id != 38
                    ? new() { Id = id, Type = "Filters/Blur", X = id * 200, Values = new() { ["Radius"] = [3f] } }
                    : new() { Id = id, Type = "Colour/HSL", X = id * 200, Values = new() { ["Hue"] = [0f] } }
            );
            edges.Add(new() { FromNode = id - 1, FromPort = "Out", ToNode = id, ToPort = "Input" });
        }

        nodes.Add(new() { Id = 39, Type = "Output/Output", X = 8000, Texts = new() { ["Usage"] = "baseColor" } });
        edges.Add(new() { FromNode = 38, FromPort = "Out", ToNode = 39, ToPort = "Input" });
        nodes.Add(new() { Id = 40, Type = "Colour/Invert", Y = 400 });
        edges.Add(new() { FromNode = 1, FromPort = "Out", ToNode = 40, ToPort = "Input" });
        return new() {
            Name = "Forty",
            Nodes = [..nodes],
            Edges = [..edges],
            Settings = new() { ["baseWidth"] = "2048", ["baseHeight"] = "2048", ["seed"] = "1" }
        };
    }
}
