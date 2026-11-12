using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Server.Graphs;

namespace Patina.Server.Api;

/// <summary>A material file's materials brought into a project as graphs (#157, ADR 0030).</summary>
static class MaterialImportEndpoint {
    public static void MapMaterialImport(this RouteGroupBuilder library) =>
        library.MapPost("/project/materials", ImportAsync)
            .Described(
                "ImportProjectMaterials",
                "Brings the materials of a glTF or another model, a MaterialX document or a USD text layer into a "
                + "project, each as a material graph of its own that reads the file's pictures from the project. "
                + "Says what each graph is and what was left out."
            );

    static async Task<Results<Ok<MaterialImportResult>, ProblemHttpResult>> ImportAsync(
        LibraryHost host,
        WorkerLink worker,
        MaterialImportRequest request,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        try {
            var imported = await MaterialImport.ImportAsync(library, worker, request, cancellationToken);
            host.RequestRescan();
            return TypedResults.Ok(imported);
        } catch (IpcException e) {
            return LibraryTreeEndpoints.Problem(e, StatusCodes.Status422UnprocessableEntity);
        } catch (Exception e) when (LibraryTreeEndpoints.Refusal(e) is { } problem) {
            return problem;
        }
    }
}
