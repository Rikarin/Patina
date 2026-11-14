using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Library;
using Patina.Server.Stacks;
using Patina.Server.Viewport;

namespace Patina.Server.Api;

/// <summary>
///     A project's settings blocks in its <c>project.yaml</c>: how it exports, how it bakes, how its viewport opens and
///     how its painting was left.
///     Each is written whole by a PUT of the project and the block, checked first, kept in the file, and answered with
///     the project's contents, so any client starts from where another left it. An export of the whole library keeps
///     its settings beside them, in <c>library.yaml</c>.
/// </summary>
static class ProjectBlockEndpoints {
    public static void MapProjectBlocks(this RouteGroupBuilder library) {
        library.MapPut("/export", SetLibraryExport)
            .Described(
                "SetLibraryExport",
                "Keeps what an export of the whole library is set to in library.yaml, where any client's next "
                + "library export starts. GET /library reads it back."
            );
        library.MapPut("/export/preset", SetDefaultPreset)
            .Described(
                "SetLibraryDefaultPreset",
                "Keeps in library.yaml the export preset a project's export starts from when it has kept none, and "
                + "that checks measure against. Null forgets it, which leaves the Starter glTF preset. "
                + "GET /export/presets says which preset that comes to."
            );
        library.MapPut("/export/targets", SetExportTargets)
            .Described(
                "SetLibraryExportTargets",
                "Keeps in library.yaml the export presets a build of the library writes when its request names none: "
                + "the platforms the library ships to, each once. GET /library reads them back as exportTargets."
            );
        library.MapPut("/project/export", SetExport)
            .Described(
                "SetProjectExport",
                "Keeps what a project's export is set to in its project.yaml, where any client's next export starts."
            );
        library.MapPut("/project/bake", SetBake)
            .Described(
                "SetProjectBake",
                "Keeps how a project's bakes run, which texture sets they make maps for and the files they read "
                + "beyond its mesh, in the project's own file."
            );
        library.MapPut("/project/viewport", SetViewport)
            .Described(
                "SetProjectViewport",
                "Keeps how the viewport is to open in a project, in its project.yaml. It is kept, not shown: "
                + "PUT /viewport/display shows it."
            );
        library.MapPut("/project/checks", SetChecks)
            .Described(
                "SetProjectChecks",
                "Keeps what a project's texture sets are held to before export in its project.yaml: a GPU memory "
                + "budget, a size limit and a texel density, which GET /stacks/{id}/checks and the texel density view "
                + "measure against."
            );
        library.MapPut("/project/paint", SetPaint)
            .Described(
                "SetProjectPaint",
                "Keeps how a project's painting was left, in its project.yaml: the colors strokes last used, its "
                + "palette, the brush and the symmetry, so they survive a restart and any client starts from them."
            );
    }

    static Results<Ok<ProjectContents>, ProblemHttpResult> SetChecks(
        LibraryHost host,
        ViewportSession viewport,
        StackSessions stacks,
        ProjectChecksRequest request
    ) {
        var kept = Keep(
            host,
            request.Project,
            request.Checks.Problem(),
            library => library.SetChecks(request.Project, request.Checks)
        );
        if (viewport.Worn is { } worn && stacks.Find(worn) is { } stack) {
            var (target, resolution) = StackChecking.Density(host.Library, stack);
            viewport.SetDensity(target, resolution);
        }

        return kept;
    }

    static Results<Ok<ProjectContents>, ProblemHttpResult> SetPaint(LibraryHost host, ProjectPaintRequest request) =>
        request.Paint.Problem() is { } problem
            ? Refusals.Problem(
                problem,
                StatusCodes.Status400BadRequest,
                Refusals.BrushRefused,
                Refusals.Reason(problem)
            )
            : Keep(host, request.Project, null, library => library.SetPaint(request.Project, request.Paint));

    static Results<Ok<LibraryInfo>, ProblemHttpResult> SetLibraryExport(
        LibraryHost host,
        LibraryExportRequest request
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                library.SetExport(request.Export);
                return TypedResults.Ok(library.Describe());
            }
        );

    static Results<Ok<LibraryInfo>, ProblemHttpResult> SetDefaultPreset(
        LibraryHost host,
        LibraryPresetRequest request
    ) =>
        KeepPresets(host, request.Preset is { } id ? [id] : [], open => open.SetDefaultPreset(request.Preset));

    static Results<Ok<LibraryInfo>, ProblemHttpResult> SetExportTargets(
        LibraryHost host,
        LibraryTargetsRequest request
    ) =>
        KeepPresets(host, request.Presets, open => open.SetExportTargets(request.Presets));

    /// <summary>
    ///     Keeps a library setting that names presets, once each names one the library has: a preset is a file, so
    ///     one removed later is only skipped.
    /// </summary>
    static Results<Ok<LibraryInfo>, ProblemHttpResult> KeepPresets(
        LibraryHost host,
        IReadOnlyList<string> named,
        Action<OpenLibrary> keep
    ) {
        var catalog = host.Library is { } library ? ExportPresetCatalog.Read(library.RootPath) : null;
        if (catalog is not null && named.FirstOrDefault(id => catalog.Find(id) is null) is { } missing) {
            return Refusals.Problem(
                $"There is no preset '{missing}' in this library.",
                StatusCodes.Status404NotFound,
                Refusals.PresetMissing,
                new Dictionary<string, string> { ["preset"] = missing }
            );
        }

        return LibraryTreeEndpoints.Guarded(
            host,
            open => {
                keep(open);
                return TypedResults.Ok(open.Describe());
            }
        );
    }

    static Results<Ok<ProjectContents>, ProblemHttpResult> SetExport(LibraryHost host, ProjectExportRequest request) =>
        Keep(host, request.Project, null, library => library.SetExport(request.Project, request.Export));

    static Results<Ok<ProjectContents>, ProblemHttpResult> SetBake(LibraryHost host, ProjectBakeRequest request) =>
        request.Bake.Options.Refusal() is { Code: { } code } refusal
            ? Refusals.Problem(refusal.Detail, StatusCodes.Status400BadRequest, code, refusal.Values)
            : Keep(
                host,
                request.Project,
                request.Bake.Options.Problem(),
                library => library.SetBake(request.Project, request.Bake)
            );

    static Results<Ok<ProjectContents>, ProblemHttpResult> SetViewport(
        LibraryHost host,
        ProjectViewportRequest request
    ) =>
        Keep(
            host,
            request.Project,
            request.Viewport.Problem(),
            library => library.SetViewport(request.Project, request.Viewport)
        );

    static Results<Ok<ProjectContents>, ProblemHttpResult> Keep(
        LibraryHost host,
        string project,
        string? problem,
        Action<OpenLibrary> keep
    ) =>
        problem is not null
            ? Refusals.BadSettings(problem, StatusCodes.Status400BadRequest)
            : LibraryTreeEndpoints.Guarded(
                host,
                library => {
                    keep(library);
                    host.RequestRescan();
                    return TypedResults.Ok(library.Contents(project));
                }
            );
}
