using Vixen.TextureGraph;
using Vixen.Texturing.Painting;

namespace Patina.Graphs;

/// <summary>
///     The canvases a graph reads as they are painted (#205), carried to the device a tile at a time. Reading a moved
///     channel again compiled the graph and uploaded all of the channel on every stroke, 64 MiB for a 4K float one; a
///     stroke now costs the tiles it touched, and no compile, as it does in the set's own layers.
/// </summary>
public sealed class GraphPaintTiles {
    readonly PaintTileCache tiles = new();

    /// <summary>How many tiles have been carried, over this object's life.</summary>
    public long TilesUploaded => tiles.TilesUploaded;

    /// <summary>Watches what a fill read from canvases being painted, in place of what the last fill read.</summary>
    /// <param name="pictures">What reads the canvases.</param>
    /// <param name="filled">The fill, whose uploads hold each channel whole.</param>
    public void Watch(GraphPictures pictures, FilledPictures filled) {
        ArgumentNullException.ThrowIfNull(pictures);
        ArgumentNullException.ThrowIfNull(filled);
        tiles.Clear();
        foreach (var live in filled.Live) {
            var stamp = live.Stamp;
            tiles.Watch(live.Image, () => pictures.Channel(stamp));
        }
    }

    /// <summary>
    ///     Carries the tiles that moved onto the uploads when every stamp that went stale is a canvas still being
    ///     painted, at the size and depth it was uploaded at.
    /// </summary>
    /// <param name="pictures">What reads the canvases.</param>
    /// <param name="stamps">What the last fill read.</param>
    /// <param name="uploads">Where it put them.</param>
    /// <returns>
    ///     The stamps as they are now, or null when something moved that only reading the pictures again can follow: a
    ///     file, a painting that ended, a channel painted for the first time, or a canvas another size.
    /// </returns>
    public IReadOnlyList<PictureStamp>? Carry(
        GraphPictures pictures,
        IReadOnlyList<PictureStamp> stamps,
        TextureUploads uploads
    ) {
        ArgumentNullException.ThrowIfNull(pictures);
        ArgumentNullException.ThrowIfNull(stamps);
        ArgumentNullException.ThrowIfNull(uploads);
        if (!stamps.Where(pictures.Stale).All(stamp => pictures.Channel(stamp) is not null) || !tiles.Fits(uploads)) {
            return null;
        }

        tiles.Sync(uploads);
        return [..stamps.Select(pictures.Restamped)];
    }
}
