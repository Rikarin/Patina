using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Server.Graphs;
using Patina.Server.Stacks;
using System.Net.WebSockets;
using System.Text.Json;

namespace Patina.Server.Api;

/// <summary>
///     Layer stacks: open a file, edit it in undoable batches, paint it with lists of stamps, follow the numbered events.
///     No pixels are in these replies: an event carries hashes, and <c>/previews/{hash}</c> serves the files.
/// </summary>
static class StackEndpoints {
    const string Tag = "Stacks";
    const int Missing = StatusCodes.Status404NotFound;
    const int Refused = StatusCodes.Status422UnprocessableEntity;
    const int Conflict = StatusCodes.Status409Conflict;
    const int Unavailable = StatusCodes.Status503ServiceUnavailable;

    public static void MapStackApi(this IEndpointRouteBuilder api) {
        var stacks = api.MapGroup("/stacks").WithTags(Tag);
        stacks.MapGet("/", GetAll).Describe("GetStacks", "The open stacks.");
        stacks.MapPost("/", Open)
            .Describe("OpenStack", "Opens a layer stack, or joins the session that has it open.", Missing, Refused);
        stacks.MapPost("/new", Create)
            .Describe("CreateStack", "Writes a new stack with a base and a paint layer, and opens it.", Conflict, Refused);
        stacks.MapGet("/{id}", GetState).Describe("GetStack", "The whole stack, its history and the newest maps.", Missing);
        stacks.MapDelete("/{id}", Close)
            .Describe("CloseStack", "Ends a session. Unsaved edits refuse this unless discard is set.", Missing, Conflict);
        stacks.MapPost("/{id}/commands", Apply)
            .Describe(
                "ApplyStackCommands",
                "Applies a batch whole or not at all, as one undo step. A stroke is a list of stamps, never pixels.",
                Missing,
                Refused
            );
        stacks.MapPost("/{id}/undo", Undo)
            .Describe("UndoStack", "Takes back the newest batch, whichever client sent it.", Missing, Conflict);
        stacks.MapPost("/{id}/redo", Redo).Describe("RedoStack", "Puts back the newest undone batch.", Missing, Conflict);
        stacks.MapPost("/{id}/save", Save)
            .Describe(
                "SaveStack",
                "Writes the pixels and then the stack. A file changed on disk refuses this unless force is set.",
                Missing,
                Conflict,
                Unavailable
            );
        stacks.MapPost("/{id}/evaluate", Evaluate)
            .Describe("EvaluateStack", "Composites again, which also rewrites maps a deleted cache has lost.", Missing);
        stacks.MapGet("/{id}/pixels", GetPixels)
            .Describe(
                "GetStackPixels",
                "A digest of each map as it stands, the same one an export reports, with no pixels in the reply.",
                Missing,
                Unavailable
            );
        stacks.MapPost("/{id}/export", Export)
            .Describe(
                "ExportStack",
                "Evaluates from nothing and writes each map to a file, and says where and what they hold.",
                Missing,
                Refused,
                Unavailable
            );
        stacks.MapGet("/{id}/events", GetEvents)
            .Describe(
                "GetStackEvents",
                "The events after a number. /ws/stacks/{id}?since= sends the same events as they happen.",
                Missing
            );
    }

    /// <summary>Text messages, one <see cref="StackEvent" /> each. Its schema comes from <c>/events</c>.</summary>
    public static void MapStackSocket(this IEndpointRouteBuilder app) =>
        app.Map("/ws/stacks/{id}", FollowAsync).ExcludeFromDescription();

    static RouteHandlerBuilder Describe(this RouteHandlerBuilder route, string name, string summary, params int[] problems) {
        route.WithName(name).WithSummary(summary);
        foreach (var problem in problems) {
            route.ProducesProblem(problem);
        }

        return route;
    }

    static Ok<IReadOnlyList<StackSummary>> GetAll(StackSessions sessions) =>
        TypedResults.Ok<IReadOnlyList<StackSummary>>([..sessions.All.Select(static s => s.Summary)]);

    static Results<Ok<StackState>, ProblemHttpResult> Open(StackOpenRequest request, StackSessions sessions) {
        try {
            return TypedResults.Ok(sessions.Open(request.Path).State);
        } catch (FileNotFoundException e) {
            return TypedResults.Problem(e.Message, statusCode: Missing);
        } catch (StackEditRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        }
    }

