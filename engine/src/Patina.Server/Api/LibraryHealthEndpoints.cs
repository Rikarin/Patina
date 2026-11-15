using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Jobs;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     Keeping a library well from any client (#192, #162): the check <c>patina library check</c> runs, pruning the
///     cache as <c>patina cache prune</c> does, removing unused revisions as <c>--purge-revisions</c> does (#292), and
///     clearing the copies of moved paint. The CLI needs the library to
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
        library.MapPost("/revisions/purge", PurgeRevisionsAsync)
            .Described(
                "PurgeRevisions",
                "Rescans, then removes the bytes under .revisions that no snapshot and no pin names any more, which "
                + "the check lists as UnusedRevision, as patina library check --purge-revisions does. No snapshot or "
                + "pin is taken while it runs."
            );
        library.MapPost("/clean", Clean)
            .Described(
                "CleanLibrary",
                "Removes the copies of paint each project keeps in .paint-backup when a new UV layout moves it, "
                + "and moves the pictures in projects that no document reads, saved or open, into the bin as one "
                + "entry."
            );
        library.MapGet("/uploads/unused", UnusedUploads)
            .Described(
                "GetUnusedUploads",
                "The files sent from another computer that no file of the library and no open document names any "
                + "more, with the room they take. One sent in the last hour counts as used. Changes nothing."
            );
        library.MapPost("/uploads/clear", ClearUploads)
            .Described(
                "ClearUploads",
                "Moves the files sent from another computer that nothing names, as GetUnusedUploads lists them, "
                + "into the bin as one entry, which Restore puts back."
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

    static async Task<Results<Ok<LibraryTidied>, ProblemHttpResult>> PurgeRevisionsAsync(
        LibraryHost host,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        try {
            var (files, bytes) = await Task.Run(
                () => LibraryRevisions.Purge(library, cancellationToken),
                cancellationToken
            );
            return TypedResults.Ok(new LibraryTidied(files, bytes));
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

    /// <summary>
    ///     Deletes the copies of moved paint, and moves the pictures nothing reads into the bin (#162). An open
    ///     document counts by what it holds now, saved or not, so a picture just put on a layer stays.
    /// </summary>
    static Results<Ok<LibraryCleaned>, ProblemHttpResult> Clean(
        LibraryHost host,
        StackSessions stacks,
        GraphSessions graphs
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                // As the copies of moved paint go: from the folders alone, which the next rescan finds as they are.
                return TypedResults.Ok(library.Care.Clean(Open(stacks, graphs)));
            }
        );

    /// <summary>The uploads nothing names, saved or open (#297).</summary>
    static Results<Ok<UnusedUploadList>, ProblemHttpResult> UnusedUploads(
        LibraryHost host,
        StackSessions stacks,
        GraphSessions graphs
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => TypedResults.Ok(library.Care.UnusedUploads(Open(stacks, graphs)))
        );

    /// <summary>Moves the uploads nothing names into the bin (#297).</summary>
    static Results<Ok<UploadsCleared>, ProblemHttpResult> ClearUploads(
        LibraryHost host,
        StackSessions stacks,
        GraphSessions graphs
    ) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => TypedResults.Ok(library.Care.ClearUploads(Open(stacks, graphs)))
        );

    /// <summary>What every open document holds now, saved or not, by its file.</summary>
    static Dictionary<string, string> Open(StackSessions stacks, GraphSessions graphs) {
        Dictionary<string, string> open = new(StringComparer.Ordinal);
        foreach (var stack in stacks.All) {
            open[stack.Path] = stack.Text;
        }

        foreach (var graph in graphs.All) {
            open[graph.Path] = graph.Text;
        }

        return open;
    }
}
