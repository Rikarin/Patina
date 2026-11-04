using Patina.Bench;
using System.Globalization;

// patina-bench: scale tooling. Not shipped.
//   generate <path> <projects> [seed]   writes a library
//   s6 <path> [projects] [seed]         generates into an empty folder if needed, then measures spike S6
//   cold-open <path>                    one cold open, as its own process; s6 starts it
switch (args) {
    case ["generate", var path, var projects, .. var rest]:
        var options = new GeneratorOptions(
            int.Parse(projects, CultureInfo.InvariantCulture),
            rest is [var seed] ? int.Parse(seed, CultureInfo.InvariantCulture) : 42
        );
        LibraryGenerator.Generate(Path.GetFullPath(path), options);
        return 0;
    case ["s6", var path, .. var rest]:
        return S6.Run(
            Path.GetFullPath(path),
            new(
                rest is [var count, ..] ? int.Parse(count, CultureInfo.InvariantCulture) : 10_000,
                rest is [_, var chosen] ? int.Parse(chosen, CultureInfo.InvariantCulture) : 42
            )
        );
    case ["cold-open", var path]:
        S6.ColdOpen(Path.GetFullPath(path));
        return 0;
    default:
        await Console.Error.WriteLineAsync(
            "Usage: patina-bench generate <path> <projects> [seed] | s6 <path> [projects] [seed]"
        );
        return 2;
}
