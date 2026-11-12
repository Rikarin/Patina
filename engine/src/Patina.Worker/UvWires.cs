using Patina.Ipc;
using Patina.Viewport;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Vixen.Core.Mathematics;
using Vixen.Interchange;

namespace Patina.Worker;

/// <summary>
///     A texture set's UV layout as lines (#137), for a 2D view to lay over its map: each edge of its triangles once,
///     in an SVG whose box is the unit square, so it scales with the map at any size and a line stays a pixel wide.
/// </summary>
/// <remarks>
///     ⚠ <b>Into the preview folder, not over the socket.</b> A layout of a hundred thousand triangles is megabytes of
///     numbers, and the socket carries hashes; the drawing is kept under its own, as a node's picture is.
/// </remarks>
static class UvWires {
    /// <summary>
    ///     How finely an end is placed, as decimals of the unit square: a texel of a 16K map is six of them, and two
    ///     corners nearer than this are one end, so an edge two triangles share is drawn once.
    /// </summary>
    const int Decimals = 6;

    /// <summary>mesh.uv-wire: the layout drawn into the preview folder, answered with its hash.</summary>
    /// <exception cref="IpcException">It names nothing, or the mesh would not read.</exception>
    public static IpcMessage Answer(IpcMessage request) {
        var ask = request.UvWire ?? throw new IpcException("mesh.uv-wire names no mesh.");
        ViewportGeometry geometry;
        try {
            geometry = ViewportGeometry.Read(ask.Mesh, ask.Set, ask.UvSet);
        } catch (Exception e) when (e is ModelFormatException or IOException or UnauthorizedAccessException) {
            throw new IpcException($"'{Path.GetFileName(ask.Mesh)}' would not read: {e.Message}");
        }

        var svg = Encoding.UTF8.GetBytes(Draw(geometry.Coordinates, geometry.Indices));
        var hash = Convert.ToHexStringLower(SHA256.HashData(svg));
        GraphPreviews.Store(ask.PreviewFolder, hash, svg, GraphPreviewFiles.Drawing);
        return GraphHost.Drawn(hash);
    }

    /// <summary>The edges of these triangles in UV space, each once, as an SVG over the unit square.</summary>
    public static string Draw(IReadOnlyList<Vector2> coordinates, IReadOnlyList<int> indices) {
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(indices);
        HashSet<(string, string)> drawn = [];
        var path = new StringBuilder();
        for (var corner = 0; corner + 2 < indices.Count; corner += 3) {
            for (var side = 0; side < 3; side++) {
                var a = Place(coordinates[indices[corner + side]]);
                var b = Place(coordinates[indices[corner + (side + 1) % 3]]);
                if (a != b && drawn.Add(string.CompareOrdinal(a, b) < 0 ? (a, b) : (b, a))) {
                    path.Append('M').Append(a).Append('L').Append(b);
                }
            }
        }

        // Once, drawn twice: a dark line under a light one, so it reads on a pale map and a dark one alike.
        return "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1 1\" preserveAspectRatio=\"none\">"
            + $"<defs><path id=\"wire\" fill=\"none\" vector-effect=\"non-scaling-stroke\" d=\"{path}\"/></defs>"
            + "<use href=\"#wire\" stroke=\"#000000\" stroke-opacity=\"0.45\" stroke-width=\"3\"/>"
            + "<use href=\"#wire\" stroke=\"#ffffff\" stroke-opacity=\"0.85\" stroke-width=\"1\"/></svg>";
    }

    /// <summary>A corner as the drawing says it: u across and v down, as the map lies.</summary>
    static string Place(Vector2 coordinate) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Math.Round(coordinate.X, Decimals)} {Math.Round(coordinate.Y, Decimals)}"
        );
}
