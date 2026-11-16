using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Export;
using Patina.Export.Presets;
using Patina.Library;

namespace Patina.Server.Api;

/// <summary>
///     What the library costs (#207): its documents' maps, uncompressed or as a platform's preset writes them, by
///     folder, and the documents over a budget. Both read the index, where the scan keeps each stack's size, so neither
///     opens a document.
/// </summary>
static class CostEndpoints {
    const int MostOnAPage = 200;

    public static void MapCostRoutes(this RouteGroupBuilder library) {
        library.MapGet("/costs", Costs)
            .Described(
                "GetLibraryCosts",
                "What a folder's documents cost, by each folder or project directly in it, costliest first: their maps "
                + "uncompressed, at each document's size. No folder is the whole library. With preset, what they take "
                + "in GPU memory as that platform's preset writes them: each of its files for each texture set, at its "
                + "size, compression and mip chain. A preset nobody has is refused with preset-missing."
            );

        library.MapGet("/costs/documents", Over)
            .Described(
                "GetDocumentsOverBudget",
                "One page of the documents below a folder whose maps cost more than a budget in bytes, costliest "
                + "first, uncompressed or, with preset, as that platform's preset writes them."
            );
    }

    static Results<Ok<LibraryCosts>, ProblemHttpResult> Costs(LibraryHost host, string? folder, string? preset) =>
        Priced(
            host,
            preset,
            (library, price) => TypedResults.Ok(
                price is null
                    ? library.Costs(folder ?? string.Empty)
                    : library.Costs(folder ?? string.Empty, price)
            )
        );

    static Results<Ok<DocumentCostPage>, ProblemHttpResult> Over(
        LibraryHost host,
        long over,
        string? folder,
        string? preset,
        int offset = 0,
        int limit = 50
    ) =>
        Priced(
            host,
            preset,
            (library, price) => over < 0 || offset < 0 || limit is < 1 or > MostOnAPage
                ? throw new ProjectRefusedException(
                    $"A budget and an offset are not negative, and a page holds 1 to {MostOnAPage}."
                )
                : TypedResults.Ok(
                    price is null
                        ? library.CostlierThan(folder ?? string.Empty, over, offset, limit)
                        : library.CostlierThan(folder ?? string.Empty, over, offset, limit, price)
                )
        );

    /// <summary>Answers with the price of the preset asked for, none for uncompressed, or refuses one nobody has.</summary>
    static Results<Ok<T>, ProblemHttpResult> Priced<T>(
        LibraryHost host,
        string? preset,
        Func<OpenLibrary, DocumentPrice?, Ok<T>> answer
    ) {
        if (preset is not null
            && host.Library is { } open
            && ExportPresetCatalog.Read(open.RootPath).Find(preset) is null) {
            return Refusals.Problem(
                $"There is no preset '{preset}' in this library.",
                StatusCodes.Status404NotFound,
                Refusals.PresetMissing,
                new Dictionary<string, string> { ["preset"] = preset }
            );
        }

        return LibraryTreeEndpoints.Guarded(
            host,
            library => answer(
                library,
                preset is null ? null : PriceOf(ExportPresetCatalog.Read(library.RootPath).Find(preset)!)
            )
        );
    }

    /// <summary>
    ///     What a stack takes on a GPU as a preset writes it: each of the preset's files for each texture set, at the
    ///     size the preset writes it, as the pre-export checks measure a file (#159). A stack with no set writes none.
    /// </summary>
    internal static DocumentPrice PriceOf(ExportPreset preset) {
        ArgumentNullException.ThrowIfNull(preset);
        return size => (long)Math.Ceiling(
            size.Sets
            * preset.Textures.Sum(texture => {
                    var (width, height) = ExportMemory.Sized(preset, texture, size.Width, size.Height);
                    return ExportMemory.Bytes(texture, width, height);
                }
            )
        );
    }
}
