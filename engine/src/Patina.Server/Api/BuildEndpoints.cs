using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Export.Presets;
using Patina.Jobs;
using Patina.Library;

namespace Patina.Server.Api;

/// <summary>
///     Builds and exports. A preview runs nothing, a build is a job like a bake, and a report holds paths, because
///     results are files (plan §8). Any client can do all of it, and no pixel is in any reply.
/// </summary>
static class BuildEndpoints {
    const string Tag = "Builds";
    const int DefaultLimit = 50;
    const int MaxLimit = 500;

    public static void MapBuildApi(this IEndpointRouteBuilder api) {
        var builds = api.MapGroup("/builds").WithTags(Tag);
        const int Missing = StatusCodes.Status404NotFound;
        const int Refused = StatusCodes.Status422UnprocessableEntity;

        api.MapGet("/export/presets", GetPresets)
            .WithTags(Tag)
            .WithName("GetExportPresets")
            .WithSummary("Every preset this library can use: the factory ones and those in shared/export-presets.")
            .ProducesProblem(Missing);
        builds.MapPost("/preview", PreviewAsync)
            .WithName("PreviewBuild")
            .WithSummary("What a build would do, from build keys alone. Nothing runs, and nothing is written.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(Missing)
            .ProducesProblem(Refused);
        builds.MapPost("/", Start)
            .WithName("StartBuild")
            .WithSummary("Queues a build. It follows as a job, and survives its Worker and the Server.")
            .ProducesProblem(Missing)
            .ProducesProblem(Refused);
        builds.MapGet("/{id}/report", GetReport)
            .WithName("GetBuildReport")
            .WithSummary("What a build did to the library, as far as it has got.")
            .ProducesProblem(Missing);
        builds.MapGet("/{id}/report/projects", GetProjects)
            .WithName("GetBuildProjects")
            .WithSummary("One page of what a finished build did to each project, with the paths of what it wrote.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(Missing);
    }

    static Results<Ok<ExportPresetList>, ProblemHttpResult> GetPresets(LibraryHost host) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        var found = ExportPresetCatalog.Read(library.RootPath);
        return TypedResults.Ok(new ExportPresetList([..found.Presets.Select(Describe)], found.Problems));
    }

    static ExportPresetInfo Describe(ExportPreset preset) =>
        new(
            preset.Id,
            preset.Name,
            preset.Engine,
            preset.Normal == GreenAxis.Up ? "y-up" : "y-down",
            preset.Path is null ? "factory" : "library",
            preset.Revision,
            [
                ..preset.Textures.Select(static t => new ExportPresetFile(
                        t.File,
                        t.Format,
                        t.Compression,
                        t.Space,
                        t.Mips,
                        [..t.Maps]
                    )
                )
            ]
        );

    static async Task<Results<Ok<BuildPreview>, ProblemHttpResult>> PreviewAsync(
        BuildRequest request,
        LibraryHost host,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = DefaultLimit
    ) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        if (offset < 0 || limit is < 1 or > MaxLimit) {
            return BadPage();
        }

        try {
            var plan = await Task.Run(
                () => BuildPipeline.Plan(library, request, ToolCatalog.Default, cancellationToken),
                cancellationToken
            );
            return TypedResults.Ok(plan.Preview(offset, limit));
        } catch (BuildRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    static Results<Accepted<JobState>, ProblemHttpResult> Start(
        BuildRequest request,
        LibraryHost host,
        JobQueue queue
    ) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        var catalog = ExportPresetCatalog.Read(library.RootPath);
        if ((request.Presets ?? library.Document.ExportTargets).FirstOrDefault(id => catalog.Find(id) is null
            ) is { } bad) {
            return TypedResults.Problem(
                $"There is no preset '{bad}'.",
                statusCode: StatusCodes.Status422UnprocessableEntity
            );
        }

        var job = queue.Enqueue(request);
        return TypedResults.Accepted($"/api/v1/jobs/{job.Id}", job);
    }

    static Results<Ok<LibraryReport>, ProblemHttpResult> GetReport(string id, LibraryHost host, JobQueue queue) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        var written = BuildJournal.ReadLibrary(BuildRunner.FolderOf(library.RootPath, Safe(id)));
        return (written ?? queue.Find(id)?.Report) is { } report ? TypedResults.Ok(report) : NoReport(id);
    }

    static Results<Ok<ProjectReportPage>, ProblemHttpResult> GetProjects(
        string id,
        LibraryHost host,
        int offset = 0,
        int limit = DefaultLimit
    ) {
        if (host.Library is not { } library) {
            return NoLibrary();
        }

        if (offset < 0 || limit is < 1 or > MaxLimit) {
            return BadPage();
        }

        var folder = BuildRunner.FolderOf(library.RootPath, Safe(id));
        return Directory.Exists(folder)
            ? TypedResults.Ok(BuildJournal.ReadProjects(folder, offset, limit))
            : NoReport(id);
    }

    /// <summary>A job's id as part of a path: only what a job's id can be.</summary>
    static string Safe(string id) => id.All(char.IsAsciiHexDigit) ? id : "-";

    static ProblemHttpResult NoLibrary() => Refusals.NoLibraryOpen();

    static ProblemHttpResult NoReport(string id) =>
        TypedResults.Problem($"Build '{id}' has no report.", statusCode: StatusCodes.Status404NotFound);

    static ProblemHttpResult BadPage() =>
        TypedResults.Problem(
            $"The offset cannot be negative and the limit is 1 to {MaxLimit}.",
            statusCode: StatusCodes.Status400BadRequest
        );
}
