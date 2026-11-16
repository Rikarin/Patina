using Patina.Ipc;

namespace Patina.Server.Viewport;

/// <summary>What the viewport draws from the preview cache, so a trim leaves it there for a new Worker (#300).</summary>
public sealed partial class ViewportSession {
    /// <summary>
    ///     The hashes of the cache files the viewport draws: the maps, a node shown alone, the look, a picture and a
    ///     graph's panorama lighting the model.
    /// </summary>
    public IReadOnlyList<string> PreviewHashes {
        get {
            lock (changing) {
                ViewportMapFile?[] files = [
                    maps.BaseColor, maps.Normal, maps.Roughness, maps.Metallic, maps.Occlusion, maps.Emissive,
                    maps.Opacity, node?.File, display.Look, display.Picture, display.Environment
                ];
                return [..files.OfType<ViewportMapFile>().Select(static file => file.Hash)];
            }
        }
    }
}
