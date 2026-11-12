using Patina.Contracts;
using Patina.Library;
using System.Buffers.Binary;
using Vixen.NodeGraph;

namespace Patina.Server.Tests;

/// <summary>The viewport following a graph by itself (#244), by the processes the bakes run on.</summary>
public sealed partial class BakeProcessTests {
    /// <summary>
    ///     The Server keeps a followed graph on the mesh by itself: its outputs after each evaluation, and the node its
    ///     selection views as the selection moves, with no client asking again (#244). Each change of what lies on the
    ///     mesh moves the count in the frame header, which is how a client knows to read the state.
    /// </summary>
    [Fact]
    public async Task A_followed_graph_keeps_up_with_its_evaluations_and_its_selection() {
        using var deadline = Deadline(120);
        StartServer();
        await DiscoverAsync(deadline.Token);
        StartWorker();
        var asset = new NodeGraphAsset {
            Name = "Followed",
            Nodes = [
                new() { Id = 1, Type = "Source/Gradient" },
                new() { Id = 2, Type = "Colour/Invert", X = 200 },
                new() { Id = 3, Type = "Output/Output", X = 400, Texts = new() { ["Usage"] = "roughness" } }
            ],
            Edges = [
                new() { FromNode = 1, FromPort = "Out", ToNode = 2, ToPort = "Input" },
                new() { FromNode = 2, FromPort = "Out", ToNode = 3, ToPort = "Input" }
            ],
            Settings = new() { ["baseWidth"] = "64", ["baseHeight"] = "64" }
        };
        var graph = await http.OpenAsync(GraphTesting.Write(Library, asset, "followed.vxtexgraph"), deadline.Token);
        var instance = (await new InstanceRegistry(DiscoveryFile).FindAsync(Library, deadline.Token))!;
        using var evaluations = await FollowAsync(instance, graph, deadline.Token);
        await NextAsync(evaluations, graph.Revision, deadline.Token);
        using var socket = await ViewportAsync(deadline.Token);

        async Task<ViewportState> UntilAsync(Func<ViewportState, bool> shown, string what) {
            while (true) {
                var state = await ViewportStateAsync(deadline.Token);
                if (shown(state)) {
                    return state;
                }

                Assert.False(deadline.IsCancellationRequested, what);
                await Task.Delay(50, deadline.Token);
            }
        }

        async Task FollowGraphAsync(ViewportFollow follow) {
            using var response = await http.PutAsJsonAsync(
                "/api/v1/viewport/follow",
                follow,
                ContractsJsonContext.Default.ViewportFollow,
                deadline.Token
            );
            response.EnsureSuccessStatusCode();
        }

        async Task SelectAsync(GraphSelection selection) {
            using var response = await http.PutAsJsonAsync(
                $"/api/v1/graphs/{graph.Id}/selection",
                selection,
                ContractsJsonContext.Default.GraphSelection,
                deadline.Token
            );
            response.EnsureSuccessStatusCode();
        }

        await FollowGraphAsync(new(graph.Id));
        var outputs = await UntilAsync(static state => state.Maps.Roughness is not null, "No outputs were laid.");
        Assert.EndsWith("Sphere.glb", outputs.Mesh, StringComparison.Ordinal);
        Assert.Equal(new(graph.Id), outputs.Following);
        var first = await FrameAsync(socket, deadline.Token);

        // An edit evaluates, and the new picture is laid with nobody asking.
        await http.EditAsync(graph.Id, deadline.Token, new GraphRemoveNodes([2], true));
        await UntilAsync(
            state => state.Maps.Roughness is { } roughness && roughness != outputs.Maps.Roughness,
            "The evaluation after the edit was not laid."
        );
        await http.ActAsync(graph.Id, "undo", deadline.Token);

        // The node the selection views, alone, as the selection moves and a pin holds it.
        await SelectAsync(new([2]));
        await FollowGraphAsync(new(graph.Id, true));
        await UntilAsync(static state => state.Node?.Node == 2, "The selected node was not shown.");
        await SelectAsync(new([1, 2]));
        await UntilAsync(static state => state.Node?.Node == 1, "The new selection was not shown.");
        await SelectAsync(new([2], 1));
        await Task.Delay(300, deadline.Token);
        Assert.Equal(1, (await ViewportStateAsync(deadline.Token)).Node?.Node);
        await SelectAsync(new([2]));
        await UntilAsync(static state => state.Node?.Node == 2, "Letting the pin go did not show the selection.");

        // A frame says what lies on the mesh has changed since the first.
        uint input = 0;
        byte[] frame;
        do {
            await SendAsync(socket, new(Input: ++input), deadline.Token);
            frame = await FrameAsync(socket, deadline.Token);
        } while (BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(16)) != input);

        Assert.NotEqual(
            BinaryPrimitives.ReadUInt32LittleEndian(first.AsSpan(28)),
            BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(28))
        );

        // Nothing selected shows the outputs, and a mesh opened keeps the graph followed and on it.
        await SelectAsync(new([]));
        await UntilAsync(static state => state.Node is null && state.Maps.Roughness is not null, "No outputs.");
        var low = Request().Low;
        using (var opened = await http.PutAsJsonAsync(
                   "/api/v1/viewport/mesh",
                   new(low),
                   ContractsJsonContext.Default.ViewportMeshRequest,
                   deadline.Token
               )) {
            opened.EnsureSuccessStatusCode();
        }

        var onMesh = await UntilAsync(
            state => state.Mesh == low && state.Maps.Roughness is not null,
            "The followed graph did not come back on the mesh."
        );
        Assert.Equal(new(graph.Id, true), onMesh.Following);
        Assert.Null(onMesh.Error);
    }
}
