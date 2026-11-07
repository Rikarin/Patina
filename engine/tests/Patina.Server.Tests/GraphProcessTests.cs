using Patina.Contracts;
using Patina.Library;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Vixen.Core.Imaging;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

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
                    Gpu.Skip(error);
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

    /// <summary>
    ///     Issue 96. A radius is counted in texels of the authoring base, so a size that moves the base moves the
    ///     radius, and a size that leaves the base alone is the same picture with more texels.
    /// </summary>
    [Fact]
    public async Task The_same_graph_at_two_sizes_is_the_same_picture_resampled() {
        using var deadline = Deadline(120);
        var instance = await StartServerAsync(deadline.Token);
        StartWorker();

        var state = await http.OpenAsync(GraphTesting.Write(Library, Blurred(128), "small.vxtexgraph"), deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EvaluatedAsync(socket, state.Revision, deadline.Token);
        var small = await OutputAsync(state, deadline.Token);

        // Size is an edit like any other, and it compiles, because a plan is built for one size.
        var edit = await http.EditAsync(
            state.Id,
            deadline.Token,
            new GraphSetSetting(TextureSizes.GraphSetting, "256")
        );
        Assert.True((await EvaluatedAsync(socket, edit.Revision, deadline.Token)).Compiled);
        var large = await OutputAsync(state, deadline.Token);

        var rebased = await OutputAsync(instance, Blurred(256), "rebased.vxtexgraph", deadline.Token);

        Assert.Equal((128, 128), (small.Width, small.Height));
        Assert.Equal((256, 256), (large.Width, large.Height));
        Assert.True(Distance(small, Halved(large)) < 2.0, $"{Distance(small, Halved(large))}");

        // What the Width setting used to do, and the reason this comparison can fail at all.
        Assert.True(Distance(small, Halved(rebased)) > 6.0, $"{Distance(small, Halved(rebased))}");

        // A build asks for the same model, and gets the same bytes.
        var baked = await BakedAsync(Blurred(128), "cli", deadline.Token, "--size", "256");
        Assert.Equal(large.Pixels, baked.Pixels);

        // A number still reaches a plan built for another size without compiling it.
        var slid = await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(2, "Radius", [4f]));
        Assert.False((await EvaluatedAsync(socket, slid.Revision, deadline.Token)).Compiled);
    }

    /// <summary>
    ///     A plan is copied on its way to a preview, so what a build compiles and what a client sees could part on
    ///     either number. One graph with both set, one axis of it clamped, is the same bytes each way.
    /// </summary>
    [Fact]
    public async Task A_preview_and_a_build_agree_on_a_graphs_size_and_its_tiling() {
        using var deadline = Deadline(120);
        var instance = await StartServerAsync(deadline.Token);
        StartWorker();

        static NodeGraphAsset At256(string tilingX) {
            var asset = Blurred(128);
            asset.Settings[TextureSizes.GraphSetting] = "256";
            asset.Settings[TextureGraphSettings.TilingX] = tilingX;
            return asset;
        }

        var preview = await OutputAsync(instance, At256("false"), "clamped.vxtexgraph", deadline.Token);
        var clamped = await BakedAsync(At256("false"), "clamped-bake", deadline.Token);
        var tiled = await BakedAsync(At256("true"), "tiled-bake", deadline.Token);

        Assert.Equal((256, 256), (preview.Width, preview.Height));
        Assert.Equal(preview.Pixels, clamped.Pixels);
        Assert.NotEqual(tiled.Pixels, clamped.Pixels);
    }

    /// <summary>
    ///     Issue 98: whether a flood settled depends on the mask, so only an evaluation can say, and it says so against
    ///     the node. Both budgets, because a warning that cannot be absent proves nothing.
    /// </summary>
    [Theory]
    [InlineData(4, true)]
    [InlineData(256, false)]
    public async Task A_flood_fill_that_ran_out_says_so_against_the_node(int iterations, bool warned) {
        using var deadline = Deadline(120);
        var (_, _, socket, first) = await OpenSampleAsync(Flooded(iterations), deadline.Token);
        using (socket) {
            var warnings = first.Diagnostics.Where(static d => d is { Severity: "Warning", Node: 2 }).ToList();
            Assert.Equal(warned, warnings.Count == 1);
            Assert.All(warnings, static warning => Assert.Equal("Iterations", warning.Port));
        }
    }

    static NodeGraphAsset Flooded(int iterations) =>
        new() {
            Name = "Flooded",
            Nodes = [
                new() { Id = 1, Type = "Source/Noise", Values = new() { ["Scale"] = [48f] } },
                new() {
                    Id = 2, Type = "Analysis/Flood Fill", X = 300, Values = new() { ["Iterations"] = [iterations] }
                },
                new() { Id = 3, Type = "Output/Output", X = 600, Texts = new() { ["Usage"] = "baseColor" } }
            ],
            Edges = [
                new() { FromNode = 1, FromPort = "Out", ToNode = 2, ToPort = "Mask" },
                new() { FromNode = 2, FromPort = "Out", ToNode = 3, ToPort = "Input" }
            ],
            Settings = new() { ["seed"] = "7", ["baseWidth"] = "256", ["baseHeight"] = "256" }
        };

    /// <summary>A hard-edged picture and a blur wide enough for its radius to show in the result.</summary>
    static NodeGraphAsset Blurred(int side) =>
        new() {
            Name = "Blurred",
            Nodes = [
                new() { Id = 1, Type = "Source/Checker", Values = new() { ["Scale X"] = [4f], ["Scale Y"] = [4f] } },
                new() { Id = 2, Type = "Filters/Blur", X = 300, Values = new() { ["Radius"] = [8f] } },
                new() { Id = 3, Type = "Output/Output", X = 600, Texts = new() { ["Usage"] = "baseColor" } }
            ],
            Edges = [
                new() { FromNode = 1, FromPort = "Out", ToNode = 2, ToPort = "Input" },
                new() { FromNode = 2, FromPort = "Out", ToNode = 3, ToPort = "Input" }
            ],
            Settings = new() { ["seed"] = "7", ["baseWidth"] = $"{side}", ["baseHeight"] = $"{side}" }
        };

    async Task<Bitmap> BakedAsync(
        NodeGraphAsset asset,
        string name,
        CancellationToken cancellationToken,
        params string[] options
    ) {
        var baked = Path.Combine(folder.FullName, name);
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in (string[]) [
                     Engine.Dll("Patina.Cli", "patina"), "bake",
                     GraphTesting.Write(Library, asset, $"{name}.vxtexgraph"), baked, ..options
                 ]) {
            info.ArgumentList.Add(argument);
        }

        using var cli = Process.Start(info)!;
        var error = cli.StandardError.ReadToEndAsync(cancellationToken);
        await cli.StandardOutput.ReadToEndAsync(cancellationToken);
        await cli.WaitForExitAsync(cancellationToken);
        Assert.True(cli.ExitCode == 0, await error);
        return PngCodec.Load(Path.Combine(baked, "baseColor.png"));
    }

    async Task<Bitmap> OutputAsync(
        ServerInstance instance,
        NodeGraphAsset asset,
        string name,
        CancellationToken cancellationToken
    ) {
        var state = await http.OpenAsync(GraphTesting.Write(Library, asset, name), cancellationToken);
        using var socket = await FollowAsync(instance, state, cancellationToken);
        await EvaluatedAsync(socket, state.Revision, cancellationToken);
        return await OutputAsync(state, cancellationToken);
    }

    async Task<Bitmap> OutputAsync(GraphState state, CancellationToken cancellationToken) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/graphs/{state.Id}/images",
            new(3, "baseColor"),
            ContractsJsonContext.Default.GraphImageRequest,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
        var image = (await response.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.GraphImage,
            cancellationToken
        ))!;
        return PngCodec.Decode(await http.GetByteArrayAsync($"/api/v1/previews/{image.Hash}", cancellationToken));
    }

    /// <summary>A picture at half the size, each texel the mean of the four it covers.</summary>
    static Bitmap Halved(Bitmap picture) {
        var (width, height) = (picture.Width / 2, picture.Height / 2);
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                for (var lane = 0; lane < 4; lane++) {
                    var sum = 0;
                    for (var tap = 0; tap < 4; tap++) {
                        sum += picture.Pixels[((y * 2 + (tap >> 1)) * picture.Width + x * 2 + (tap & 1)) * 4 + lane];
                    }

                    pixels[(y * width + x) * 4 + lane] = (byte)((sum + 2) / 4);
                }
            }
        }

        return new(width, height, pixels);
    }

    /// <summary>The mean difference of two pictures of one size, out of 255.</summary>
    static double Distance(Bitmap a, Bitmap b) {
        Assert.Equal((a.Width, a.Height), (b.Width, b.Height));
        var (first, second) = (a.Pixels, b.Pixels);
        return Enumerable.Range(0, first.Length).Average(at => Math.Abs(first[at] - second[at]));
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

    /// <summary>A number on a compound reaches its inner nodes without a compile (plan §3.2).</summary>
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

            // An exposed parameter is a text on the node, and it is a value edit all the same.
            var knobbed = types.First(static type => type.Path.StartsWith("Patterns/", StringComparison.Ordinal)
                && type.Settings.Any(static setting => setting.Kind == "Float")
            );
            var knob = knobbed.Settings.First(static setting => setting.Kind == "Float");
            var second = await http.EditAsync(state.Id, deadline.Token, new GraphAddNode(knobbed.Path, 0, 800, -1));
            var other = Assert.Single(second.Created).Node;
            await EvaluatedAsync(socket, second.Revision, deadline.Token);
            var value = ((knob.Minimum ?? 0f) + (knob.Maximum ?? 1f)) / 2f;
            var turned = await http.EditAsync(
                state.Id,
                deadline.Token,
                new GraphSetText(other, knob.Name, value.ToString("R", CultureInfo.InvariantCulture))
            );
            Assert.False((await EvaluatedAsync(socket, turned.Revision, deadline.Token)).Compiled);
        }
    }

    /// <summary>Plan §3.2's formats on the whole path: 32 bits and an odd size reach a client.</summary>
    [Fact]
    public async Task A_graph_at_32_bits_and_an_odd_size_previews_and_a_slider_does_not_recompile() {
        using var deadline = Deadline(120);
        var asset = GraphTesting.Sample();
        asset.Settings["bitDepth"] = "32";
        asset.Settings["baseWidth"] = "301";
        asset.Settings["baseHeight"] = "167";
        var (_, state, socket, first) = await OpenSampleAsync(asset, deadline.Token);
        using (socket) {
            var noise = first.Previews.Single(static preview => preview.Node == 1);
            Assert.Equal((150, 83), (noise.Width, noise.Height));

            var edit = await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(2, "Hue", [0.25f]));
            var slid = await EvaluatedAsync(socket, edit.Revision, deadline.Token);
            Assert.False(slid.Compiled);
            Assert.Contains(slid.Previews, static preview => preview.Node == 2);
        }
    }

    [Fact]
    public async Task A_default_a_number_reads_by_expression_changes_the_picture() {
        using var deadline = Deadline(120);
        var (_, state, socket, _) = await OpenSampleAsync(GraphTesting.Sample(), deadline.Token);
        using (socket) {
            var bound = await http.EditAsync(state.Id, deadline.Token, new GraphSetText(1, "=Scale", "1 + Wear * 8"));
            var first = await EvaluatedAsync(socket, bound.Revision, deadline.Token);
            var noise = first.Previews.Single(static p => p.Node == 1);

            var moved = await http.EditAsync(
                state.Id,
                deadline.Token,
                new GraphSetParameter("Wear", new("Wear", "1", string.Empty, "Float", 0f, 1f, string.Empty, []))
            );
            var second = await EvaluatedAsync(socket, moved.Revision, deadline.Token);
            Assert.True(second.Compiled);
            Assert.NotEqual(noise.Hash, second.Previews.Single(static p => p.Node == 1).Hash);
        }
    }

    [Fact]
    public async Task A_bitmap_beside_the_graph_evaluates_and_a_missing_one_says_so() {
        using var deadline = Deadline(120);
        var pixels = Enumerable.Range(0, 4 * 4 * 4).Select(static i => (byte)(i * 4)).ToArray();
        PngCodec.Save(Path.Combine(Library, "picture.png"), new(4, 4, pixels));
        var asset = GraphTesting.Sample() with {
            Nodes = [
                ..GraphTesting.Sample().Nodes,
                new() { Id = 6, Type = "Source/Bitmap", Texts = new() { ["Source"] = "picture.png" } }
            ]
        };
        var (_, state, socket, first) = await OpenSampleAsync(asset, deadline.Token);
        using (socket) {
            Assert.Contains(first.Previews, static p => p.Node == 6);
            Assert.DoesNotContain(first.Diagnostics, static d => d.Id == "PT0001");

            var gone = await http.EditAsync(state.Id, deadline.Token, new GraphSetText(6, "Source", "nothing.png"));
            var refused = await EvaluatedAsync(socket, gone.Revision, deadline.Token);
            var problem = refused.Diagnostics.Single(static d => d.Id == "PT0001");
            Assert.Contains("nothing.png", problem.Message, StringComparison.Ordinal);
            Assert.Equal(6, problem.Node);
        }
    }

    [Fact]
    public async Task An_inline_ramp_and_curve_change_the_picture() {
        using var deadline = Deadline(120);
        var asset = GraphTesting.Sample() with {
            Nodes = [
                ..GraphTesting.Sample().Nodes,
                new() { Id = 6, Type = "Source/Gradient" }, new() { Id = 7, Type = "Colour/Curve" }
            ],
            Edges = [
                ..GraphTesting.Sample().Edges,
                new() { FromNode = 6, FromPort = "Out", ToNode = 7, ToPort = "Input" }
            ]
        };
        var (_, state, socket, first) = await OpenSampleAsync(asset, deadline.Token);
        using (socket) {
            var plain = first.Previews.Single(static p => p.Node == 6).Hash;
            var ramped = await http.EditAsync(
                state.Id,
                deadline.Token,
                new GraphSetText(6, "Ramp", "ramp:0 ff0000ff,0.5 00ff00ff,1 0000ffff")
            );
            var second = await EvaluatedAsync(socket, ramped.Revision, deadline.Token);
            Assert.NotEqual(plain, second.Previews.Single(static p => p.Node == 6).Hash);
            Assert.DoesNotContain(second.Diagnostics, static d => d.Id == "PT0001");

            var curved = await http.EditAsync(
                state.Id,
                deadline.Token,
                new GraphSetText(7, "Curve", "curve:r=0 1,1 0;g=0 0,0.5 0.8,1 1")
            );
            var third = await EvaluatedAsync(socket, curved.Revision, deadline.Token);
            Assert.Contains(third.Previews, static p => p.Node == 7);
            Assert.DoesNotContain(third.Diagnostics, static d => d.Id == "PT0001");

            var wrong = await http.EditAsync(state.Id, deadline.Token, new GraphSetText(6, "Ramp", "ramp:nonsense"));
            var refused = await EvaluatedAsync(socket, wrong.Revision, deadline.Token);
            Assert.Contains(refused.Diagnostics, static d => d is { Node: 6, Port: "Ramp", Severity: "Error" });
        }
    }

    /// <summary>
    ///     Everything a Worker resolves, through the real command: a bitmap beside the graph, a ramp and a curve
    ///     written inline, a compound from the folder beside it. The Worker's own full-size picture is the oracle.
    /// </summary>
    [Fact]
    public async Task Patina_bake_reads_what_the_worker_reads_and_writes_the_same_bytes() {
        using var deadline = Deadline(180);
        var pixels = Enumerable.Range(0, 4 * 4 * 4).Select(static i => (byte)(i * 4)).ToArray();
        PngCodec.Save(Path.Combine(Library, "picture.png"), new(4, 4, pixels));
        var compounds = Directory.CreateDirectory(Path.Combine(Library, LibraryLayout.CompoundsFolder)).FullName;
        GraphTesting.Write(compounds, Tint("Tint"), "Tint.vxtexgraph");

        static GraphEdgeAsset Edge(int from, int to) =>
            new() { FromNode = from, FromPort = "Out", ToNode = to, ToPort = "Input" };

        NodeGraphAsset asset = new() {
            Name = "Everything",
            Nodes = [
                new() { Id = 1, Type = "Source/Bitmap", Texts = new() { ["Source"] = "picture.png" } },
                new() {
                    Id = 2, Type = "Colour/Curve", Texts = new() { ["Curve"] = "curve:r=0 1,1 0;g=0 0,0.5 0.8,1 1" }
                },
                new() { Id = 3, Type = "Tint" },
                new() { Id = 4, Type = "Output/Output", Texts = new() { ["Usage"] = "baseColor" } },
                new() { Id = 5, Type = "Source/Gradient", Texts = new() { ["Ramp"] = "ramp:0 ff0000ff,1 0000ffff" } },
                new() { Id = 6, Type = "Output/Output", Texts = new() { ["Usage"] = "emissive" } }
            ],
            Edges = [Edge(1, 2), Edge(2, 3), Edge(3, 4), Edge(5, 6)],
            Settings = new() { ["seed"] = "7", ["baseWidth"] = "64", ["baseHeight"] = "64" }
        };
        var (_, state, socket, first) = await OpenSampleAsync(asset, deadline.Token);
        using (socket) {
            Assert.DoesNotContain(first.Diagnostics, static d => d.Severity == "Error");
            var folderOut = Path.Combine(folder.FullName, "bake");
            var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add(Engine.Dll("Patina.Cli", "patina"));
            info.ArgumentList.Add("bake");
            info.ArgumentList.Add(state.Path);
            info.ArgumentList.Add(folderOut);
            using var cli = Process.Start(info)!;
            var error = cli.StandardError.ReadToEndAsync(deadline.Token);
            await cli.StandardOutput.ReadToEndAsync(deadline.Token);
            await cli.WaitForExitAsync(deadline.Token);
            Assert.True(cli.ExitCode == 0, await error);

            foreach (var (node, usage) in new[] { (4, "baseColor"), (6, "emissive") }) {
                using var response = await http.PostAsJsonAsync(
                    $"/api/v1/graphs/{state.Id}/images",
                    new(node, usage),
                    ContractsJsonContext.Default.GraphImageRequest,
                    deadline.Token
                );
                response.EnsureSuccessStatusCode();
                var image = (await response.Content.ReadFromJsonAsync(
                    ContractsJsonContext.Default.GraphImage,
                    deadline.Token
                ))!;
                var baked = await File.ReadAllBytesAsync(Path.Combine(folderOut, usage + ".png"), deadline.Token);
                Assert.Equal(image.Hash, Convert.ToHexStringLower(SHA256.HashData(baked)));
            }
        }
    }

    /// <summary>
    ///     ⚠ An image input has no default, so a compound opened on its own reported TG0002 and showed its author
    ///     nothing. A stand-in feeds it, and is in no client's document.
    /// </summary>
    [Fact]
    public async Task A_compound_with_an_image_input_shows_its_nodes_when_opened_on_its_own() {
        using var deadline = Deadline(120);
        var compounds = Directory.CreateDirectory(Path.Combine(Library, LibraryLayout.CompoundsFolder)).FullName;
        var file = GraphTesting.Write(compounds, Tint("Tint"), "Tint.vxtexgraph");
        var instance = await StartServerAsync(deadline.Token);
        var state = await http.OpenAsync(file, deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        StartWorker();
        var first = await EvaluatedAsync(socket, state.Revision, deadline.Token);

        Assert.DoesNotContain(first.Diagnostics, static d => d.Severity == "Error");
        var after = await http.StateAsync(state.Id, deadline.Token);
        Assert.Equal([1, 2, 3], after.Nodes.Select(static node => node.Id));
        Assert.Contains(after.Previews, static p => p.Node == 2);
        Assert.DoesNotContain(after.Previews, static p => p.Node > 3);
    }

    /// <summary>A compound that inverts what it is fed.</summary>
    static NodeGraphAsset Tint(string name) =>
        new() {
            Name = name,
            Nodes = [
                new() { Id = 1, Type = SubGraphs.InputType }, new() { Id = 2, Type = "Colour/Invert", X = 200 },
                new() { Id = 3, Type = SubGraphs.OutputType, X = 400 }
            ],
            Edges = [
                new() { FromNode = 1, FromPort = "Input", ToNode = 2, ToPort = "Input" },
                new() { FromNode = 2, FromPort = "Out", ToNode = 3, ToPort = "Out" }
            ],
            Interface = [
                new() { Name = "Input", Direction = PortDirection.Input, Kind = PortKind.Image },
                new() { Name = "Out", Direction = PortDirection.Output, Kind = PortKind.Image }
            ]
        };

    [Fact]
    public async Task A_full_size_image_is_a_png_by_hash_at_the_size_of_the_graph() {
        using var deadline = Deadline(120);
        var sample = GraphTesting.Sample(512);
        var nodes = sample.Nodes.Select(static node => node.Id != 1
                    ? node
                    : node with { Values = new() { ["Scale"] = [4f] } }
            )
            .ToArray();
        var (_, state, socket, _) = await OpenSampleAsync(sample with { Nodes = nodes }, deadline.Token);
        using (socket) {
            using var response = await http.PostAsJsonAsync(
                $"/api/v1/graphs/{state.Id}/images",
                new(1, "Out"),
                ContractsJsonContext.Default.GraphImageRequest,
                deadline.Token
            );
            response.EnsureSuccessStatusCode();
            var image = (await response.Content.ReadFromJsonAsync(
                ContractsJsonContext.Default.GraphImage,
                deadline.Token
            ))!;
            Assert.Equal((512, 512), (image.Width, image.Height));
            var png = await http.GetByteArrayAsync($"/api/v1/previews/{image.Hash}", deadline.Token);
            Assert.Equal(image.Hash, Convert.ToHexStringLower(SHA256.HashData(png)));
            var picture = PngCodec.Decode(png);
            Assert.Equal(512, picture.Width);

            // It tiles: the last row runs on into the first as smoothly as any row runs into the next.
            double Step(int a, int b) =>
                Enumerable.Range(0, 512)
                    .Average(x => Math.Abs(picture.Pixels[(a * 512 + x) * 4] - picture.Pixels[(b * 512 + x) * 4]));

            Assert.True(Step(511, 0) < 3 * Math.Max(1, Enumerable.Range(0, 511).Average(y => Step(y, y + 1))));

            using var missing = await http.PostAsJsonAsync(
                $"/api/v1/graphs/{state.Id}/images",
                new(1, "Nothing"),
                ContractsJsonContext.Default.GraphImageRequest,
                deadline.Token
            );
            Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        }
    }

    [Fact]
    public async Task A_compound_evaluates_on_its_own_and_its_host_follows_a_save() {
        using var deadline = Deadline(120);
        var compounds = Directory.CreateDirectory(Path.Combine(Library, LibraryLayout.CompoundsFolder));
        NodeGraphAsset compound = new() {
            Name = "Mine",
            Nodes = [
                new() { Id = 1, Type = "Source/Noise", Values = new() { ["Scale"] = [4f] } },
                new() { Id = 2, Type = "Sub-graph/Output", X = 300 }
            ],
            Edges = [new() { FromNode = 1, FromPort = "Out", ToNode = 2, ToPort = "Result" }],
            Interface = [new() { Name = "Result", Direction = PortDirection.Output, Kind = PortKind.Image }],
            Settings = new() { ["seed"] = "7", ["baseWidth"] = "64", ["baseHeight"] = "64" }
        };
        var file = GraphTesting.Write(compounds.FullName, compound, "Mine.vxtexgraph");
        var host = GraphTesting.Sample() with {
            Nodes = [..GraphTesting.Sample().Nodes, new() { Id = 6, Type = "Mine", Y = 400 }]
        };
        var (instance, state, socket, first) = await OpenSampleAsync(host, deadline.Token);
        using (socket) {
            var picture = first.Previews.Single(static p => p.Node == 6).Hash;
            var inner = await http.OpenAsync(file, deadline.Token);
            using var innerSocket = await FollowAsync(instance, inner, deadline.Token);
            var own = await EvaluatedAsync(innerSocket, inner.Revision, deadline.Token);
            Assert.Contains(own.Previews, static p => p.Node == 1);

            await http.EditAsync(inner.Id, deadline.Token, new GraphSetValue(1, "Scale", [12f]));
            await http.ActAsync(inner.Id, "save", deadline.Token);

            var changed = await EvaluatedAsync(socket, state.Revision, deadline.Token);
            Assert.NotEqual(picture, changed.Previews.Single(static p => p.Node == 6).Hash);
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
