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
                                                        [--role material|mask|filter|brush] [--zip]
                                       """;

    public const string ImportUsage = """
                                        patina package import <package.zip | package.ptpkg>
                                                              (--library <path> | --into <folder>)
                                      """;

    const int ExitClean = 0;
    const int ExitRefused = 1;
    const int ExitUsage = 2;

    /// <summary>Prints a graph's or a package's inputs, outputs, parameters, presets and description.</summary>
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
        var zip = false;
        for (var at = 0; at < options.Length; at += 2) {
            switch (options[at..]) {
                case ["--library", var path, ..]:
                    library = path;
                    break;
                case ["--role", var named, ..]:
                    role = named;
                    break;
                case ["--zip", ..]:
                    zip = true;
                    at--;
                    break;
                default:
                    Console.Error.WriteLine(PackageUsage);
                    return ExitUsage;
            }
        }

        try {
            var into = Path.GetFullPath(folder);
            var writing = zip ? Directory.CreateTempSubdirectory("patina-package-").FullName : into;
            var (package, manifest) = GraphPackages.PublishTo(
                Path.GetFullPath(graph),
                writing,
                library is null ? null : Path.GetFullPath(library),
                GraphPackages.RoleOf(role)
            );
            Console.WriteLine(zip ? Zipped(package, manifest, into, writing) : package);
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

    /// <summary>A package published into a scratch folder, zipped into the folder asked for; the zip's path.</summary>
    static string Zipped(string package, PackageManifest manifest, string into, string scratch) {
        try {
            Directory.CreateDirectory(into);
            var file = Path.Combine(into, manifest.Name + PackageArchives.ZipExtension);
            PackageArchives.Zip(package, file);
            return file;
        } finally {
            Directory.Delete(scratch, true);
        }
    }

    /// <summary>Brings a package, zipped or as its folder, into a library's packages or another folder.</summary>
    public static int Import(string source, string[] options) {
        var into = options switch {
            ["--library", var library] => Path.Combine(Path.GetFullPath(library), PackageManifests.LibraryFolder),
            ["--into", var folder] => Path.GetFullPath(folder),
            _ => null
        };
        if (into is null) {
            Console.Error.WriteLine(ImportUsage);
            return ExitUsage;
        }

        try {
            var (package, manifest, replaced) = PackageArchives.Import(Path.GetFullPath(source), into);
            Console.WriteLine(package);
            Console.Error.WriteLine(
                replaced
                    ? $"{manifest.Name} replaced the package of its name."
                    : $"{manifest.Name} is brought in, written by {manifest.Writer}."
            );
            return ExitClean;
        } catch (FileNotFoundException e) {
            Console.Error.WriteLine(e.Message);
            return ExitUsage;
        } catch (Exception e) when (e is InvalidDataException or LibraryFormatException or IOException) {
            Console.Error.WriteLine($"Nothing brought in: {e.Message}");
            return ExitRefused;
        }
    }
}
