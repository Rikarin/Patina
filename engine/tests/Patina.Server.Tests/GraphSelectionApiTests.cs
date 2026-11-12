using Patina.Contracts;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>
///     A graph session's selection, and the viewport following it, through the API with no Worker (#244): what any
///     client can ask for, so none has to rebuild it.
/// </summary>
public sealed class GraphSelectionApiTests : IDisposable {
    readonly ServerFixture server = new();

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => server.Dispose();

    async Task<(HttpClient Client, GraphState State)> OpenAsync() {
        var client = server.AuthorisedClient();
        return (client, await client.OpenAsync(GraphTesting.Write(server.Library, GraphTesting.Sample()), Token));
    }

    async Task<GraphSelection> SelectAsync(HttpClient client, string id, GraphSelection selection) {
        using var response = await client.PutAsJsonAsync(
            $"/api/v1/graphs/{id}/selection",
            selection,
            ContractsJsonContext.Default.GraphSelection,
            Token
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphSelection, Token))!;
    }

    async Task<HttpResponseMessage> FollowAsync(HttpClient client, ViewportFollow follow) =>
        await client.PutAsJsonAsync(
            "/api/v1/viewport/follow",
            follow,
            ContractsJsonContext.Default.ViewportFollow,
            Token
        );

    async Task<ViewportState> ViewportAsync(HttpClient client) =>
        (await client.GetFromJsonAsync("/api/v1/viewport", ContractsJsonContext.Default.ViewportState, Token))!;

    [Fact]
    public async Task A_selection_is_the_session_s_every_client_hears_it_and_it_is_no_edit() {
        var (client, state) = await OpenAsync();
        using (client) {
            Assert.Empty(state.Selection.Nodes);

            // Nodes the graph does not hold are left out, and so is a second mention.
            var chosen = await SelectAsync(client, state.Id, new([3, 99, 3, 1], 42));
            Assert.Equal([3, 1], chosen.Nodes);
            Assert.Null(chosen.Pinned);

            var held = await client.StateAsync(state.Id, Token);
            Assert.Equal([3, 1], held.Selection.Nodes);
            Assert.Equal(state.Revision, held.Revision);
            Assert.False(held.History.CanUndo);

            var heard = Assert.Single((await client.EventsAsync(state.Id, state.Sequence, Token)).Events);
            Assert.Equal(GraphEventKind.Selected, heard.Kind);
            Assert.Equal([3, 1], heard.Selection?.Nodes);

            // The same choice again tells nobody anything.
            await SelectAsync(client, state.Id, new([3, 1]));
            Assert.Equal(heard.Sequence, (await client.StateAsync(state.Id, Token)).Sequence);

            var pinned = await SelectAsync(client, state.Id, new([1], 2));
            Assert.Equal(2, pinned.Pinned);

            // A node taken out of the graph leaves the selection.
            await client.EditAsync(state.Id, Token, new GraphRemoveNodes([2, 3]));
            var after = (await client.StateAsync(state.Id, Token)).Selection;
            Assert.Equal([1], after.Nodes);
            Assert.Null(after.Pinned);

            using var nobody = await client.PutAsJsonAsync(
                "/api/v1/graphs/nothing/selection",
                new GraphSelection([1]),
                ContractsJsonContext.Default.GraphSelection,
                Token
            );
            Assert.Equal(HttpStatusCode.NotFound, nobody.StatusCode);
        }
    }

    [Fact]
    public async Task The_viewport_follows_a_graph_until_something_else_is_laid_or_the_graph_closes() {
        var (client, state) = await OpenAsync();
        using (client) {
            using (var nobody = await FollowAsync(client, new("nothing"))) {
                Assert.Equal(HttpStatusCode.NotFound, nobody.StatusCode);
            }

            using (var followed = await FollowAsync(client, new(state.Id, true))) {
                followed.EnsureSuccessStatusCode();
                var shown = await followed.Content.ReadFromJsonAsync(
                    ContractsJsonContext.Default.ViewportState,
                    Token
                );
                Assert.Equal(new(state.Id, true), shown!.Following);
            }

            // Pictures laid by hand take the graph's place.
            using (var laid = await client.PutAsJsonAsync(
                       "/api/v1/viewport/maps",
                       new ViewportMapPaths(),
                       ContractsJsonContext.Default.ViewportMapPaths,
                       Token
                   )) {
                laid.EnsureSuccessStatusCode();
            }

            Assert.Null((await ViewportAsync(client)).Following);

            using (var again = await FollowAsync(client, new(state.Id))) {
                again.EnsureSuccessStatusCode();
            }

            using (var stopped = await FollowAsync(client, new(null))) {
                stopped.EnsureSuccessStatusCode();
            }

            Assert.Null((await ViewportAsync(client)).Following);

            // A graph that closes is let go by the follower.
            using (var again = await FollowAsync(client, new(state.Id))) {
                again.EnsureSuccessStatusCode();
            }

            using (var closed = await client.DeleteAsync($"/api/v1/graphs/{state.Id}?discard=true", Token)) {
                closed.EnsureSuccessStatusCode();
            }

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while ((await ViewportAsync(client)).Following is not null) {
                Assert.True(DateTime.UtcNow < deadline, "The viewport still follows a closed graph.");
                await Task.Delay(20, Token);
            }
        }
    }
}
