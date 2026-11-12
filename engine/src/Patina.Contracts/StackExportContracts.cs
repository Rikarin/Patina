namespace Patina.Contracts;

/// <summary>Writes each channel of a stack to a file.</summary>
/// <param name="Folder">Where, absolute or relative to the library.</param>
/// <param name="Layer">
///     One layer to write on its own, such as a flattened folder, composited alone over the channels' defaults, or
///     null for the whole stack. Its files are named after it.
/// </param>
public sealed record StackExportRequest(string Folder, string? Layer = null) {
    /// <summary>
    ///     The file format of every map, as <c>GET /export/presets</c> lists them, or null for 8-bit RGBA PNGs, which
    ///     hold exactly the pixels <see cref="StackPixels.Digest" /> is of. A map keeps its own lanes where the format
    ///     holds them, and otherwise the nearest it does: a JPEG of a color with alpha drops the alpha.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>What a KTX2 or DDS file holds: none, half, float or bc1 to bc7. Null is none.</summary>
    public string? Compression { get; init; }

    /// <summary>For JPEG, 1 to 100; for BC6H and BC7, fast, balanced or best. Null is the format's default.</summary>
    public string? Quality { get; init; }

    /// <summary>Whether a KTX2 or DDS file carries a mip chain.</summary>
    public bool? Mips { get; init; }

    /// <summary>
    ///     With <see cref="Layer" />, whether to write the layer's mask alone rather than the layer: one map,
    ///     <c>mask</c>, white where the layer shows, with the mask's own layers and effects, even while it is turned off.
    /// </summary>
    public bool? Mask { get; init; }
}

/// <summary>A digest of a map's pixels, so that two pictures can be compared with neither crossing the API.</summary>
/// <param name="Usage">Which channel.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Digest">SHA-256 of its pixels as 8-bit RGBA, row by row.</param>
/// <param name="File">Where it was written, for an export.</param>
public sealed record StackPixels(string Usage, int Width, int Height, string Digest, string? File = null);
