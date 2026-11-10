using Patina.Contracts;
using Patina.Graphs;
using Patina.Library;
using System.Text.Json;
using Vixen.Core.Yaml;

namespace Patina.Cli;

/// <summary>
///     <c>patina graph info</c> and <c>patina package</c>: what a graph or a package takes and makes, as JSON for a
///     script, and publishing a graph as a package (ADR 0027), with no Server.
/// </summary>
static class GraphCommands {
    public const string InfoUsage = "  patina graph info <graph.vxtexgraph | package.ptpkg>";

    public const string PackageUsage = """
                                         patina package <graph.vxtexgraph> <folder> [--library <path>]
                                                        [--role material|mask|filter|brush]
                                       """;

    const int ExitClean = 0;
    const int ExitRefused = 1;
    const int ExitUsage = 2;

    /// <summary>Prints a graph's or a package's inputs, outputs, parameters, presets and what it says about itself.</summary>
    public static int Info(string path) {
        try {
            var (_, graph, package) = GraphFactsOf.Read(path);
            Console.WriteLine(
                JsonSerializer.Serialize(GraphFactsOf.Of(graph, package), PackageJsonContext.Default.GraphFacts)
            );
            return ExitClean;
        } catch (Exception e) when (e is FileNotFoundException
                                        or LibraryFormatException
                                        or YamlParseException
                                        or YamlBindingException) {
            Console.Error.WriteLine(e.Message);
            return ExitUsage;
        }
    }

    /// <summary>Publishes a graph as a package in a folder, replacing one of its name.</summary>
    public static int Package(string graph, string folder, string[] options) {
        string? library = null;
        var role = GraphRoles.Material;
        for (var at = 0; at < options.Length; at += 2) {
            switch (options[at..]) {
                case ["--library", var path, ..]:
                    library = path;
                    break;
                case ["--role", var named, ..]:
                    role = named;
                    break;
                default:
                    Console.Error.WriteLine(PackageUsage);
                    return ExitUsage;
            }
        }

        try {
            var (package, manifest) = GraphPackages.PublishTo(
                Path.GetFullPath(graph),
                Path.GetFullPath(folder),
                library is null ? null : Path.GetFullPath(library),
                GraphPackages.RoleOf(role)
            );
            Console.WriteLine(package);
            foreach (var compound in manifest.Compounds) {
                Console.WriteLine($"  compound {compound.From}");
            }

            foreach (var file in manifest.Files) {
                Console.WriteLine($"  picture  {file.From}");
            }

            return ExitClean;
        } catch (FileNotFoundException e) {
            Console.Error.WriteLine(e.Message);
            return ExitUsage;
        } catch (Exception e) when (e is PackageRefusedException or YamlParseException or YamlBindingException) {
            Console.Error.WriteLine($"Nothing published: {e.Message}");
            return ExitRefused;
        }
    }
}
