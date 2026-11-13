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
    readonly Dictionary<(string Path, string Set, int UvSet, string Shown), (DateTime Written, Mesh Held)> held = [];

    /// <exception cref="IpcException">The file cannot be read as a mesh with a layout.</exception>
    public Mesh Of(string path) => Of(path, string.Empty);

    /// <summary>
    ///     One texture set's triangles, laid out in the coordinate set it chose (#210), and of those only the ones an
    ///     isolation shows when it names any parts or materials (#136).
    /// </summary>
    /// <exception cref="IpcException">The file cannot be read as a mesh with that layout.</exception>
    public Mesh Of(string path, string set, int uvSet = 0, string[]? parts = null, string[]? materials = null) {
        var (named, sets) = (parts ?? [], materials ?? []);
        var shown = string.Join('\n', [..named, string.Empty, ..sets]);
        try {
            var written = File.GetLastWriteTimeUtc(path);
            if (held.TryGetValue((path, set, uvSet, shown), out var known) && known.Written == written) {
                return known.Held;
            }

            var geometry = set.Length == 0 && shown.Length == 0
                ? ViewportGeometry.Read(path)
                : ViewportGeometry.Read(path, set, uvSet, named, sets);
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
            held[(path, set, uvSet, shown)] = (written, mesh);
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
        var key = string.Join('\n', [path, ..parts, string.Empty, ..materials]);
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

    /// <summary>
    ///     The whole model with each worn material laid out in the coordinate set it chose (#210), which a pick reads
    ///     its place on the map from. Held for the last layouts asked about, since a pick asks again for every click.
    /// </summary>
    /// <exception cref="IpcException">The file cannot be read as a mesh with those layouts.</exception>
    public ViewportGeometry Laid(string path, IReadOnlyDictionary<string, int> uvSets) {
        var named = uvSets.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => $"{pair.Key}={pair.Value}");
        var key = string.Join('\n', [path, ..named]);
        try {
            var written = File.GetLastWriteTimeUtc(path);
            if (laid is { } known && known.Key == key && known.Written == written) {
                return known.Geometry;
            }

            var geometry = ViewportGeometry.Read(path, uvSets);
            laid = (key, written, geometry);
            return geometry;
        } catch (Exception e) when (e is ModelFormatException or IOException or UnauthorizedAccessException) {
            throw new IpcException($"'{path}' would not read: {e.Message}");
        }
    }

    (string Key, DateTime Written, ViewportGeometry Geometry)? laid;

    /// <summary>One model.</summary>
    /// <param name="Geometry">Its triangles.</param>
    /// <param name="Projection">What a ray or a brush reaches them through.</param>
    /// <param name="Tiles">Why its layout reaches past UDIM tile 1001, which Patina cannot paint, or null.</param>
    internal sealed record Mesh(ViewportGeometry Geometry, PaintProjection Projection, string? Tiles);
}
