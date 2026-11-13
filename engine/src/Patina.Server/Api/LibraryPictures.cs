using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Export;

namespace Patina.Server.Api;

/// <summary>
///     A library's pictures as a client meets them (#260): shown, whatever they are stored as, and read whole before
///     they are brought in, so a broken one is refused at the door and not found by the layer that reads it.
/// </summary>
static class LibraryPictures {
    /// <summary>A picture as a PNG a client shows, or the refusal of one that does not read.</summary>
    public static Results<PhysicalFileHttpResult, FileContentHttpResult, ProblemHttpResult> Preview(string file) {
        try {
            return TypedResults.File(MapPictures.Preview(file), "image/png");
        } catch (Exception e) when (IsUnreadable(e)) {
            return Refused(file, e);
        }
    }

    /// <summary>
    ///     The refusal of a file named as a picture that does not read as one, or null for one that does, for a file that
    ///     is no picture, which something else judges, and for one that is not there, which the import says.
    /// </summary>
    public static ProblemHttpResult? Unreadable(string file) {
        if (!PictureFormats.IsPicture(file) || !File.Exists(file)) {
            return null;
        }

        try {
            _ = MapPictures.ReadFile(file);
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
