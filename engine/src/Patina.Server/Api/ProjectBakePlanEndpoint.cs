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

    public static void MapProjectBakePlan(this RouteGroupBuilder library) {
        library.MapGet("/project/bake/plan", GetPlan)
            .Described(
                "GetProjectBakePlan",
                "The bakes a project's own settings ask for, one per texture set to bake or one for its mesh, ready "
                + "for POST /jobs/bakes. A document, by its path below the library, chooses the mesh; output puts "
                + "the maps in a folder of the Server's machine instead of the project's; set, once per texture set, "
                + "bakes those instead of the ones the project ticks."
            );
        library.MapPost("/project/bake/import", ImportAsync)
            .Described(
                "ImportProjectBake",
                "Brings maps baked in another program in as a texturing document's bake: each picture is copied where "
                + "the document's own bake goes, as a PNG, and replaces the map of its name there, and the document, "
                + "open or not, reads them as a bake of its mesh, UV set and material as they stand. A PNG is copied "
                + "as it is; any other picture, and a normal whose green runs down, is written as a 16-bit PNG with "
                + "its green up by the GPU engine first."
            );
    }

    static async Task<Results<Ok<BakeImported>, ProblemHttpResult>> ImportAsync(
        LibraryHost host,
        BakeLinks links,
        PicturePreviews pictures,
        BakeImportRequest request,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        List<string> written = [];
        var reading = string.Empty;
        try {
            var asked = BakeImport.Greens(
                request with {
                    Maps = [
                        ..request.Maps.Select(map => map with { File = Path.GetFullPath(map.File, library.RootPath) })
                    ]
                },
                library.Contents(request.Project).NormalFormat
            );
            BakeImport.Check(asked, true);
            List<BakeImportMap> maps = [];
            foreach (var map in asked.Maps) {
                if (!BakeImport.Decoded(map)) {
                    maps.Add(map);
                    continue;
                }

                // The Server decodes no picture (ADR 0042): the Worker writes it as a PNG with its green up (#300).
                var png = pictures.MapFile();
                reading = map.File;
                written.Add(png);
                await pictures.MapAsync(map.File, png, map.GreenDown == true, cancellationToken);
                maps.Add(map with { File = png, GreenDown = false });
            }

            return LibraryTreeEndpoints.Guarded(
                host,
                open => {
                    var (folder, document) = BakeImport.Run(open.RootPath, asked with { Maps = maps });
                    links.Imported(document, folder);
                    return TypedResults.Ok(new BakeImported(LibraryLayout.Relative(open.RootPath, folder)));
                }
            );
        } catch (Exception e) when (LibraryPictures.Refused(reading, e) is { } refused) {
            return refused;
        } catch (Exception e) when (LibraryTreeEndpoints.Refusal(e) is { } problem) {
            return problem;
        } finally {
            foreach (var png in written) {
                File.Delete(png);
            }
        }
    }

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
    ///     A preview as a project's bake plan would show it (#296), or the refusal: no library, no such project or
    ///     document, or a project file that cannot be read. A preview that names no project is as it was sent.
    /// </summary>
    internal static ProblemHttpResult? Resolve(
        LibraryHost host,
        ViewportBakePreview asked,
        out ViewportBakePreview resolved
    ) {
        resolved = asked;
        if (asked.Project is null) {
            return null;
        }

        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        try {
            resolved = Resolved(library, asked);
            return null;
        } catch (Exception e) when (LibraryTreeEndpoints.Refusal(e) is { } problem) {
            return problem;
        }
    }

    /// <summary>
    ///     A preview that names a project, with the meshes, cage and options that project's bake plan takes in place of
    ///     those sent (#296), so a client shows the bake it would run without working out its paths.
    /// </summary>
    /// <exception cref="FileNotFoundException">The library has no such project.</exception>
    /// <exception cref="ProjectRefusedException">The project has no mesh, or lists no such document.</exception>
    static ViewportBakePreview Resolved(OpenLibrary library, ViewportBakePreview asked) {
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
