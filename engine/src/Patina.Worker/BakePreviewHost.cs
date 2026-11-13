using Patina.Contracts;
using Patina.Export;
using Patina.Ipc;
using Patina.Viewport;
using Vixen.Interchange;

namespace Patina.Worker;

/// <summary>
///     Lays a bake's setup over the viewport (#149). It reads the meshes, has the shapes made and hands them to the
///     renderer's overlay, and keeps nothing: a new Worker is told the preview again by the Server.
/// </summary>
static class BakePreviewHost {
    /// <summary>
    ///     Shows the preview the message asks for, or takes it off, and answers with the key to its
    ///     colours.
    /// </summary>
    public static IpcMessage Show(IpcMessage request, ViewportRenderer renderer) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(renderer);
        var preview = request.BakePreview ?? throw new IpcException("viewport.bake-preview says nothing.");
        if (preview.Low.Length == 0) {
            renderer.SetOverlay([], []);
            renderer.Neutral = false;
            return Shown(request, BakePreviewState.Off);
        }

        BakePreviewDrawing drawing;
        try {
            drawing = BakePreviewShapes.Build(preview, request.BakePreviewIsolation);
        } catch (Exception e) when (e is ModelFormatException
                                        or InvalidDataException
                                        or IOException
                                        or UnauthorizedAccessException) {
            throw new IpcException($"The bake's setup cannot be shown: {e.Message}");
        }

        renderer.SetOverlay(drawing.Triangles, drawing.Lines);
        renderer.Neutral = preview.Neutral;
        return Shown(request, new(preview, drawing.Parts, drawing.Warnings));
    }

    static IpcMessage Shown(IpcMessage request, BakePreviewState state) =>
        new() { Id = request.Id, Type = IpcTypes.Ok, BakePreviewShown = state };
}
