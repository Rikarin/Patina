using Patina.Library;

namespace Patina.Cli;

/// <summary><c>patina library</c>: the library layer's operations, with no Server needed.</summary>
static class LibraryCommands {
    /// <summary>What <c>patina library check</c> takes.</summary>
    public const string CheckUsage = "  patina library check <path> [--all] [--purge-revisions]";

    const string AllFlag = "--all";
    const string PurgeFlag = "--purge-revisions";
    const int DefaultPruneDays = 30;

    const int ExitClean = 0;
    const int ExitFindings = 1;
    const int ExitUsage = 2;

    /// <summary>Findings of one kind printed by default: a broken library can have ten thousand of each.</summary>
    const int FindingsPerKind = 20;

    public static int New(string path, string[] options) {
        if (options is not ([] or ["--name", _])) {
            return Usage();
        }

        var root = Path.GetFullPath(path);
        var name = options is [_, var given] ? given : Path.GetFileNameWithoutExtension(root);
        if (OpenLibrary.Exists(root)) {
            Console.Error.WriteLine($"'{root}' is a library already.");
            return ExitFindings;
        }

        LibraryFiles.CreateDefaultLibrary(root, name);
        Console.WriteLine($"Created library '{name}' in {root}");
        return ExitClean;
    }

    /// <summary>Removes built objects and reports nothing has touched for a while.</summary>
    public static int Prune(string path, string[] options) {
        var days = DefaultPruneDays;
        if (options is ["--days", var text] && (!int.TryParse(text, out days) || days < 0)) {
            Console.Error.WriteLine($"'{text}' is not a number of days.");
            return 2;
        }

        using var taken = LibraryLock.Take(path);
        if (taken is null) {
            Console.Error.WriteLine("A Server has this library open.");
            return 1;
        }

        Console.WriteLine($"Removed {CacheSweep.Prune(path, TimeSpan.FromDays(days))} old cache entries.");
        return 0;
    }

    public static int Check(string path, string[] options) {
        var purge = options.Contains(PurgeFlag);
        if (options.Any(static o => o is not (AllFlag or PurgeFlag)) || options.Distinct().Count() != options.Length) {
            return Usage();
        }

        var root = Path.GetFullPath(path);
        if (!OpenLibrary.Exists(root)) {
            Console.Error.WriteLine($"'{root}' is not a library: it has no {LibraryLayout.LibraryFile}.");
            return ExitFindings;
        }

        // Removing kept bytes wants nobody taking a snapshot meanwhile, and only a Server takes one.
        using var taken = purge ? LibraryLock.Take(root) : null;
        if (purge && taken is null) {
            Console.Error.WriteLine("A Server has this library open.");
            return ExitFindings;
        }

        try {
            using var library = OpenLibrary.Open(root);
            var report = library.Check();
            Print(report, options.Contains(AllFlag));
            if (purge) {
                var (files, bytes) = RevisionPurge.Run(root, report);
                Console.WriteLine($"Removed {files} unused revisions, {bytes} bytes.");
            }

            return report.HasErrors ? ExitFindings : ExitClean;
        } catch (LibraryFormatException e) {
            Console.Error.WriteLine(e.Message);
            return ExitFindings;
        }
    }

    static void Print(CheckReport report, bool all) {
        foreach (var group in report.Findings.GroupBy(static f => f.Kind)) {
            var findings = group.ToList();
            Console.WriteLine($"{group.Key} ({findings.Count}){(findings[0].IsError ? " error" : string.Empty)}");
            foreach (var finding in all ? findings : findings.Take(FindingsPerKind)) {
                Console.WriteLine($"  {finding.Path}: {finding.Detail}");
            }

            if (!all && findings.Count > FindingsPerKind) {
                Console.WriteLine($"  ... and {findings.Count - FindingsPerKind} more; --all lists them");
            }
        }

        Console.WriteLine(
            $"{report.Projects} projects, {report.Assets} shared assets, {report.Outputs} outputs: "
            + $"{report.Findings.Count} findings, {report.Findings.Count(static f => f.IsError)} errors"
        );
    }

    static int Usage() {
        Console.Error.WriteLine(
            $"Usage: patina library new <path> [--name <name>] | {CheckUsage.Trim()}"
        );
        return ExitUsage;
    }
}
