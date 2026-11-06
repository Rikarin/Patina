using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Server.Graphs;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     Layer stacks: open a file, edit it in undoable batches, paint it with lists of stamps, follow the numbered
///     events.
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

        void Post(string route, Delegate handler, string name, string summary, params int[] problems) =>
            stacks.MapPost(route, handler).Describe(name, summary, problems);

        void Get(string route, Delegate handler, string name, string summary, params int[] problems) =>
            stacks.MapGet(route, handler).Describe(name, summary, problems);

        Get("/", GetAll, "GetStacks", "The open stacks.");
        Get(
            "/vocabulary",
            static () => TypedResults.Ok(StackDocuments.Vocabulary),
            "GetStackVocabulary",
            "The blend modes, filters, mask sources, generators, mesh maps and brush alphas a stack can name."
        );
        Post("/", Open, "OpenStack", "Opens a layer stack, or joins the session that has it open.", Missing, Refused);
        Post(
            "/new",
            Create,
            "CreateStack",
            "Writes a new stack with a base and a paint layer, and opens it.",
            Conflict,
            Refused
        );
        Get("/{id}", GetState, "GetStack", "The whole stack, its history and the newest maps.", Missing);
        stacks.MapDelete("/{id}", Close)
            .Describe(
                "CloseStack",
                "Ends a session. Unsaved edits refuse this unless discard is set.",
                Missing,
                Conflict
            );
        Post(
            "/{id}/commands",
            Apply,
            "ApplyStackCommands",
            "Applies a batch whole or not at all, as one undo step. A stroke is a list of stamps, never pixels.",
            Missing,
            Refused
        );
        Post(
            "/{id}/undo",
            Undo,
            "UndoStack",
            "Takes back the newest batch, whichever client sent it.",
            Missing,
            Conflict
        );
        Post("/{id}/redo", Redo, "RedoStack", "Puts back the newest undone batch.", Missing, Conflict);
        Post(
            "/{id}/save",
            Save,
            "SaveStack",
            "Writes the pixels and then the stack. A file changed on disk refuses this unless force is set.",
            Missing,
            Conflict,
            Unavailable
        );
        stacks.MapPut("/{id}/mesh-maps", UseMeshMaps)
            .Describe("SetStackMeshMaps", "Chooses the bake its generators and bake masks read.", Missing);
        Post(
            "/{id}/evaluate",
            Evaluate,
            "EvaluateStack",
            "Composites again, which also rewrites maps a deleted cache has lost.",
            Missing
        );
        Get(
            "/{id}/id-colours",
            GetIdColours,
            "GetStackIdColours",
            "The colours of the id map the stack reads, the largest first, and the one at a place on it if asked.",
            Missing
        );
        Get(
            "/{id}/pixels",
            GetPixels,
            "GetStackPixels",
            "A digest of each map as it stands, the same one an export reports, with no pixels in the reply.",
            Missing,
            Unavailable
        );
        Post(
            "/{id}/export",
            Export,
            "ExportStack",
            "Evaluates from nothing and writes each map to a file, and says where and what they hold.",
            Missing,
            Refused,
            Unavailable
        );
        Get(
            "/{id}/events",
            GetEvents,
            "GetStackEvents",
            "The events after a number. /ws/stacks/{id}?since= sends the same events as they happen.",
            Missing
        );
    }

    /// <summary>Text messages, one <see cref="StackEvent" /> each. Its schema comes from <c>/events</c>.</summary>
    public static void MapStackSocket(this IEndpointRouteBuilder app) =>
        app.Map("/ws/stacks/{id}", FollowAsync).ExcludeFromDescription();

    static Ok<IReadOnlyList<StackSummary>> GetAll(StackSessions sessions) =>
        TypedResults.Ok<IReadOnlyList<StackSummary>>([..sessions.All.Select(static s => s.Summary)]);

    static Results<Ok<T>, ProblemHttpResult> Refusable<T>(Func<T> act) {
        try {
            return TypedResults.Ok(act());
        } catch (FileNotFoundException e) {
            return TypedResults.Problem(e.Message, statusCode: Missing);
        } catch (StackEditRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        }
    }

    /// <summary>A step in a session's history, which has to exist and has to have something to do.</summary>
    static Results<Ok<StackEditResult>, ProblemHttpResult> Step(
        string id,
        StackSessions sessions,
        Func<StackSession, StackEditResult?> act,
        string what
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        return Refusable(() => act(session)) switch {
            { Result: Ok<StackEditResult?> { Value: { } done } } => TypedResults.Ok(done),
            { Result: ProblemHttpResult problem } => problem,
            _ => TypedResults.Problem($"There is nothing to {what}.", statusCode: Conflict)
        };
    }

    static Results<Ok<StackState>, ProblemHttpResult> Open(StackOpenRequest request, StackSessions sessions) =>
        Refusable(() => sessions.Open(request.Path).State);

    static Results<Ok<StackState>, ProblemHttpResult> Create(StackCreateRequest request, StackSessions sessions) {
        var known = Refusable(() => sessions.Resolve(request.Path));
        if (known.Result is not Ok<string> { Value: { } path }) {
            return (ProblemHttpResult)known.Result;
        }

        if (File.Exists(path)) {
            return TypedResults.Problem($"'{path}' already exists.", statusCode: Conflict);
        }

        if (request.Width <= 0 || request.Height <= 0) {
            return TypedResults.Problem("A stack has a size.", statusCode: Refused);
        }

        StackDocuments.Create(path, request.Width, request.Height);
        return TypedResults.Ok(sessions.Open(path).State);
    }

    static Results<Ok<StackState>, ProblemHttpResult> GetState(string id, StackSessions sessions) =>
        sessions.Find(id) is { } session ? TypedResults.Ok(session.State) : NoSession(id);

    static Results<NoContent, ProblemHttpResult> Close(string id, StackSessions sessions, bool discard = false) {
        var session = sessions.Find(id);
        if (session?.Summary.Dirty == true && !discard) {
            return TypedResults.Problem(
                "This stack has unsaved edits. Save it, or close it with discard=true.",
                statusCode: Conflict
            );
        }

        if (session is null) {
            return NoSession(id);
        }

        sessions.Close(session);
        return TypedResults.NoContent();
    }

    static Results<Ok<StackEditResult>, ProblemHttpResult> Apply(
        string id,
        StackCommandBatch batch,
        StackSessions sessions
    ) =>
        Step(id, sessions, session => session.Apply(batch), "apply");

    static Results<Ok<StackEditResult>, ProblemHttpResult> Undo(string id, StackSessions sessions) =>
        Step(id, sessions, static session => session.Undo(), "undo");

    static Results<Ok<StackEditResult>, ProblemHttpResult> Redo(string id, StackSessions sessions) =>
        Step(id, sessions, static session => session.Redo(), "redo");

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

    static Results<Ok<StackIdColours>, ProblemHttpResult> GetIdColours(
        string id,
        StackSessions sessions,
        float? u = null,
        float? v = null
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        return session.MeshMaps is { } folder && IdColours.Read(folder, u, v) is { } colours
            ? TypedResults.Ok(colours)
            : TypedResults.Problem("This stack has no bake with an id map.", statusCode: StatusCodes.Status404NotFound);
    }

    static Results<NoContent, ProblemHttpResult> UseMeshMaps(
        string id,
        StackMeshMapsRequest request,
        StackSessions sessions
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        session.UseMeshMaps(request.Folder is { } folder ? Path.GetFullPath(folder) : null);
        return TypedResults.NoContent();
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
        bool clean = false,
        bool sampled = false
    ) =>
        ReadAsync(id, sessions, evaluator, clean, null, cancellationToken, sampled);

    static async Task<Results<Ok<IReadOnlyList<StackPixels>>, ProblemHttpResult>> Export(
        string id,
        StackExportRequest request,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken
    ) {
        try {
            return await ReadAsync(id, sessions, evaluator, true, sessions.Resolve(request.Folder), cancellationToken);
        } catch (StackEditRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        }
    }

    static async Task<Results<Ok<IReadOnlyList<StackPixels>>, ProblemHttpResult>> ReadAsync(
        string id,
        StackSessions sessions,
        StackEvaluator evaluator,
        bool clean,
        string? folder,
        CancellationToken cancellationToken,
        bool sampled = false
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            return TypedResults.Ok(await evaluator.PixelsAsync(session, clean, folder, cancellationToken, sampled));
        } catch (TimeoutException e) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        } catch (IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        }
    }

    static Results<Ok<StackEventPage>, ProblemHttpResult> GetEvents(
        string id,
        StackSessions sessions,
        long since = 0
    ) =>
        sessions.Find(id) is { } session ? TypedResults.Ok(session.EventsAfter(since)) : NoSession(id);

    static Task FollowAsync(string id, HttpContext context, StackSessions sessions, long? since = null) =>
        ApiRoutes.FollowAsync(
            context,
            sessions.Find(id),
            ContractsJsonContext.Default.StackEvent,
            static item => item.Kind == GraphEventKind.Closed,
            static item => item.Sequence,
            since
        );

    static ProblemHttpResult NoSession(string id) =>
        TypedResults.Problem($"There is no open stack '{id}'.", statusCode: Missing);
}
