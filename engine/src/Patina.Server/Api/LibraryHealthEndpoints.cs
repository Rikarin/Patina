using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Jobs;
using Patina.Library;

namespace Patina.Server.Api;

/// <summary>
///     Keeping a library well from any client (#192, #162): the check <c>patina library check</c> runs, pruning the
///     cache as <c>patina cache prune</c> does, and clearing the copies of moved paint. The CLI needs the library to
///     itself; these run in the Server that holds it.
/// </summary>
static class LibraryHealthEndpoints {
    const int DefaultPruneDays = 30;

    public static void MapLibraryHealthApi(this IEndpointRouteBuilder api) {
        var library = api.MapGroup("/library").WithTags("Library");
        library.MapPost("/check", CheckAsync)
            .Described(
                "CheckLibrary",
                "Rescans, then reports the library's health by kind of finding, errors first, as patina library "
                + "check prints it. Changes nothing."
            );
        library.MapPost("/cache/prune", Prune)
            .Described(
                "PruneCache",
                "Removes built objects, bakes and reports nothing has touched for a number of days, 30 by default. "
                + "A later build makes them again. Refused while a bake or build is queued or running."
            );
        library.MapPost("/clean", Clean)
            .Described(
                "CleanLibrary",
                "Removes the copies of paint each project keeps in .paint-backup when a new UV layout moves it."
            );
    }

    static async Task<Results<Ok<LibraryHealth>, ProblemHttpResult>> CheckAsync(
        LibraryHost host,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        try {
            var report = await Task.Run(() => library.Check(cancellationToken: cancellationToken), cancellationToken);
            return TypedResults.Ok(Health(report));
        } catch (Exception e) when (LibraryTreeEndpoints.Refusal(e) is { } problem) {
            return problem;
        }
    }

    /// <summary>The report as a client reads it: grouped, errors first, each kind cut to its first findings.</summary>
    internal static LibraryHealth Health(CheckReport report) =>
        new(
            report.Projects,
            report.Assets,
            report.Outputs,
            report.Findings.Count(static finding => finding.IsError),
            [
                ..report.Findings.GroupBy(static finding => finding.Kind)
                    .Select(static group => group.ToList())
                    .Select(static found => new LibraryFindingGroup(
                            found[0].Kind.ToString(),
                            found[0].IsError,
                            found.Count,
                            [
                                ..found.Take(LibraryHealth.FindingsPerKind)
                                    .Select(static finding => new LibraryFinding(finding.Path, finding.Detail))
                            ]
                        )
                    )
                    .OrderByDescending(static group => group.IsError)
                    .ThenBy(static group => group.Kind, StringComparer.Ordinal)
            ]
        );

    static Results<Ok<LibraryTidied>, ProblemHttpResult> Prune(
        LibraryHost host,
        JobQueue jobs,
        CachePruneRequest? request
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                var days = request?.Days ?? DefaultPruneDays;
                if (days < 0) {
                    throw new ProjectRefusedException("A cache entry may go after 0 days or more.");
                }

                // A build may be about to read an object it found a moment ago, and a bake writes into the cache.
                if (jobs.Page().Jobs.Any(static job => job.State is JobStates.Queued or JobStates.Running)) {
                    throw new ProjectRefusedException("A bake or build is running. Prune the cache once it ends.");
                }

                var folder = Path.Combine(library.RootPath, LibraryLayout.CacheFolder);
                var before = FolderSize.Of(folder);
                var removed = CacheSweep.Prune(library.RootPath, TimeSpan.FromDays(days));
                return TypedResults.Ok(new LibraryTidied(removed, Math.Max(0, before - FolderSize.Of(folder))));
            }
        );

    static Results<Ok<LibraryTidied>, ProblemHttpResult> Clean(LibraryHost host) =>
        LibraryTreeEndpoints.Guarded(
            host,
            static library => {
                var (folders, bytes) = PaintBackups.Remove(library.RootPath);
                return TypedResults.Ok(new LibraryTidied(folders, bytes));
            }
        );
}
