using Patina.Cli;
using Patina.Contracts;

switch (args) {
    case ["version"] or ["--version"]:
        Console.WriteLine(ProductInfo.Version);
        return 0;
    case ["bake", var graph, var folder]:
        return GraphBake.Run(graph, folder);
    default:
        await Console.Error.WriteLineAsync("Usage: patina version | patina bake <graph.vxtexgraph> <folder>");
        return 2;
}
