namespace Patina.Contracts;

/// <summary>Writes each channel of a stack to a file.</summary>
/// <param name="Folder">Where, absolute or relative to the library.</param>
/// <param name="Layer">
///     One layer to write on its own, such as a flattened folder, composited alone over the channels' defaults, or
///     null for the whole stack. Its files are named after it.
/// </param>
public sealed record StackExportRequest(string Folder, string? Layer = null);

/// <summary>A digest of a map's pixels, so that two pictures can be compared with neither crossing the API.</summary>
/// <param name="Usage">Which channel.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Digest">SHA-256 of its pixels as 8-bit RGBA, row by row.</param>
/// <param name="File">Where it was written, for an export.</param>
public sealed record StackPixels(string Usage, int Width, int Height, string Digest, string? File = null);
