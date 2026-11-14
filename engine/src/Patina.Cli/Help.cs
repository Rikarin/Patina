namespace Patina.Cli;

/// <summary>
///     What <c>patina help</c>, <c>patina --help</c> and <c>patina &lt;command&gt; --help</c> print (#186): one
///     usage per command, kept beside the command that reads it, so the help cannot list a flag it does not take.
/// </summary>
static class Help {
    /// <summary>Each command by the words that name it, longest first, with what it does and its usage.</summary>
    static readonly (string[] Words, string Summary, string Usage)[] Commands = [
        (["graph", "info"], "Prints what a graph or a package takes and makes, as JSON.", GraphCommands.InfoUsage),
        (["library", "new"], "Makes a new, empty library.", "  patina library new <path> [--name <name>]"),
        (["library", "check"], "Reports what is wrong with a library.", LibraryCommands.CheckUsage),
        (["cache", "prune"], "Removes built objects nothing has used for a while.",
            "  patina cache prune <library> [--days <n>]"),
        (["bake"], "Evaluates a graph or a package on the GPU and writes each output as a PNG.", GraphBake.Usage),
        (["bake-maps"], "Bakes mesh maps from a high mesh onto a low one, on the CPU.", MeshMapCommand.Usage),
        (["package"], "Publishes a graph as a package: its compounds and pictures in one folder.",
            GraphCommands.PackageUsage),
        (["build", "compare"], "Prints what differs between two builds, as JSON.", BuildCompareCommand.Usage),
        (["build"], "Rebuilds and exports a library's projects.", BuildCommand.Usage),
        (["version"], "Prints the version.", "  patina version"),
        (["help"], "Prints this, or one command's usage.", "  patina help [<command>]")
    ];

    /// <summary>Every command, with one line about each.</summary>
    public static string All =>
        "Usage: patina <command> [options]. Every command takes --help.\n\n"
        + string.Join(
            '\n',
            Commands.Select(static c => (Name: string.Join(' ', c.Words), c.Summary))
                .OrderBy(static c => c.Name, StringComparer.Ordinal)
                .Select(static c => $"  {c.Name,-14} {c.Summary}")
        );

    /// <summary>Prints the usage of the command <paramref name="words" /> start with.</summary>
    /// <returns>0 when there is such a command, otherwise 2.</returns>
    public static int Print(IReadOnlyList<string> words, TextWriter writer) {
        foreach (var (named, summary, usage) in Commands) {
            if (words.Count >= named.Length && named.SequenceEqual(words.Take(named.Length), StringComparer.Ordinal)) {
                writer.WriteLine($"{summary}\n\nUsage:\n{usage}");
                return 0;
            }
        }

        return Unknown(words);
    }

    /// <summary>Says there is no such command, and lists those there are.</summary>
    public static int Unknown(IReadOnlyList<string> words) {
        Console.Error.WriteLine($"There is no command 'patina {string.Join(' ', words)}'.\n\n{All}");
        return 2;
    }
}
