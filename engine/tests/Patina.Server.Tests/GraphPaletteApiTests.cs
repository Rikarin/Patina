using Patina.Contracts;
using System.Net;
using Vixen.NodeGraph;

namespace Patina.Server.Tests;

/// <summary>The palette's side of a graph: what a search offers, and how a new node is wired.</summary>
public sealed partial class GraphApiTests {
    /// <summary>What Enter added while the palette was a substring filter in category order (#183).</summary>
    [Theory]
    [InlineData("Levels", "Auto Levels")]
    [InlineData("Tile", "Tile Random")]
    [InlineData("Gradient", "Gradient Map")]
    [InlineData("Warp", "Directional Warp")]
    [InlineData("Output", "Channel Shuffle")]
    public async Task The_first_match_is_what_was_typed(string typed, string addedBefore) {
        var (client, state) = await OpenAsync();
        using (client) {
            var found = await client.SearchAsync(state.Id, $"query={typed}", Token);
            Assert.Equal(typed, found[0]);
            Assert.Contains(addedBefore, found);
        }
    }

    [Fact]
    public async Task A_search_ignores_case_and_nothing_typed_offers_everything() {
        var (client, state) = await OpenAsync();
        using (client) {
            Assert.Equal("Levels", (await client.SearchAsync(state.Id, "query=levels", Token))[0]);
            Assert.Equal("Noise", (await client.SearchAsync(state.Id, "query=Source/Noise", Token))[0]);
            Assert.True((await client.SearchAsync(state.Id, string.Empty, Token)).Count > 50);
            Assert.Empty(await client.SearchAsync(state.Id, "query=zzzzzz", Token));
        }
    }

    [Fact]
    public async Task A_search_for_a_wire_keeps_what_the_wire_can_use() {
        var (client, state) = await OpenAsync();
        using (client) {
            var image = await client.SearchAsync(state.Id, "takes=Image", Token);
            Assert.Contains("Blur", image);
            Assert.DoesNotContain("Noise", image);
            var outputs = await client.SearchAsync(state.Id, "gives=Image", Token);
            Assert.Contains("Noise", outputs);
            Assert.DoesNotContain("Output", outputs);
            var both = await client.SearchAsync(state.Id, "takes=Image&gives=Image", Token);
            Assert.Contains("Blur", both);
            Assert.DoesNotContain("Noise", both);
            Assert.DoesNotContain("Output", both);

            var path = $"/api/v1/graphs/{state.Id}/node-types/search?takes=Teapot";
            using var refused = await client.GetAsync(path, Token);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        }
    }

    [Fact]
    public async Task A_search_offers_the_boundary_nodes_only_a_compound_has() {
        var client = server.AuthorisedClient();
        using (client) {
            var graph = GraphTesting.Sample() with {
                Interface = [new() { Name = "Base", Direction = PortDirection.Input, Kind = PortKind.Image }]
            };
            var state = await client.OpenAsync(GraphTesting.Write(server.Library, graph), Token);
            Assert.Equal(["Input", "Output"], await client.SearchAsync(state.Id, "query=Sub-graph", Token));
        }
    }

    [Fact]
    public async Task A_new_node_wires_to_the_first_port_that_fits_as_one_undo_step() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Filters/Blur", 0, 0, -1),
                new GraphWire(1, -1)
            );
            var wired = await client.StateAsync(state.Id, Token);
            var edge = wired.Edges.Single(static edge => edge is { FromNode: 1, ToNode: > 5 });
            Assert.Equal(("Out", "Input"), (edge.FromPort, edge.ToPort));

            await client.ActAsync(state.Id, "undo", Token);
            Assert.Equal(state.Nodes.Count, (await client.StateAsync(state.Id, Token)).Nodes.Count);
        }
    }

    /// <summary>A node made from a selection of several takes each of them on an input of its own, in turn (#183).</summary>
    [Fact]
    public async Task A_new_node_from_several_takes_each_on_the_next_free_input() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Colour/Blend", 0, 0, -1),
                new GraphWire(2, -1),
                new GraphWire(5, -1)
            );
            var after = await client.StateAsync(state.Id, Token);
            var blend = after.Nodes.Max(static node => node.Id);
            var into = after.Edges.Where(edge => edge.ToNode == blend)
                .ToDictionary(static edge => edge.ToPort, static edge => edge.FromNode);
            Assert.Equal(2, into["Background"]);
            Assert.Equal(5, into["Foreground"]);

            // An input that is already wired is still the one a single wire takes when nothing else fits.
            await client.EditAsync(state.Id, Token, new GraphWire(3, blend));
            var again = await client.StateAsync(state.Id, Token);
            Assert.Contains(again.Edges, edge => edge is { FromNode: 3, ToPort: "Background" } && edge.ToNode == blend);
        }
    }

    [Fact]
    public async Task A_new_node_with_nothing_that_fits_is_still_added() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Source/Noise", 0, 0, -1),
                new GraphWire(1, -1)
            );
            var after = await client.StateAsync(state.Id, Token);
            Assert.Equal(state.Nodes.Count + 1, after.Nodes.Count);
            Assert.Equal(state.Edges.Count, after.Edges.Count);
        }
    }

    [Fact]
    public async Task A_node_placed_on_a_wire_takes_its_place_in_it() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Filters/Blur", 0, 0, -1),
                new GraphWire(1, -1, "Out"),
                new GraphWire(-1, 2, ToPort: "Input")
            );
            var after = await client.StateAsync(state.Id, Token);
            var blur = after.Nodes.Max(static node => node.Id);
            Assert.DoesNotContain(after.Edges, static edge => edge is { FromNode: 1, ToNode: 2 });
            Assert.Contains(after.Edges, edge => edge.FromNode == 1 && edge.ToNode == blur);
            Assert.Contains(after.Edges, edge => edge.FromNode == blur && edge.ToNode == 2);
        }
    }
}
