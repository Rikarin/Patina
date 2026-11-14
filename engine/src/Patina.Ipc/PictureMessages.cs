using Patina.Contracts;

namespace Patina.Ipc;

/// <summary>
///     A request that decodes a picture, which the Worker answers for the Server (#293): one of these, as its type
///     says.
/// </summary>
/// <param name="Read">For <see cref="IpcTypes.PictureRead" />: the picture.</param>
/// <param name="IdColours">For <see cref="IpcTypes.BakeIdColours" />: the bake.</param>
/// <param name="Compression">For <see cref="IpcTypes.ExportCompress" />: the planned preview.</param>
public sealed record PictureAsk(
    PictureReadAsk? Read = null,
    IdColoursAsk? IdColours = null,
    CompressionStep? Compression = null
);

/// <summary>What a picture request answers: the part its type asks for.</summary>
/// <param name="Unreadable">For a picture read: why it does not read as a picture, or null for one that read.</param>
/// <param name="IdColours">For a bake's ID colours: the colours, or null for no ID map.</param>
/// <param name="Compression">For a compression preview: what it measured.</param>
public sealed record PictureAnswer(
    string? Unreadable = null,
    StackIdColours? IdColours = null,
    CompressionPreviewInfo? Compression = null
);

/// <summary>A picture for the Worker to decode, and where the PNG a client is shown of it goes.</summary>
/// <param name="File">The picture.</param>
/// <param name="Preview">Where to write the PNG, or null to read it whole and write nothing.</param>
/// <param name="Side">The PNG's longer side at most, or null for the picture's own size.</param>
public sealed record PictureReadAsk(string File, string? Preview, int? Side);

/// <summary>A bake's folder whose ID map's colours are wanted, and a place on the map to ask about.</summary>
/// <param name="Folder">The bake's folder.</param>
/// <param name="U">Across the map, 0 to 1, or null.</param>
/// <param name="V">Down it, or null.</param>
public sealed record IdColoursAsk(string Folder, float? U, float? V);
