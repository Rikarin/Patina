using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Graphs;

namespace Patina.Server.Api;

/// <summary>
///     Saving a picture the Server has written (#184): Save Image in the 2D View, and any client that wants a node's
///     picture as a file. The bytes go from the preview cache to the file on the Server's machine, never through JSON.
/// </summary>
static class PreviewSaveEndpoint {
    public static void MapPreviewSave(this IEndpointRouteBuilder api, string tag) =>
        api.MapPost("/previews/{hash}/save", Save)
            .WithTags(tag)
            .Describe(
                "SavePreview",
                "Writes a picture the Server has written, by its hash, to a PNG file on the Server's machine, "
                + "replacing one there. POST /graphs/{id}/images writes a node's picture at full size first.",
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity
            );

    static Results<NoContent, ProblemHttpResult> Save(string hash, PreviewSaveRequest request, PreviewStore store) {
        if (store.Find(hash) is not { } path) {
            return Refusals.Problem(
                $"There is no picture '{hash}'. Ask for the node's picture again.",
                StatusCodes.Status404NotFound,
                Refusals.FileMissing,
                new Dictionary<string, string> { ["file"] = hash }
            );
        }

        if (!Path.IsPathFullyQualified(request.File)
            || !Directory.Exists(Path.GetDirectoryName(request.File))) {
            return Refusals.Problem(
                $"'{request.File}' is not a file in a folder that exists, so the picture cannot be saved there.",
                StatusCodes.Status422UnprocessableEntity,
                Refusals.FileUnwritable,
                Refusals.Reason(request.File)
            );
        }

        try {
            var writing = request.File + ".saving";
            File.Copy(path, writing, true);
            SharedFile.MoveOver(writing, request.File);
            return TypedResults.NoContent();
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            return Refusals.Unwritable(e, StatusCodes.Status409Conflict);
        }
    }
}
