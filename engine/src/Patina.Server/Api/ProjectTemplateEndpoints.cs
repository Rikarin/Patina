using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;

namespace Patina.Server.Api;

/// <summary>
///     Project templates (#144): a project's settings kept under a name in the library's <c>project-templates</c>
///     folder, which <c>POST /library/projects</c> starts a new project from when its request names one.
/// </summary>
static class ProjectTemplateEndpoints {
    public static void MapProjectTemplates(this RouteGroupBuilder library) {
        library.MapGet("/project-templates", List)
            .Described("ListProjectTemplates", "The project templates the library keeps, by name.");
        library.MapPost("/project-templates", Save)
            .Described(
                "SaveProjectTemplate",
                "Keeps a project's settings as a template: Auto Unwrap and Preserve Paint, and how it exports, bakes, "
                + "opens the viewport, checks and paints. Its meshes and documents stay its own. A template of the "
                + "same name is replaced."
            );
        library.MapDelete("/project-templates", Delete)
            .Described(
                "DeleteProjectTemplate",
                "Throws a project template away. Projects made from it keep their settings."
            );
    }

    static Results<Ok<ProjectTemplateList>, ProblemHttpResult> List(LibraryHost host) =>
        LibraryTreeEndpoints.Guarded(host, static library => TypedResults.Ok(Listed(library)));

    static ProjectTemplateList Listed(Patina.Library.OpenLibrary library) => new(library.Templates());

    static Results<Ok<ProjectTemplateList>, ProblemHttpResult> Save(LibraryHost host, ProjectTemplateRequest request) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                library.SaveTemplate(request.Project, request.Name);
                return TypedResults.Ok(Listed(library));
            }
        );

    static Results<Ok<ProjectTemplateList>, ProblemHttpResult> Delete(LibraryHost host, string name) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => library.DeleteTemplate(name)
                ? TypedResults.Ok(Listed(library))
                : throw new FileNotFoundException($"There is no project template '{name.Trim()}'.", name.Trim())
        );
}
