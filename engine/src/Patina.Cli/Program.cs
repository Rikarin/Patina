using Patina.Cli;
using Patina.Contracts;

switch (args) {
    case ["version"] or ["--version"]:
        Console.WriteLine(ProductInfo.Version);
        return 0;
    case ["bake", var graph, var folder]:
        return GraphBake.Run(graph, folder);
    case ["bake-maps", .. var options]:
        return MeshMapCommand.Run(options);
    case ["library", "new", var path, .. var options]:
        return LibraryCommands.New(path, options);
    case ["library", "check", var path, .. var options]:
        return LibraryCommands.Check(path, options);
    default:
        await Console.Error.WriteLineAsync(
            """
            Usage:
              patina version | patina bake <graph.vxtexgraph> <folder>
            """
            + "\n"
            + MeshMapCommand.Usage
            + "\n"
            + """
                patina library new <path> [--name <name>]
                patina library check <path> [--all]
              """
        );
        return 2;
}
