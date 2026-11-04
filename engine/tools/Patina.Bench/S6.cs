using Patina.Library;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Patina.Bench;

/// <summary>
///     Spike S6 (plan §6): the Library at scale. Prints each measurement as it finishes, in milliseconds. Timings
///     repeat and report the median and the worst, because one number from a laptop proves little.
/// </summary>
static class S6 {
    const int BuildRuns = 3;
    const int ColdOpenRuns = 7;
    const int SearchRounds = 20;

    /// <summary>What a person types: whole words, short prefixes, two words, a tag, and words nothing matches.</summary>
    static readonly string[] Searches = [
        "crate", "cr", "c", "rust", "rust crate", "oak do", "props", "hero", "00042", "zzzz", "steel sword", "metal"
    ];

    public static int Run(string root, GeneratorOptions options) {
        Line("machine", $"{Environment.ProcessorCount} cores, {Environment.OSVersion}, {Environment.Version}");
        var generated = EnsureGenerated(root, options);

        var sizes = Measure.Folder(root);
        Line(
            "corpus",
            $"{options.Projects} projects, {generated.Assets.Count} assets, {sizes.Files} files, {sizes.Bytes >> 20} MiB"
        );

        Build(root);
        Line("index size", $"{new FileInfo(LibraryLayout.IndexPath(root)).Length >> 10} KiB");
        Cold(root);

        using var library = OpenLibrary.Open(root);
        Search(library);
        Paging(library, options.Projects);
        Rescans(root, library, generated);
        QueriesDuringRescan(root, library, generated);
        FanOut(root, library, generated);
        Line("working set at the end", $"{Environment.WorkingSet >> 20} MiB");
        return 0;
    }

