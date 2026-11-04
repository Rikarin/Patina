using Patina.Contracts;

switch (args) {
    case ["version"] or ["--version"]:
        Console.WriteLine(ProductInfo.Version);
        return 0;
    default:
        await Console.Error.WriteLineAsync("Usage: patina version");
        return 2;
}
