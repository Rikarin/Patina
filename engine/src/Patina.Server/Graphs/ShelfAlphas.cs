using Patina.Contracts;
using Vixen.Texturing.Painting;

namespace Patina.Server.Graphs;

/// <summary>
///     The brush alphas on the shelf (#203): the shapes a brush's stamp can take, which a click makes the brush's. A
///     client draws each as a stamp of the brush in hand, so the shelf asks the Server for no picture of one.
/// </summary>
public static class ShelfAlphas {
    /// <summary>The type of an alpha on the shelf.</summary>
    public const string Role = "alpha";

    /// <summary>What an alpha's source starts with, before the name a brush takes: <c>alpha:Square</c>.</summary>
    public const string Prefix = "alpha:";

    /// <summary>Every alpha a brush can name, round first.</summary>
    public static IReadOnlyList<MaterialInfo> All { get; } = [
        ..PaintAlphas.Names.Select(static name => new MaterialInfo(name, Role, Prefix + name, Role, "shipped", []))
    ];
}
