using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Graphs;
using Patina.Library;
using Patina.Server.Graphs;
using Vixen.TextureGraph;

namespace Patina.Server.Api;

/// <summary>
///     Relinking a file (#185): every node of an open graph that reads one file reads another, as one undo step, be it
///     a Bitmap's picture, a Text's font or a Vector's SVG (#306). It is what the dependency view of Publish as Package
///     offers for a file it cannot find, and it is the Server's so that any client names the new file the same way.
///     Naming a node points that node alone, which is how a client picks a Bitmap's Source from a list rather than
///     typing it (#292). A file named through a parameter (<c>$Font</c>) is pointed where the parameter holds it, and
///     one a compound's node hands its compound is pointed on that node (#311).
/// </summary>
static class GraphRelinkEndpoint {
    public static void MapGraphRelink(this RouteGroupBuilder graphs) =>
        graphs.MapPost("/{id}/relink", Relink)
            .Describe(
                "RelinkGraphPicture",
                "Points every node that reads a file (a Bitmap's picture, a Text's font, a Vector's SVG) at another "
                + "file, as one undo step, or only the node named by node. A file a node reads through a parameter "
                + "is pointed in the parameter's default, choices and presets, and one a compound's node hands its "
                + "compound on that node. The file is named as the graph finds it: below the graph's folder or the "
                + "library when it is in one, else whole.",
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
                    : throw new GraphEditRefusedException("With no library open, a file is named by its whole path.");
            if (!File.Exists(file)) {
                throw new GraphEditRefusedException($"There is no file at '{request.To}'.");
            }

            var name = CompoundFolders.PictureName(session.ImageRoots, file);
            var state = session.State;
            var reading = Reading(state.Nodes, request).ToList();
            List<GraphCommand> commands = [
                ..reading.Select(node => new GraphSetText(node.Id, GraphFiles.SettingOf(node.Type)!, name))
            ];
            if (request.Node is null && request.From.Trim() is { Length: > 0 } from) {
                commands.AddRange(Handed(state.Nodes, from, name));
                commands.AddRange(Parameters(state, from, name));
            }

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

    /// <summary>Each setting of a compound's node that hands its compound the file.</summary>
    static IEnumerable<GraphCommand> Handed(IEnumerable<GraphNodeState> nodes, string from, string to) =>
        nodes.Where(static node => GraphFiles.SettingOf(node.Type) is null)
            .SelectMany(node => node.Texts.Where(text => text.Value.Trim() == from)
                .Select(text => new GraphSetText(node.Id, text.Key, to))
            );

    /// <summary>
    ///     The parameters a file-reading node reads its file through, each holding the file as its default, among its
    ///     choices or in a preset, pointed at the new one: what the parameter can hold is what a package carries.
    /// </summary>
    static IEnumerable<GraphCommand> Parameters(GraphState state, string from, string to) {
        HashSet<string> knobs = [
            ..state.Nodes.Select(static node => GraphFiles.SettingOf(node.Type) is { } setting
                && TextureGraphParameters.IsReference(
                    node.Texts.GetValueOrDefault(setting, string.Empty).Trim(),
                    out var knob
                )
                        ? knob
                        : null
                )
                .OfType<string>()
        ];
        Func<string, string> pointed = text => text.Trim() == from ? to : text;
        return [
            ..state.Parameters
                .Where(p => knobs.Contains(p.Name) && p.Accepted.Prepend(p.Default).Any(text => text.Trim() == from))
                .Select(parameter => new GraphSetParameter(
                        parameter.Name,
                        parameter with {
                            Default = pointed(parameter.Default),
                            Accepted = [..parameter.Accepted.Select(pointed)],
                            Labels = null
                        }
                    )
                ),
            ..Presets(state, knobs, from, pointed)
        ];
    }

    /// <summary>
    ///     Each preset holding the file in a parameter a node reads it through, saved again with it pointed. A
    ///     preset may name a parameter the graph no longer declares, which saving it would refuse, so that is left out.
    /// </summary>
    static IEnumerable<GraphCommand> Presets(
        GraphState state,
        HashSet<string> knobs,
        string from,
        Func<string, string> pointed
    ) {
        var declared = state.Parameters.Select(static p => p.Name).ToHashSet(StringComparer.Ordinal);
        return state.Presets
            .Where(preset => preset.Values.Any(value => knobs.Contains(value.Key) && value.Value.Trim() == from))
            .Select(preset => new GraphSavePreset(
                    preset.Name,
                    preset.Values.Where(value => declared.Contains(value.Key))
                        .ToDictionary(
                            static value => value.Key,
                            value => knobs.Contains(value.Key) ? pointed(value.Value) : value.Value,
                            StringComparer.Ordinal
                        )
                )
            );
    }

    /// <summary>What the undo step is called: a picture's when only Bitmaps read the file.</summary>
    static string LabelOf(GraphRelinkRequest request, List<GraphNodeState> reading) {
        var what = reading.Count > 0 && reading.TrueForAll(static node => node.Type == GraphFiles.BitmapNode)
            ? "Picture"
            : "File";
        return request.Node is null ? $"Relink {what}" : $"Choose {what}";
    }
}
