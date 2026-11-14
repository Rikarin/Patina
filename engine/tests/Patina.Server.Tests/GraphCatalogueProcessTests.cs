using Patina.Contracts;
using Patina.Library;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using Vixen.Core.Imaging;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Server.Tests;

/// <summary>
///     Every node a palette offers, the way an author gets one: from <c>node-types</c>, by a command, with its images
///     wired, through the real Server and Worker, and judged by a preview arriving. Plan §6 P2's exit rests on this.
/// </summary>
public sealed class GraphCatalogueProcessTests : IDisposable {
    const string MeshBake = "It reads a meshmap: reference, a measurement of a mesh, and baking one is P4.";

    /// <summary>What cannot work yet, and why. One that starts working fails the test, so this only shrinks.</summary>
    static readonly Dictionary<string, string> CannotWorkYet = new(StringComparer.Ordinal) {
        ["Source/Mesh Map"] = MeshBake,
        ["Generators/Curvature Edge Wear"] = MeshBake,
        ["Generators/Dirt"] = MeshBake,
        ["Generators/Dust"] = MeshBake,
        ["Generators/Grunge Rough Dirty"] = MeshBake,
        ["Generators/Mask Editor"] = MeshBake,
        ["Generators/Metal Edge Wear"] = MeshBake,
        ["Generators/Position Gradient"] = MeshBake,
        ["Generators/Cavity Select"] = MeshBake,
        ["Generators/Direction"] = MeshBake,
        ["Generators/Distance From Point"] = MeshBake,
        ["Generators/Drips"] = MeshBake,
        ["Generators/Edge Select"] = MeshBake,
        ["Generators/Fibrous Edge Wear"] = MeshBake,
        ["Generators/Frayed Edges"] = MeshBake,
        ["Generators/Ground Grime"] = MeshBake,
        ["Generators/ID Mask"] = MeshBake,
        ["Generators/Light"] = MeshBake,
        ["Generators/Linear Gradient"] = MeshBake,
        ["Generators/Moss"] = MeshBake,
        ["Generators/Paint Chips"] = MeshBake,
        ["Generators/Splashes"] = MeshBake,
        ["Generators/Sun Fade"] = MeshBake,
        ["Generators/Wrinkles"] = MeshBake,
        ["Generators/Surface Scratches"] = MeshBake,
        ["Generators/Thin Parts"] = MeshBake,
        ["Generators/Water Line"] = MeshBake,
        ["Stylised/Baked Lighting"] = MeshBake,
        ["Stylised/Environment Lighting"] = MeshBake,
        ["Weathering/Edge Damage"] = MeshBake,
        ["Weathering/Peeling Paint"] = MeshBake,
        ["Weathering/Rust"] = MeshBake,
        ["Weathering/Shut Lines"] = MeshBake,
        ["Weathering/Water Drops"] = MeshBake,
        ["Weathering/Weathered Rock"] = MeshBake,
        ["Weathering/Worn Edges"] = MeshBake,
        ["Weathering/Worn Fabric"] = MeshBake,
        ["Weathering/Worn Leather"] = MeshBake,
        ["Weathering/Worn Metal"] = MeshBake
    };

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-catalogue-");
    readonly string socketPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");
    readonly List<Process> started = [];
    readonly HttpClient http = new();
    readonly List<string> flat = [];

    string Library => Directory.CreateDirectory(Path.Combine(folder.FullName, "Game.ptlib")).FullName;

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

    void Start(string dll, params string[] arguments) {
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
    }

