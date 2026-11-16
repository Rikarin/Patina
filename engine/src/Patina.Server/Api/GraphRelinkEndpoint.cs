using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Graphs;
using Patina.Library;
using Patina.Server.Graphs;

namespace Patina.Server.Api;

/// <summary>
///     Relinking a file (#185): every node of an open graph that reads one file reads another, as one undo step, be it
///     a Bitmap's picture, a Text's font or a Vector's SVG (#306). It is what the dependency view of Publish as Package
///     offers for a file it cannot find, and it is the Server's so that any client names the new file the same way.
///     Naming a node points that node alone, which is how a client picks a Bitmap's Source from a list rather than
///     typing it (#292).
/// </summary>
static class GraphRelinkEndpoint {
    public static void MapGraphRelink(this RouteGroupBuilder graphs) =>
        graphs.MapPost("/{id}/relink", Relink)
            .Describe(
                "RelinkGraphPicture",
                "Points every node that reads a file (a Bitmap's picture, a Text's font, a Vector's SVG) at another "
                + "file, as one undo step, or only the node named by node. The file is named as the graph finds it: "
                + "below the graph's folder or the library when it is in one, else whole.",
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
                        "With no library open, a file is named by its whole path."
                    );
            if (!File.Exists(file)) {
                throw new GraphEditRefusedException($"There is no file at '{request.To}'.");
            }

            var name = CompoundFolders.PictureName(session.ImageRoots, file);
            var reading = Reading(session.State.Nodes, request).ToList();
            List<GraphCommand> commands = [
                ..reading.Select(node => new GraphSetText(node.Id, GraphFiles.SettingOf(node.Type)!, name))
            ];
            if (commands.Count == 0) {
                throw new GraphEditRefusedException(
                    request.Node is { } node
                        ? $"Node {node} of this graph reads no file."
                        : $"No node of this graph reads '{request.From}'."
                );
            }

            return TypedResults.Ok(session.Apply(new(commands, LabelOf(request, reading))));
        } catch (GraphEditRefusedException e) {
            return Refusals.Of(e);
        }
    }

    /// <summary>The node the request names, or every one reading its file, of those that read one.</summary>
    static IEnumerable<GraphNodeState> Reading(IEnumerable<GraphNodeState> nodes, GraphRelinkRequest request) {
        var from = request.From.Trim();
        return nodes.Where(node => GraphFiles.SettingOf(node.Type) is { } setting
            && (request.Node is { } id
                ? node.Id == id
                : node.Texts.TryGetValue(setting, out var source) && source.Trim() == from)
        );
    }

    /// <summary>What the undo step is called: a picture's when only Bitmaps read the file.</summary>
    static string LabelOf(GraphRelinkRequest request, IReadOnlyList<GraphNodeState> reading) {
        var what = reading.All(static node => node.Type == GraphFiles.BitmapNode) ? "Picture" : "File";
        return request.Node is null ? $"Relink {what}" : $"Choose {what}";
    }
}
