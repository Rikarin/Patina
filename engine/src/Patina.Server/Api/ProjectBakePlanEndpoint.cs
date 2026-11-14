using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;

namespace Patina.Server.Api;

/// <summary>
///     What baking a project with its own settings asks for (#186), worked out where <c>patina bake-maps --project</c>
///     works it out, so that every client bakes a project the same way and none has to rebuild the requests.
/// </summary>
static class ProjectBakePlanEndpoint {
    const string ProjectsPrefix = LibraryLayout.ProjectsFolder + "/";

    public static void MapProjectBakePlan(this RouteGroupBuilder library) =>
        library.MapGet("/project/bake/plan", GetPlan)
            .Described(
                "GetProjectBakePlan",
                "The bakes a project's own settings ask for, one per texture set to bake or one for its mesh, ready "
                + "for POST /jobs/bakes. A document, by its path below the library, chooses the mesh."
            );

    static Results<Ok<ProjectBakePlan>, ProblemHttpResult> GetPlan(
        LibraryHost host,
        string project,
        string? document
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                var folder = Path.GetDirectoryName(LibraryFiles.ProjectFile(library.RootPath, project))!;
                var inside = $"{ProjectsPrefix}{project}/";
                var file = document is not null && document.StartsWith(inside, StringComparison.Ordinal)
                    ? document[inside.Length..]
                    : document;
                try {
                    return TypedResults.Ok(
                        new ProjectBakePlan(folder, ProjectBakes.Requests(folder, ProjectBakes.Read(folder), file))
                    );
                } catch (ArgumentException e) {
                    throw new ProjectRefusedException(e.Message, e);
                }
            }
        );
}