    async Task<ServerInstance> StartAsync(CancellationToken cancellationToken) {
        var discovery = Path.Combine(folder.FullName, "servers.json");
        Start(
            Engine.Dll("Patina.Server", "patina-server"),
            "--library",
            Library,
            $"--Patina:DiscoveryFile={discovery}",
            $"--Patina:Worker:SocketPath={socketPath}",
            "--Patina:WatchStdin=true"
        );
        var registry = new InstanceRegistry(discovery);
        while (true) {
            if (await registry.FindAsync(Library, cancellationToken) is { } instance) {
                http.BaseAddress = new($"http://127.0.0.1:{instance.Port}");
                http.DefaultRequestHeaders.Authorization = new("Bearer", instance.Token);
                Start(Engine.Dll("Patina.Worker", "patina-worker"), "--socket", socketPath, "--watch-stdin");
                return instance;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    static async Task<GraphEvaluation> EvaluationAsync(
        ClientWebSocket socket,
        long revision,
        CancellationToken cancellationToken
    ) {
        var buffer = new byte[4 * 1024 * 1024];
        while (true) {
            var length = 0;
            ValueWebSocketReceiveResult part;
            do {
                part = await socket.ReceiveAsync(buffer.AsMemory(length), cancellationToken);
                length += part.Count;
            } while (!part.EndOfMessage);

            var item = JsonSerializer.Deserialize(buffer.AsSpan(0, length), ContractsJsonContext.Default.GraphEvent)!;
            if (item.Evaluation is { } evaluation && evaluation.Revision >= revision) {
                return evaluation;
            }
        }
    }

    /// <summary>
    ///     What a node of a type needs beyond its wires: a setting left at its default that means nothing yet. These
    ///     are what an author would type, and none of them makes a failure pass.
    /// </summary>
    static IEnumerable<GraphCommand> Settings(GraphNodeType type, int node) {
        if (type.Path == "Source/Bitmap") {
            yield return new GraphSetText(node, "Source", "picture.png");
        }

        if (type.Path == "Source/Vector") {
            yield return new GraphSetText(node, "File", "drawing.svg");
        }

        if (type.Path == "Output/Output") {
            yield return new GraphSetText(node, "Usage", "normal");
        }
    }

    /// <summary>One type on its own in the graph: why it failed, or null.</summary>
    async Task<string?> TryAsync(
        string graph,
        ClientWebSocket socket,
        GraphNodeType type,
        CancellationToken cancellationToken
    ) {
        const int Source = 1;
        const int Added = -1;
        List<GraphCommand> commands = [new GraphAddNode(type.Path, 300, 300, Added)];
        commands.AddRange(
            type.Ports.Where(static port => port is { Direction: "Input", Kind: "Image" })
                .Select(port => new GraphConnect(Source, "Out", Added, port.Name))
        );
        var refused = await http.SendAsync(graph, new(commands), cancellationToken);
        if (!refused.IsSuccessStatusCode) {
            return $"the commands were refused: {await refused.Content.ReadAsStringAsync(cancellationToken)}";
        }

        var added = (await refused.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.GraphEditResult,
            cancellationToken
        ))!;
        refused.Dispose();
        var node = added.Created[0].Node;
        var result = added;
        foreach (var setting in Settings(type, node)) {
            result = await http.EditAsync(graph, cancellationToken, setting);
        }

        string? failure;
        using (var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
            patience.CancelAfter(TimeSpan.FromSeconds(60));
            try {
                var evaluation = await EvaluationAsync(socket, result.Revision, patience.Token);
                if (evaluation.Error?.StartsWith("No Vulkan", StringComparison.Ordinal) == true) {
                    Gpu.Skip(evaluation.Error);
                }

                var problems = evaluation.Diagnostics.Where(static d => d.Severity == "Error").ToArray();
                var state = await http.StateAsync(graph, cancellationToken);
                failure = evaluation.Error
                    ?? (problems.Length > 0
                        ? string.Join("; ", problems.Select(static d => $"{d.Id}: {d.Message}"))
                        : state.Previews.Any(preview => preview.Node == node)
                            ? null
                            : "no preview came back");
                if (failure is null) {
                    var preview = state.Previews.First(preview => preview.Node == node);
                    var png = await http.GetByteArrayAsync($"/api/v1/previews/{preview.Hash}", cancellationToken);
                    var picture = PngCodec.Decode(png);
                    Assert.Equal(preview.Width, picture.Width);
                    if (picture.Pixels.Chunk(4).Distinct(ByteRuns.Comparer).Count() == 1) {
                        flat.Add(type.Path);
                    }
                }
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                failure = "the evaluation never ended";
            }
        }

        await http.EditAsync(graph, cancellationToken, new GraphRemoveNodes([node]));
        return failure;
    }

    /// <summary>Every node and compound at both bit depths, since a kernel's format is chosen by the graph's.</summary>
    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public async Task Every_node_and_compound_a_palette_offers_gives_a_preview(int depth) {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(20));
        var token = deadline.Token;

        var pixels = Enumerable.Range(0, 4 * 4 * 4).Select(static i => (byte)(i * 4)).ToArray();
        PngCodec.Save(Path.Combine(Library, "picture.png"), new(4, 4, pixels));
        File.WriteAllText(
            Path.Combine(Library, "drawing.svg"),
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">
              <circle cx="5" cy="5" r="3" fill="#c33"/>
            </svg>
            """
        );
        NodeGraphAsset host = new() {
            Name = "Catalogue",
            Nodes = [
                new() { Id = 1, Type = "Source/Noise" },
                new() { Id = 2, Type = "Output/Output", X = 600, Texts = new() { ["Usage"] = "baseColor" } }
            ],
            Edges = [new() { FromNode = 1, FromPort = "Out", ToNode = 2, ToPort = "Input" }],
            Settings = new() {
                ["seed"] = "7",
                ["baseWidth"] = "64",
                ["baseHeight"] = "64",
                ["bitDepth"] = depth.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        };

        var instance = await StartAsync(token);
        var state = await http.OpenAsync(GraphTesting.Write(Library, host), token);
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(
            new($"ws://127.0.0.1:{instance.Port}/ws/graphs/{state.Id}?since=0&access_token={instance.Token}"),
            token
        );
        var first = await EvaluationAsync(socket, state.Revision, token);
        if (first.Error?.StartsWith("No Vulkan", StringComparison.Ordinal) == true) {
            Gpu.Skip(first.Error);
        }

        var types = (await http.GetFromJsonAsync(
            $"/api/v1/graphs/{state.Id}/node-types",
            ContractsJsonContext.Default.IReadOnlyListGraphNodeType,
            token
        ))!;
        var paths = types.Select(static type => type.Path).ToHashSet(StringComparer.Ordinal);
        Assert.Superset(TextureCompoundLibrary.Shipped.ToHashSet(StringComparer.Ordinal), paths);

        Dictionary<string, string> failed = new(StringComparer.Ordinal);
        foreach (var type in types) {
            if (await TryAsync(state.Id, socket, type, token) is { } why) {
                failed[type.Path] = why;
            }
        }

        var report = string.Join('\n', failed.Select(static f => $"{f.Key}: {f.Value}"));
        if (Environment.GetEnvironmentVariable("PATINA_CATALOGUE_OUT") is { Length: > 0 } file) {
            await File.WriteAllTextAsync(
                $"{file}.{depth}",
                $"{types.Count} types, {TextureCompoundLibrary.Shipped.Length} shipped compounds\n{report}\n"
                + $"A single colour, on noise and default values: {string.Join(", ", flat)}\n",
                token
            );
        }

        var unexpected = failed.Where(static f => !CannotWorkYet.ContainsKey(f.Key)).ToArray();
        Assert.True(
            unexpected.Length == 0,
            $"{unexpected.Length} of {types.Count} fail:\n"
            + string.Join('\n', unexpected.Select(static f => $"{f.Key}: {f.Value}"))
        );
        Assert.DoesNotContain(CannotWorkYet.Keys, path => paths.Contains(path) && !failed.ContainsKey(path));
    }
}

/// <summary>Compares pixels by their bytes.</summary>
sealed class ByteRuns : IEqualityComparer<byte[]> {
    public static ByteRuns Comparer { get; } = new();

    public bool Equals(byte[]? x, byte[]? y) => x.AsSpan().SequenceEqual(y);

    public int GetHashCode(byte[] obj) => BitConverter.ToInt32(obj);
}
