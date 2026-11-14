using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     Pictures of the node types a layer stack names (#290): its generators, filters and mask steps, each drawn as a
///     graph's palette draws it, so a layer's generator list can show what each one makes.
/// </summary>
static class StackNodePictures {
    public static void MapStackNodePictures(this RouteGroupBuilder stacks) =>
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
        } catch (TimeoutException e) {
            return Refusals.Of(e, StatusCodes.Status503ServiceUnavailable);
        }
    }
}
