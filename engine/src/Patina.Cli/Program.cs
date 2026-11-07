using Patina.Cli;
using Patina.Contracts;

switch (args) {
    case ["version"] or ["--version"]:
        Console.WriteLine(ProductInfo.Version);
        return 0;
    case ["bake-maps", .. var options]:
        return MeshMapCommand.Run(options);
    case ["bake", var graph, var folder, .. var options] when GraphBake.Options(options) is var (library, meshMaps,
        size):
        return GraphBake.Run(graph, folder, library, meshMaps, size);
    case ["build", .. var options]:
        return await BuildCommand.RunAsync(options);
    case ["library", "new", var path, .. var options]:
        return LibraryCommands.New(path, options);
    case ["library", "check", var path, .. var options]:
        return LibraryCommands.Check(path, options);
    case ["cache", "prune", var path, .. var options]:
        return LibraryCommands.Prune(path, options);
    default:
        await Console.Error.WriteLineAsync(
            """
            Usage:
              patina version
              patina bake <graph.vxtexgraph> <folder> [--library <path>] [--mesh-maps <bake folder>] [--size <side>]
              patina cache prune <library> [--days <n>]
            """
            + "\n"
            + MeshMapCommand.Usage
            + "\n"
            + BuildCommand.Usage
            + "\n"
            + """
                patina library new <path> [--name <name>]
                patina library check <path> [--all]
              """
        );
        return 2;
}