    static Results<Ok<StackState>, ProblemHttpResult> Create(StackCreateRequest request, StackSessions sessions) {
        try {
            var path = sessions.Resolve(request.Path);
            if (File.Exists(path)) {
                return TypedResults.Problem($"'{path}' already exists.", statusCode: Conflict);
            }

            if (request.Width <= 0 || request.Height <= 0) {
                return TypedResults.Problem("A stack has a size.", statusCode: Refused);
            }

            StackDocuments.Create(path, request.Width, request.Height);
            return TypedResults.Ok(sessions.Open(path).State);
        } catch (StackEditRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        }
    }

    static Results<Ok<StackState>, ProblemHttpResult> GetState(string id, StackSessions sessions) =>
        sessions.Find(id) is { } session ? TypedResults.Ok(session.State) : NoSession(id);

    static Results<NoContent, ProblemHttpResult> Close(string id, StackSessions sessions, bool discard = false) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        if (session.Summary.Dirty && !discard) {
            return TypedResults.Problem(
                "This stack has unsaved edits. Save it, or close it with discard=true.",
                statusCode: Conflict
            );
        }

        sessions.Close(session);
        return TypedResults.NoContent();
    }

    static Results<Ok<StackEditResult>, ProblemHttpResult> Apply(string id, StackCommandBatch batch, StackSessions sessions) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            return TypedResults.Ok(session.Apply(batch));
        } catch (StackEditRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        }
    }

    static Results<Ok<StackEditResult>, ProblemHttpResult> Undo(string id, StackSessions sessions) =>
        Walk(id, sessions, static session => session.Undo(), "undo");

    static Results<Ok<StackEditResult>, ProblemHttpResult> Redo(string id, StackSessions sessions) =>
        Walk(id, sessions, static session => session.Redo(), "redo");

    static Results<Ok<StackEditResult>, ProblemHttpResult> Walk(
        string id,
        StackSessions sessions,
        Func<StackSession, StackEditResult?> walk,
        string name
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        return walk(session) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.Problem($"There is nothing to {name}.", statusCode: Conflict);
    }

    static async Task<Results<Ok<StackEditResult>, ProblemHttpResult>> Save(
        string id,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken,
        bool force = false
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            return TypedResults.Ok(await evaluator.SaveAsync(session, force, cancellationToken));
        } catch (GraphFileChangedException e) {
            return TypedResults.Problem(e.Message, statusCode: Conflict);
        } catch (TimeoutException e) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        }
    }

    static Results<NoContent, ProblemHttpResult> Evaluate(string id, StackSessions sessions) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        session.Evaluate();
        return TypedResults.NoContent();
    }

    static Task<Results<Ok<IReadOnlyList<StackPixels>>, ProblemHttpResult>> GetPixels(
        string id,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken,
        bool clean = false
    ) =>
        ReadAsync(id, sessions, evaluator, clean, null, cancellationToken);

    static Task<Results<Ok<IReadOnlyList<StackPixels>>, ProblemHttpResult>> Export(
        string id,
        StackExportRequest request,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken
    ) =>
        ReadAsync(id, sessions, evaluator, true, sessions.Resolve(request.Folder), cancellationToken);

    static async Task<Results<Ok<IReadOnlyList<StackPixels>>, ProblemHttpResult>> ReadAsync(
        string id,
        StackSessions sessions,
        StackEvaluator evaluator,
        bool clean,
        string? folder,
        CancellationToken cancellationToken
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            return TypedResults.Ok(await evaluator.PixelsAsync(session, clean, folder, cancellationToken));
        } catch (TimeoutException e) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        } catch (IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        }
    }

    static Results<Ok<StackEventPage>, ProblemHttpResult> GetEvents(string id, StackSessions sessions, long since = 0) =>
        sessions.Find(id) is { } session ? TypedResults.Ok(session.EventsAfter(since)) : NoSession(id);

    static async Task FollowAsync(string id, HttpContext context, StackSessions sessions, long? since = null) {
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
        var listening = GraphEndpoints.WatchForCloseAsync(socket, stop);
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
        StackSession session,
        long since,
        CancellationToken cancellationToken
    ) {
        var cursor = since;
        while (true) {
            foreach (var item in session.EventsAfter(cursor).Events) {
                var message = JsonSerializer.SerializeToUtf8Bytes(item, ContractsJsonContext.Default.StackEvent);
                await socket.SendAsync(message, WebSocketMessageType.Text, true, cancellationToken);
                cursor = item.Sequence;
                if (item.Kind == GraphEventKind.Closed) {
                    return;
                }
            }

            await session.WaitAsync(cursor, cancellationToken);
        }
    }

    static ProblemHttpResult NoSession(string id) =>
        TypedResults.Problem($"There is no open stack '{id}'.", statusCode: Missing);
}
