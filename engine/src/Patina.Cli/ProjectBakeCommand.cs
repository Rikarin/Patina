using Patina.Contracts;
using Patina.Jobs;
using Patina.Library;
using System.Globalization;
using System.Text.Json;

namespace Patina.Cli;

/// <summary>
///     <c>patina bake-maps --project</c> (#186): a project's bake with its own settings, as Quick Bake makes it, with no
///     Server and no editor. The maps go where the project's documents read them, so a build after it exports them.
/// </summary>
static class ProjectBakeCommand {
    public const string Usage =
        "  patina bake-maps --project <folder> [--document <file>] [--set <material>]... [--out <folder>]\n"
        + "        [--threads <n>] [--cache <folder>] [--dry-run]";

    /// <summary>Whether a command line is a project's bake rather than one of meshes.</summary>
    public static bool Is(string[] options) => options.Contains("--project", StringComparer.Ordinal);

    public static int Run(string[] options) {
        IReadOnlyList<BakeRequest> requests;
        Read read;
        try {
            read = Parse(options);
            requests = ProjectBakes.Requests(
                read.Project,
                ProjectBakes.Read(read.Project),
                read.Document,
                read.Output,
                read.Sets
            );
            foreach (var request in requests) {
                BakeInputs.Check(request);
            }
        } catch (Exception e) when (e is BakeRefusedException
                                        or FormatException
                                        or FileNotFoundException
                                        or ArgumentException
                                        or LibraryFormatException) {
            Console.Error.WriteLine(e.Message);
            return 2;
        }

        if (read.DryRun) {
            Console.WriteLine(
                JsonSerializer.Serialize(new(read.Project, requests), PackageJsonContext.Default.ProjectBakePlan)
            );
            return 0;
        }

        foreach (var request in requests) {
            Console.Error.WriteLine(Described(request));
            if (MeshMapCommand.Bake(request, read.Threads, read.Cache) is not 0 and var failed) {
                return failed;
            }
        }

        return 0;
    }

    static string Described(BakeRequest request) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{(request.Material is { } set ? $"Texture set {set}" : "The mesh")}: "
            + $"{string.Join(", ", request.Options.Maps)} at {request.Options.Resolution} into {request.Output}"
        );

    static Read Parse(string[] options) {
        Read read = new();
        for (var at = 0; at < options.Length; at++) {
            var value = at + 1 < options.Length ? options[at + 1] : null;
            switch (options[at]) {
                case "--dry-run":
                    read.DryRun = true;
                    continue;
                case "--threads" when int.TryParse(value, CultureInfo.InvariantCulture, out var threads):
                    read.Threads = threads;
                    break;
                case "--project" when value is not null:
                    read.Project = Path.GetFullPath(value);
                    break;
                case "--document" when value is not null:
                    read.Document = value;
                    break;
                case "--set" when value is not null:
                    read.Sets.Add(value);
                    break;
                case "--out" when value is not null:
                    read.Output = value;
                    break;
                case "--cache" when value is not null:
                    read.Cache = value;
                    break;
                default:
                    throw new FormatException($"'{options[at]}' is not an option here.\n{Usage}");
            }

            at++;
        }

        return read.Project.Length > 0 ? read : throw new FormatException($"--project is required.\n{Usage}");
    }

    sealed class Read {
        public string Project { get; set; } = string.Empty;
        public string? Document { get; set; }
        public string? Output { get; set; }
        public string? Cache { get; set; }
        public int Threads { get; set; }
        public bool DryRun { get; set; }
        public List<string> Sets { get; } = [];
    }
}
