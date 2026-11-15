namespace Patina.Contracts;

/// <summary>The body of <c>POST /api/v1/graphs/{id}/sample</c>: a place on one node's picture (#184).</summary>
/// <param name="Node">The node.</param>
/// <param name="Port">Its output port, or the usage of an Output node.</param>
/// <param name="U">Across the picture, 0 at its left edge and 1 at its right.</param>
/// <param name="V">Down it, 0 at its top and 1 at its bottom.</param>
public sealed record GraphSampleRequest(int Node, string Port, double U, double V);

/// <summary>
///     One texel of a node's picture as the graph stored it, at the size the graph computes it: its numbers at the
///     precision of its format, unclamped, where a preview's PNG holds eight bits of each lane between 0 and 1.
/// </summary>
/// <param name="Node">The node.</param>
/// <param name="Port">Its output port, or the usage of an Output node.</param>
/// <param name="X">The texel's column.</param>
/// <param name="Y">Its row, from the top.</param>
/// <param name="Width">The picture's width, which is the size the graph computes it.</param>
/// <param name="Height">Its height.</param>
/// <param name="Format">
///     How the picture is stored: <c>R8</c>, <c>Rg8</c>, <c>Rgba8</c>, <c>R16Float</c>, <c>Rgba16Float</c>,
///     <c>R32Float</c> or <c>Rgba32Float</c>.
/// </param>
/// <param name="Values">The texel's lanes as stored: one for a grey, two for a normal's, four for a colour.</param>
// SK7005: a wire record's constructor is its JSON shape, one parameter per field, so fewer parameters means a new API.
#pragma warning disable SK7005
public sealed record GraphSample(
    int Node,
    string Port,
    int X,
    int Y,
    int Width,
    int Height,
    string Format,
    IReadOnlyList<double> Values
);
#pragma warning restore SK7005
