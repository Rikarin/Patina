using Patina.Contracts;
using Patina.Ipc;
using Patina.Server.Graphs;

namespace Patina.Server.Viewport;

/// <summary>
///     Keeps the graph the viewport follows on the mesh (#244). It reads the graph session's events as any client would,
///     and lays the graph on the mesh again after each evaluation, and after each change of selection when it shows the
///     node the selection views. Any client can ask for this, so none has to rebuild it (plan §8). It holds nothing the
///     Server does not: the following is the viewport's, and a show the following has moved past is dropped.
/// </summary>
public sealed class GraphFollower(
    ViewportSession viewport,
    GraphSessions graphs,
    GraphEvaluator evaluator,
    PreviewStore store,
    ServerIdentity identity,
    ILogger<GraphFollower> logger
) : BackgroundService {
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ServiceLoop.RunAsync(logger, nameof(GraphFollower), LoopAsync, stoppingToken);

    async Task LoopAsync(CancellationToken stoppingToken) {
        while (true) {
            var (following, number) = viewport.Following;
            if (following?.Graph is not { } id) {
                await viewport.FollowChangedAsync(number, stoppingToken);
                continue;
            }

            if (graphs.Find(id) is not { } graph) {
                viewport.Unfollow(number);
                continue;
            }

            await FollowAsync(graph, following.Node, number, stoppingToken);
        }
    }

    /// <summary>Follows one graph until the following changes or the graph closes.</summary>
    async Task FollowAsync(GraphSession graph, bool node, long number, CancellationToken stoppingToken) {
        var changed = viewport.FollowChangedAsync(number, stoppingToken);
        var seen = graph.Sequence;
        // What a graph that has evaluated already shows, without waiting for its next edit.
        int? shownNode = null;
        if (graph.State.Evaluation is not null) {
            shownNode = await ShowAsync(graph, node, number, stoppingToken);
        }

        while (!changed.IsCompleted) {
            var next = graph.WaitAsync(seen, stoppingToken);
            if (await Task.WhenAny(next, changed) == changed) {
                break;
            }

            await next;
            var events = graph.After(seen);
            if (events.Count == 0) {
                continue;
            }

            seen = events[^1].Sequence;
            if (events.Any(static e => e.Kind == GraphEventKind.Closed)) {
                viewport.Unfollow(number);
                return;
            }

            var evaluated = events.Any(static e =>
                e is { Kind: GraphEventKind.Evaluated, Evaluation.Error: null } or { Kind: GraphEventKind.Resync }
            );
            var moved = node && events.Any(static e => e.Kind == GraphEventKind.Selected) && graph.Viewed != shownNode;
            if (evaluated || moved) {
                shownNode = await ShowAsync(graph, node, number, stoppingToken);
            }
        }
    }

    /// <summary>
    ///     Lays the graph on the mesh as it stands: the node its selection views alone, or its Output nodes. A graph with
    ///     nothing to show, or a Worker that cannot show it now, leaves the mesh as it is until the next evaluation.
    /// </summary>
    /// <returns>The node shown alone, or null.</returns>
    async Task<int?> ShowAsync(GraphSession graph, bool node, long number, CancellationToken stoppingToken) {
        try {
            var viewed = node ? graph.Viewed : null;
            var port = viewed is { } chosen ? GraphOnMesh.PortOf(graph, chosen, null) : null;
            var shown = viewed is { } alone && port is not null
                ? await GraphOnMesh.NodeAsync(graph, alone, port, evaluator, store.Root, stoppingToken)
                : await GraphOnMesh.OutputsAsync(graph, evaluator, store.Root, stoppingToken);
            if (shown is null) {
                return null;
            }

            var standIn = viewport.State is { Mesh: null, Display.Shape: var shape }
                ? PreviewShape.File(identity.Library, shape)
                : null;
            viewport.ShowFollowed(number, standIn, shown);
            return shown.Node?.Shown.Node;
        } catch (Exception e) when (e is IpcException or IOException or TimeoutException) {
            // The Worker went, or no longer holds this revision: the next evaluation shows it.
            ServerLog.ViewportFailed(logger, e.Message);
            return null;
        }
    }
}
