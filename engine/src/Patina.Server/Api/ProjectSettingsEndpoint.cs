using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>What a whole project shares, which can take a Worker's time when a mesh has no UV layout.</summary>
static class ProjectSettingsEndpoint {
    public static async Task<Results<Ok<ProjectContents>, ProblemHttpResult>> SetAsync(
        LibraryHost host,
        WorkerLink worker,
        StackSessions stacks,
        ProjectSettingsRequest request,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        var project = request.Project;
        try {
            if (request.AutoUnwrap is { } automatic) {
                library.SetAutoUnwrap(project, automatic);
            }

            var before = library.Contents(project);
            var onDocument = before.Documents.FirstOrDefault(static d => d.Mesh is not null)?.Mesh;
            var current = before.Meshes.FirstOrDefault(m => m.Id == onDocument);

            string? Held(string? below) =>
                below is null ? null : library.ProjectFile(project, LibraryTreeEndpoints.Relative(project, below));

            var mesh = request.Mesh ?? Held(current?.Path);
            if (mesh is null || (request.Mesh is null && request.High is null && request.AutoUnwrap != true)) {
                return TypedResults.Ok(before);
            }

            var added = library.AddMesh(project, mesh, request.High ?? Held(current?.High));
            if (before.AutoUnwrap) {
                added = await UnwrappedAsync(library, worker, project, added, cancellationToken);
            }

            HashSet<string> open = [..stacks.All.Select(static session => session.Path)];
            foreach (var document in library.UseMesh(project, added.Id)) {
                var file = library.ProjectFile(project, document.File);
                if (!open.Contains(file)) {
                    StackDocuments.SetModel(file, added.File);
                }
            }

            host.RequestRescan();
            return TypedResults.Ok(library.Contents(project));
        } catch (IpcException e) {
            return LibraryTreeEndpoints.Problem(e, StatusCodes.Status422UnprocessableEntity);
        } catch (Exception e) when (LibraryTreeEndpoints.Refusal(e) is { } problem) {
            return problem;
        }
    }

    /// <summary>The mesh, or a new one beside it if it had no UV layout. What it stood on stays as it was.</summary>
    static async Task<ProjectMesh> UnwrappedAsync(
        OpenLibrary library,
        WorkerLink worker,
        string project,
        ProjectMesh mesh,
        CancellationToken cancellationToken
    ) {
        var source = library.ProjectFile(project, mesh.File);
        var output = library.ProjectFile(project, $"{Path.GetFileNameWithoutExtension(mesh.File)}_unwrapped.glb");
        var connection = await worker.ConnectionAsync(cancellationToken);
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.MeshUnwrap, MeshUnwrap = new(source, output) },
            cancellationToken
        );
        var high = mesh.High is null ? null : library.ProjectFile(project, mesh.High);
        return reply.MeshUnwrapped == true ? library.AddMesh(project, output, high) : mesh;
    }
}
