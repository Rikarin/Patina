using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Server.Graphs;
using System.Net.WebSockets;
using System.Text.Json;

namespace Patina.Server.Api;

/// <summary>
///     Texture graphs: open a file, edit it in undoable batches, follow the numbered events, read the previews. Any
///     client can do all of it (plan §8). Pixels are never in these replies: an event carries hashes, and
///     <c>/previews/{hash}</c> serves the files.
/// </summary>
static class GraphEndpoints {
    const string Tag = "Graphs";

    public static void MapGraphApi(this IEndpointRouteBuilder api) {
        var graphs = api.MapGroup("/graphs").WithTags(Tag);
        const int Missing = StatusCodes.Status404NotFound;
        const int Refused = StatusCodes.Status422UnprocessableEntity;
        const int Conflict = StatusCodes.Status409Conflict;
        const int Unavailable = StatusCodes.Status503ServiceUnavailable;

        graphs.MapGet("/", GetAll).Describe("GetGraphs", "The open graphs.");
        graphs.MapPost("/", Open)
            .Describe(
                "OpenGraph",
                "Opens a graph file, or joins the session that has it open, and starts evaluating it.",
                Missing,
                Refused
            );
        graphs.MapGet("/{id}", GetState)
            .Describe("GetGraph", "The whole document, its undo history and the newest previews.", Missing);
        graphs.MapDelete("/{id}", Close)
            .Describe(
                "CloseGraph",
                "Ends a session. Unsaved edits refuse this unless discard is set.",
                Missing,
                Conflict
            );
        graphs.MapGet("/{id}/node-types", GetNodeTypes)
            .Describe(
                "GetGraphNodeTypes",
                "Every node this graph can use, with ports and settings, for a palette.",
                Missing
            );
        graphs.MapGet("/{id}/compounds", GetCompounds)
            .Describe(
                "GetGraphCompounds",
                "The compounds this graph can hold, and the path that opens the graph behind each.",
                Missing
            );
        graphs.MapPost("/{id}/commands", Apply)
            .Describe(
                "ApplyGraphCommands",
                "Applies a batch of edits, whole or not at all, as one undo step.",
                Missing,
                Refused
            );
        graphs.MapPost("/{id}/undo", Undo)
            .Describe("UndoGraph", "Takes back the newest batch, whichever client sent it.", Missing, Conflict);
        graphs.MapPost("/{id}/redo", Redo)
            .Describe("RedoGraph", "Puts back the newest undone batch.", Missing, Conflict);
        graphs.MapPost("/{id}/save", Save)
            .Describe(
                "SaveGraph",
                "Writes the document to its file. A file changed on disk refuses this unless force is set.",
                Missing,
                Conflict,
                Refused
            );
        graphs.MapPost("/{id}/evaluate", Evaluate)
            .Describe(
                "EvaluateGraph",
                "Evaluates again, which also rewrites previews a deleted cache has lost.",
                Missing
            );
        graphs.MapPost("/{id}/images", WriteImage)
            .Describe(
                "WriteGraphImage",
                "Writes a node's picture at the size the graph computes it. /previews/{hash} serves the PNG.",
                Missing,
                Conflict,
                Unavailable
            );
        graphs.MapGet("/{id}/events", GetEvents)
            .Describe(
                "GetGraphEvents",
                "The events after a number. /ws/graphs/{id}?since= sends the same events as they happen.",
                Missing
            );
        api.MapGet("/previews/{hash}", GetPreview)
            .WithTags(Tag)
            .Produces<byte[]>(StatusCodes.Status200OK, "image/png")
            .Describe(
                "GetPreview",
                "A node's preview as a PNG, by the hash an event carried. It never changes, so cache it for ever.",
                Missing
            );
    }

    /// <summary>Text messages, one <see cref="GraphEvent" /> each. Its schema comes from <c>/events</c>.</summary>
    public static void MapGraphSocket(this IEndpointRouteBuilder app) =>
        app.Map("/ws/graphs/{id}", FollowAsync).ExcludeFromDescription();

    static void Describe(this RouteHandlerBuilder route, string name, string summary, params int[] problems) {
        route.WithName(name).WithSummary(summary);
        foreach (var problem in problems) {
            route.ProducesProblem(problem);
        }
    }

    static Ok<IReadOnlyList<GraphSummary>> GetAll(GraphSessions sessions) =>
        TypedResults.Ok<IReadOnlyList<GraphSummary>>([..sessions.All.Select(static s => s.Summary)]);

