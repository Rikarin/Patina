using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     Whether reloading a mesh moves anything on it (#89), asked before anything changes: a reload that moves nothing
///     touches no paint and leaves open documents open, with their undo.
/// </summary>
static class MeshReloadCheck {
    /// <summary>
    ///     Why reloading the project's mesh from a file would move something on it, or null when nothing would.
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="worker">The Worker, which reads both meshes.</param>
    /// <param name="project">The project.</param>
    /// <param name="mesh">The file to reload from.</param>
    /// <param name="cancellationToken">Stops waiting for the Worker.</param>
    /// <exception cref="IpcException">The Worker could not read a mesh's materials.</exception>
    public static async Task<string?> WhyItMovesAsync(
        OpenLibrary library,
        WorkerLink worker,
        string project,
        string mesh,
        CancellationToken cancellationToken
    ) {
        var contents = library.Contents(project);
        var onDocument = contents.Documents.FirstOrDefault(static d => d.Mesh is not null)?.Mesh;
        if (contents.Meshes.FirstOrDefault(m => m.Id == onDocument) is not { } current) {
            return "The project has no mesh to reload.";
        }

        // ⚠ Another name is another file under the documents, which an open one would not follow; and a mesh Auto
        // Unwrap laid out is a file of its own, which reloading lays out again.
        var held = library.ProjectFile(project, LibraryTreeEndpoints.Relative(project, current.Path));
        if (!string.Equals(Path.GetFileName(held), Path.GetFileName(mesh), StringComparison.Ordinal)) {
            return $"'{Path.GetFileName(mesh)}' is not the file the project's mesh '{Path.GetFileName(held)}' is.";
        }

        var connection = await worker.ConnectionAsync(cancellationToken);
        if (!(await MaterialsAsync(connection, held, cancellationToken)).SetEquals(
                await MaterialsAsync(connection, mesh, cancellationToken)
            )) {
            return "The new mesh's materials are not the old one's.";
        }

        foreach (var document in contents.Documents.Where(d =>
                     d.Kind == DocumentKinds.Texturing && d.Mesh == current.Id
                 )) {
            var stack = library.ProjectFile(project, LibraryTreeEndpoints.Relative(project, document.Path));
            var uvSet = StackDocuments.Parse(SharedFile.ReadAllText(stack)).Sets.FirstOrDefault()?.UvSet ?? 0;
            var reply = await connection.SendAsync(
                new() {
                    Type = IpcTypes.MeshSameLayout,
                    MeshMovePaint = new(held, mesh, [], document.Material ?? string.Empty, string.Empty, uvSet)
                },
                cancellationToken
            );
            if (reply.SameLayout != true) {
                return $"'{document.Name}' is laid out differently on the new mesh.";
            }
        }

        return null;
    }

    static async Task<HashSet<string>> MaterialsAsync(
        IpcConnection connection,
        string mesh,
        CancellationToken cancellationToken
    ) {
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.MeshMaterials, MeshFile = mesh },
            cancellationToken
        );
        return [..reply.Materials ?? []];
    }
}
