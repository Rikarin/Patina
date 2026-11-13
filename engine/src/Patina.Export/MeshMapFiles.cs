using Patina.Jobs;
using Vixen.Core.Imaging;
using Vixen.Geometry.Remeshing;

namespace Patina.Export;

/// <summary>How baked maps are stored: at what depth, under what name, and how they read back.</summary>
public static class MeshMapFiles {
    /// <summary>Patina's name for a map, which is the Mesh Map node's name for it too.</summary>
    public static string NameOf(MeshMapKind kind) =>
        kind switch {
            MeshMapKind.Normal => "normal",
            MeshMapKind.Displacement => "height",
            MeshMapKind.AmbientOcclusion => "ao",
            MeshMapKind.BentNormal => "bent",
            MeshMapKind.Curvature => "curvature",
            MeshMapKind.Thickness => "thickness",
            MeshMapKind.Position => "position",
            MeshMapKind.WorldNormal => "world",
            MeshMapKind.Id => "id",
            MeshMapKind.Colour => "colour",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a map.")
        };

    /// <summary>The extension a format's files have.</summary>
    public static string ExtensionOf(string format) => format is BakeInputs.Exr16 or BakeInputs.Exr32 ? ".exr" : ".png";

    /// <summary>
    ///     The file for a plane. An integer format stores it centred and scaled, and <see cref="MeshMapPlane.Scale" />
    ///     goes in the manifest. A float format stores the measurement, because nothing clips it.
    /// </summary>
    /// <exception cref="ArgumentException">The format is not one.</exception>
    public static byte[] Encode(MeshMapPlane plane, string format) {
        ArgumentNullException.ThrowIfNull(plane);
        return format switch {
            BakeInputs.Png8 => PngCodec.Encode(Rgba8(plane)),
            BakeInputs.Png16 => Png16(plane),
            BakeInputs.Exr16 => ExrCodec.Encode(
                new(plane.Resolution, plane.Rows, plane.Channels, plane.Samples),
                ExrPixels.Half
            ),
            BakeInputs.Exr32 => ExrCodec.Encode(new(plane.Resolution, plane.Rows, plane.Channels, plane.Samples)),
            _ => throw new ArgumentException($"'{format}' is not a format.", nameof(format))
        };
    }

    /// <summary>The plane as 8-bit RGBA, which is what a browser can show and a graph can read.</summary>
    public static Bitmap Rgba8(MeshMapPlane plane) {
        ArgumentNullException.ThrowIfNull(plane);
        var (width, height) = (plane.Resolution, plane.Rows);
        var pixels = new byte[width * height * 4];

        Parallel.For(
            0,
            height,
            y => {
                for (var x = 0; x < width; x++) {
                    var at = y * width + x;
                    var into = at * 4;
                    for (var lane = 0; lane < 3; lane++) {
                        var sample = plane.Channels == 1 ? at : at * plane.Channels + lane;
                        pixels[into + lane] = (byte)MathF.Round(plane.Unit(sample) * byte.MaxValue);
                    }

                    pixels[into + 3] = byte.MaxValue;
                }
            }
        );

        return new(width, height, pixels);
    }

    static byte[] Png16(MeshMapPlane plane) {
        var samples = new ushort[plane.Samples.Length];

        Parallel.For(0, samples.Length, at => samples[at] = (ushort)MathF.Round(plane.Unit(at) * ushort.MaxValue));

        return PngCodec.Encode16(plane.Resolution, plane.Rows, plane.Channels, samples);
    }
}
