using Patina.Contracts;
using System.Net;
using Vixen.Core.Yaml;
using Vixen.NodeGraph;

namespace Patina.Server.Tests;

/// <summary>A big graph stays quick to set up: its evaluation pauses as a stack's does (#306).</summary>
public sealed partial class GraphProcessTests {
    /// <summary>
    ///     A paused graph takes edits and saves them without evaluating them, every client hears it paused, and a
    ///     resume evaluates every edit at once.
    /// </summary>
    [Fact]
    public async Task A_paused_graph_takes_and_saves_edits_and_evaluates_them_when_resumed() {
        using var deadline = Deadline(120);
        var (_, state, socket, first) = await OpenSampleAsync(GraphTesting.Sample(), deadline.Token);
        using (socket) {
            Assert.True(await PaceAsync(state.Id, true, deadline.Token));
            var edited = await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(5, "Radius", [6f]));

            // Nothing is evaluated while it is paused, however long the edit waits.
            await Task.Delay(TimeSpan.FromSeconds(1), deadline.Token);
            var paused = await http.StateAsync(state.Id, deadline.Token);
            Assert.True(paused.Paused);
            Assert.True(paused.Settled < edited.Revision, $"Settled at {paused.Settled} of {edited.Revision}.");
            Assert.Equal(first.Previews.Count, paused.Previews.Count);
            var events = await http.EventsAsync(state.Id, state.Sequence, deadline.Token);
            Assert.Contains(events.Events, static e => e.Kind == GraphEventKind.Paced);

            // A save is never a pause's: the edit reaches the file.
            await http.ActAsync(state.Id, "save", deadline.Token);
            var saved = YamlSerializer.Parse<NodeGraphAsset>(
                await File.ReadAllTextAsync(Path.Combine(Library, "sample.vxtexgraph"), deadline.Token)
            );
            Assert.Equal([6f], saved.Nodes.Single(static n => n.Id == 5).Values["Radius"]);

            Assert.False(await PaceAsync(state.Id, false, deadline.Token));
            var resumed = await EvaluatedAsync(socket, edited.Revision, deadline.Token);
            Assert.Contains(resumed.Previews, static p => p.Node == 5);
            Assert.False((await http.StateAsync(state.Id, deadline.Token)).Paused);
        }

        using var missing = await http.PutAsJsonAsync(
            "/api/v1/graphs/nothing/pace",
            new GraphPace(true),
            ContractsJsonContext.Default.GraphPace,
            deadline.Token
        );
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    async Task<bool> PaceAsync(string id, bool paused, CancellationToken token) {
        using var response = await http.PutAsJsonAsync(
            $"/api/v1/graphs/{id}/pace",
            new GraphPace(paused),
            ContractsJsonContext.Default.GraphPace,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphPace, token))!.Paused;
    }
}
