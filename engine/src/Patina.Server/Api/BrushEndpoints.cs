using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     Brush and tool presets (#124): the shelf of them, saving the brush as it stands as a new one, and rewriting one
///     from the brush or as a favourite. A preset is a document of a project, so the library tree moves and deletes it.
/// </summary>
static class BrushEndpoints {
    public static void MapBrushRoutes(this RouteGroupBuilder library) {
        library.MapGet("/brushes", GetBrushes)
            .Described(
                "GetBrushPresets",
                "The brush and tool presets a project can use: the shipped ones, its own and other projects', "
                + "favourites first."
            );

        library.MapPost("/brushes", CreateBrush)
            .Described("CreateBrushPreset", "Saves a brush as a new preset, a document of the project.");

        library.MapPut("/brushes", UpdateBrush)
            .Described(
                "UpdateBrushPreset",
                "Rewrites a preset: from the brush as it stands, or as a favourite or not. A shipped preset keeps a "
                + "copy in the project instead."
            );
    }

    static Results<Ok<BrushPresetList>, ProblemHttpResult> GetBrushes(
        LibraryHost host,
        string? project,
        string? search
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => TypedResults.Ok(new BrushPresetList(BrushShelf.List(library, project, search)))
        );

    static Results<Ok<BrushPresetInfo>, ProblemHttpResult> CreateBrush(
        LibraryHost host,
        BrushPresetCreateRequest request
    ) =>
        request.Preset.Problem() is { } problem
            ? Refusals.Problem(problem, StatusCodes.Status400BadRequest, Refusals.BrushRefused)
            : LibraryTreeEndpoints.Guarded(
                host,
                library => {
                    var made = BrushShelf.Create(library, request);
                    host.RequestRescan();
                    return TypedResults.Ok(made);
                }
            );

    static Results<Ok<BrushPresetInfo>, ProblemHttpResult> UpdateBrush(
        LibraryHost host,
        BrushPresetUpdateRequest request
    ) =>
        request.Preset.Problem() is { } problem
            ? Refusals.Problem(problem, StatusCodes.Status400BadRequest, Refusals.BrushRefused)
            : LibraryTreeEndpoints.Guarded(
                host,
                library => {
                    var changed = BrushShelf.Update(library, request);
                    host.RequestRescan();
                    return TypedResults.Ok(changed);
                }
            );
}
