using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;
using Vixen.NodeGraph;

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
        graphs.MapGet("/{id}/node-types/search", SearchNodeTypes)
            .Describe(
                "SearchGraphNodeTypes",
                "The nodes this graph can use, best match first. "
                + "takes and gives keep those a wire of that kind can use.",
                Missing,
                Refused
            );
        graphs.MapGet("/{id}/compounds", GetCompounds)
            .Describe(
                "GetGraphCompounds",
                "The compounds this graph can hold, and the path that opens the graph behind each.",
                Missing
            );
        graphs.MapPost("/{id}/compounds", Extract)
            .Describe(
                "ExtractGraphCompound",
                "Saves some nodes as a new compound beside the graph and holds it in their place, as one undo step.",
                Missing,
                Refused
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
        graphs.MapPut("/{id}/mesh-maps", UseMeshMaps)
            .Describe(
                "SetGraphMeshMaps",
                "Chooses the folder of baked maps this graph's Mesh Map nodes read, and remembers it beside the file.",
                Missing
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

    static Results<NoContent, ProblemHttpResult> UseMeshMaps(
        string id,
        GraphMeshMapsRequest request,
        GraphSessions sessions
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        session.UseMeshMaps(request.Folder is { } folder ? Path.GetFullPath(folder) : null);
        return TypedResults.NoContent();
    }

    static Results<Ok<GraphState>, ProblemHttpResult> GetState(string id, GraphSessions sessions) =>
        sessions.Find(id) is { } session ? TypedResults.Ok(session.State) : NoSession(id);

    static Results<NoContent, ProblemHttpResult> Close(
        string id,
        GraphSessions sessions,
        LibraryHost host,
        bool discard = false
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        if (session.Summary.Dirty && !discard) {
            return Refusals.Problem(
                "This graph has unsaved edits. Save it first, or close it discarding them.",
                StatusCodes.Status409Conflict,
                Refusals.Unsaved
            );
        }

        var thrownAway = session.Summary.Dirty;
        sessions.Close(session);
        if (thrownAway && host.Library is { } library) {
            Recovery.Clear(library.RootPath, session.Path); // or what was refused would be offered back
        }

        return TypedResults.NoContent();
    }

    static Results<Ok<IReadOnlyList<GraphNodeType>>, ProblemHttpResult> GetNodeTypes(
        string id,
        GraphSessions sessions
    ) =>
        sessions.Find(id) is { } session
            ? TypedResults.Ok<IReadOnlyList<GraphNodeType>>(
                [
                    ..session.NodeTypes.OrderBy(static type => type.Path, StringComparer.Ordinal)
                        .Select(GraphDocuments.Definition)
                ]
            )
            : NoSession(id);

    /// <summary>Ranks through Vixen's search, so that every palette of every client agrees on the order.</summary>
    /// <param name="id">The session.</param>
    /// <param name="query">What was typed. Empty offers everything, in path order.</param>
    /// <param name="takes">The kind of wire a node must have an input for.</param>
    /// <param name="gives">The kind of input a node must have an output for.</param>
    /// <param name="sessions">The open graphs.</param>
    static Results<Ok<GraphNodeMatches>, ProblemHttpResult> SearchNodeTypes(
        string id,
        string? query,
        string? takes,
        string? gives,
        GraphSessions sessions
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        if (!TryFilter(takes, PortDirection.Input, out var input)
            || !TryFilter(gives, PortDirection.Output, out var output)) {
            return TypedResults.Problem(
                $"'{takes ?? gives}' does not name a port kind.",
                statusCode: StatusCodes.Status422UnprocessableEntity
            );
        }

        var found = NodeSearch.Rank(session.NodeTypes, query, input);
        return TypedResults.Ok(
            new GraphNodeMatches(
                [
                    ..found.Where(match => output is not { } wanted
                            || NodeSearch.Landing(match.Type, wanted) is not null
                        )
                        .Select(static match => match.Type.Path)
                ]
            )
        );
    }

    static bool TryFilter(string? kind, PortDirection direction, out PortFilter? filter) {
        filter = null;
        if (string.IsNullOrEmpty(kind)) {
            return true;
        }

        if (!Enum.TryParse<PortKind>(kind, out var parsed) || !Enum.IsDefined(parsed)) {
            return false;
        }

        filter = new(parsed, direction);
        return true;
    }

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

    static Results<Ok<GraphExtraction>, ProblemHttpResult> Extract(
        string id,
        GraphExtractRequest request,
        GraphSessions sessions,
        LibraryHost host
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            var extraction = sessions.Extract(session, request);
            host.RequestRescan();
            return TypedResults.Ok(extraction);
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

    static Results<Ok<GraphEditResult>, ProblemHttpResult> Save(
        string id,
        GraphSessions sessions,
        LibraryHost host,
        StackSessions stacks,
        bool force = false
    ) {
        if (sessions.Find(id) is not { } session) {
            return NoSession(id);
        }

        try {
            var result = session.Save(force);
            sessions.Republish(session);
            if (host.Library is { } library) {
                Recovery.Clear(library.RootPath, session.Path);
                MaterialShelf.RefreshOpen(library, stacks);
            }

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

    static Task FollowAsync(string id, HttpContext context, GraphSessions sessions, long? since = null) =>
        ApiRoutes.FollowAsync(
            context,
            sessions.Find(id),
            ContractsJsonContext.Default.GraphEvent,
            static item => item.Kind == GraphEventKind.Closed,
            static item => item.Sequence,
            since
        );

    static ProblemHttpResult NoSession(string id) =>
        Refusals.Problem(
            $"There is no open graph '{id}'.",
            StatusCodes.Status404NotFound,
            Refusals.GraphNotOpen,
            new Dictionary<string, string> { ["id"] = id }
        );
}
