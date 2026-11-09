using Patina.Export.Presets;

namespace Patina.Export;

/// <summary>
///     What an exported file takes in GPU memory: the one measure the pre-export checks (#159) and the compression
///     preview (#217) both state, so a budget and a preview never disagree about a file.
/// </summary>
public static class ExportMemory {
    /// <summary>
    ///     What one texel takes on a GPU: a block-compressed size, or each lane at its depth, three lanes as four.
    /// </summary>
    public static double BytesPerTexel(string format, string compression, int lanes) {
        var held = lanes switch {
            <= 1 => 1,
            2 => 2,
            _ => 4
        };
        return compression switch {
            ExportCompressions.Bc1 or ExportCompressions.Bc4 => 0.5,
            ExportCompressions.Bc3 or ExportCompressions.Bc5 or ExportCompressions.Bc6H or ExportCompressions.Bc7 => 1,
            ExportCompressions.Half => 2 * held,
            ExportCompressions.Wide => 4 * held,
            _ => format switch {
                ExportFormats.Png16 or ExportFormats.Tiff16 or ExportFormats.Exr16 => 2 * held,
                ExportFormats.Exr32 or ExportFormats.Tiff32 or ExportFormats.Hdr => 4 * held,
                _ => held
            }
        };
    }

    /// <summary>What one file takes at a size, with a third more for a mip chain.</summary>
    public static double Bytes(ExportTexture texture, int width, int height) {
        ArgumentNullException.ThrowIfNull(texture);
        return (double)width
            * height
            * BytesPerTexel(texture.Format, texture.Compression, texture.Channels.Count)
            * (texture.Mips ? 4d / 3d : 1d);
    }

    /// <summary>The size a file is written at from maps of a size: the preset's longest side, then its own scale.</summary>
    public static (int Width, int Height) Sized(ExportPreset preset, ExportTexture texture, int width, int height) {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(texture);
        var longest = Math.Max(width, height);
        if (longest == 0) {
            return (width, height);
        }

        var wanted = (double)(preset.Size ?? longest) * texture.Scale;
        var scale = Math.Max(1, Math.Round(wanted)) / longest;
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }
}
