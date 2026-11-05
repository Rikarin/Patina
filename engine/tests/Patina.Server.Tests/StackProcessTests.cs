using Patina.Contracts;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Patina.Bench;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>Painting as a client sees it: the real Server and Worker, with strokes sent as lists of stamps.</summary>
public sealed class StackProcessTests : IDisposable {
    const int Side = 1024;

    /// <summary>The size of the picture a stroke on the mesh is measured on.</summary>
    const int Picture = 800;

    static readonly IReadOnlyList<float> Red = [0.9f, 0.1f, 0.05f, 1f];

    static readonly Dictionary<string, IReadOnlyList<float>> Tint = new() { ["baseColor"] = [0.8f, 0.7f, 0.5f, 1f] };

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-stack-");
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

    /// <summary>
    ///     A drag sent in parts is one undo step, composited as tiles through the plan, and an undo and a redo come
    ///     back
    ///     to the same picture, down to the digest.
    /// </summary>
    [Fact]
    public async Task A_drag_in_parts_is_one_undo_step_and_repairs_its_tiles() {
        using var deadline = Deadline(180);
        var (instance, state) = await StartAsync("Crate.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EvaluatedAsync(socket, state.Revision, deadline.Token);
        var blank = await PixelsAsync(state.Id, false, deadline.Token);

        await PaintAsync(state.Id, "drag", Line(100, 100, 12), true, deadline.Token);
        var part = await PaintAsync(state.Id, "drag", Line(220, 100, 12), false, deadline.Token);
        var painted = await EvaluatedAsync(socket, part.Revision, deadline.Token);

        Assert.True(painted.Repaired > 0, "The stroke ran whole, so this never reached the tiles.");
        var after = await PixelsAsync(state.Id, false, deadline.Token);
        Assert.NotEqual(Digest(blank, "baseColor"), Digest(after, "baseColor"));
        Assert.Equal(Digest(blank, "roughness"), Digest(after, "roughness"));

        var undone = await ActAsync(state.Id, "undo", deadline.Token);
        Assert.False(undone.History.CanUndo);
        await EvaluatedAsync(socket, undone.Revision, deadline.Token);
        Assert.Equal(
            Digest(blank, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );

        var redone = await ActAsync(state.Id, "redo", deadline.Token);
        await EvaluatedAsync(socket, redone.Revision, deadline.Token);
        Assert.Equal(
            Digest(after, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
    }

    /// <summary>
    ///     The exit criterion of P5: what the strokes left, what a clean evaluation makes and what the exported file
    ///     holds are one picture. The digest is the shared hash a viewport compares against.
    /// </summary>
    [Fact]
    public async Task What_you_paint_is_what_exports() {
        using var deadline = Deadline(180);
        var (instance, state) = await StartAsync("Export.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);

        // The modes the old pane could not reproduce, with a layer over the stroke to composite through.
        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", null, null, 0.7f, "Overlay"));
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Top", Tint));
        var top = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(top, null, null, 0.4f, "Multiply"));
        var first = await PaintAsync(state.Id, "a", Line(80, 300, 30), false, deadline.Token);
        await EvaluatedAsync(socket, first.Revision, deadline.Token);
        var second = await PaintAsync(state.Id, "b", Line(600, 700, 30), false, deadline.Token);
        await EvaluatedAsync(socket, second.Revision, deadline.Token);

        var composite = await PixelsAsync(state.Id, false, deadline.Token);
        var exported = await ExportAsync(state.Id, Path.Combine(folder.FullName, "out"), deadline.Token);

        Assert.Equal(state.Channels.Count, exported.Count);
        foreach (var map in exported) {
            Assert.Equal(map.Digest, Digest(composite, map.Usage));
            var file = PngCodec.Decode(File.ReadAllBytes(map.File!));
            Assert.Equal(map.Digest, Convert.ToHexStringLower(SHA256.HashData(file.Pixels)));
        }
    }

    /// <summary>
    ///     The same criterion on a mesh, through the viewport. Viewport pixels are tonemapped, so what is compared is the
    ///     picture the plan means: the top level of the texture each material slot samples, read back from the
    ///     viewport's own device, as 8-bit RGBA. Its digest must equal that of the file an export wrote for the channel,
    ///     from a clean evaluation of the whole stack, after strokes that cross the mesh's seams, an undo and a redo.
    /// </summary>
    [Fact]
    public async Task What_the_viewport_samples_is_what_exports() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Sampled.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", null, null, 0.7f, "Overlay"));
        using var frames = await WearAsync(instance, state.Id, deadline.Token);

        var view = await AimAsync(0.5, 0.5, deadline.Token);
        var first = await EditAsync(state.Id, deadline.Token, Across("one", view, 300, 500, 300, false));
        await EvaluatedAsync(socket, first.Revision, deadline.Token);
        var live = await EditAsync(state.Id, deadline.Token, Across("two", view, 200, 620, 420, true));
        var repaired = await EvaluatedAsync(socket, live.Revision, deadline.Token);
        Assert.True(repaired.Repaired > 0, "The stroke ran whole, so this never reached the viewport as tiles.");
        var closed = await EditAsync(state.Id, deadline.Token, Across("two", view, 620, 200, 200, false));
        await EvaluatedAsync(socket, closed.Revision, deadline.Token);

        await SameAsExportAsync(state, "first", deadline.Token);

        await ActAsync(state.Id, "undo", deadline.Token);
        await SameAsExportAsync(state, "undone", deadline.Token);

        await ActAsync(state.Id, "redo", deadline.Token);
        await SameAsExportAsync(state, "redone", deadline.Token);
    }

    /// <summary>A stroke on a mesh is a list of positions and a camera, so a new Worker lands it in the same texels.</summary>
    [Fact]
    public async Task A_worker_killed_in_a_stroke_on_the_mesh_loses_nothing() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Mesh.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        using var frames = await WearAsync(instance, state.Id, deadline.Token);
        var view = await AimAsync(0.5, 0.5, deadline.Token);

        await EditAsync(state.Id, deadline.Token, Across("drag", view, 200, 400, 300, true));
        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        var last = await EditAsync(state.Id, deadline.Token, Across("drag", view, 400, 620, 300, false));
        StartWorker();
        await EvaluatedAsync(socket, last.Revision, deadline.Token);
        var replayed = await PixelsAsync(state.Id, false, deadline.Token);

        var other = await CreateAsync("Whole.vxlayers", deadline.Token);
        await BindMeshAsync(other.Id, deadline.Token);
        await EditAsync(other.Id, deadline.Token, Across("drag", view, 200, 400, 300, true));
        await EditAsync(other.Id, deadline.Token, Across("drag", view, 400, 620, 300, false));
        Assert.Equal(
            Digest(await PixelsAsync(other.Id, false, deadline.Token), "baseColor"),
            Digest(replayed, "baseColor")
        );
    }

    /// <summary>
    ///     Spike S5 for painting (ADR 0009): the Worker dies in the middle of a drag, more stamps arrive with nobody
    ///     there, and the next Worker makes the same bytes as a session that was never interrupted.
    /// </summary>
    [Fact]
    public async Task A_worker_killed_mid_stroke_loses_nothing() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Killed.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EvaluatedAsync(socket, state.Revision, deadline.Token);

        await PaintAsync(state.Id, "before", Line(100, 500, 10), false, deadline.Token);
        await PaintAsync(state.Id, "drag", Line(100, 200, 10), true, deadline.Token);
        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);

        await PaintAsync(state.Id, "drag", Line(400, 200, 10), false, deadline.Token);
        var clock = Stopwatch.StartNew();
        StartWorker();
        var after = await EvaluatedAsync(socket, (await StateAsync(state.Id, deadline.Token)).Revision, deadline.Token);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"S5: the pixels were back {clock.ElapsedMilliseconds} ms after the new Worker started"
        );
        Assert.True(after.Compiled);
        var replayed = await PixelsAsync(state.Id, false, deadline.Token);

