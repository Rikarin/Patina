using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     The node types a layer stack names beyond the vocabulary's lists: the project's own generators (#298), and
///     pictures of any type (#290), each drawn as a graph's palette draws it, so a layer's generator list can show
///     what each one makes.
/// </summary>
static class StackNodePictures {
    public static void MapStackNodePictures(this RouteGroupBuilder stacks) {
        stacks.MapGet("/{id}/sources", GetSources)
            .Describe(
                "GetStackSources",
                "The project's own compounds an image input can read as a generator: each reads no image and writes at "
                + "least one, with what it takes and, for one that writes several, its results. The shipped ones are "
                + "the vocabulary's sources.",
                StatusCodes.Status404NotFound
            );
        stacks.MapPost("/{id}/node-types/thumbnails", DrawAsync)
            .Describe(
                "GetStackNodeTypeThumbnails",
                "Pictures of node types this stack can name, such as its generators, each at its defaults with a "
                + "noise on its picture inputs, by hash under /previews, as a graph's palette draws them. A generator "
                + "is drawn from the stack's mesh maps, so it shows what it makes on this mesh. A type with no "
                + "picture, or none this stack knows, has none.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status503ServiceUnavailable
            );
    }

    static Results<Ok<IReadOnlyList<StackFilterKind>>, ProblemHttpResult> GetSources(
        string id,
        StackSessions sessions
    ) =>
        sessions.Find(id) is { } session
            ? TypedResults.Ok(StackNodeKinds.OwnSources(session.Folder))
            : StackEndpoints.NoSession(id);

    /// <summary>Draws node types small, through the Worker, once per type and Vixen.</summary>
    /// <param name="id">The stack, whose folder's compounds are among the types.</param>
    /// <param name="request">The types, by path, as sources.</param>
    /// <param name="sessions">The open stacks.</param>
    /// <param name="thumbnails">The pictures.</param>
    /// <param name="cancellationToken">Stops it between pictures.</param>
    static async Task<Results<Ok<ThumbnailList>, ProblemHttpResult>> DrawAsync(
        string id,
        ThumbnailRequest request,
        StackSessions sessions,
        Thumbnails thumbnails,
        CancellationToken cancellationToken
    ) {
        if (sessions.Find(id) is not { } session) {
            return StackEndpoints.NoSession(id);
        }

        var types = StackNodeKinds.Types(session.Folder);
        try {
            return TypedResults.Ok(
                new ThumbnailList(
                    await thumbnails.DrawNodesAsync(
                        new NodePictureScope(session.Path, session.Folder, session.ImageRoots, types) {
                            MeshMaps = session.MeshMaps
                        },
                        request.Sources,
                        cancellationToken
                    )
                )
            );
        } catch (Exception e) when (e is IpcDisconnectedException or TimeoutException) {
            // No Worker came, or the one drawing went (#309).
            return Refusals.OfEngine(e, StatusCodes.Status503ServiceUnavailable);
        }
    }
}
