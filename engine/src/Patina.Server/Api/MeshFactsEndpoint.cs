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
            return Refusals.WrongKind(file, MeshFormats.Meshes, StatusCodes.Status400BadRequest);
        }

        if (!File.Exists(file)) {
            return Refusals.NoFile(file);
        }

        try {
            var connection = await worker.ConnectionAsync(cancellationToken);
            var reply = await connection.SendAsync(
                new() { Type = IpcTypes.MeshFacts, MeshFile = file },
                cancellationToken
            );
            return reply.Facts is { } facts
                ? TypedResults.Ok(facts)
                : Refusals.Problem(
                    "The GPU engine said nothing about the mesh.",
                    StatusCodes.Status502BadGateway,
                    Refusals.EngineUnavailable
                );
        } catch (IpcException e) {
            return Refusals.OfEngine(e, StatusCodes.Status422UnprocessableEntity);
        }
    }
}
