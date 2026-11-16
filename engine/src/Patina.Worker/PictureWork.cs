using Patina.Export;
using Patina.Export.Builds;
using Patina.Ipc;

namespace Patina.Worker;

/// <summary>
///     What decodes a picture for the Server, which decodes none (#293, plan §3.1): a library picture read whole or
///     shown as a PNG, a bake's ID colours, and a compression preview. None of it uses the device or keeps anything,
///     so it is answered beside the queue.
/// </summary>
static class PictureWork {
    public static IpcMessage Answer(IpcMessage request, CancellationToken cancellationToken) =>
        new() { Type = IpcTypes.Ok, Id = request.Id, Pictured = Answered(request, cancellationToken) };

    static PictureAnswer Answered(IpcMessage request, CancellationToken cancellationToken) =>
        (request.Type, request.Picture) switch {
            (IpcTypes.PictureRead, { Read: { Map: true, Preview: { } map } read }) => new(
                PictureReads.Map(read.File, map, read.TurnGreen)
            ),
            (IpcTypes.PictureRead, { Read: { } read }) => new(PictureReads.Read(read.File, read.Preview, read.Side)),
            (IpcTypes.BakeIdColours, { IdColours: { } bake }) => new(
                IdColours: IdColours.Read(bake.Folder, bake.U, bake.V)
            ),
            (IpcTypes.ExportCompress, { Compression: { } step }) => new(
                Compression: ExportPreviews.Known(step) ?? ExportPreviews.Run(step, cancellationToken)
            ),
            _ => throw new IpcException($"{request.Type} names nothing to decode.")
        };
}
