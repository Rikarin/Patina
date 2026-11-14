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
                "A picture the library holds, as a client can show it: a PNG or a JPEG as its file, and any other "
                + "picture as an 8-bit PNG of it, such as a TIFF, a TGA, a PSD or an EXR (#260). With side, any "
                + "picture as a PNG whose longer side is at most that many texels, for a thumbnail (#269)."
            );
    }

    /// <summary>
    ///     Brings a picture into a project once it has read whole, as an import does. A path below the library that
    ///     climbs out of it is not read at all: the library refuses it.
    /// </summary>
    static async Task<Results<Ok<ProjectPicture>, ProblemHttpResult>> AddPicture(
        LibraryHost host,
        PicturePreviews previews,
        ProjectPictureRequest request,
        CancellationToken cancellationToken
    ) {
        var file = request.Picture;
        if (host.Library is { } open && !Path.IsPathFullyQualified(file)) {
            var root = Path.GetFullPath(open.RootPath);
            file = Path.GetFullPath(file, root);
            if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
                file = string.Empty;
            }
        }

        return await LibraryPictures.UnreadableAsync(previews, file, cancellationToken) is { } refused
            ? refused
            : LibraryTreeEndpoints.Guarded(
                host,
                library => TypedResults.Ok(new ProjectPicture(library.AddPicture(request.Project, request.Picture)))
            );
    }

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
    ///     A picture file below the library: a PNG or a JPEG as it is, which every client shows, and any other as a PNG
    ///     of it, kept in the cache by its content; with a side, any as a PNG no larger, for a thumbnail (#269).
    ///     ⚠ Below the library and nowhere else, and only a picture: a path that climbs out of it, or names a document,
    ///     is not a picture the library holds.
    /// </summary>
    static async Task<Results<PhysicalFileHttpResult, ProblemHttpResult>> GetPicture(
        LibraryHost host,
        PicturePreviews previews,
        string path,
        CancellationToken cancellationToken,
        int? side = null
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        var root = Path.GetFullPath(library.RootPath);
        var file = Path.GetFullPath(path, root);
        if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !PictureFormats.IsPicture(file)
            || !File.Exists(file)) {
            return TypedResults.Problem(
                $"The library holds no picture '{path}'.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        if (side is null) {
            switch (Path.GetExtension(file).ToLowerInvariant()) {
                case ".png":
                    return TypedResults.PhysicalFile(file, "image/png");
                case ".jpg" or ".jpeg":
                    return TypedResults.PhysicalFile(file, "image/jpeg");
            }
        }

        int? most = side is { } asked
            ? Math.Clamp(asked, PicturePreviews.LeastSide, PicturePreviews.MostSide)
            : null;
        return await LibraryPictures.PreviewAsync(previews, file, most, cancellationToken);
    }
}
