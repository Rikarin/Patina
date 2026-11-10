using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;

namespace Patina.Server.Api;

/// <summary>What a mesh is (#146), which the Worker reads, since Assimp is a native parser that may crash.</summary>
static class MeshFactsEndpoint {
    /// <summary>
    ///     The facts of a mesh file: one below the library, as a project lists it, or any file on this machine, so a
    ///     client can say what a mesh is before it is brought in.
    /// </summary>
    public static async Task<Results<Ok<MeshFacts>, ProblemHttpResult>> GetAsync(
        LibraryHost host,
        WorkerLink worker,
        string path,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        var file = Path.IsPathFullyQualified(path) ? path : LibraryLayout.Full(library.RootPath, path);
        if (!MeshFormats.IsMesh(file)) {
            return TypedResults.Problem(
                $"'{Path.GetFileName(file)}' is not a mesh Patina reads: use {string.Join(", ", MeshFormats.Meshes)}.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        if (!File.Exists(file)) {
            return TypedResults.Problem($"There is no file '{path}'.", statusCode: StatusCodes.Status404NotFound);
        }

        try {
            var connection = await worker.ConnectionAsync(cancellationToken);
            var reply = await connection.SendAsync(
                new() { Type = IpcTypes.MeshFacts, MeshFile = file },
                cancellationToken
            );
            return reply.Facts is { } facts
                ? TypedResults.Ok(facts)
                : TypedResults.Problem("The Worker said nothing about the mesh.", statusCode: 502);
        } catch (IpcException e) {
            return LibraryTreeEndpoints.Problem(e, StatusCodes.Status422UnprocessableEntity);
        }
    }
}
