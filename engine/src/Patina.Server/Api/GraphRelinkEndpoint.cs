using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;

namespace Patina.Server.Api;

/// <summary>
///     Relinking a picture (#185): every Bitmap of an open graph that reads one file reads another, as one undo step.
///     It is what the dependency view of Publish as Package offers for a picture it cannot find, and it is the
///     Server's so that any client names the new file the same way.
/// </summary>
static class GraphRelinkEndpoint {
    const string BitmapNode = "Source/Bitmap";
    const string SourceSetting = "Source";

    public static void MapGraphRelink(this RouteGroupBuilder graphs) =>
        graphs.MapPost("/{id}/relink", Relink)
            .Describe(
                "RelinkGraphPicture",
                "Points every Bitmap node that reads a picture at another file, as one undo step. The file is named "
                + "as the graph finds it: below the graph's folder or the library when it is in one, else whole.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity
            );

    static Results<Ok<GraphEditResult>, ProblemHttpResult> Relink(
        string id,
        GraphRelinkRequest request,
        GraphSessions sessions,
        LibraryHost host
    ) {
        if (sessions.Find(id) is not { } session) {
            return GraphEndpoints.NoSession(id);
        }

        try {
            var file = host.Library is { } library
                ? Path.GetFullPath(request.To, library.RootPath)
                : Path.IsPathRooted(request.To)
                    ? request.To
                    : throw new GraphEditRefusedException(
                        "With no library open, a picture is named by its whole path."
                    );
            if (!File.Exists(file)) {
                throw new GraphEditRefusedException($"There is no picture at '{request.To}'.");
            }

            var name = CompoundFolders.PictureName(session.ImageRoots, file);
            var from = request.From.Trim();
            List<GraphCommand> commands = [
                ..session.State.Nodes.Where(node => node.Type == BitmapNode
                        && node.Texts.TryGetValue(SourceSetting, out var source)
                        && source.Trim() == from
                    )
                    .Select(node => new GraphSetText(node.Id, SourceSetting, name))
            ];
            return commands.Count == 0
                ? throw new GraphEditRefusedException($"No Bitmap node of this graph reads '{request.From}'.")
                : TypedResults.Ok(session.Apply(new(commands, "Relink Picture")));
        } catch (GraphEditRefusedException e) {
            return Refusals.Of(e);
        }
    }
}
