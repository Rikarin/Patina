using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;

namespace Patina.Server.Api;

/// <summary>
///     What the library costs (#207): its documents' maps, uncompressed, by folder, and the documents over a budget.
///     Both read the index, where the scan keeps each stack's size, so neither opens a document.
/// </summary>
static class CostEndpoints {
    const int MostOnAPage = 200;

    public static void MapCostRoutes(this RouteGroupBuilder library) {
        library.MapGet("/costs", Costs)
            .Described(
                "GetLibraryCosts",
                "What a folder's documents cost, by each folder or project directly in it, costliest first: their maps "
                + "uncompressed, at each document's size. No folder is the whole library."
            );

        library.MapGet("/costs/documents", Over)
            .Described(
                "GetDocumentsOverBudget",
                "One page of the documents below a folder whose maps cost more than a budget in bytes, costliest first."
            );
    }

    static Results<Ok<LibraryCosts>, ProblemHttpResult> Costs(LibraryHost host, string? folder) =>
        LibraryTreeEndpoints.Guarded(host, library => TypedResults.Ok(library.Costs(folder ?? string.Empty)));

    static Results<Ok<DocumentCostPage>, ProblemHttpResult> Over(
        LibraryHost host,
        long over,
        string? folder,
        int offset = 0,
        int limit = 50
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => over < 0 || offset < 0 || limit is < 1 or > MostOnAPage
                ? throw new ProjectRefusedException(
                    $"A budget and an offset are not negative, and a page holds 1 to {MostOnAPage}."
                )
                : TypedResults.Ok(library.CostlierThan(folder ?? string.Empty, over, offset, limit))
        );
}
