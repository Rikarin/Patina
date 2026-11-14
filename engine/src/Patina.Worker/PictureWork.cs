using Patina.Export;
using Patina.Export.Builds;
using Patina.Ipc;

namespace Patina.Worker;

/// <summary>
///     What decodes a picture for the Server, which decodes none (#293, plan §3.1): a library picture read whole or shown
///     as a PNG, a bake's ID colours, and a compression preview. None of it uses the device or keeps anything, so it
///     is answered beside the queue.
/// </summary>
static class PictureWork {
    public static IpcMessage Answer(IpcMessage request, CancellationToken cancellationToken) =>
        request.Type switch {
            IpcTypes.PictureRead => Read(request),
            IpcTypes.BakeIdColours => IdColoursOf(request),
            _ => Compress(request, cancellationToken)
        };

    static IpcMessage Read(IpcMessage request) {
        var ask = request.PictureRead ?? throw new IpcException("picture.read names no picture.");
        return new() {
            Type = IpcTypes.Ok,
            Id = request.Id,
            PictureReadEnd = new(PictureReads.Read(ask.File, ask.Preview, ask.Side))
        };
    }

    static IpcMessage IdColoursOf(IpcMessage request) {
        var ask = request.IdColoursAsk ?? throw new IpcException("bake.id-colours names no bake.");
        return new() { Type = IpcTypes.Ok, Id = request.Id, IdColours = IdColours.Read(ask.Folder, ask.U, ask.V) };
    }

    static IpcMessage Compress(IpcMessage request, CancellationToken cancellationToken) {
        var step = request.CompressionStep ?? throw new IpcException("export.compress plans nothing.");
        return new() {
            Type = IpcTypes.Ok,
            Id = request.Id,
            CompressionPreview = ExportPreviews.Known(step) ?? ExportPreviews.Run(step, cancellationToken)
        };
    }
}
