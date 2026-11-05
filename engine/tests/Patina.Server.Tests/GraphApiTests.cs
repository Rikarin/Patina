using Patina.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>Editing a graph through the API with no Worker: the Server alone owns the document.</summary>
public sealed class GraphApiTests : IDisposable {
    readonly ServerFixture server = new();

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => server.Dispose();

    async Task<(HttpClient Client, GraphState State)> OpenAsync() {
        var client = server.AuthorisedClient();
        return (client, await client.OpenAsync(GraphTesting.Write(server.Library, GraphTesting.Sample()), Token));
    }

    [Fact]
    public async Task Opening_a_graph_reads_its_nodes_edges_and_parameters() {
        var (client, state) = await OpenAsync();
        using (client) {
            Assert.Equal("Sample", state.Name);
            Assert.Equal([1, 2, 3, 4, 5], state.Nodes.Select(static node => node.Id));
            Assert.Equal(4, state.Edges.Count);
            Assert.Equal(4f, Assert.Single(state.Nodes[0].Values["Scale"]));
            Assert.Equal("baseColor", state.Nodes[3].Texts["Usage"]);
            Assert.Equal("7", state.Settings["seed"]);
            var wear = Assert.Single(state.Parameters);
            Assert.Equal(("Wear", "Float", 0f, 1f), (wear.Name, wear.Kind, wear.Minimum, wear.Maximum));
            Assert.False(state.History.CanUndo);
            Assert.False(state.History.Dirty);
        }
    }

    [Fact]
    public async Task Opening_the_same_file_twice_joins_one_session() {
        var (client, state) = await OpenAsync();
        using (client) {
            var again = await client.OpenAsync(state.Path, Token);
            Assert.Equal(state.Id, again.Id);
            var relative = await client.OpenAsync(Path.GetFileName(state.Path), Token);
            Assert.Equal(state.Id, relative.Id);
        }
    }

    [Fact]
    public async Task A_file_that_is_missing_or_not_a_graph_is_refused() {
        using var client = server.AuthorisedClient();
        using var missing = await client.PostAsJsonAsync(
            "/api/v1/graphs",
            new(Path.Combine(server.Library, "nothing.vxtexgraph")),
            ContractsJsonContext.Default.GraphOpenRequest,
            Token
        );
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var bad = Path.Combine(server.Library, "bad.vxtexgraph");
        await File.WriteAllTextAsync(bad, "nodes: [ this is not", Token);
        using var unreadable = await client.PostAsJsonAsync(
            "/api/v1/graphs",
            new(bad),
            ContractsJsonContext.Default.GraphOpenRequest,
            Token
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unreadable.StatusCode);
    }

    [Fact]
    public async Task The_node_types_describe_a_palette() {
        var (client, state) = await OpenAsync();
        using (client) {
            var types = (await client.GetFromJsonAsync(
                $"/api/v1/graphs/{state.Id}/node-types",
                ContractsJsonContext.Default.IReadOnlyListGraphNodeType,
                Token
            ))!;

            Assert.True(types.Count >= 49, $"{types.Count} types");
            var blur = types.Single(static type => type.Path == "Filters/Blur");
            Assert.Equal(("Blur", "Filters"), (blur.Title, blur.Category));
            Assert.Equal("Image", blur.Ports.Single(static port => port.Name == "Input").Kind);
            Assert.Equal(
                ("Input", "Float"),
                (blur.Ports.Single(static p => p.Name == "Radius").Direction,
                    blur.Ports.Single(static p => p.Name == "Radius").Kind)
            );
            Assert.Equal(8f, Assert.Single(blur.Ports.Single(static p => p.Name == "Radius").Default));
            var blend = types.Single(static type => type.Path == "Colour/Blend");
            Assert.Contains("Multiply", blend.Settings.Single(static s => s.Name == "Mode").Accepted);
            Assert.Contains(types, static type => type.Path == "Patterns/Cells");
        }
    }

