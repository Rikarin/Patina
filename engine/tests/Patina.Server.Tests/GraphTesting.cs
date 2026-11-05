using Patina.Contracts;
using System.Net.Http.Json;
using Vixen.Core.Yaml;
using Vixen.NodeGraph;

namespace Patina.Server.Tests;

/// <summary>Graphs to edit, and the calls every graph test makes.</summary>
static class GraphTesting {
    /// <summary>
    ///     Noise (1) into HSL (2) into Invert (3) into an Output (4), with a blur (5) hanging off the noise, in
    ///     an order no canonical writer would pick.
    /// </summary>
    public static NodeGraphAsset Sample(int side = 128) =>
        new() {
            Name = "Sample",
            Nodes = [
                Node(5, "Filters/Blur", 300, 200, "Radius", 2f),
                Node(1, "Source/Noise", 0, 0, "Scale", 4f),
                Node(3, "Colour/Invert", 600, 0),
                Node(2, "Colour/HSL", 300, 0, "Saturation", 0.5f),
                new() { Id = 4, Type = "Output/Output", X = 900, Texts = new() { ["Usage"] = "baseColor" } }
            ],
            Edges = [Edge(3, 4), Edge(2, 3), Edge(1, 2), Edge(1, 5)],
            Settings = new() { ["seed"] = "7", ["baseWidth"] = $"{side}", ["baseHeight"] = $"{side}" },
            Parameters = [
                new() {
                    Name = "Wear",
                    Default = "0.5",
                    Kind = SettingKind.Float,
                    Minimum = 0f,
                    Maximum = 1f
                }
            ],
            Groups = [new() { Title = "Base", Nodes = [1, 2] }],
            Comments = [new() { Text = "Rust goes here", X = 10, Y = 20, Width = 200, Height = 100 }]
        };

    public static string Write(string folder, NodeGraphAsset asset, string name = "sample.vxtexgraph") {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, YamlSerializer.ToYaml(asset));
        return path;
    }

    public static async Task<GraphState> OpenAsync(this HttpClient client, string path, CancellationToken token) {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/graphs",
            new(path),
            ContractsJsonContext.Default.GraphOpenRequest,
            token
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphState, token))!;
    }

    public static async Task<GraphState> StateAsync(this HttpClient client, string id, CancellationToken token) =>
        (await client.GetFromJsonAsync($"/api/v1/graphs/{id}", ContractsJsonContext.Default.GraphState, token))!;

    public static Task<HttpResponseMessage> SendAsync(
        this HttpClient client,
        string id,
        GraphCommandBatch batch,
        CancellationToken token
    ) =>
        client.PostAsJsonAsync(
            $"/api/v1/graphs/{id}/commands",
            batch,
            ContractsJsonContext.Default.GraphCommandBatch,
            token
        );

    public static async Task<GraphEditResult> EditAsync(
        this HttpClient client,
        string id,
        CancellationToken token,
        params GraphCommand[] commands
    ) {
        using var response = await client.SendAsync(id, new(commands), token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphEditResult, token))!;
    }

    /// <summary>Undo, redo or save: a POST that answers with an edit result.</summary>
    public static async Task<GraphEditResult> ActAsync(
        this HttpClient client,
        string id,
        string action,
        CancellationToken token
    ) {
        using var response = await client.PostAsync($"/api/v1/graphs/{id}/{action}", null, token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphEditResult, token))!;
    }

    public static async Task<GraphEventPage> EventsAsync(
        this HttpClient client,
        string id,
        long since,
        CancellationToken token
    ) =>
        (await client.GetFromJsonAsync(
            $"/api/v1/graphs/{id}/events?since={since}",
            ContractsJsonContext.Default.GraphEventPage,
            token
        ))!;

    /// <summary>What a client holds, kept current by nothing but deltas.</summary>
    public static async Task<IReadOnlyList<GraphCompound>> CompoundsAsync(
        this HttpClient client,
        string id,
        CancellationToken token
    ) =>
        (await client.GetFromJsonAsync(
            $"/api/v1/graphs/{id}/compounds",
            ContractsJsonContext.Default.IReadOnlyListGraphCompound,
            token
        ))!;

    public static (SortedDictionary<int, GraphNodeState> Nodes, HashSet<GraphEdgeState> Edges) Replay(
        GraphState start,
        IEnumerable<GraphEvent> events
    ) {
        var nodes = new SortedDictionary<int, GraphNodeState>(start.Nodes.ToDictionary(static node => node.Id));
        var edges = start.Edges.ToHashSet();
        foreach (var delta in events.Select(static e => e.Delta).OfType<GraphDelta>()) {
            foreach (var node in delta.Nodes) {
                nodes[node.Id] = node;
            }

            foreach (var id in delta.RemovedNodes) {
                nodes.Remove(id);
            }

            edges.ExceptWith(delta.RemovedEdges);
            edges.UnionWith(delta.Edges);
        }

        return (nodes, edges);
    }

    static GraphNodeAsset Node(int id, string type, float x, float y, string? port = null, float value = 0f) =>
        new() {
            Id = id,
            Type = type,
            X = x,
            Y = y,
            Values = port is null ? [] : new() { [port] = [value] }
        };

    static GraphEdgeAsset Edge(int from, int to) =>
        new() { FromNode = from, FromPort = "Out", ToNode = to, ToPort = "Input" };
}
