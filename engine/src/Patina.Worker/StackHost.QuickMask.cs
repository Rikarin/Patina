using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Viewport;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>
///     A session's quick mask (#127): a canvas the journal paints like any other, which strokes may be kept to and the
///     viewport may lay over the model in red. It is never a map of the stack, so no evaluation reads it.
/// </summary>
sealed partial class StackHost {
    /// <summary>
    ///     Lays the quick mask the Server asked to be shown over the model, or takes it off. Only the tiles a stroke
    ///     wrote since the last time cross, as a map's repair does.
    /// </summary>
    void FeedQuickMask(Session stack, int material) {
        var shown = stack.QuickMaskImage();
        if (shown is null) {
            if (stack.QuickMaskFed is not null) {
                viewport.Live.ClearLive(ViewportSlot.QuickMask, material);
                stack.QuickMaskFed = null;
            }

            return;
        }

        var (image, file) = shown.Value;
        if (stack.QuickMaskFed is { } fed && fed.File == file && fed.Image == image) {
            if (fed.Revision == image.Revision) {
                return;
            }

            List<(int TileX, int TileY)> moved = [];
            image.TilesChangedSince(fed.Revision, moved);
            List<ViewportRepair> repairs = [];
            foreach (var (x, y) in moved) {
                var rect = image.TileRect(x, y);
                var bytes = new byte[rect.Area * PaintImage.BytesPerTexel];
                image.CopyRect(rect, bytes);
                repairs.Add(new(new(rect.X, rect.Y, rect.Width, rect.Height), bytes));
            }

            viewport.Live.RepairLive(ViewportSlot.QuickMask, repairs, material);
        } else {
            viewport.Live.SetLive(ViewportSlot.QuickMask, image.Width, image.Height, image.ToTexels(), material);
        }

        stack.QuickMaskFed = new(file, image, image.Revision);
    }

    internal sealed partial class Session {
        /// <summary>The quick mask the viewport lays over the model, relative to the stack, or null.</summary>
        public string? QuickMask { get; private set; }

        /// <summary>What the viewport was last given of the quick mask, or null when it holds none.</summary>
        public QuickMaskFeed? QuickMaskFed { get; set; }

        /// <summary>Whether a paint file is a quick mask, which a save leaves out.</summary>
        static bool IsQuickMask(string file) =>
            Path.GetFileName(file).Contains(StackQuickMasks.FileMarker, StringComparison.Ordinal);

        /// <summary>The quick mask to show, or null when none is shown or nothing has painted it here yet.</summary>
        public (PaintImage Image, string File)? QuickMaskImage() {
            if (QuickMask is not { } name) {
                return null;
            }

            var file = LibraryLayout.Beside(folder, name);
            return canvases.Open(file) is { } canvas ? (canvas.Channel(PaintReference.Mask), file) : null;
        }

        /// <summary>
        ///     What a stroke is kept to: the quick mask the journal names, as this Worker holds it now, which a replay
        ///     has brought to where it stood when the stroke began. One nothing has painted here holds the stroke back
        ///     everywhere, as a mask of black would; the Server names one only once a stroke has painted it.
        /// </summary>
        PaintImageKeep? Keep(StackPaintPart part, int width, int height) {
            if (part.Keep is not { } name) {
                return null;
            }

            var file = LibraryLayout.Beside(folder, name);
            var mask = canvases.Open(file)?.Channel(PaintReference.Mask) ?? new PaintImage(width, height);
            return new PaintImageKeep(mask, width, height, part.KeepOutside);
        }
    }

    /// <summary>What the viewport holds of a quick mask: which canvas, which picture of it, and how new.</summary>
    internal sealed record QuickMaskFeed(string File, PaintImage Image, long Revision);
}
