using Patina.Ipc;
using Patina.Library;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>
///     Small pictures: a layer's fill alone for the Layers pane, and a layered material's first set for the shelf.
/// </summary>
sealed partial class StackHost {
    public IpcMessage Thumbnails(IpcMessage request) {
        var ask = request.StackThumbnails ?? throw new IpcException("stack.thumbnails names no stack.");
        return new() { Type = IpcTypes.Ok, StackLayerThumbnails = Find(ask.Session).Thumbnails(previews, ask.Folder) };
    }

    /// <summary>
    ///     A layered material's first set, composited once for the shelf. Its paint is read from beside its file, as an
    ///     open stack's is, and nothing is kept but the picture.
    /// </summary>
    public IpcMessage Thumbnail(MaterialThumbnailAsk ask) {
        var document = LayerStackYaml.Read(ask.Document);
        if (document.Sets.FirstOrDefault() is not { } set) {
            return GraphHost.Drawn(null);
        }

        var shelf = compounds.Of(ask.Folder is null ? null : Path.Combine(ask.Folder, LibraryLayout.CompoundsFolder));
        SortedSet<string> unbaked = new(StringComparer.Ordinal);
        using LayerStackEvaluator run = new(
            gpu.Evaluator,
            new PaintCanvasStore(long.MaxValue),
            ask.Path,
            (uploads, plan, owed) => pictures.Fill(uploads, plan, [..owed], ask.ImageRoots, null, unbaked)
                .Refusals.Select(static refusal => refusal.Message)
                .ToArray()
        ) { Draft = true };
        if (run.Prepare(document with { Sets = [set] }, set, shelf.Registry, shelf.Source).Count > 0) {
            return GraphHost.Drawn(null);
        }

        var output = run.Outputs.FirstOrDefault(output => output.Usage == ask.Usage);
        if (output.Usage is null) {
            return GraphHost.Drawn(null);
        }

        using var bake = run.Evaluate();
        return GraphHost.Drawn(previews.Write(bake, [output.Image], ask.PreviewFolder)[0].Hash);
    }

    internal sealed partial class Session {
        const int ThumbnailSide = 64;
        const string ThumbnailMap = "baseColor";

        /// <summary>Each layer's picture by the document it was drawn from, so it is drawn once.</summary>
        readonly Dictionary<string, string> thumbnails = new(StringComparer.Ordinal);

        /// <summary>
        ///     Each fill with something to show, alone and small. It is its own stack, so a stroke's plan pays nothing,
        ///     and a layer that has not changed is not drawn again.
        /// </summary>
        public IReadOnlyList<StackLayerThumbnail> Thumbnails(PreviewWriter writer, string folder) {
            List<StackLayerThumbnail> drawn = [];
            var chosen = document.Sets.First(candidate => candidate.Name == set);
            foreach (var layer in Fills(chosen.Layers)) {
                var alone = chosen with {
                    Layers = [layer with { Mask = new(), Opacity = 1f, Blend = LayerBlendMode.Copy, Enabled = true }]
                };
                var small = document with { BaseWidth = ThumbnailSide, BaseHeight = ThumbnailSide, Sets = [alone] };
                var key = LayerStackYaml.Write(small);
                if (!thumbnails.TryGetValue(key, out var hash) && Draw(small, alone, writer, folder) is { } made) {
                    hash = thumbnails[key] = made;
                }

                if (hash is not null) {
                    drawn.Add(new(layer.Id, hash));
                }
            }

            return drawn;
        }

        string? Draw(LayerStackAsset small, TextureSetAsset alone, PreviewWriter writer, string folder) {
            using LayerStackEvaluator run = new(evaluator, canvases, path, fill) { Draft = true };
            if (run.Prepare(small, alone, compounds.Registry, compounds.Source).Count > 0) {
                return null;
            }

            var output = run.Outputs.FirstOrDefault(static output => output.Usage == ThumbnailMap);
            if (output.Usage is null) {
                return null;
            }

            using var bake = run.Evaluate();
            return writer.Write(bake, [output.Image], folder)[0].Hash;
        }
    }
}
