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
                + "for POST /jobs/bakes. A document, by its path below the library, chooses the mesh; output puts "
                + "the maps in a folder of the Server's machine instead of the project's; set, once per texture set, "
                + "bakes those instead of the ones the project ticks."
            );

    static Results<Ok<ProjectBakePlan>, ProblemHttpResult> GetPlan(
        LibraryHost host,
        string project,
        string? document,
        string? output,
        string[]? set
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                var (folder, file) = Located(library, project, document);
                var maps = string.IsNullOrWhiteSpace(output) ? null : output.Trim();
                try {
                    var requests = ProjectBakes.Requests(folder, ProjectBakes.Read(folder), file, maps, set ?? []);
                    return TypedResults.Ok(new ProjectBakePlan(folder, requests));
                } catch (Exception e) when (e is ArgumentException or LibraryFormatException) {
                    throw new ProjectRefusedException(e.Message, e);
                }
            }
        );

    /// <summary>
    ///     A preview that names a project, with the meshes, cage and options that project's bake plan takes in place of
    ///     those sent (#296), so a client shows the bake it would run without working out its paths.
    /// </summary>
    /// <exception cref="FileNotFoundException">The library has no such project.</exception>
    /// <exception cref="ProjectRefusedException">The project has no mesh, or lists no such document.</exception>
    internal static ViewportBakePreview Resolved(OpenLibrary library, ViewportBakePreview asked) {
        if (asked.Project is not { } project) {
            return asked;
        }

        var (folder, file) = Located(library, project, asked.Document);
        try {
            var setup = ProjectBakes.Setup(folder, ProjectBakes.Read(folder), file);
            return asked with {
                High = setup.High,
                Low = setup.Low,
                Cage = setup.Cage,
                Material = null,
                Options = setup.Options
            };
        } catch (Exception e) when (e is ArgumentException or LibraryFormatException) {
            throw new ProjectRefusedException(e.Message, e);
        }
    }

    /// <summary>A project's folder, and a document by its path below the library as a file below that folder.</summary>
    static (string Folder, string? File) Located(OpenLibrary library, string project, string? document) {
        var folder = Path.GetDirectoryName(LibraryFiles.ProjectFile(library.RootPath, project))!;
        var inside = $"{ProjectsPrefix}{project}/";
        var file = document is not null && document.StartsWith(inside, StringComparison.Ordinal)
            ? document[inside.Length..]
            : document;
        return (folder, file);
    }
}
