using Patina.Contracts;
using Patina.Export;
using Patina.Graphs;
using Patina.Ipc;
using Patina.Library;
using Patina.Viewport;
using System.Diagnostics;
using System.Security.Cryptography;
using Vixen.Authoring;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
using Vixen.NodeGraph;
using Vixen.Terrain;
using Vixen.TextureGraph;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>What an evaluation hands the viewport: only what it changed, in the bytes an export writes.</summary>
sealed partial class StackHost {
    /// <summary>
    ///     Hands the viewport what this evaluation changed, in the bytes an export writes. Only the rectangles a repair
    ///     rewrote cross, and a picture nothing touched costs nothing. No device is shared: the bytes are in this
    ///     process, which is also why a Worker with a dead viewport still paints.
    /// </summary>
    void Feed(Session stack, TextureBake bake) {
        var material = viewport.Live.MaterialOf(stack.SetName);
        foreach (var output in stack.Run.Outputs) {
            if (Slot(output.Usage) is not { } slot) {
                continue;
            }

            var key = bake.KeyOf(output.Image);
            var known = stack.Fed.TryGetValue(slot, out var fed);
            if (known && fed == key) {
                continue;
            }

            if (known && bake.RepairOf(output.Image) is { Length: > 0 } rects) {
                TextureRect[] list = [..stack.Narrowed(rects)];
                var patches = bake.Read(output.Image, list);
                viewport.Live.RepairLive(
                    slot,
                    [
                        ..list.Zip(patches)
                            .Select(static pair => new ViewportRepair(
                                    new(pair.First.X, pair.First.Y, pair.First.Width, pair.First.Height),
                                    pair.Second.Pixels
                                )
                            )
                    ],
                    material
                );
            } else {
                var map = bake.Read(output.Image);
                viewport.Live.SetLive(slot, map.Width, map.Height, map.Pixels, material);
            }

            stack.Fed[slot] = key;
        }

        // A map the stack no longer writes leaves the viewport with it, or the old picture would stay on the mesh.
        HashSet<ViewportSlot> written =
            [..stack.Run.Outputs.Select(static output => Slot(output.Usage)).OfType<ViewportSlot>()];
        foreach (var gone in stack.Fed.Keys.Where(slot => !written.Contains(slot)).ToList()) {
            viewport.Live.ClearLive(gone, material);
            stack.Fed.Remove(gone);
        }

        FeedQuickMask(stack, material);

        stack.Reported();
    }

    static ViewportSlot? Slot(string usage) =>
        usage switch {
            "baseColor" => ViewportSlot.BaseColor,
            "normal" => ViewportSlot.Normal,
            "roughness" => ViewportSlot.Roughness,
            "metalness" => ViewportSlot.Metallic,
            "occlusion" => ViewportSlot.Occlusion,
            "emissive" => ViewportSlot.Emissive,
            "opacity" => ViewportSlot.Opacity,
            _ => null
        };

    /// <summary>What the viewport's draw call samples for each map, read back from the viewport's own device.</summary>
    List<StackPixels> Sampled(Session stack) {
        List<StackPixels> pixels = [];
        foreach (var output in stack.Run.Outputs) {
            if (Slot(output.Usage) is { } slot
                && viewport.Live.Sampled(slot, viewport.Live.MaterialOf(stack.SetName)) is { } bytes) {
                var size = stack.Run.Plan!.SizeOf(output.Image);
                pixels.Add(new(output.Usage, size.X, size.Y, Convert.ToHexStringLower(SHA256.HashData(bytes))));
            }
        }

        return pixels;
    }
}
