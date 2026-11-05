using Patina.Contracts;
using Patina.Export;
using Patina.Jobs;
using Patina.Library;
using System.Globalization;
using Vixen.Interchange;

namespace Patina.Cli;

/// <summary>
///     <c>patina bake-maps</c>: a bake in this process, with no Server and no Worker, for a build farm. It is the same
///     run a Worker does, and it can share a cache with one.
/// </summary>
internal static class MeshMapCommand {
    public const string Usage =
        "  patina bake-maps --high <mesh> --low <mesh> --out <folder> [--cage <mesh>] [--resolution <n>]\n"
        + "        [--maps ao,curvature,...] [--format png8|png16|exr16|exr32] [--supersampling <n>]\n"
        + "        [--rays <n>] [--search <fraction>] [--reach <fraction>] [--skew <0-1>] [--skew-map <png>]\n"
        + "        [--match-by-name] [--gutter <n>] [--space tangent|object] [--name <stem>]\n"
        + "        [--threads <n>] [--cache <folder>]";

    public static int Run(string[] options) {
        BakeRequest request;
        var threads = 0;
        string? cacheFolder = null;
        try {
            request = Parse(options, ref threads, ref cacheFolder);
            BakeInputs.Check(request);
        } catch (Exception e) when (e is BakeRefusedException or FormatException or FileNotFoundException) {
            Console.Error.WriteLine(e.Message);
            return 2;
        }

        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, args) => {
            args.Cancel = true;
            stop.Cancel();
        };

        var scratch = cacheFolder is null ? Directory.CreateTempSubdirectory("patina-bake-") : null;
        var cache = new BakeCache(cacheFolder ?? scratch!.FullName);
        try {
            var key = BakeInputs.KeyOf(request, ToolCatalog.Default).Hex;
            var manifest = cache.Find(key);
            var cached = manifest is not null;
            manifest ??= MeshMapBakeRun.Run(request, cache.FolderOf(key), threads, Progress(), stop.Token);

            var result = cache.Deliver(key, manifest, request, cached);
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{result.Covered} texels, {result.Missed} found no surface."
                )
            );
            foreach (var warning in result.Warnings) {
                Console.Error.WriteLine(warning);
            }

            foreach (var map in result.Maps) {
                Console.WriteLine(map.Path);
            }

            return 0;
        } catch (OperationCanceledException) {
            Console.Error.WriteLine("Stopped.");
            return 130;
        } catch (Exception e) when (e is IOException or InvalidDataException or ModelFormatException) {
            Console.Error.WriteLine($"Nothing baked: {e.Message}");
            return 1;
        } finally {
            scratch?.Delete(true);
        }
    }

    static Action<double> Progress() {
        var last = -1;
        return fraction => {
            var percent = (int)(fraction * 100);
            if (Interlocked.Exchange(ref last, percent) != percent) {
                Console.Error.Write($"\r{percent,3}%");
            }
        };
    }

    static BakeRequest Parse(string[] options, ref int threads, ref string? cache) {
        Dictionary<string, string> named = new(StringComparer.Ordinal);
        var match = false;

        for (var at = 0; at < options.Length; at++) {
            if (options[at] == "--match-by-name") {
                match = true;
            } else if (options[at].StartsWith("--", StringComparison.Ordinal) && at + 1 < options.Length) {
                named[options[at][2..]] = options[++at];
            } else {
                throw new FormatException($"'{options[at]}' is not an option.\n{Usage}");
            }
        }

        string Required(string name) =>
            named.GetValueOrDefault(name) ?? throw new FormatException($"--{name} is required.\n{Usage}");

        int Number(string name, int fallback) =>
            named.TryGetValue(name, out var text) ? int.Parse(text, CultureInfo.InvariantCulture) : fallback;

        double Fraction(string name, double fallback) =>
            named.TryGetValue(name, out var text) ? double.Parse(text, CultureInfo.InvariantCulture) : fallback;

        threads = Number("threads", 0);
        cache = named.GetValueOrDefault("cache");

        var maps = named.GetValueOrDefault("maps") is { } list
            ? list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : null;

        return new() {
            High = Path.GetFullPath(Required("high")),
            Low = Path.GetFullPath(Required("low")),
            Output = Path.GetFullPath(Required("out")),
            Cage = named.GetValueOrDefault("cage") is { } cage ? Path.GetFullPath(cage) : null,
            Resolution = Number("resolution", 2048),
            Maps = maps,
            Format = named.GetValueOrDefault("format", "png16"),
            Supersampling = Number("supersampling", 1),
            OcclusionRays = Number("rays", 64),
            SearchRadius = Fraction("search", 0.05),
            OcclusionRadius = Fraction("reach", 0.5),
            Skew = Fraction("skew", 0),
            SkewMap = named.GetValueOrDefault("skew-map") is { } skew ? Path.GetFullPath(skew) : null,
            MatchByName = match,
            Gutter = Number("gutter", 4),
            Space = named.GetValueOrDefault("space", "tangent"),
            Name = named.GetValueOrDefault("name")
        };
    }
}
