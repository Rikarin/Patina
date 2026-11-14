using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;

namespace Patina.Server.Api;

/// <summary>
///     A library's pictures as a client meets them (#260): shown, whatever they are stored as, and read whole before
///     they are brought in, so a broken one is refused at the door and not found by the layer that reads it. Both
///     decode off the request's thread, and only once for a picture's content (#269).
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
            var kept = await Task.Run(() => previews.Preview(file, side), cancellationToken);
            return TypedResults.PhysicalFile(kept, "image/png");
        } catch (Exception e) when (IsUnreadable(e)) {
            return Refused(file, e);
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
            await Task.Run(() => previews.Check(file), cancellationToken);
            return null;
        } catch (Exception e) when (IsUnreadable(e)) {
            return Refused(file, e);
        }
    }

    /// <summary>
    ///     What a decoder throws for bytes that are not the picture they claim to be. A header that promises more than
    ///     the file holds runs a reader off its end as often as it says so.
    /// </summary>
    static bool IsUnreadable(Exception e) =>
        e is InvalidDataException
            or EndOfStreamException
            or ArgumentException
            or IndexOutOfRangeException
            or NotSupportedException
            or OverflowException;

    static ProblemHttpResult Refused(string file, Exception e) =>
        Refusals.Problem(
            $"{Path.GetFileName(file)} does not read as a picture: {e.Message}",
            StatusCodes.Status422UnprocessableEntity,
            Refusals.PictureUnreadable,
            new Dictionary<string, string> { ["file"] = Path.GetFileName(file), ["reason"] = e.Message }
        );
}
