using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     Checks before export (#159): the Worker measures a stack's set, and the Server holds it to its project. Any
///     client lists the problems in its own words from their codes, and goes to each. A project's sets are checked at
///     once, and any check is made for one platform the project ships to, by the preset that writes for it.
/// </summary>
static class StackCheckEndpoints {
    const string PresetQuery = "The export preset of a platform to check for, or none for the project's own.";

    public static void MapStackChecks(this RouteGroupBuilder stacks) =>
        stacks.MapGet("/{id}/checks", GetChecks)
            .Describe(
                "GetStackChecks",
                "Checks the texture set before export, against its project's budget, size limit, texel density and "
                + "export preset, and its layout and values against what a mesh and a physical material can have. Each "
                + "problem has a stable code and where it is. A preset checks it for that platform, with the limits "
                + "the project keeps for it. " + PresetQuery,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status503ServiceUnavailable
            );

    public static void MapProjectChecks(this RouteGroupBuilder library) =>
        library.MapGet("/project/checks", GetProjectChecksAsync)
            .Described(
                "GetProjectChecks",
                "Checks every texture set and layered material of a project at once, as GET /stacks/{id}/checks "
                + "does one. A set open in a session is measured by the GPU engine; one nobody has open is judged on "
                + "what its file says, its limits, its files and its bake, and says it was not measured. "
                + PresetQuery
            );

    static async Task<Results<Ok<StackChecks>, ProblemHttpResult>> GetChecks(
        string id,
        string? preset,
        StackSessions sessions,
        StackEvaluator evaluator,
        LibraryHost host,
        CancellationToken cancellationToken
    ) {
        var library = host.Library;
        var project = sessions.Find(id) is { } open ? StackChecking.ProjectOf(library, open.Path) : null;
        var (chosen, refused) = Chosen(project, library, preset);
        if (refused is not null) {
            return refused;
        }

        return await StackEndpoints.AskWorkerAsync(
            id,
            sessions,
            async session => {
                var measured = await evaluator.MeasureAsync(session, cancellationToken);
                return StackChecking.Judge(
                    session.State,
                    measured,
                    project,
                    library?.Describe().TexelDensity ?? 0,
                    chosen!.Preset
                ) with {
                    Document = StackChecking.DocumentOf(library, session.Path)
                };
            }
        );
    }

    static async Task<Results<Ok<ProjectChecks>, ProblemHttpResult>> GetProjectChecksAsync(
        string project,
        string? preset,
        LibraryHost host,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        ProjectContents contents;
        try {
            contents = library.Contents(project);
        } catch (Exception e) when (LibraryTreeEndpoints.Refusal(e) is { } refused) {
            return refused;
        }

        var (chosen, refusal) = Chosen(contents, library, preset);
        if (refusal is not null) {
            return refusal;
        }

        var density = library.Describe().TexelDensity;
        List<StackChecks> sets = [];
        foreach (var document in contents.Documents.Where(static document => IsStack(document.Kind))) {
            var file = Path.GetFullPath(document.Path, library.RootPath);
            if (await StandingAsync(file, sessions, evaluator, cancellationToken) is var (state, measured)) {
                var judged = StackChecking.Judge(state, measured, contents, density, chosen!.Preset);
                sets.Add(judged with { Document = document.Path });
            }
        }

        var name = chosen!.Preset?.Name ?? string.Empty;
        return TypedResults.Ok(new ProjectChecks(project, name, sets.Sum(static set => set.MemoryBytes), sets));
    }

    static bool IsStack(string kind) => kind is DocumentKinds.Texturing or DocumentKinds.LayeredMaterial;

    /// <summary>
    ///     A stack file as it stands: as its open session has it, measured by the Worker where it answers, or as its
    ///     file says. Null for a file that has gone.
    /// </summary>
    static async Task<(StackState State, StackMeasured? Measured)?> StandingAsync(
        string file,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken
    ) {
        if (sessions.All.FirstOrDefault(session => session.Path == file) is { } open) {
            return (open.State, await MeasuredAsync(evaluator, open, cancellationToken));
        }

        // A stack nobody has open is read as its file says; nothing reaches the Worker, which holds no session of it.
        if (!File.Exists(file)) {
            return null;
        }

        StackSession unopened = new(file, SharedFile.ReadAllText(file), static () => { });
        return (unopened.State with { Id = string.Empty }, null);
    }

    /// <summary>What the Worker measured of an open set, or null when it could not, and the set is judged unmeasured.</summary>
    static async Task<StackMeasured?> MeasuredAsync(
        StackEvaluator evaluator,
        StackSession session,
        CancellationToken cancellationToken
    ) {
        try {
            return await evaluator.MeasureAsync(session, cancellationToken);
        } catch (Exception e) when (e is IpcException or TimeoutException or IOException) {
            return null;
        }
    }

    /// <summary>The preset a check measures with, or the refusal of one no library or project holds.</summary>
    static (Holder? Chosen, ProblemHttpResult? Refused) Chosen(
        ProjectContents? project,
        OpenLibrary? library,
        string? preset
    ) {
        try {
            return (new(StackChecking.PresetOf(project, library, preset)), null);
        } catch (KeyNotFoundException e) {
            return (null,
                Refusals.Problem(
                    e.Message,
                    StatusCodes.Status404NotFound,
                    Refusals.PresetMissing,
                    new Dictionary<string, string> { ["preset"] = preset ?? string.Empty }
                ));
        }
    }

    /// <summary>A preset that may be none, when the library has no presets at all.</summary>
    sealed record Holder(ExportPreset? Preset);
}
