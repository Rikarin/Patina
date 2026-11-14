namespace Patina.Ipc;

/// <summary>A picture for the Worker to decode (#293), and where the PNG a client is shown of it goes.</summary>
/// <param name="File">The picture.</param>
/// <param name="Preview">Where to write the PNG, or null to read it whole and write nothing.</param>
/// <param name="Side">The PNG's longer side at most, or null for the picture's own size.</param>
public sealed record PictureReadAsk(string File, string? Preview, int? Side);

/// <summary>How a picture read: null for one that read, or the decoder's own sentence for one that did not.</summary>
/// <param name="Unreadable">Why it does not read as a picture, or null.</param>
public sealed record PictureReadEnd(string? Unreadable);

/// <summary>A bake's folder whose ID map's colours are wanted, and a place on the map to ask about.</summary>
/// <param name="Folder">The bake's folder.</param>
/// <param name="U">Across the map, 0 to 1, or null.</param>
/// <param name="V">Down it, or null.</param>
public sealed record IdColoursAsk(string Folder, float? U, float? V);
