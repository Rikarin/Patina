using Patina.Contracts;
using Patina.Export.Builds;
using System.Text.Json;

namespace Patina.Cli;

/// <summary>
///     <c>patina build compare</c> (#217): what differs between two builds, from the report folders <c>patina build</c>
///     prints, as JSON.
/// </summary>
static class BuildCompareCommand {
    public const string Usage = "  patina build compare <earlier report folder> <later report folder>";

    /// <summary>Prints the comparison.</summary>
    /// <returns>0, or 2 when a folder holds no finished build's report.</returns>
    public static int Run(string before, string after) {
        foreach (var folder in (string[])[before, after]) {
            if (BuildJournal.ReadLibrary(Path.GetFullPath(folder)) is null) {
                Console.Error.WriteLine($"'{folder}' holds no finished build's report.");
                return 2;
            }
        }

        var comparison = BuildComparisons.Compare(Path.GetFullPath(before), Path.GetFullPath(after));
        Console.WriteLine(JsonSerializer.Serialize(comparison, ContractsJsonContext.Default.BuildComparison));
        return 0;
    }
}
