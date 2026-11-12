using Patina.Contracts;
using Patina.Graphs;
using System.Globalization;
using Vixen.NodeGraph;

namespace Patina.Server.Graphs;

/// <summary>
///     What a new graph can start from (#183): nothing, an Output node for each material channel, or a copy of one of the
///     starter presets that ship with Patina. The Server holds the list, so every client offers the same.
/// </summary>
public static class GraphTemplates {
    /// <summary>A graph with no nodes.</summary>
    public const string Empty = "empty";

    /// <summary>An Output node for each channel a material has, in a column, for a graph built from nothing.</summary>
    public const string Outputs = "outputs";

    const string OutputType = "Output/Output";
    const float Column = 600;
    const float Row = 160;

    static readonly string[] Channels = ["baseColor", "normal", "roughness", "metalness", "height", "occlusion"];

    /// <summary>Every template, with what a person calls it: Empty, Material Outputs, then each starter preset.</summary>
    public static IReadOnlyList<StackTerm> All => [
        new(Empty, "Empty"),
        new(Outputs, "Material Outputs"),
        ..MaterialShelf.StarterNames.Select(static name => new StackTerm(name, name))
    ];

    /// <summary>A new graph's document from a template.</summary>
    /// <param name="name">The graph's name.</param>
    /// <param name="side">The side it evaluates at, or null for the default.</param>
    /// <param name="template">One of <see cref="All" />, or null for <see cref="Empty" />.</param>
    /// <exception cref="GraphEditRefusedException">There is no such template.</exception>
    public static NodeGraphAsset Make(string name, int? side, string? template) {
        var empty = GraphDocuments.New(name, side);
        switch (template) {
            case null or Empty:
                return empty;
            case Outputs:
                return empty with {
                    Nodes = [
                        ..Channels.Select(static (channel, at) => new GraphNodeAsset {
                                Id = at + 1,
                                Type = OutputType,
                                X = Column,
                                Y = at * Row,
                                Texts = new() { ["Usage"] = channel }
                            }
                        )
                    ]
                };
        }

        if (!MaterialShelf.StarterNames.Contains(template, StringComparer.Ordinal)) {
            throw new GraphEditRefusedException($"There is no graph template '{template}'.");
        }

        var starter = GraphDocuments.Parse(MaterialShelf.StarterText(template));
        Dictionary<string, string> settings = new(starter.Settings, StringComparer.Ordinal);
        foreach (var (key, value) in empty.Settings.Where(held => !settings.ContainsKey(held.Key))) {
            settings[key] = value;
        }

        if (side is { } chosen) {
            settings[TextureSizes.GraphSetting] = chosen.ToString(CultureInfo.InvariantCulture);
        }

        return starter with { Name = empty.Name, Settings = settings };
    }
}
