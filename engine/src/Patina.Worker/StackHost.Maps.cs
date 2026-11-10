using Patina.Contracts;
using Patina.Ipc;
using Vixen.Core.Imaging;
using Vixen.TextureGraph;
using Vixen.Texturing.Layers;

namespace Patina.Worker;

/// <summary>What a stack's maps hold, as digests and as files, and one layer of it evaluated alone (#107).</summary>
sealed partial class StackHost {
    /// <summary>
    ///     Each map of an evaluation, as a digest, and as a file named after the stack when a folder is given.
    /// </summary>
    static List<StackPixels> Read(LayerStackEvaluator run, TextureBake bake, string name, string? folder) {
        var outputs = run.Outputs;
        var maps = bake.Read([..outputs.Select(static output => output.Image)]);
        List<StackPixels> pixels = [];
        foreach (var (output, map) in outputs.Zip(maps)) {
            pixels.Add(new(output.Usage, map.Width, map.Height, Digest(map), Export(name, output.Usage, map, folder)));
        }

        return pixels;
    }

    static string? Export(string name, string usage, Bitmap map, string? folder) {
        if (folder is null) {
            return null;
        }

        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"{name}.{usage}.png");
        File.WriteAllBytes(file, PngCodec.Encode(map));
        return file;
    }

    internal sealed partial class Session {
        /// <summary>
        ///     One layer of this set evaluated alone, as the only layer of its set, over the channels' defaults: a
        ///     flattened folder composites its canvas, any other layer what it holds (#107). Its own mask and
        ///     opacity count. The caller disposes it.
        /// </summary>
        /// <param name="id">The layer.</param>
        /// <param name="name">What its files are named after: its name, with nothing a file name cannot hold.</param>
        /// <exception cref="IpcException">The layer is not here, or it does not compile alone.</exception>
        public LayerStackEvaluator Alone(string id, out string name) {
            var chosen = LayerStackEdit.SetFor(document, set)
                ?? throw new IpcException($"This stack has no texture set '{set}'.");
            var layer = LayerStackEdit.Find(document, new(set, id))
                ?? throw new IpcException($"This stack has no layer '{id}'.");
            var invalid = Path.GetInvalidFileNameChars();
            name = string.Concat(
                (layer.Name.Length > 0 ? layer.Name : layer.Id).Select(letter =>
                    invalid.Contains(letter) ? '_' : letter
                )
            );
            var alone = chosen with { Layers = [layer with { Enabled = true }] };
            LayerStackEvaluator run = new(evaluator, canvases, path, fill);
            var refused = run.Prepare(document with { Sets = [alone] }, alone, compounds.Registry, compounds.Source);
            if (refused.Count > 0 || run.Plan is null) {
                run.Dispose();
                throw new IpcException(string.Join(' ', refused));
            }

            return run;
        }
    }
}
