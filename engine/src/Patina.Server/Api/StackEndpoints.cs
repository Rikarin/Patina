using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;
using Patina.Server.Viewport;
using System.Security.Cryptography;
using System.Text;

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
        stacks.MapStackChecks();

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
            "/{id}/clip",
            Clip,
            "CopyStackLayers",
            "Copies layers with their pixels as they stand, for a pasteLayers command in this stack or another.",
            Missing,
            Refused,
            Unavailable
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
        Get(
            "/{id}/history",
            Results<Ok<HistoryList>, ProblemHttpResult>(string id, StackSessions sessions) =>
                sessions.Find(id)?.Steps is { } steps ? TypedResults.Ok(steps) : NoSession(id),
            "GetStackHistory",
            "The undo history as a list, for a client that shows it.",
            Missing
        );
        Post(
            "/{id}/history",
            Results<Ok<StackEditResult>, ProblemHttpResult>(string id, HistoryJump jump, StackSessions sessions) =>
                sessions.Find(id) is { } open
                    ? UndoHistory.Answer(() => open.JumpTo(jump.Step), jump.Step)
                    : NoSession(id),
            "JumpStackHistory",
            "Undoes or redoes until a step is the newest that stands, as one event.",
            Missing,
            Conflict
        );
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
        Post(
            "/{id}/find",
            FindLayer,
            "FindStackLayer",
            "The layer on top at a place on the map, as a click on the mesh or the map finds it. Its answer is a layer "
            + "id: the Worker weighs each layer there and no picture leaves it.",
            Missing,
            Refused,
            Unavailable
        );
        Get(
            "/{id}/thumbnails",
            GetThumbnails,
            "GetStackThumbnails",
            "Each fill drawn alone and small, under /previews. A layer that has not changed is not drawn again.",
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
        Get(
            "/{id}/mesh-parts",
            GetMeshParts,
            "GetStackMeshParts",
            "The named meshes in the stack's texture set, which a layer can be limited to.",
            Missing,
            Refused,
            Unavailable
        );
        Post(
            "/{id}/mesh-mask",
            DrawMeshMask,
            "DrawStackMeshMask",
            "Draws where some named meshes lie in the layout, as a picture for a setLayer command's meshMask.",
            Missing,
            Refused,
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
            return Refusals.Of(e, Refused);
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

    static Results<Ok<StackState>, ProblemHttpResult> Open(
        StackOpenRequest request,
        StackSessions sessions,
        ViewportSession viewport
    ) =>
        Refusable(() => {
                var session = sessions.Open(request.Path);
                viewport.Claim(session.Id);
                return session.State;
            }
        );

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

    static Results<NoContent, ProblemHttpResult> Close(
        string id,
        StackSessions sessions,
        LibraryHost host,
        AutosaveGate autosave,
        bool discard = false
    ) {
        var session = sessions.Find(id);
        if (session?.Summary.Dirty == true && !discard) {
            return Refusals.Problem(
                "This stack has unsaved edits. Save it first, or close it discarding them.",
                Conflict,
                Refusals.Unsaved
            );
        }

        if (session is null) {
            return NoSession(id);
        }

        var thrownAway = session.Summary.Dirty;
        sessions.Close(session);
        if (thrownAway && host.Library is { } library) {
            // Or what was refused would be offered back, by an autosave kept already or still being written.
            autosave.Forget(library.RootPath, session.Path);
        }

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
        } catch (IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        } catch (Exception e) when (e is TimeoutException or IOException) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        }
    }

    static async Task<Results<Ok<StackClip>, ProblemHttpResult>> Clip(
        string id,
        StackClipRequest request,
        StackSessions sessions,
        StackEvaluator evaluator,
        StackClips clips,
        CancellationToken cancellationToken
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            return TypedResults.Ok(await evaluator.CopyAsync(session, request.Layers, clips, cancellationToken));
        } catch (Exception e) when (e is StackEditRefusedException or IpcException) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        } catch (Exception e) when (e is TimeoutException or IOException) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        }
    }

    static Task<Results<Ok<StackFound>, ProblemHttpResult>> FindLayer(
        string id,
        StackFindRequest request,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken
    ) =>
        AskWorkerAsync(
            id,
            sessions,
            async session => new StackFound(await evaluator.FindAsync(session, request.U, request.V, cancellationToken))
        );

    /// <summary>
    ///     A question the Worker answers about an open stack. A stack that would not evaluate is the stack's to say and
    ///     not the Worker's to be missing for, so it is refused; a Worker that is missing or went is unavailable.
    /// </summary>
    internal static async Task<Results<Ok<T>, ProblemHttpResult>> AskWorkerAsync<T>(
        string id,
        StackSessions sessions,
        Func<StackSession, Task<T>> ask
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            return TypedResults.Ok(await ask(session));
        } catch (IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        } catch (TimeoutException e) {
            return Refusals.Of(e, Unavailable);
        } catch (IOException e) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        }
    }

    static async Task<Results<Ok<StackThumbnails>, ProblemHttpResult>> GetThumbnails(
        string id,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            var drawn = await evaluator.ThumbnailsAsync(session, cancellationToken);
            return TypedResults.Ok(
                new StackThumbnails([..drawn.Select(static item => new StackThumbnail(item.Layer, item.Hash))])
            );
        } catch (Exception e) when (e is IpcException or IOException or TimeoutException) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
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

    static async Task<Results<Ok<StackMeshParts>, ProblemHttpResult>> GetMeshParts(
        string id,
        StackSessions sessions,
        WorkerLink worker,
        CancellationToken cancellationToken
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        return await MeshPartsAsync(session, worker, null, cancellationToken);
    }

    static async Task<Results<Ok<StackMeshParts>, ProblemHttpResult>> DrawMeshMask(
        string id,
        StackMeshMaskRequest request,
        StackSessions sessions,
        WorkerLink worker,
        ServerIdentity identity,
        CancellationToken cancellationToken
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        if (session.ModelFile is not { } mesh || identity.Library is not { } library) {
            return TypedResults.Problem("This stack is on no mesh.", statusCode: Refused);
        }

        var side = session.State.Width;
        var name = Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes($"{side}\n{string.Join('\n', request.Parts.Order(StringComparer.Ordinal))}")
            )
        )[..16];
        var file = Path.Combine(
            Path.GetDirectoryName(session.Path)!,
            "pictures",
            $"{LibraryLayout.MeshMaskPrefix}{name}.png"
        );
        try {
            var connection = await worker.ConnectionAsync(cancellationToken);
            await connection.SendAsync(
                new() { Type = IpcTypes.MeshPartMask, MeshPartMask = new(mesh, request.Parts, side, file) },
                cancellationToken
            );
        } catch (IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        } catch (Exception e) when (e is IOException or TimeoutException) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        }

        return await MeshPartsAsync(
            session,
            worker,
            Path.GetRelativePath(library, file).Replace('\\', '/'),
            cancellationToken
        );
    }

    static async Task<Results<Ok<StackMeshParts>, ProblemHttpResult>> MeshPartsAsync(
        StackSession session,
        WorkerLink worker,
        string? asset,
        CancellationToken cancellationToken
    ) {
        if (session.ModelFile is not { } mesh) {
            return TypedResults.Ok(new StackMeshParts([], asset));
        }

        try {
            var connection = await worker.ConnectionAsync(cancellationToken);
            var reply = await connection.SendAsync(
                new() { Type = IpcTypes.MeshParts, MeshFile = mesh, MeshSet = session.Set },
                cancellationToken
            );
            return TypedResults.Ok(new StackMeshParts(reply.Parts ?? [], asset));
        } catch (IpcException e) {
            return TypedResults.Problem(e.Message, statusCode: Refused);
        } catch (Exception e) when (e is IOException or TimeoutException) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
        }
    }

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
            return Refusals.Of(e, Refused);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            return TypedResults.Problem(e.Message, statusCode: Unavailable);
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
            return Refusals.Of(e, Unavailable);
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
        Refusals.Problem(
            $"There is no open stack '{id}'.",
            Missing,
            Refusals.StackNotOpen,
            new Dictionary<string, string> { ["id"] = id }
        );
}
