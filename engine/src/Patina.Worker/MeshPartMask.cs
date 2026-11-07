using Patina.Ipc;
using Patina.Viewport;
using Vixen.Core.Imaging;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>A picture of where some of a model's named meshes lie in its layout, which a layer's mask reads.</summary>
static class MeshPartMask {
    /// <summary>How far past an island it runs, as paint does, so a seam does not show the layer below.</summary>
    const int Gutter = 4;

    const int BytesPerTexel = 4;

    public static void Draw(MeshPartMaskAsk ask) {
        var side = ask.Side;
        var covered = PaintCoverage.FromTriangles(side, side, ViewportGeometry.Layout(ask.Mesh, [..ask.Parts]));
        string[] others = [..ViewportGeometry.Parts(ask.Mesh, string.Empty).Except(ask.Parts)];
        var left = PaintCoverage.FromTriangles(side, side, ViewportGeometry.Layout(ask.Mesh, others));
        var held = new bool[side * side];
        for (var at = 0; at < held.Length; at++) {
            held[at] = covered.IsCovered(at);
        }

        for (var pass = 0; pass < Gutter; pass++) {
            held = Grown(held, side, left);
        }

        var pixels = new byte[held.Length * BytesPerTexel];
        for (var at = 0; at < held.Length; at++) {
            var shade = held[at] ? byte.MaxValue : byte.MinValue;
            pixels[at * BytesPerTexel] = shade;
            pixels[at * BytesPerTexel + 1] = shade;
            pixels[at * BytesPerTexel + 2] = shade;
            pixels[at * BytesPerTexel + 3] = byte.MaxValue;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ask.Output)!);
        PngCodec.Save(ask.Output, new(side, side, pixels));
    }

    /// <summary>One texel further, stopping at the parts left out, or the gutter would colour their edges.</summary>
    static bool[] Grown(bool[] held, int side, PaintCoverage left) {
        var grown = (bool[])held.Clone();
        Parallel.For(
            0,
            side,
            y => {
                for (var x = 0; x < side; x++) {
                    var at = y * side + x;
                    grown[at] |= !left.IsCovered(at)
                        && ((x > 0 && held[at - 1])
                            || (x < side - 1 && held[at + 1])
                            || (y > 0 && held[at - side])
                            || (y < side - 1 && held[at + side]));
                }
            }
        );
        return grown;
    }
}