    static Results<Ok<GraphState>, ProblemHttpResult> Open(GraphOpenRequest request, GraphSessions sessions) {
        try {
            return TypedResults.Ok(sessions.Open(request.Path).State);
        } catch (FileNotFoundException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status404NotFound);
        } catch (GraphEditRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    static Results<Ok<GraphState>, ProblemHttpResult> GetState(string id, GraphSessions sessions) =>
        sessions.Find(id) is { } session ? TypedResults.Ok(session.State) : NoSession(id);

    static Results<NoContent, ProblemHttpResult> Close(string id, GraphSessions sessions, bool discard = false) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        if (session.Summary.Dirty && !discard) {
            return TypedResults.Problem(
                "This graph has unsaved edits. Save it, or close it with discard=true.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        sessions.Close(session);
        return TypedResults.NoContent();
    }

    static Results<Ok<IReadOnlyList<GraphNodeType>>, ProblemHttpResult> GetNodeTypes(
        string id,
        GraphSessions sessions
    ) =>
        sessions.Find(id) is { } session
            ? TypedResults.Ok<IReadOnlyList<GraphNodeType>>(
                [
                    ..session.Registry.Types.OrderBy(static type => type.Path, StringComparer.Ordinal)
                        .Select(GraphDocuments.Definition)
                ]
            )
            : NoSession(id);

    static Results<Ok<IReadOnlyList<GraphCompound>>, ProblemHttpResult> GetCompounds(
        string id,
        GraphSessions sessions
    ) =>
        sessions.Find(id) is { } session
            ? TypedResults.Ok(GraphSessions.Compounds(session.Folder))
            : NoSession(id);

    static Results<Ok<GraphEditResult>, ProblemHttpResult> Apply(
        string id,
        GraphCommandBatch batch,
        GraphSessions sessions
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            return TypedResults.Ok(session.Apply(batch));
        } catch (GraphEditRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    static Results<Ok<GraphEditResult>, ProblemHttpResult> Undo(string id, GraphSessions sessions) =>
        Walk(id, sessions, static session => session.Undo(), "undo");

    static Results<Ok<GraphEditResult>, ProblemHttpResult> Redo(string id, GraphSessions sessions) =>
        Walk(id, sessions, static session => session.Redo(), "redo");

    static Results<Ok<GraphEditResult>, ProblemHttpResult> Walk(
        string id,
        GraphSessions sessions,
        Func<GraphSession, GraphEditResult?> walk,
        string name
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        return walk(session) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.Problem($"There is nothing to {name}.", statusCode: StatusCodes.Status409Conflict);
    }

    static Results<Ok<GraphEditResult>, ProblemHttpResult> Save(string id, GraphSessions sessions, bool force = false) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            var result = session.Save(force);
            sessions.Republish(session);
            return TypedResults.Ok(result);
        } catch (GraphFileChangedException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status409Conflict);
        } catch (GraphEditRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    static async Task<Results<Ok<GraphImage>, ProblemHttpResult>> WriteImage(
        string id,
        GraphImageRequest request,
        GraphSessions sessions,
        GraphEvaluator evaluator,
        CancellationToken cancellationToken
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            var image = await evaluator.ImageAsync(session, request.Node, request.Port, cancellationToken);
            return TypedResults.Ok(new GraphImage(image.Hash, image.Width, image.Height));
        } catch (IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status409Conflict);
        } catch (Exception e) when (e is IOException or TimeoutException) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    static Results<Accepted, ProblemHttpResult> Evaluate(string id, GraphSessions sessions) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        session.Evaluate();
        return TypedResults.Accepted((string?)null);
    }

    static Results<Ok<GraphEventPage>, ProblemHttpResult> GetEvents(
        string id,
        GraphSessions sessions,
        long since = 0
    ) =>
        sessions.Find(id) is { } session ? TypedResults.Ok(session.EventsAfter(since)) : NoSession(id);

    static Results<PhysicalFileHttpResult, ProblemHttpResult> GetPreview(
        string hash,
        PreviewStore store,
        HttpContext context
    ) {
        if (store.Find(hash) is not { } path) {
            return TypedResults.Problem(
                $"There is no preview '{hash}'. Evaluating the graph again writes it.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        return TypedResults.PhysicalFile(path, "image/png");
    }

    static async Task FollowAsync(string id, HttpContext context, GraphSessions sessions, long? since = null) {
        if (!context.WebSockets.IsWebSocketRequest) {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (sessions.Find(id) is not { } session) {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var listening = WatchForCloseAsync(socket, stop);
        try {
            await SendEventsAsync(socket, session, since ?? session.State.Sequence, stop.Token);
            if (socket.State == WebSocketState.Open) {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, context.RequestAborted);
            }
        } catch (WebSocketException) {
            // The client went away without a goodbye.
        } catch (OperationCanceledException) {
            // The client left, or the Server is stopping.
        } finally {
            await stop.CancelAsync();
            await listening;
        }
    }

    static async Task SendEventsAsync(
        WebSocket socket,
        GraphSession session,
        long since,
        CancellationToken cancellationToken
    ) {
        var cursor = since;
        while (true) {
            foreach (var item in session.EventsAfter(cursor).Events) {
                var message = JsonSerializer.SerializeToUtf8Bytes(item, ContractsJsonContext.Default.GraphEvent);
                await socket.SendAsync(message, WebSocketMessageType.Text, true, cancellationToken);
                cursor = item.Sequence;
                if (item.Kind == GraphEventKind.Closed) {
                    return;
                }
            }

            await session.WaitAsync(cursor, cancellationToken);
        }
    }

    /// <summary>Nothing a client sends here means anything; reading is how a socket learns it has closed.</summary>
    static async Task WatchForCloseAsync(WebSocket socket, CancellationTokenSource stop) {
        var buffer = new byte[256];
        try {
            while (socket.State == WebSocketState.Open) {
                var received = await socket.ReceiveAsync(buffer, stop.Token);
                if (received.MessageType == WebSocketMessageType.Close) {
                    break;
                }
            }
        } catch (Exception e) when (e is WebSocketException or OperationCanceledException) {
            // Over either way.
        }

        await stop.CancelAsync();
    }

    static ProblemHttpResult NoSession(string id) =>
        TypedResults.Problem($"There is no open graph '{id}'.", statusCode: StatusCodes.Status404NotFound);
}