        // The same history, on a Worker that never died, from the same file.
        var control = await CreateAsync("Control.vxlayers", deadline.Token);
        await PaintAsync(control.Id, "before", Line(100, 500, 10), false, deadline.Token);
        await PaintAsync(control.Id, "drag", Line(100, 200, 10), true, deadline.Token);
        await PaintAsync(control.Id, "drag", Line(400, 200, 10), false, deadline.Token);
        Assert.Equal(
            Digest(await PixelsAsync(control.Id, false, deadline.Token), "baseColor"),
            Digest(replayed, "baseColor")
        );

        // History came through as well: the drag undoes as one step, on a Worker that never held it.
        await ActAsync(state.Id, "undo", deadline.Token);
        await ActAsync(control.Id, "undo", deadline.Token);
        Assert.Equal(
            Digest(await PixelsAsync(control.Id, false, deadline.Token), "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
    }

    /// <summary>
    ///     What ADR 0009's end to end figure is: from sending stamps to holding the map they changed, as a client does
    ///     it, with the Server, the socket and the picture's file in the way. It prints and asserts nothing about time.
    /// </summary>
    [Fact]
    public async Task A_stamp_reaches_a_client_as_a_preview() {
        using var deadline = Deadline(300);
        var size = int.TryParse(Environment.GetEnvironmentVariable("PATINA_S4_SIZE"), out var chosen) ? chosen : Side;
        var worker = StartWorker();
        var (instance, state) = await AttachAsync("Live.vxlayers", deadline.Token, size);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EvaluatedAsync(socket, state.Revision, deadline.Token);
        for (var layer = 0; layer < 18; layer++) {
            await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", $"Over {layer}", Tint));
        }

        var layers = (await StateAsync(state.Id, deadline.Token)).Layers;
        var last = await EditAsync(
            state.Id,
            deadline.Token,
            [
                ..layers.Skip(2)
                    .Select(static (layer, index) => (StackCommand)new StackSetLayer(
                            layer.Id,
                            null,
                            null,
                            0.3f,
                            index % 2 == 0 ? "Multiply" : "Overlay"
                        )
                    )
            ]
        );
        await EvaluatedAsync(socket, last.Revision, deadline.Token);
        var compiled = await PaintAsync(state.Id, "first", Line(100, 100, 2), false, deadline.Token);
        await EvaluatedAsync(socket, compiled.Revision, deadline.Token);

        List<double> times = [];
        var repaired = 0;
        for (var stamp = 0; stamp < 120; stamp++) {
            var clock = Stopwatch.GetTimestamp();
            var sent = await PaintAsync(state.Id, "drag", [new(100f + stamp * 6f, 300f)], true, deadline.Token);
            var evaluation = await EvaluatedAsync(socket, sent.Revision, deadline.Token);
            foreach (var preview in evaluation.Previews) {
                _ = await http.GetByteArrayAsync($"/api/v1/previews/{preview.Hash}", deadline.Token);
            }

            repaired = evaluation.Repaired;
            if (stamp > 10) {
                times.Add(Stopwatch.GetElapsedTime(clock).TotalMilliseconds);
            }
        }

        times.Sort();
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"S4 end to end, {size}², 20 layers: p50 {times[times.Count / 2]:F1} ms, p95 "
            + $"{times[times.Count * 95 / 100]:F1} ms, {repaired} ops repaired, Worker {worker.Id}"
        );
        Assert.True(repaired > 0);
    }

