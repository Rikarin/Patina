using Patina.Export;
using Patina.Ipc;
using Patina.Viewport;
using Vixen.Interchange;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>
///     Model files as a brush reaches them, read once per file: the viewport's pick and every stack on that model share
///     one tree. A file rewritten in place is read again, and all of this comes back from the file after a crash.
/// </summary>
sealed class MeshShelf {
    readonly Dictionary<(string Path, string Set), (DateTime Written, Mesh Held)> held = [];

    /// <exception cref="IpcException">The file cannot be read as a mesh with a layout.</exception>
    public Mesh Of(string path) => Of(path, string.Empty);

    /// <summary>One texture set's triangles, or all of them for a set that is no material of the file.</summary>
    /// <exception cref="IpcException">The file cannot be read as a mesh with a layout.</exception>
    public Mesh Of(string path, string set) {
        try {
            var written = File.GetLastWriteTimeUtc(path);
            if (held.TryGetValue((path, set), out var known) && known.Written == written) {
                return known.Held;
            }

            var geometry = set.Length == 0 ? ViewportGeometry.Read(path) : ViewportGeometry.Read(path, set);
            if (!geometry.HasCoordinates) {
                throw new IpcException($"'{path}' has no texture coordinates, so nothing can be painted on it.");
            }

            var mesh = new Mesh(
                geometry,
                PaintProjection.Over(geometry.Positions, geometry.Coordinates, geometry.Indices),
                OneTile.Problem(
                    [..geometry.Indices.Select(index => geometry.Coordinates[index])],
                    set.Length == 0 ? $"'{Path.GetFileName(path)}'" : $"Texture set '{set}'"
                )
            );
            held[(path, set)] = (written, mesh);
            return mesh;
        } catch (Exception e) when (e is ModelFormatException or IOException or UnauthorizedAccessException) {
            throw new IpcException($"'{path}' would not read: {e.Message}");
        }
    }

    /// <summary>
    ///     The triangles an isolation draws, as a ray reaches them, and the model's own number for each (#136).
    ///     Held for the last isolation asked about, since a pick asks again for every click.
    /// </summary>
    /// <exception cref="IpcException">The file cannot be read as a mesh.</exception>
    public (PaintProjection Projection, int[] Triangles) Shown(string path, string[] parts, string[] materials) {
        var key = string.Join('\n', [path, ..parts, "", ..materials]);
        try {
            var written = File.GetLastWriteTimeUtc(path);
            if (shown is { } known && known.Key == key && known.Written == written) {
                return (known.Projection, known.Triangles);
            }

            var (geometry, triangles) = ViewportGeometry.Shown(path, parts, materials);
            var projection = PaintProjection.Over(geometry.Positions, geometry.Coordinates, geometry.Indices);
            shown = (key, written, projection, triangles);
            return (projection, triangles);
        } catch (Exception e) when (e is ModelFormatException or IOException or UnauthorizedAccessException) {
            throw new IpcException($"'{path}' would not read: {e.Message}");
        }
    }

    (string Key, DateTime Written, PaintProjection Projection, int[] Triangles)? shown;

    /// <summary>One model.</summary>
    /// <param name="Geometry">Its triangles.</param>
    /// <param name="Projection">What a ray or a brush reaches them through.</param>
    /// <param name="Tiles">Why its layout reaches past UDIM tile 1001, which Patina cannot paint, or null.</param>
    internal sealed record Mesh(ViewportGeometry Geometry, PaintProjection Projection, string? Tiles);
}
