using Patina.Cli;
using Patina.Contracts;

switch (args) {
    case ["version"] or ["--version"]:
        Console.WriteLine(ProductInfo.Version);
        return 0;
    case ["library", "new", var path, .. var options]:
        return LibraryCommands.New(path, options);
    case ["library", "check", var path, .. var options]:
        return LibraryCommands.Check(path, options);
    default:
        await Console.Error.WriteLineAsync(
            """
            Usage:
              patina version
              patina library new <path> [--name <name>]
              patina library check <path> [--all]
            """
        );
        return 2;
}