    /// <summary>A stack that saved comes back as the same picture, from the files alone.</summary>
    [Fact]
    public async Task A_saved_stack_reads_back_as_the_same_picture() {
        using var deadline = Deadline(180);
        var (instance, state) = await StartAsync("Saved.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        var painted = await PaintAsync(state.Id, "a", Line(50, 60, 20), false, deadline.Token);
        await EvaluatedAsync(socket, painted.Revision, deadline.Token);
        var before = await PixelsAsync(state.Id, false, deadline.Token);

        using (var saved = await http.PostAsync($"/api/v1/stacks/{state.Id}/save", null, deadline.Token)) {
            saved.EnsureSuccessStatusCode();
        }

        Assert.False((await StateAsync(state.Id, deadline.Token)).History.Dirty);
        Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(state.Path)!, "*.vxpaint"));
        using (var closed = await http.DeleteAsync($"/api/v1/stacks/{state.Id}", deadline.Token)) {
            closed.EnsureSuccessStatusCode();
        }

        var again = await OpenAsync(state.Path, deadline.Token);
        Assert.NotEqual(state.Id, again.Id);
        Assert.Equal(
            Digest(before, "baseColor"),
            Digest(await PixelsAsync(again.Id, false, deadline.Token), "baseColor")
        );
    }

    /// <summary>
    ///     A long session is bounded by checkpoints, and a checkpoint is not allowed to take undo away: a new Worker
    ///     asked
    ///     to go behind one starts from further back.
    /// </summary>
    [Fact]
    public async Task A_worker_lost_after_a_checkpoint_can_still_undo_past_it() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Long.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EvaluatedAsync(socket, state.Revision, deadline.Token);
        var blank = await PixelsAsync(state.Id, false, deadline.Token);

        // More stamps than a checkpoint allows, in strokes.
        StackEditResult last = null!;
        for (var stroke = 0; stroke < 5; stroke++) {
            last = await PaintAsync(state.Id, $"s{stroke}", Line(20, 100 + stroke * 40, 500), false, deadline.Token);
        }

        await EvaluatedAsync(socket, last.Revision, deadline.Token);
        await PixelsAsync(state.Id, false, deadline.Token);
        Assert.NotEmpty(Directory.GetDirectories(Path.Combine(Library, ".ptcache", "paint", state.Id)));

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();
        for (var stroke = 0; stroke < 5; stroke++) {
            await ActAsync(state.Id, "undo", deadline.Token);
        }

        Assert.Equal(
            Digest(blank, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
    }

    /// <summary>
    ///     A first stroke makes a file, and a save writes it. A new Worker replaying that stroke must not start from
    ///     the
    ///     file, or the stroke lands twice and its undo reaches a picture nobody painted.
    /// </summary>
    [Fact]
    public async Task A_worker_lost_after_a_save_undoes_to_before_the_file_existed() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Fresh.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EvaluatedAsync(socket, state.Revision, deadline.Token);
        var blank = await PixelsAsync(state.Id, false, deadline.Token);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke("soft", "paint", "baseColor", new(30f, 0.3f), Red, Line(100, 100, 40))
        );
        var painted = await PixelsAsync(state.Id, false, deadline.Token);
        using (var saved = await http.PostAsync($"/api/v1/stacks/{state.Id}/save", null, deadline.Token)) {
            saved.EnsureSuccessStatusCode();
        }

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();
        Assert.Equal(
            Digest(painted, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
        await ActAsync(state.Id, "undo", deadline.Token);
        Assert.Equal(
            Digest(blank, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
    }

    async Task SameAsExportAsync(StackState state, string when, CancellationToken token) {
        var exported = await ExportAsync(state.Id, Path.Combine(folder.FullName, when), token);
        var sampled = await SampledAsync(state.Id, token);

        // Read until the frame's texture has caught up with the document: a pick of the viewport races nothing else.
        foreach (var map in exported) {
            var file = PngCodec.Decode(File.ReadAllBytes(map.File!));
            Assert.Equal(map.Digest, Convert.ToHexStringLower(SHA256.HashData(file.Pixels)));
            Assert.Equal(map.Digest, Digest(sampled, map.Usage));
        }

        Assert.Equal(exported.Count, sampled.Count);
    }

    async Task BindMeshAsync(string id, CancellationToken token) {
        var meshes = Directory.CreateDirectory(Path.Combine(folder.FullName, "meshes")).FullName;
        if (!File.Exists(Path.Combine(meshes, BakeFixture.LowFile))) {
            BakeFixture.Write(meshes, 0.05f);
        }

        await EditAsync(id, token, new StackSetModel(Path.Combine(meshes, BakeFixture.LowFile)));
    }

    /// <summary>Puts a stack on the viewport and holds a socket open that takes frames, which is what makes it draw.</summary>
    async Task<IDisposable> WearAsync(ServerInstance instance, string id, CancellationToken token) {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(
            new($"ws://127.0.0.1:{instance.Port}/ws/viewport?access_token={instance.Token}"),
            token
        );
        var drain = new byte[1 << 20];
        var draining = Task.Run(
            async () => {
                while (socket.State == WebSocketState.Open) {
                    await socket.ReceiveAsync(drain, token);
                }
            },
            token
        );
        using var response = await http.PutAsJsonAsync(
            "/api/v1/viewport/stack",
            new(id),
            ContractsJsonContext.Default.ViewportStackRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return new Drained(socket, draining);
    }

    async Task<StackView> AimAsync(double x, double y, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/viewport/pick",
            new(x, y, null, Picture, Picture),
            ContractsJsonContext.Default.ViewportPickRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        var picked = (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.ViewportPick, token))!;
        Assert.True(picked.Found, "The middle of the picture should be on the housing.");
        return picked.View;
    }

    /// <summary>A stroke straight across the picture, over whatever flats and seams are under it.</summary>
    static StackStroke Across(string stroke, StackView view, int from, int to, int row, bool open) =>
        new(
            stroke,
            "paint",
            "baseColor",
            new(30f, Spacing: 0.25f),
            Red,
            [..Enumerable.Range(0, 8).Select(step => new StackStamp(from + (to - from) * step / 7f, row, 1f))],
            open,
            View: view
        );

    async Task<IReadOnlyList<StackPixels>> SampledAsync(string id, CancellationToken token) {
        using var response = await http.GetAsync($"/api/v1/stacks/{id}/pixels?sampled=true", token);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return (await response.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.IReadOnlyListStackPixels,
            token
        ))!;
    }

    sealed class Drained(ClientWebSocket socket, Task draining) : IDisposable {
        public void Dispose() {
            socket.Abort();
            socket.Dispose();
            _ = draining.ContinueWith(static _ => { }, TaskScheduler.Default);
        }
    }

    static string Digest(IReadOnlyList<StackPixels> maps, string usage) =>
        maps.Single(m => string.Equals(m.Usage, usage, StringComparison.Ordinal)).Digest;

    static IReadOnlyList<StackStamp> Line(float x, float y, int count) =>
        [..Enumerable.Range(0, count).Select(index => new StackStamp(x + index * 6f, y + index % 3))];

    static CancellationTokenSource Deadline(int seconds) {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
        return deadline;
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

    async Task<(ServerInstance, StackState)> StartAsync(string name, CancellationToken token) {
        var (instance, state, _) = await BootAsync(name, token);
        return (instance, state);
    }

    Task<(ServerInstance, StackState)> StartAsync(string name, CancellationToken token, out Process worker) {
        worker = StartWorker();
        return AttachAsync(name, token);
    }

    async Task<(ServerInstance, StackState, Process)> BootAsync(string name, CancellationToken token) {
        var worker = StartWorker();
        var (instance, state) = await AttachAsync(name, token);
        return (instance, state, worker);
    }

    async Task<(ServerInstance, StackState)> AttachAsync(string name, CancellationToken token, int size = Side) {
        Start(
            Engine.Dll("Patina.Server", "patina-server"),
            "--library",
            Library,
            $"--Patina:DiscoveryFile={DiscoveryFile}",
            $"--Patina:Worker:SocketPath={socketPath}",
            "--Patina:WatchStdin=true"
        );
        var registry = new Library.InstanceRegistry(DiscoveryFile);
        while (true) {
            if (await registry.FindAsync(Library, token) is { } instance) {
                http.BaseAddress = new($"http://127.0.0.1:{instance.Port}");
                http.DefaultRequestHeaders.Authorization = new("Bearer", instance.Token);
                return (instance, await CreateAsync(name, token, size));
            }

            await Task.Delay(50, token);
        }
    }

    async Task<StackState> CreateAsync(string name, CancellationToken token, int size = Side) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/stacks/new",
            new(name, size, size),
            ContractsJsonContext.Default.StackCreateRequest,
            token
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackState, token))!;
    }

    async Task<StackState> OpenAsync(string path, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/stacks",
            new(path),
            ContractsJsonContext.Default.StackOpenRequest,
            token
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackState, token))!;
    }

    async Task<StackState> StateAsync(string id, CancellationToken token) =>
        (await http.GetFromJsonAsync($"/api/v1/stacks/{id}", ContractsJsonContext.Default.StackState, token))!;

    Task<StackEditResult> PaintAsync(
        string id,
        string stroke,
        IReadOnlyList<StackStamp> stamps,
        bool open,
        CancellationToken token
    ) =>
        EditAsync(
            id,
            token,
            new StackStroke(stroke, "paint", "baseColor", new(24f, Spacing: 0.25f), Red, stamps, open)
        );

    async Task<StackEditResult> EditAsync(string id, CancellationToken token, params StackCommand[] commands) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{id}/commands",
            new(commands),
            ContractsJsonContext.Default.StackCommandBatch,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackEditResult, token))!;
    }

    async Task<StackEditResult> ActAsync(string id, string action, CancellationToken token) {
        using var response = await http.PostAsync($"/api/v1/stacks/{id}/{action}", null, token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackEditResult, token))!;
    }

    async Task<IReadOnlyList<StackPixels>> PixelsAsync(string id, bool clean, CancellationToken token) {
        using var response = await http.GetAsync($"/api/v1/stacks/{id}/pixels?clean={clean}", token);
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity) {
            Assert.Skip(await response.Content.ReadAsStringAsync(token));
        }

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.IReadOnlyListStackPixels,
            token
        ))!;
    }

    async Task<IReadOnlyList<StackPixels>> ExportAsync(string id, string to, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{id}/export",
            new(to),
            ContractsJsonContext.Default.StackExportRequest,
            token
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.IReadOnlyListStackPixels,
            token
        ))!;
    }

    static async Task<ClientWebSocket> FollowAsync(ServerInstance instance, StackState state, CancellationToken token) {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(
            new($"ws://127.0.0.1:{instance.Port}/ws/stacks/{state.Id}?since=0&access_token={instance.Token}"),
            token
        );
        return socket;
    }

    static async Task<StackEvaluation> EvaluatedAsync(ClientWebSocket socket, long revision, CancellationToken token) {
        var buffer = new byte[1024 * 1024];
        while (true) {
            var length = 0;
            ValueWebSocketReceiveResult part;
            do {
                part = await socket.ReceiveAsync(buffer.AsMemory(length), token);
                length += part.Count;
            } while (!part.EndOfMessage);

            var item = JsonSerializer.Deserialize(buffer.AsSpan(0, length), ContractsJsonContext.Default.StackEvent)!;
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
}
