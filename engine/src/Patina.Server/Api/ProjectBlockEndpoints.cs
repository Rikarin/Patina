using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;

namespace Patina.Server.Api;

/// <summary>
///     A project's settings blocks in its <c>project.yaml</c>: how it exports, how it bakes, how its viewport opens and
///     how its painting was left.
///     Each is written whole by a PUT of the project and the block, checked first, kept in the file, and answered with
///     the project's contents, so any client starts from where another left it.
/// </summary>
static class ProjectBlockEndpoints {
    public static void MapProjectBlocks(this RouteGroupBuilder library) {
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
        library.MapPut("/project/paint", SetPaint)
            .Described(
                "SetProjectPaint",
                "Keeps how a project's painting was left, in its project.yaml: the colours strokes last used, its "
                + "palette, the brush and the symmetry, so they survive a restart and any client starts from them."
            );
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
            ? TypedResults.Problem(problem, statusCode: StatusCodes.Status400BadRequest)
            : LibraryTreeEndpoints.Guarded(
                host,
                library => {
                    keep(library);
                    host.RequestRescan();
                    return TypedResults.Ok(library.Contents(project));
                }
            );
}
