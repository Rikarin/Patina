using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Export.Presets;
using Patina.Library;

namespace Patina.Server.Api;

/// <summary>
///     Export presets as files a client edits (#155, ADR 0019). A preset lives in the library's or a project's
///     <c>export-presets</c> folder, and every write goes through the reader, so the API can make nothing a hand-written
///     file could not say. Factory presets are read-only: a client duplicates one into the library or a project.
/// </summary>
static class PresetEndpoints {
    const string Tag = "Builds";

    public static void MapPresetApi(this IEndpointRouteBuilder api) {
        var presets = api.MapGroup("/export/presets").WithTags(Tag);
        presets.MapGet("/{id}", Get)
            .WithName("GetExportPreset")
            .WithSummary("A preset as its file says it, where it comes from, and its text.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        presets.MapPut("/{id}", Put)
            .WithName("PutExportPreset")
            .WithSummary("Keeps a preset as a file in the library's or a project's export-presets, renaming one if asked.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        presets.MapDelete("/{id}", Delete)
            .WithName("DeleteExportPreset")
            .WithSummary("Removes a library's or a project's preset file. A factory preset cannot be removed.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        presets.MapPost("/check", Check)
            .WithName("CheckExportPreset")
            .WithSummary("What a preset would be and the files it would write for a project, keeping nothing.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        presets.MapPost("/import", Import)
            .WithName("ImportExportPreset")
            .WithSummary("Brings a preset file into the library's or a project's export-presets, after reading it.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    static Results<Ok<ExportPresetSource>, ProblemHttpResult> Get(string id, LibraryHost host, string? project = null) {
        if (host.Library is not { } library) {
            return Refusals.NoLibraryOpen();
        }

        if (Folder(library, project) is null) {
            return NoProject(project);
        }

        return ExportPresetCatalog.Read(library.RootPath, project).Find(id) is { } preset
            ? TypedResults.Ok(SourceOf(preset))
            : Missing(id);
    }

    static Results<Ok<ExportPresetSource>, ProblemHttpResult> Put(
        string id,
        ExportPresetWrite write,
        LibraryHost host
    ) {
        if (host.Library is not { } library) {
            return Refusals.NoLibraryOpen();
        }

        if (Folder(library, write.Project) is not { } folder) {
            return NoProject(write.Project);
        }

        try {
            var file = ExportPresetWriter.Save(folder, write.Preset with { Id = id }, write.Replaces);
            var origin = write.Project is null ? ExportPresetCatalog.Origins.Library : ExportPresetCatalog.Origins.Project;
            var preset = ExportPresetReader.Read(SharedFile.ReadAllText(file), Path.GetFileName(file), file) with {
                Origin = origin
            };
            return TypedResults.Ok(SourceOf(preset));
        } catch (ExportPresetException e) {
            return Invalid(id, e.Message);
        }
    }

    static Results<NoContent, ProblemHttpResult> Delete(string id, LibraryHost host, string? project = null) {
        if (host.Library is not { } library) {
            return Refusals.NoLibraryOpen();
        }

        if (Folder(library, project) is not { } folder) {
            return NoProject(project);
        }

        string file;
        try {
            file = ExportPresetWriter.FileOf(folder, id);
        } catch (ExportPresetException) {
            return Missing(id);
        }

        if (File.Exists(file)) {
            File.Delete(file);
            return TypedResults.NoContent();
        }

        return ExportPresetCatalog.Read(null).Find(id) is not null
            ? Refusals.Problem(
                $"'{id}' is a factory preset, which cannot be removed.",
                StatusCodes.Status409Conflict,
                Refusals.PresetFactory,
                new Dictionary<string, string> { ["preset"] = id }
            )
            : Missing(id);
    }

    static Results<Ok<ExportPresetCheck>, ProblemHttpResult> Check(ExportPresetCheckRequest request, LibraryHost host) {
        if (host.Library is not { } library) {
            return Refusals.NoLibraryOpen();
        }

        if (request.Project is not null && Folder(library, request.Project) is null) {
            return NoProject(request.Project);
        }

        var text = ExportPresetWriter.Text(request.Preset);
        try {
            var preset = ExportPresetWriter.Check(request.Preset);
            return TypedResults.Ok(new ExportPresetCheck(null, Names(library, request.Project, preset), text));
        } catch (ExportPresetException e) {
            return TypedResults.Ok(new ExportPresetCheck(e.Message, [], text));
        }
    }

    static Results<Ok<ExportPresetSource>, ProblemHttpResult> Import(ExportPresetImport request, LibraryHost host) {
        if (host.Library is not { } library) {
            return Refusals.NoLibraryOpen();
        }

        if (Folder(library, request.Project) is not { } folder) {
            return NoProject(request.Project);
        }

        if (!File.Exists(request.File)) {
            return TypedResults.Problem($"There is no file '{request.File}'.", statusCode: StatusCodes.Status404NotFound);
        }

        try {
            var read = ExportPresetReader.Read(SharedFile.ReadAllText(request.File), Path.GetFileName(request.File));
            var file = ExportPresetWriter.FileOf(folder, read.Id);
            LibraryFiles.Replace(file, SharedFile.ReadAllText(request.File));
            var origin = request.Project is null
                ? ExportPresetCatalog.Origins.Library
                : ExportPresetCatalog.Origins.Project;
            return TypedResults.Ok(SourceOf(read with { Path = file, Origin = origin }));
        } catch (ExportPresetException e) {
            return Invalid(Path.GetFileNameWithoutExtension(request.File), e.Message);
        }
    }

    /// <summary>
    ///     Each file a preset writes for a project, named as a build would name it: every set, and the set's name in
    ///     the file when there are several. With no project, a project called by the preset's id.
    /// </summary>
    static List<ExportPresetFileName> Names(OpenLibrary library, string? project, ExportPreset preset) {
        var contents = project is null ? null : library.Contents(project);
        var values = contents is null
            ? new NamingValues(preset.Id, preset.Id)
            : ProjectNaming.Of(library, project!, contents.Name, preset.Id);
        IReadOnlyList<string> sets = contents is { ExportSets.Count: > 0 } ? contents.ExportSets : [string.Empty];
        var several = sets.Count > 1;
        return [
            ..sets.SelectMany(set => preset.Textures.Select(texture => new ExportPresetFileName(
                        texture.File,
                        set,
                        NamingTemplate.Resolve(
                                several ? NamingTemplate.PerSet(texture.File) : texture.File,
                                values.Of(set) with {
                                    Space = texture.Space,
                                    Size = preset.Size is { } side ? (int)Math.Round(side * texture.Scale) : 0
                                },
                                ExportFormats.ExtensionOf(texture.Format)
                            )
                            .Replace(Path.DirectorySeparatorChar, '/')
                    )
                )
            )
        ];
    }

    static ExportPresetSource SourceOf(ExportPreset preset) =>
        new(ExportPresetWriter.DocumentOf(preset), preset.Origin, preset.Path, ExportPresetCatalog.TextOf(preset));

    /// <summary>The folder a preset is kept in: the library's, or a project's, or null when there is no such project.</summary>
    static string? Folder(OpenLibrary library, string? project) {
        if (project is null) {
            return ExportPresetCatalog.LibraryFolder(library.RootPath);
        }

        try {
            library.Contents(project);
            return ExportPresetCatalog.ProjectFolder(library.RootPath, project);
        } catch (Exception e) when (e is FileNotFoundException or ArgumentException or LibraryFormatException) {
            return null;
        }
    }

    static ProblemHttpResult NoProject(string? project) =>
        TypedResults.Problem($"There is no project '{project}'.", statusCode: StatusCodes.Status404NotFound);

    static ProblemHttpResult Missing(string id) =>
        Refusals.Problem(
            $"There is no preset '{id}' here.",
            StatusCodes.Status404NotFound,
            Refusals.PresetMissing,
            new Dictionary<string, string> { ["preset"] = id }
        );

    static ProblemHttpResult Invalid(string id, string reason) =>
        Refusals.Problem(
            reason,
            StatusCodes.Status422UnprocessableEntity,
            Refusals.PresetInvalid,
            new Dictionary<string, string> { ["preset"] = id, ["reason"] = reason }
        );
}
