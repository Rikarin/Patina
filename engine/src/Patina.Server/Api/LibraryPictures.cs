using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;

namespace Patina.Server.Api;

/// <summary>
///     A library's pictures as a client meets them (#260): shown, whatever they are stored as, and read whole before
///     they are brought in, so a broken one is refused at the door and not found by the layer that reads it. The Worker
///     decodes them (#293), and only once for a picture's content (#269).
/// </summary>
static class LibraryPictures {
    /// <summary>A picture as a PNG a client shows, at most so large, or the refusal of one that won't read.</summary>
    public static async Task<Results<PhysicalFileHttpResult, ProblemHttpResult>> PreviewAsync(
        PicturePreviews previews,
        string file,
        int? side,
        CancellationToken cancellationToken
    ) {
        try {
            var kept = await previews.PreviewAsync(file, side, cancellationToken);
            return TypedResults.PhysicalFile(kept, "image/png");
        } catch (Exception e) when (Refused(file, e) is { } refused) {
            return refused;
        }
    }

    /// <summary>
    ///     The refusal of a file named as a picture that does not read as one, or null for one that does, for a file
    ///     that is no picture, which something else judges, and for one that is not there, which the import says.
    /// </summary>
    public static async Task<ProblemHttpResult?> UnreadableAsync(
        PicturePreviews previews,
        string file,
        CancellationToken cancellationToken
    ) {
        if (!PictureFormats.IsPicture(file) || !File.Exists(file)) {
            return null;
        }

        try {
            await previews.CheckAsync(file, cancellationToken);
            return null;
        } catch (Exception e) when (Refused(file, e) is { } refused) {
            return refused;
        }
    }

    /// <summary>
    ///     A picture that does not read, or a Worker that did not come or could not read the file, as a refusal; null
    ///     for anything else.
    /// </summary>
    internal static ProblemHttpResult? Refused(string file, Exception e) =>
        e switch {
            PictureUnreadableException => Refusals.Problem(
                $"{Path.GetFileName(file)} does not read as a picture: {e.Message}",
                StatusCodes.Status422UnprocessableEntity,
                Refusals.PictureUnreadable,
                new Dictionary<string, string> { ["file"] = Path.GetFileName(file), ["reason"] = e.Message }
            ),
            TimeoutException timeout => Refusals.Of(timeout, StatusCodes.Status503ServiceUnavailable),
            IpcException => Refusals.OfEngine(e, StatusCodes.Status422UnprocessableEntity),
            _ => null
        };
}
