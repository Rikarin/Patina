using Patina.Contracts;
using Patina.Library;
using Patina.Server.Api;
using Patina.Server.Graphs;
using Patina.Server.Stacks;

namespace Patina.Server;

/// <summary>
///     A finished bake reaches the open documents it is of, whichever client asked for it (#268). The Server owns which
///     bake a document reads (plan §3.1), so a client that bakes and never says what the bake was for, as a Blender
///     add-on would not, still leaves the set reading it. A texture set reads the bake of its own mesh and material,
///     wherever it went; any open document reads a bake into the folder it already reads again.
/// </summary>
/// <remarks>
///     Called before the job is marked done, so a client that hears it is done and reads the set finds the bake there.
/// </remarks>
public sealed class BakeLinks(LibraryHost host, StackSessions stacks, GraphSessions graphs) {
    /// <summary>Points the open documents a delivered bake is of at its folder.</summary>
    public void Delivered(BakeRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        var output = Folder(request.Output);
        string? document;
        try {
            document = BakeLayouts.DocumentOf(host.Library, request);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException or LibraryFormatException) {
            document = null; // the project could not be read: a set reading this folder already still follows it
        }

        foreach (var stack in stacks.All) {
            if (string.Equals(stack.Path, document, StringComparison.Ordinal) || Same(stack.MeshMaps, output)) {
                Link(stack.UseMeshMaps, output);
            }
        }

        foreach (var graph in graphs.All) {
            if (Same(graph.MeshMaps, output)) {
                Link(graph.UseMeshMaps, output);
            }
        }
    }

    /// <summary>
    ///     Points one document at the bake. The bake is delivered whatever happens here: a document whose choice could
    ///     not be written down is one a client can still point at it.
    /// </summary>
    static void Link(Action<string?> use, string output) {
        try {
            use(output);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            // The file beside the document is held: the session reads the bake all the same, and choosing it writes it.
        }
    }

    static bool Same(string? folder, string output) =>
        folder is not null && string.Equals(Folder(folder), output, StringComparison.Ordinal);

    static string Folder(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