    [Fact]
    public async Task A_batch_adds_and_wires_nodes_and_names_them_for_the_client() {
        var (client, state) = await OpenAsync();
        using (client) {
            var result = await client.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Colour/Invert", 10, 20, -1),
                new GraphConnect(1, "Out", -1, "Input"),
                new GraphSetValue(-1, "Red", [0f])
            );

            var created = Assert.Single(result.Created);
            Assert.Equal(-1, created.Ref);
            Assert.Equal(6, created.Node);
            var after = await client.StateAsync(state.Id, Token);
            Assert.Contains(new(1, "Out", 6, "Input"), after.Edges);
            var node = after.Nodes.Single(static node => node.Id == 6);
            Assert.Equal((10f, 20f), (node.X, node.Y));
            Assert.Equal(state.Revision + 1, after.Revision);
            Assert.True(after.History.Dirty);
        }
    }

    [Fact]
    public async Task Undo_and_redo_go_a_batch_at_a_time_and_cover_every_kind_of_edit() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Colour/Invert", 1, 2, -1),
                new GraphConnect(5, "Out", -1, "Input"),
                new GraphDisconnect(3, "Input"),
                new GraphMoveNodes([new(1, 50, 60)]),
                new GraphSetValue(2, "Hue", [0.25f]),
                new GraphSetText(4, "Usage", "normal"),
                new GraphSetSetting("seed", "9"),
                new GraphRemoveNodes([5])
            );
            var edited = await client.StateAsync(state.Id, Token);
            Assert.Equal("normal", edited.Nodes.Single(static n => n.Id == 4).Texts["Usage"]);
            Assert.DoesNotContain(edited.Nodes, static n => n.Id == 5);

            await client.ActAsync(state.Id, "undo", Token);
            var undone = await client.StateAsync(state.Id, Token);
            AssertSameDocument(state, undone);
            Assert.False(undone.History.CanUndo);
            Assert.False(undone.History.Dirty);
            Assert.True(undone.History.CanRedo);

            await client.ActAsync(state.Id, "redo", Token);
            AssertSameDocument(edited, await client.StateAsync(state.Id, Token));

            await client.ActAsync(state.Id, "undo", Token);
            using var nothing = await client.PostAsync($"/api/v1/graphs/{state.Id}/undo", null, Token);
            Assert.Equal(HttpStatusCode.Conflict, nothing.StatusCode);
        }
    }

    [Fact]
    public async Task A_refused_batch_changes_nothing_and_sends_no_event() {
        var (client, state) = await OpenAsync();
        using (client) {
            GraphCommand[][] refused = [
                [new GraphAddNode("No/Such Node", 0, 0)],
                [new GraphRemoveNodes([99])],
                [new GraphMoveNodes([new(99, 0, 0)])],
                [new GraphConnect(1, "Nope", 2, "Input")],
                [new GraphConnect(3, "Out", 1, "Nope")],
                [new GraphConnect(3, "Out", 2, "Input")], // a cycle
                [new GraphDisconnect(1, "Input")],
                [new GraphSetValue(1, "Nope", [1f])],
                [new GraphSetValue(1, "Scale", [1f, 2f])],
                [new GraphSetValue(2, "Input", [1f])],
                [new GraphSetText(4, "Usage", "no such usage")],
                [new GraphSetText(4, "Nope", "x")],
                [new GraphSetSetting("nope", "1")],
                [new GraphSetSetting("baseWidth", "0")],
                [new GraphAddNode("Colour/Invert", 0, 0, 5)],
                []
            ];
            foreach (var commands in refused) {
                // A good command first, so that a partial application would show.
                GraphCommand[] batch = commands.Length == 0 ? [] : [new GraphMoveNodes([new(1, 7, 7)]), ..commands];
                using var response = await client.SendAsync(state.Id, new(batch), Token);
                Assert.True(
                    response.StatusCode == HttpStatusCode.UnprocessableEntity,
                    $"{commands.FirstOrDefault()}: {response.StatusCode}"
                );
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
                Assert.False(string.IsNullOrEmpty(problem.GetProperty("detail").GetString()));
            }

            var after = await client.StateAsync(state.Id, Token);
            AssertSameDocument(state, after);
            Assert.Equal(state.Revision, after.Revision);
            Assert.Empty((await client.EventsAsync(state.Id, state.Sequence, Token)).Events);
        }
    }

    [Fact]
    public async Task A_drag_merges_into_one_undo_and_undo_goes_back_to_before_it() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.1f]));
            foreach (var value in new[] { 0.2f, 0.3f, 0.4f }) {
                using var merged = await client.SendAsync(
                    state.Id,
                    new([new GraphSetValue(2, "Hue", [value])], Merge: true),
                    Token
                );
                merged.EnsureSuccessStatusCode();
            }

            var dragged = await client.StateAsync(state.Id, Token);
            Assert.Equal(0.4f, Assert.Single(dragged.Nodes[1].Values["Hue"]));

            await client.ActAsync(state.Id, "undo", Token);
            var undone = await client.StateAsync(state.Id, Token);
            Assert.False(undone.History.CanUndo);
            Assert.DoesNotContain("Hue", undone.Nodes[1].Values.Keys);

            await client.ActAsync(state.Id, "redo", Token);
            Assert.Equal(0.4f, Assert.Single((await client.StateAsync(state.Id, Token)).Nodes[1].Values["Hue"]));
        }
    }

    [Fact]
    public async Task Merging_stops_at_a_save_so_undo_can_return_to_the_saved_state() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.1f]));
            await client.ActAsync(state.Id, "save", Token);
            using var merged = await client.SendAsync(
                state.Id,
                new([new GraphSetValue(2, "Hue", [0.2f])], Merge: true),
                Token
            );
            merged.EnsureSuccessStatusCode();
            Assert.True((await client.StateAsync(state.Id, Token)).History.Dirty);

            await client.ActAsync(state.Id, "undo", Token);
            var undone = await client.StateAsync(state.Id, Token);
            Assert.False(undone.History.Dirty);
            Assert.Equal(0.1f, Assert.Single(undone.Nodes[1].Values["Hue"]));
        }
    }

    [Fact]
    public async Task Events_are_numbered_and_their_deltas_rebuild_the_document_for_a_second_client() {
        var (first, state) = await OpenAsync();
        using (first) {
            using var second = server.AuthorisedClient();
            await first.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Colour/Invert", 0, 0, -1),
                new GraphConnect(1, "Out", -1, "Input")
            );
            await first.EditAsync(state.Id, Token, new GraphRemoveNodes([5]));
            await second.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.5f]));
            await second.ActAsync(state.Id, "undo", Token);
            await first.ActAsync(state.Id, "undo", Token);
            await first.ActAsync(state.Id, "redo", Token);
            await first.EditAsync(state.Id, Token, new GraphMoveNodes([new(3, 1, 1)]));

            var page = await second.EventsAsync(state.Id, state.Sequence, Token);
            var edits = page.Events.Where(static e => e.Kind != GraphEventKind.Evaluated).ToArray();
            Assert.Equal(
                Enumerable.Range(1, page.Events.Count).Select(static i => (long)i),
                page.Events.Select(e => e.Sequence - state.Sequence)
            );
            Assert.Equal(
                [
                    GraphEventKind.Edited, GraphEventKind.Edited, GraphEventKind.Edited, GraphEventKind.Undone,
                    GraphEventKind.Undone, GraphEventKind.Redone, GraphEventKind.Edited
                ],
                edits.Select(static e => e.Kind)
            );

            var held = GraphTesting.Replay(state, edits);
            var now = await first.StateAsync(state.Id, Token);
            Assert.Equal(now.Nodes.Select(static n => n.Id), held.Nodes.Keys);
            foreach (var node in now.Nodes) {
                var replayed = held.Nodes[node.Id];
                Assert.Equal((node.Type, node.X, node.Y), (replayed.Type, replayed.X, replayed.Y));
                Assert.Equal(node.Values.Keys.Order(), replayed.Values.Keys.Order());
            }

            Assert.True(held.Edges.SetEquals(now.Edges));
            Assert.Equal(now.Revision, edits[^1].Revision);
        }
    }

    [Fact]
    public async Task A_second_client_on_the_socket_sees_the_first_clients_edits_and_a_late_one_catches_up() {
        var (client, state) = await OpenAsync();
        using (client) {
            var sockets = server.Server.CreateWebSocketClient();
            sockets.ConfigureRequest =
                static request => request.Headers.Authorization = $"Bearer {ServerFixture.AccessToken}";
            var address = new Uri(server.Server.BaseAddress, $"ws/graphs/{state.Id}?since={state.Sequence}");
            using var socket = await sockets.ConnectAsync(new UriBuilder(address) { Scheme = "ws" }.Uri, Token);

            await client.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.5f]));
            await client.ActAsync(state.Id, "undo", Token);

            var edited = await ReceiveAsync(socket);
            Assert.Equal((state.Sequence + 1, GraphEventKind.Edited), (edited.Sequence, edited.Kind));
            Assert.Equal(2, Assert.Single(edited.Delta!.Nodes).Id);
            var undone = await ReceiveAsync(socket);
            Assert.Equal((state.Sequence + 2, GraphEventKind.Undone), (undone.Sequence, undone.Kind));

            // A client that comes back later says where it got to, and misses nothing.
            using var late = await sockets.ConnectAsync(new UriBuilder(address) { Scheme = "ws" }.Uri, Token);
            Assert.Equal(edited.Sequence, (await ReceiveAsync(late)).Sequence);
            Assert.Equal(undone.Sequence, (await ReceiveAsync(late)).Sequence);
        }
    }

    [Fact]
    public async Task A_client_that_asks_for_events_nobody_holds_is_told_to_read_the_state() {
        var (client, state) = await OpenAsync();
        using (client) {
            var page = await client.EventsAsync(state.Id, state.Sequence + 50, Token);
            Assert.Equal(GraphEventKind.Resync, Assert.Single(page.Events).Kind);
        }
    }

    [Fact]
    public async Task A_saved_file_reopens_as_the_same_document_with_the_same_bytes() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Colour/Invert", 10, 20, -1),
                new GraphConnect(1, "Out", -1, "Input"),
                new GraphSetValue(2, "Hue", [0.125f])
            );
            var saved = await client.ActAsync(state.Id, "save", Token);
            Assert.False(saved.History.Dirty);
            var edited = await client.StateAsync(state.Id, Token);
            var bytes = await File.ReadAllBytesAsync(state.Path, Token);

            using var closed = await client.DeleteAsync($"/api/v1/graphs/{state.Id}", Token);
            Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);
            using var gone = await client.GetAsync($"/api/v1/graphs/{state.Id}", Token);
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

            var reopened = await client.OpenAsync(state.Path, Token);
            Assert.NotEqual(state.Id, reopened.Id);
            AssertSameDocument(edited, reopened);
            Assert.Empty(reopened.Repairs);
            await client.ActAsync(reopened.Id, "save", Token);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(state.Path, Token));
        }
    }

    [Fact]
    public async Task Closing_with_unsaved_edits_needs_discard_and_leaves_the_file() {
        var (client, state) = await OpenAsync();
        using (client) {
            var before = await File.ReadAllTextAsync(state.Path, Token);
            await client.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.5f]));

            using var refused = await client.DeleteAsync($"/api/v1/graphs/{state.Id}", Token);
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            using var discarded = await client.DeleteAsync($"/api/v1/graphs/{state.Id}?discard=true", Token);
            Assert.Equal(HttpStatusCode.NoContent, discarded.StatusCode);
            Assert.Equal(before, await File.ReadAllTextAsync(state.Path, Token));
        }
    }

    [Fact]
    public async Task A_file_changed_on_disk_refuses_a_save_unless_forced() {
        var (client, state) = await OpenAsync();
        using (client) {
            await File.AppendAllTextAsync(state.Path, "# someone else\n", Token);
            using var refused = await client.PostAsync($"/api/v1/graphs/{state.Id}/save", null, Token);
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

            using var forced = await client.PostAsync($"/api/v1/graphs/{state.Id}/save?force=true", null, Token);
            Assert.Equal(HttpStatusCode.OK, forced.StatusCode);
            Assert.DoesNotContain(
                "someone else",
                await File.ReadAllTextAsync(state.Path, Token),
                StringComparison.Ordinal
            );
        }
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("not-a-hash")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task A_preview_that_is_not_in_the_cache_is_a_404(string hash) {
        using var client = server.AuthorisedClient();
        using var response = await client.GetAsync($"/api/v1/previews/{Uri.EscapeDataString(hash)}", Token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_graph_api_needs_the_token() {
        using var client = server.CreateClient();
        using var response = await client.GetAsync("/api/v1/graphs", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    static void AssertSameDocument(GraphState expected, GraphState actual) {
        Assert.Equal(Project(expected), Project(actual));
        Assert.Equal(expected.Edges.ToHashSet(), actual.Edges.ToHashSet());
        Assert.Equal(expected.Settings.OrderBy(static s => s.Key), actual.Settings.OrderBy(static s => s.Key));
    }

    static string[] Project(GraphState state) => [
        ..state.Nodes.Select(static node =>
            $"{node.Id} {node.Type} {node.X} {node.Y} "
            + string.Join(
                ",",
                node.Values.OrderBy(static v => v.Key).Select(static v => $"{v.Key}={string.Join('/', v.Value)}")
            )
            + " "
            + string.Join(",", node.Texts.OrderBy(static t => t.Key).Select(static t => $"{t.Key}={t.Value}"))
        )
    ];

    async Task<GraphEvent> ReceiveAsync(WebSocket socket) {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(Token);
        patience.CancelAfter(TimeSpan.FromSeconds(30));
        var buffer = new byte[256 * 1024];
        var length = 0;
        while (true) {
            var part = await socket.ReceiveAsync(buffer.AsMemory(length), patience.Token);
            length += part.Count;
            if (part.EndOfMessage) {
                return JsonSerializer.Deserialize(buffer.AsSpan(0, length), ContractsJsonContext.Default.GraphEvent)!;
            }
        }
    }
}
