namespace Patina.Ipc;

/// <summary>Asks for one texel of one picture of a loaded graph, at the size the graph computes it (#184).</summary>
/// <param name="Session">The graph.</param>
/// <param name="Node">The node.</param>
/// <param name="Port">Its output port, or the usage of an Output node.</param>
/// <param name="U">Across the picture, 0 at its left edge and 1 at its right.</param>
/// <param name="V">Down it, 0 at its top.</param>
public sealed record GraphSampleAsk(string Session, int Node, string Port, double U, double V);

/// <summary>One texel as the graph stored it: its numbers at the precision of its format, unclamped.</summary>
/// <param name="X">The texel's column.</param>
/// <param name="Y">Its row, from the top.</param>
/// <param name="Width">The picture's width, which is the size the graph computes it.</param>
/// <param name="Height">Its height.</param>
/// <param name="Format">How the picture is stored, as Vixen names the format.</param>
/// <param name="Values">The texel's lanes as stored, one to four of them.</param>
public sealed record GraphSampled(int X, int Y, int Width, int Height, string Format, IReadOnlyList<double> Values);