    /// <summary>Runs as its own process, so its time covers the runtime starting, the JIT and an empty pool.</summary>
    public static void ColdOpen(string root) {
        var started = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        using var library = OpenLibrary.Open(root);
        var info = library.Describe();
        var page = library.Projects(new(Limit: 100));
        var first = library.Projects(new("rust", Limit: 100));
        var elapsed = DateTime.UtcNow - started;
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{elapsed.TotalMilliseconds:F1} {info.ProjectCount} {page.Items.Count} {first.Total}"
            )
        );
    }

    static GeneratedLibrary EnsureGenerated(string root, GeneratorOptions options) {
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) {
            throw new IOException(
                $"'{root}' is not empty: S6 edits the library it measures, so it starts from its own."
            );
        }

        var clock = Stopwatch.StartNew();
        var generated = LibraryGenerator.Generate(root, options);
        Line("generate", clock);
        return generated;
    }

    static void Build(string root) {
        List<double> times = [];
        for (var run = 0; run < BuildRuns; run++) {
            var cache = Path.Combine(root, LibraryLayout.CacheFolder);
            if (Directory.Exists(cache)) {
                Directory.Delete(cache, true);
            }

            using var library = OpenLibrary.Open(root);
            var scan = library.Rescan();
            times.Add(scan.Elapsed.TotalMilliseconds);
            Line($"full build run {run + 1}", $"{scan.Elapsed.TotalMilliseconds:F0} ms, {scan.Files} files");
        }

        Summary("full build", times);
    }

    static void Cold(string root) {
        List<double> times = [];
        for (var run = 0; run < ColdOpenRuns; run++) {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true };
            info.ArgumentList.Add("cold-open");
            info.ArgumentList.Add(root);
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            times.Add(double.Parse(output.Split(' ')[0], CultureInfo.InvariantCulture));
            Line($"cold open run {run + 1}", output.Trim());
        }

        Summary("cold open (process start to first pages)", times);
    }

    static void Search(OpenLibrary library) {
        List<double> all = [];
        foreach (var search in Searches) {
            List<double> times = [];
            var total = 0;
            for (var round = 0; round < SearchRounds; round++) {
                var clock = Stopwatch.StartNew();
                total = library.Projects(new(search, Limit: 100)).Total;
                times.Add(clock.Elapsed.TotalMilliseconds);
            }

            Summary($"search '{search}' ({total} hits)", times);
            all.AddRange(times);
        }

        Summary("search, all", all);

        var tagged = Timed(() => library.Projects(new("rust", ["hero"], Limit: 100)));
        Line("search with tag", tagged);
        Line("tag only", Timed(() => library.Projects(new(null, ["hero", "metal"], Limit: 100))));
        Line("assets search", Timed(() => library.Assets(new("rust", Limit: 100))));
    }

    static void Paging(OpenLibrary library, int projects) {
        Line("first page", Timed(() => library.Projects(new(Limit: 100))));
        Line("middle page", Timed(() => library.Projects(new(Offset: projects / 2, Limit: 100))));
        Line("last page", Timed(() => library.Projects(new(Offset: projects - 100, Limit: 100))));
    }

    static void Rescans(string root, OpenLibrary library, GeneratedLibrary generated) {
        // The last build's stamps are fresh: wait out the racy window so that this measures the common case.
        Thread.Sleep(TimeSpan.FromSeconds(2.5));
        library.Rescan();
        Scan("rescan, nothing changed", library);
        Scan("rescan, nothing changed (again)", library);

        foreach (var count in (int[])[1, 100, 1000]) {
            var touched = generated.Projects.Take(count).Select(p => LibraryFiles.ProjectFile(root, p)).ToList();
            foreach (var file in touched) {
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-1));
            }

            Scan($"rescan, {count} touched, same bytes", library);
            foreach (var file in touched) {
                var read = DocumentCodec.ReadProject(File.ReadAllText(file), file).Document;
                LibraryFiles.Replace(file, DocumentCodec.Write(read with { Name = read.Name + " edited" }));
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-1));
            }

            Scan($"rescan, {count} edited", library);
        }
    }

    /// <summary>The browser keeps working while a big rescan writes: the claim write-ahead logging is there for.</summary>
    static void QueriesDuringRescan(string root, OpenLibrary library, GeneratedLibrary generated) {
        var touched = generated.Projects.Take(generated.Projects.Count / 2);
        foreach (var path in touched) {
            File.SetLastWriteTimeUtc(LibraryFiles.ProjectFile(root, path), DateTime.UtcNow.AddMinutes(-2));
        }

        ScanResult? scan = null;
        var scanner = new Thread(() => scan = library.Rescan());
        scanner.Start();
        List<double> times = [];
        while (scanner.IsAlive) {
            var clock = Stopwatch.StartNew();
            library.Projects(new("rust", Limit: 100));
            times.Add(clock.Elapsed.TotalMilliseconds);
        }

        scanner.Join();
        Line($"search during a rescan of {scan!.Hashed} files ({times.Count} searches)", Describe(times));
    }

    static void Scan(string label, OpenLibrary library) {
        var scan = library.Rescan();
        Line(label, $"{scan.Elapsed.TotalMilliseconds:F1} ms, {scan.Hashed} hashed, {scan.Changed} changed");
    }

    static void FanOut(string root, OpenLibrary library, GeneratedLibrary generated) {
        var impact = library.Impact(generated.Popular, 0, 100)!;
        Line(
            "popular asset",
            $"{impact.Asset.Path}: {impact.Asset.UsageCount} direct, {impact.Projects} dependent, {impact.Pinned} pinned"
        );
        var times = Enumerable.Range(0, 20)
            .Select(_ => {
                    var clock = Stopwatch.StartNew();
                    library.Impact(generated.Popular, 0, 100);
                    return clock.Elapsed.TotalMilliseconds;
                }
            )
            .ToList();
        Summary("impact query", times);

        var clock = Stopwatch.StartNew();
        var before = library.Check();
        Line("check, first", $"{clock.ElapsedMilliseconds} ms, {before.Outputs} outputs, {Stale(before)} stale");

        // Everything built: then a change to the popular material must stale exactly what depends on it.
        var keys = BuildAll(root, library);
        clock.Restart();
        var built = library.Check();
        Line("check, all built", $"{clock.ElapsedMilliseconds} ms, {Stale(built)} stale, {keys} objects");

        var file = LibraryFiles.AssetFile(root, impact.Asset.Path);
        File.AppendAllText(file, "# edited\n", Encoding.UTF8);
        clock.Restart();
        var after = library.Check();
        var rebuilding = after.Findings.Where(static f => f.Kind == FindingKind.StaleOutput)
            .Select(static f => f.Path)
            .Distinct()
            .Count();
        Line(
            "check, popular edited",
            $"{clock.ElapsedMilliseconds} ms, {Stale(after)} stale outputs in {rebuilding} projects, "
            + $"impact said {impact.Projects - impact.Pinned} rebuild at most"
        );
    }

    /// <summary>Stands in for the Worker: writes an empty object under every key the check wants.</summary>
    static int BuildAll(string root, OpenLibrary library) {
        var objects = new ObjectStore(root);
        var stale = library.Check().Findings.Where(static f => f.Kind == FindingKind.StaleOutput).ToList();
        foreach (var finding in stale) {
            var key = finding.Detail[(finding.Detail.LastIndexOf(' ') + 1)..];
            objects.Put(new(key), []);
        }

        return stale.Count;
    }

    static int Stale(CheckReport report) => report.Findings.Count(static f => f.Kind == FindingKind.StaleOutput);

    static string Timed<T>(Func<T> query) {
        List<double> times = [];
        for (var round = 0; round < SearchRounds; round++) {
            var clock = Stopwatch.StartNew();
            query();
            times.Add(clock.Elapsed.TotalMilliseconds);
        }

        return Describe(times);
    }

    static void Summary(string label, List<double> times) => Line(label, Describe(times));

    static string Describe(List<double> times) {
        times.Sort();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"median {times[times.Count / 2]:F2} ms, p95 {times[Math.Min(times.Count - 1, times.Count * 95 / 100)]:F2} ms, max {times[^1]:F2} ms"
        );
    }

    static void Line(string label, Stopwatch clock) => Line(label, $"{clock.ElapsedMilliseconds} ms");

    static void Line(string label, string value) => Console.WriteLine($"{label}: {value}");
}

/// <summary>Sizes on disk.</summary>
static class Measure {
    public static (int Files, long Bytes) Folder(string root) {
        var files = new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
        return (files.Count, files.Sum(static f => f.Length));
    }
}
