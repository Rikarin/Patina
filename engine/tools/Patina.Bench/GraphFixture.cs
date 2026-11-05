using Patina.Library;
using System.Text;
using Vixen.Core.Yaml;
using Vixen.NodeGraph;

namespace Patina.Bench;

/// <summary>
///     Adds a real texture graph to a library as a shared asset, which the placeholders of
///     <see cref="LibraryGenerator" /> are not: rows of a noise and a chain of colour and blur nodes, the last node an
///     Output, so that the graph editor's tests and spike S7 can open something the Worker evaluates.
/// </summary>
static class GraphFixture {
    const int Columns = 25;
    const float Spacing = 280;
    const int Side = 256;

    static readonly string[] Chain = ["Colour/HSL", "Filters/Blur", "Colour/Invert"];

    /// <param name="library">The library's folder, which must exist.</param>
    /// <param name="nodes">How many nodes, at least 2.</param>
    /// <param name="name">The asset's name, which also names its file.</param>
    public static int Write(string library, int nodes, string name) {
        List<GraphNodeAsset> written = [];
        List<GraphEdgeAsset> edges = [];
        for (var id = 1; id <= nodes; id++) {
            var (row, column) = Math.DivRem(id - 1, Columns);
            var last = id == nodes;
            var type = last ? "Output/Output" : column == 0 ? "Source/Noise" : Chain[column % Chain.Length];
            written.Add(
                new() {
                    Id = id,
                    Type = type,
                    X = column * Spacing,
                    Y = row * Spacing,
                    Texts = last ? new() { ["Usage"] = "baseColor" } : []
                }
            );
            if (column > 0) {
                edges.Add(new() { FromNode = id - 1, FromPort = "Out", ToNode = id, ToPort = "Input" });
            }
        }

        var document = new NodeGraphAsset {
            Name = name,
            Nodes = [..written],
            Edges = [..edges],
            Settings = new() { ["seed"] = "7", ["baseWidth"] = $"{Side}", ["baseHeight"] = $"{Side}" }
        };
        LibraryFiles.WriteAsset(
            Path.GetFullPath(library),
            $"graphs/{name.Replace(' ', '_')}.vxtexgraph",
            Encoding.UTF8.GetBytes(YamlSerializer.ToYaml(document)),
            new(AssetId.From(Guid.NewGuid()), "graph", name, [], [])
        );
        return 0;
    }
}
