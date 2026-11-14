using Patina.Cli;
using Patina.Contracts;

switch (args) {
    case ["version"] or ["--version"]:
        Console.WriteLine(ProductInfo.Version);
        return 0;
    case [] or ["help"] or ["--help"] or ["-h"]:
        Console.WriteLine(Help.All);
        return 0;
    case ["help", .. var command]:
        return Help.Print(command, Console.Out);
    case [.. var command, "--help" or "-h"]:
        return Help.Print(command, Console.Out);
    case ["bake-maps", .. var options]:
        return MeshMapCommand.Run(options);
    case ["bake", var graph, var folder, .. var options] when GraphBake.Options(options) is { } read:
        return GraphBake.Run(graph, folder, read);
    case ["graph", "info", var graph]:
        return GraphCommands.Info(graph);
    case ["package", "import", var source, .. var options]:
        return GraphCommands.Import(source, options);
    case ["package", var graph, var folder, .. var options]:
        return GraphCommands.Package(graph, folder, options);
    case ["build", "compare", var before, var after]:
        return BuildCompareCommand.Run(before, after);
    case ["build", .. var options]:
        return await BuildCommand.RunAsync(options);
    case ["library", "new", var path, .. var options]:
        return LibraryCommands.New(path, options);
    case ["library", "check", var path, .. var options]:
        return LibraryCommands.Check(path, options);
    case ["cache", "prune", var path, .. var options]:
        return LibraryCommands.Prune(path, options);
    default:
        // A command that is known but was given the wrong options gets its own usage, not the whole list.
        Help.Print(args, Console.Error);
        return 2;
}
