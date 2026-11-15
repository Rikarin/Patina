using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Patina.Contracts;
using Patina.Library;
using System.Globalization;

namespace Patina.Server.Api;

/// <summary>
///     A file sent from a client's own computer (#4, ADR 0041): a client of a Server on another machine has no
///     other way to give it a picture or a mesh of its own, since everything else takes a path on the Server's
///     machine.
/// </summary>
static class UploadEndpoints {
    public static void MapUploads(this RouteGroupBuilder library) {
        library.MapPost("/uploads", Upload)
            .Accepts<Stream>("application/octet-stream")
            .WithMetadata(new RequestSizeLimitAttribute(LibraryUploads.MostBytes))
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .Described(
                "UploadLibraryFile",
                "Keeps a picture or a mesh a client sent from its own computer in the library's uploads folder, and "
                + "answers with its path on the Server's machine, which a project's mesh, a picture brought into a "
                + "project and a bake's high mesh take. The same file sent again is kept once."
            );
    }

    static async Task<Results<Ok<UploadedFile>, ProblemHttpResult>> Upload(
        LibraryHost host,
        IOptions<ServerOptions> options,
        HttpRequest request,
        string name,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        if (!LibraryUploads.Takes(name)) {
            return Refusals.Problem(
                $"'{name}' is not the name of a picture or a mesh Patina reads.",
                StatusCodes.Status400BadRequest,
                Refusals.UploadKind,
                new Dictionary<string, string> { ["name"] = name }
            );
        }

        try {
            var kept = await LibraryUploads.KeepAsync(
                library.RootPath,
                name,
                request.Body,
                cancellationToken,
                options.Value.UploadMostBytes ?? LibraryUploads.MostBytes
            );
            return TypedResults.Ok(new UploadedFile(kept));
        } catch (Exception e) when (e is UploadTooLargeException or BadHttpRequestException) {
            return TooLarge();
        } catch (IOException e) {
            return Refusals.Busy(e) ?? Refusals.Unwritable(e, StatusCodes.Status409Conflict);
        }
    }

    static ProblemHttpResult TooLarge() =>
        Refusals.Problem(
            new UploadTooLargeException().Message,
            StatusCodes.Status413PayloadTooLarge,
            Refusals.UploadTooLarge,
            new Dictionary<string, string> {
                ["most"] = (LibraryUploads.MostBytes >> 30).ToString(CultureInfo.InvariantCulture)
            }
        );
}
