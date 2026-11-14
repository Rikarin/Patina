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
        "  patina bake-maps --high <mesh> [--high <mesh>...] --low <mesh> --out <folder> [--cage <mesh>]\n"
        + "        [--resolution <n>] [--maps normal,height,ao,curvature,...] [--format png8|png16|exr16|exr32]\n"
        + "        [--supersampling <n>] [--gutter <n>] [--space tangent|object] [--name <stem>]\n"
        + "        [--front <distance>] [--rear <distance>] [--units relative|absolute] [--ignore-backfaces]\n"
        + "        [--skew <0-1>] [--skew-map <png>] [--match-by-name] [--low-suffix <s>] [--high-suffix <s>]\n"
        + "        [--rays <n>] [--reach <distance>] [--ao-spread <degrees>] [--ao-falloff none|linear|smooth]\n"
        + "        [--ao-distribution cosine|uniform] [--ao-minimum <distance>] [--ao-ignore-backfaces]\n"
        + "        [--ao-within-part] [--ground-plane] [--ground-offset <distance>] [--thickness-raw]\n"
        + "        [--height-range measured|rays|custom] [--height-scale <distance>]\n"
        + "        [--id-source auto|material|material-colour|vertex-colour|mesh|shell]\n"
        + "        [--id-palette random|hue|grey] [--auto-cage]\n"
        + "        [--threads <n>] [--cache <folder>]";

    /// <summary>The options that take no value.</summary>
    static readonly HashSet<string> Switches = [
        "match-by-name", "ignore-backfaces", "auto-cage", "ao-ignore-backfaces", "ao-within-part", "ground-plane",
        "thickness-raw"
    ];

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

        return Bake(request, threads, cacheFolder);
    }

    /// <summary>One bake, from the cache when it holds it, with its maps' paths on standard output.</summary>
    /// <returns>0, or 1 when it failed, or 130 when it was stopped.</returns>
    public static int Bake(BakeRequest request, int threads, string? cacheFolder) {
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, args) => {
            args.Cancel = true;
            stop.Cancel();
        };

        var scratch = cacheFolder is null ? Directory.CreateTempSubdirectory("patina-bake-") : null;
        var cache = new BakeCache(cacheFolder ?? scratch!.FullName);
        try {
            var key = BakeKeyReads.KeyOf(request, ToolCatalog.Default, MeshFileReads.Instance).Hex;
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

    /// <summary>The command line as named values, switches that are on, and the high meshes in order.</summary>
    static (Dictionary<string, string> Named, HashSet<string> On, List<string> High) Scan(string[] options) {
        Dictionary<string, string> named = new(StringComparer.Ordinal);
        HashSet<string> on = new(StringComparer.Ordinal);
        List<string> high = [];

        for (var at = 0; at < options.Length; at++) {
            var option = options[at];
            if (option.StartsWith("--", StringComparison.Ordinal) && Switches.Contains(option[2..])) {
                on.Add(option[2..]);
            } else if (option == "--high" && at + 1 < options.Length) {
                high.Add(Path.GetFullPath(options[++at]));
            } else if (option.StartsWith("--", StringComparison.Ordinal) && at + 1 < options.Length) {
                named[option[2..]] = options[++at];
            } else {
                throw new FormatException($"'{option}' is not an option.\n{Usage}");
            }
        }

        return (named, on, high);
    }

    static BakeRequest Parse(string[] options, ref int threads, ref string? cache) {
        var (named, on, high) = Scan(options);

        string Required(string name) =>
            named.GetValueOrDefault(name) ?? throw new FormatException($"--{name} is required.\n{Usage}");

        int Number(string name, int fallback) =>
            named.TryGetValue(name, out var text) ? int.Parse(text, CultureInfo.InvariantCulture) : fallback;

        double Fraction(string name, double fallback) =>
            named.TryGetValue(name, out var text) ? double.Parse(text, CultureInfo.InvariantCulture) : fallback;

        string Word(string name, string fallback) => named.GetValueOrDefault(name, fallback);

        threads = Number("threads", 0);
        cache = named.GetValueOrDefault("cache");

        if (high.Count == 0) {
            throw new FormatException($"--high is required.\n{Usage}");
        }

        // The command line's default is the two maps every bake measures, as it always was: a farm asks for more.
        var maps = named.GetValueOrDefault("maps") is { } list
            ? list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : ["normal", "height"];
        var front = Fraction("front", 0.05);

        return new() {
            High = high,
            Low = Path.GetFullPath(Required("low")),
            Output = Path.GetFullPath(Required("out")),
            Cage = named.GetValueOrDefault("cage") is { } cage ? Path.GetFullPath(cage) : null,
            Name = named.GetValueOrDefault("name"),
            Options = new() {
                Resolution = Number("resolution", 2048),
                Maps = maps,
                Format = Word("format", "png16"),
                Supersampling = Number("supersampling", 1),
                Gutter = Number("gutter", 4),
                Space = Word("space", "tangent"),
                Rays = new() {
                    Front = front,
                    Rear = Fraction("rear", front),
                    Units = Word("units", "relative"),
                    IgnoreBackfaces = on.Contains("ignore-backfaces"),
                    AutomaticCage = on.Contains("auto-cage"),
                    Skew = Fraction("skew", 0),
                    SkewMap = named.GetValueOrDefault("skew-map") is { } skew ? Path.GetFullPath(skew) : string.Empty,
                    MatchByName = on.Contains("match-by-name"),
                    LowSuffix = Word("low-suffix", "_low"),
                    HighSuffix = Word("high-suffix", "_high")
                },
                Occlusion = new() {
                    Rays = Number("rays", 64),
                    Distance = Fraction("reach", 0.5),
                    Spread = Fraction("ao-spread", 90),
                    Falloff = Word("ao-falloff", "none"),
                    Distribution = Word("ao-distribution", "cosine"),
                    Minimum = Fraction("ao-minimum", 0),
                    IgnoreBackfaces = on.Contains("ao-ignore-backfaces"),
                    WithinPart = on.Contains("ao-within-part"),
                    GroundPlane = on.Contains("ground-plane"),
                    GroundOffset = Fraction("ground-offset", 0)
                },
                Thickness = new() { Normalised = !on.Contains("thickness-raw") },
                Height = new() { Range = Word("height-range", "measured"), Scale = Fraction("height-scale", 0.05) },
                Id = new() { Source = Word("id-source", "auto"), Palette = Word("id-palette", "random") }
            }
        };
    }
}
