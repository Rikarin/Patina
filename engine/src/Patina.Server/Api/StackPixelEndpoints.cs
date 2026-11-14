using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Export;
using Patina.Export.Presets;
using Patina.Ipc;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     What a stack's maps hold: a digest of each, and the maps written as files, the whole stack's, one layer's alone
///     or one layer's mask, as PNGs or in an export's format (#47, #153).
/// </summary>
static class StackPixelEndpoints {
    const int Missing = StatusCodes.Status404NotFound;
    const int Refused = StatusCodes.Status422UnprocessableEntity;
    const int Unavailable = StatusCodes.Status503ServiceUnavailable;

    public static void MapStackPixels(this RouteGroupBuilder stacks) {
        stacks.MapGet("/{id}/pixels", GetPixels)
            .Describe(
                "GetStackPixels",
                "A digest of each map as it stands, the same one an export reports, with no pixels in the reply.",
                Missing,
                Unavailable
            );
        stacks.MapPost("/{id}/export", ExportAsync)
            .Describe(
                "ExportStack",
                "Evaluates from nothing and writes each map to a file, and says where and what they hold.",
                Missing,
                Refused,
                Unavailable
            );
    }

    static Task<Results<Ok<IReadOnlyList<StackPixels>>, ProblemHttpResult>> GetPixels(
        string id,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken,
        bool clean = false,
        bool sampled = false
    ) =>
        ReadAsync(id, sessions, evaluator, new(clean, null) { Sampled = sampled }, cancellationToken);

    static async Task<Results<Ok<IReadOnlyList<StackPixels>>, ProblemHttpResult>> ExportAsync(
        string id,
        StackExportRequest request,
        StackSessions sessions,
        StackEvaluator evaluator,
        CancellationToken cancellationToken
    ) {
        if (Problem(request) is { } problem) {
            return Refusals.Problem(
                problem,
                StatusCodes.Status400BadRequest,
                Refusals.ExportSettingsInvalid,
                new Dictionary<string, string> { ["reason"] = problem }
            );
        }

        try {
            var reading = new StackReading(true, sessions.Resolve(request.Folder)) {
                Layer = request.Layer, File = FileOf(request), Mask = request.Mask == true
            };
            return await ReadAsync(id, sessions, evaluator, reading, cancellationToken);
        } catch (StackEditRefusedException e) {
            return Refusals.Of(e, Refused);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            return Refusals.Unwritable(e, Unavailable);
        }
    }

    /// <summary>Why an export cannot be what it asks, or null.</summary>
    static string? Problem(StackExportRequest request) {
        if (request.Mask == true && request.Layer is null) {
            return "A mask is exported with its layer; name the layer.";
        }

        if (request.Format is { } format) {
            return MapFile.Problem(format, request.Compression, request.Quality);
        }

        return request.Compression is not null || request.Quality is not null || request.Mips == true
            ? "A compression, a quality or mips go with a format; the default PNG takes none."
            : null;
    }

    /// <summary>The file each map is written as, what the request left out filled in, or null for PNGs.</summary>
    static StackFileAsk? FileOf(StackExportRequest request) =>
        request.Format is { } format
            ? new(
                format,
                request.Compression ?? ExportCompressions.None,
                MapFile.Quality(format, request.Quality),
                request.Mips == true
            )
            : null;

    static async Task<Results<Ok<IReadOnlyList<StackPixels>>, ProblemHttpResult>> ReadAsync(
        string id,
        StackSessions sessions,
        StackEvaluator evaluator,
        StackReading reading,
        CancellationToken cancellationToken
    ) {
        if (sessions.Find(id) is not { } session) {
            return StackEndpoints.NoSession(id);
        }

        if (reading.Layer is { } layer && !session.Has(layer)) {
            return Refusals.Problem(
                "That layer is not in this document any more.",
                Refused,
                Refusals.StackLayerMissing,
                new Dictionary<string, string> { ["layer"] = layer }
            );
        }

        try {
            return TypedResults.Ok(await evaluator.PixelsAsync(session, reading, cancellationToken));
        } catch (TimeoutException e) {
            return Refusals.Of(e, Unavailable);
        } catch (IpcException e) {
            return Refusals.OfEngine(e, Refused);
        }
    }
}
