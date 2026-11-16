using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;

namespace Patina.Server.Viewport;

/// <summary>
///     The pictures laid on the mesh, as a client names them and as the Worker reads them: paths, and paths with the
///     hash of what is in each, so a file rewritten in place is read again.
/// </summary>
static class ViewportMapFiles {
    /// <summary>The pictures a client named, each hashed.</summary>
    /// <exception cref="FileNotFoundException">A file is not there.</exception>
    public static ViewportMapSet Hashed(ViewportMapPaths paths) =>
        new(
            Hash(paths.BaseColor),
            Hash(paths.Normal),
            Hash(paths.Roughness),
            Hash(paths.Metallic),
            Hash(paths.Occlusion),
            Hash(paths.Emissive),
            Hash(paths.Opacity)
        ) {
            Layers = paths.Layers is { } layers
                ? new(
                    Hash(layers.Coat),
                    Hash(layers.CoatRoughness),
                    Hash(layers.Sheen),
                    Hash(layers.SheenColor),
                    Hash(layers.SheenRoughness),
                    Hash(layers.Transmission)
                )
                : null
        };

    /// <summary>The pictures laid, as a client names them.</summary>
    public static ViewportMapPaths Paths(ViewportMapSet set) =>
        new(
            set.BaseColor?.Path,
            set.Normal?.Path,
            set.Roughness?.Path,
            set.Metallic?.Path,
            set.Occlusion?.Path,
            set.Emissive?.Path,
            set.Opacity?.Path,
            set.Layers is { } layers
                ? new(
                    layers.Coat?.Path,
                    layers.CoatRoughness?.Path,
                    layers.Sheen?.Path,
                    layers.SheenColor?.Path,
                    layers.SheenRoughness?.Path,
                    layers.Transmission?.Path
                )
                : null
        );

    /// <summary>A file and the hash of what is in it, or null for no file.</summary>
    /// <exception cref="FileNotFoundException">The file is not there.</exception>
    public static ViewportMapFile? Hash(string? path) {
        if (path is null) {
            return null;
        }

        var full = Path.GetFullPath(path);
        return File.Exists(full)
            ? new(full, Revision.OfFile(full).Text)
            : throw new FileNotFoundException($"There is no file at '{path}'.", path);
    }
}
