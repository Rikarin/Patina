// Compiled into every test project (tests/Directory.Build.props), so each sweeps what earlier runs left (#294).

[assembly: Xunit.AssemblyFixture(typeof(Patina.Testing.TempSweep))]

namespace Patina.Testing;

/// <summary>
///     The temp folders tests make (#294). A test removes its own with <see cref="Delete" />, which waits for a process
///     or a file still closing, as a Server just stopped does; a run first removes what runs before it left.
/// </summary>
static class TempFolders {
    /// <summary>How long a test's folder is waited for, a little at a time, before it is left to the sweep.</summary>
    static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>Removes a folder and all in it, waiting for whatever still holds a file; never fails a test.</summary>
    public static void Delete(string folder) {
        var until = DateTime.UtcNow + Patience;
        while (true) {
            try {
                if (Directory.Exists(folder)) {
                    Directory.Delete(folder, true);
                }

                return;
            } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                if (DateTime.UtcNow > until) {
                    return; // the next run's sweep takes it
                }

                Thread.Sleep(100);
            }
        }
    }

    /// <summary>
    ///     Removes the <c>patina-*</c> folders in the temp folder older than <paramref name="age" /> that a test made:
    ///     each ends in a random name. One that ends in a process id is a running Server's own, which it clears.
    /// </summary>
    public static void Sweep(TimeSpan age) {
        var cutoff = DateTime.UtcNow - age;
        foreach (var folder in new DirectoryInfo(Path.GetTempPath()).EnumerateDirectories("patina-*")) {
            var last = folder.Name[(folder.Name.LastIndexOf('-') + 1)..];
            if (folder.LastWriteTimeUtc < cutoff && !last.All(char.IsAsciiDigit)) {
                try {
                    folder.Delete(true);
                } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                    // In use, or not ours to remove: the next run tries again.
                }
            }
        }
    }
}

/// <summary>Once per test run: removes what runs over a day ago left, which no run still going can be using.</summary>
sealed class TempSweep {
    public TempSweep() => TempFolders.Sweep(TimeSpan.FromDays(1));
}
