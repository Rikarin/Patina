using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;

namespace Patina.Server.Api;

/// <summary>
///     The pictures and meshes a library holds, as a client picks them where a dialog of its own machine cannot see
///     this one's disk (ADR 0033): listed, served, and a picture brought into a project by its path below the library.
/// </summary>
static class LibraryFileEndpoints {
    public static void MapLibraryFiles(this RouteGroupBuilder library) {
        library.MapPost("/project/pictures", AddPicture)
            .Described("AddProjectPicture", "Brings a picture into a project, for a layer or a mask to read.");

        library.MapGet("/files", ListFiles)
            .Described(
                "ListLibraryFiles",
                "The pictures or meshes the library's projects hold, a project's own first: what a client offers to "
                + "pick from, where a dialog of its own machine cannot see this one's disk."
            );

        library.MapGet("/picture", GetPicture)
            .Produces<byte[]>(StatusCodes.Status200OK, "image/png")
            .Described(
                "GetLibraryPicture",
                "A picture the library holds, as its file: what a client shows of a projection or a stencil it frames."
            );
    }

    static Results<Ok<ProjectPicture>, ProblemHttpResult> AddPicture(LibraryHost host, ProjectPictureRequest request) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => TypedResults.Ok(new ProjectPicture(library.AddPicture(request.Project, request.Picture)))
        );

    /// <summary>How many files a list of the library's holds at most, which a person can still look through.</summary>
    const int MaxListedFiles = 1000;

    /// <summary>The pictures or the meshes the library holds, by kind: <c>picture</c> or <c>mesh</c>.</summary>
    static Results<Ok<LibraryFileList>, ProblemHttpResult> ListFiles(LibraryHost host, string kind, string? project) {
        Func<string, bool>? wanted = kind switch {
            "picture" => PictureFormats.IsPicture,
            "mesh" => MeshFormats.IsMesh,
            _ => null
        };
        return wanted is null
            ? Refusals.Problem(
                $"'{kind}' is no kind of file the library lists: picture or mesh.",
                StatusCodes.Status400BadRequest,
                Refusals.FileKindUnknown,
                new Dictionary<string, string> { ["kind"] = kind }
            )
            : LibraryTreeEndpoints.Guarded(
                host,
                library => TypedResults.Ok(library.Files(wanted, project, MaxListedFiles))
            );
    }

    /// <summary>
    ///     A picture file below the library, served as it is. ⚠ Below the library and nowhere else, and only a picture:
    ///     a path that climbs out of it, or names a document, is not a picture the library holds.
    /// </summary>
    static Results<PhysicalFileHttpResult, ProblemHttpResult> GetPicture(LibraryHost host, string path) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        var root = Path.GetFullPath(library.RootPath);
        var file = Path.GetFullPath(path, root);
        var type = Path.GetExtension(file).ToLowerInvariant() switch {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => null
        };
        if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || type is null
            || !File.Exists(file)) {
            return TypedResults.Problem(
                $"The library holds no picture '{path}'.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return TypedResults.PhysicalFile(file, type);
    }
}
