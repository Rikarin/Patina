using Patina.Contracts;
using Patina.Export;
using Patina.Export.Presets;
using Patina.Graphs;
using Patina.Ipc;
using Vixen.Core.Imaging;
using Vixen.TextureGraph;
using Vixen.Texturing.Layers;

namespace Patina.Worker;

/// <summary>What a stack's maps hold, as digests and as files, and one layer of it evaluated alone (#107).</summary>
sealed partial class StackHost {
    /// <summary>
    ///     Each map of an evaluation, as a digest, and as a file named after the stack when a folder is given: an 8-bit
    ///     RGBA PNG of the digested pixels, or the file asked for, made from the map at the depth it was evaluated at.
    /// </summary>
    static List<StackPixels> Read(
        LayerStackEvaluator run,
        TextureBake bake,
        string name,
        string? folder,
        StackFileAsk? file
    ) {
        var outputs = run.Outputs;
        var images = outputs.Select(static output => output.Image).ToArray();
        var maps = bake.Read(images);
        var raw = folder is not null && file is not null ? bake.ReadRaw(images) : null;
        List<StackPixels> pixels = [];
        for (var at = 0; at < images.Length; at++) {
            var (output, map) = (outputs[at], maps[at]);
            var written = raw is null
                ? Export(name, output.Usage, map, folder)
                : Export(name, output.Usage, RawPictures.ToFloat(raw[at]).Image, folder!, file!);
            pixels.Add(new(output.Usage, map.Width, map.Height, Digest(map), written));
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

    /// <summary>One map in the format asked for, packed and encoded as an export's file is (#47).</summary>
    static string Export(string name, string usage, FloatImage map, string folder, StackFileAsk ask) {
        byte[] bytes;
        try {
            bytes = MapFile.Encode(usage, map, ask.Format, ask.Compression, ask.Quality, ask.Mips, default);
        } catch (ArgumentException e) {
            throw new IpcException(e.Message);
        }

        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"{name}.{usage}{ExportFormats.ExtensionOf(ask.Format)}");
        File.WriteAllBytes(file, bytes);
        return file;
    }

    /// <summary>The channel a mask exported alone is written as, which Output nodes and exports both know.</summary>
    const string MaskUsage = "mask";

    internal sealed partial class Session {
        /// <summary>
        ///     One layer of this set evaluated alone, as the only layer of its set, over the channels' defaults: a
        ///     flattened folder composites its canvas, any other layer what it holds (#107). Its own mask and
        ///     opacity count. The caller disposes it. ⚠ Without the set's bake, which <see cref="Run" /> lays beneath
        ///     the layers (#86): the bake is the set's and not the layer's, and a normal combined with it would no
        ///     longer be what the folder paints.
        /// </summary>
        /// <param name="id">The layer.</param>
        /// <param name="mask">
        ///     Whether to evaluate its mask alone instead: one map, <c>mask</c>, white where the layer shows and black
        ///     where it does not, with the mask's own layers and effects and nothing of the layer's content or opacity.
        ///     It is the only layer of a set whose one channel is that map, a white fill under the layer's mask over
        ///     black, so the mask is evaluated by the same code a layer's is (#153).
        /// </param>
        /// <param name="name">What its files are named after: its name, with nothing a file name cannot hold.</param>
        /// <exception cref="IpcException">The layer is not here, or it does not compile alone.</exception>
        public LayerStackEvaluator Alone(string id, bool mask, out string name) {
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
            var alone = mask
                ? chosen with {
                    Channels = [new() { Usage = MaskUsage, Default = [0f, 0f, 0f, 1f] }],
                    Layers = [
                        new() {
                            Id = layer.Id,
                            Name = layer.Name,
                            Values = new() { [MaskUsage] = [1f, 1f, 1f, 1f] },
                            Mask = layer.Mask with { Enabled = true }
                        }
                    ]
                }
                : chosen with { Layers = [layer with { Enabled = true }] };
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
