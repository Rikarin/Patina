using Patina.Ipc;

namespace Patina.Server.Stacks;

/// <summary>What reading a stack's maps asks for.</summary>
/// <param name="Clean">Whether to evaluate from nothing rather than read what the strokes left.</param>
/// <param name="Folder">Where to write each map, or null to write nothing.</param>
public sealed record StackReading(bool Clean, string? Folder) {
    /// <summary>Whether to read what the viewport samples rather than evaluate anything.</summary>
    public bool Sampled { get; init; }

    /// <summary>One layer to evaluate alone, or null for the whole set.</summary>
    public string? Layer { get; init; }

    /// <summary>The file each map is written as, or null for 8-bit RGBA PNGs.</summary>
    public StackFileAsk? File { get; init; }

    /// <summary>With a layer, whether to write its mask alone.</summary>
    public bool Mask { get; init; }
}
