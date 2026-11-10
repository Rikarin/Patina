using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Graphs;
using Patina.Library;
using Vixen.Core.Yaml;

namespace Patina.Server.Api;

/// <summary>
///     Publishing a graph as a package (ADR 0027): what it would bring, then the package itself in the library's
///     <c>packages/</c>, where every project's shelf finds it. The graph is published as it is saved.
/// </summary>
static class PackageEndpoints {
    const string ProjectsPrefix = LibraryLayout.ProjectsFolder + "/";

    public static void MapPackageRoutes(this RouteGroupBuilder library) {
        library.MapGet("/packages/plan", GetPackagePlan)
            .Described(
                "GetPackagePlan",
                "What publishing a saved graph as a package would bring: its project's compounds, the shipped ones it "
                + "reads, its pictures, and anything that stops it, with what compiling it said."
            );

        library.MapPost("/packages", PublishPackage)
            .Described(
                "PublishPackage",
                "Publishes a saved graph as a package in the library's packages folder, replacing one of its name. "
                + "Every project's shelf then offers it."
            );
    }

    static Results<Ok<PackagePlan>, ProblemHttpResult> GetPackagePlan(LibraryHost host, string graph, string? role) =>
        Packaged(host, graph, role, static (file, library, role, _) => GraphPackages.Plan(file, library.RootPath, role));

    static Results<Ok<PackagePublished>, ProblemHttpResult> PublishPackage(LibraryHost host, PackageRequest request) =>
        Packaged(
            host,
            request.Graph,
            request.Role,
            static (file, library, role, tags) => GraphPackages.Publish(file, library.RootPath, role, tags)
        );

    static Results<Ok<T>, ProblemHttpResult> Packaged<T>(
        LibraryHost host,
        string graph,
        string? role,
        Func<string, OpenLibrary, string, IReadOnlyList<string>, T> act
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        try {
            var file = LibraryLayout.Beside(library.RootPath, graph);
            var document = DocumentOf(library, graph);
            return TypedResults.Ok(
                act(
                    file,
                    library,
                    GraphPackages.RoleOf(role ?? document?.Role ?? GraphRoles.Material),
                    document?.Tags ?? []
                )
            );
        } catch (Exception e) when (e is PackageRefusedException
                                        or InvalidDataException
                                        or LibraryFormatException
                                        or YamlParseException
                                        or YamlBindingException) {
            return Refusals.Problem(
                e.Message,
                StatusCodes.Status422UnprocessableEntity,
                Refusals.PackageRefused,
                Refusals.Reason(e.Message)
            );
        } catch (Exception e) when (LibraryTreeEndpoints.Refusal(e) is { } problem) {
            return problem;
        }
    }

    /// <summary>
    ///     The graph as its project lists it, whose role it is published as and whose tags (ADR 0018) the package
    ///     carries, or null for a graph no project lists.
    /// </summary>
    static ProjectDocumentInfo? DocumentOf(OpenLibrary library, string graph) {
        if (!graph.StartsWith(ProjectsPrefix, StringComparison.Ordinal)) {
            return null;
        }

        var project = Path.GetDirectoryName(graph[ProjectsPrefix.Length..])!.Replace('\\', '/');
        try {
            return library.Contents(project).Documents.FirstOrDefault(d => d.Path == graph);
        } catch (Exception e) when (e is IOException or LibraryFormatException) {
            return null;
        }
    }
}
