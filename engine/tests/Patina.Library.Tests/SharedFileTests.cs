using Patina.Contracts;
using System.Text.RegularExpressions;

namespace Patina.Library.Tests;

/// <summary>
///     A read of a library file never stands in the way of somebody saving it. On Windows CI a library scan hashed
///     a graph with <c>File.ReadAllBytes</c>, which shares reading only, just as a client wrote the graph, and the
///     client's write failed with "being used by another process" (Patina run 42647749439).
/// </summary>
public sealed partial class SharedFileTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-shared-");

    public void Dispose() => folder.Delete(true);

    /// <summary>
    ///     A file held open by a read can be written over and deleted. This is the check that means something on
    ///     Windows, where an open handle's share mode is enforced; on Linux and macOS it is advisory and this passes
    ///     whatever the read shares, which is why the share itself is asserted below as well.
    /// </summary>
    [Fact]
    public void A_file_being_read_can_be_written_over_and_deleted() {
        var file = Path.Combine(folder.FullName, "slope.vxtexgraph");
        File.WriteAllText(file, "name: Slope");

        using (var reading = SharedFile.OpenRead(file)) {
            Assert.Equal('n', reading.ReadByte());

            // A client's save straight over it: the write that failed on the Windows runner.
            File.WriteAllText(file, "name: Ramp");
        }

        Assert.Equal("name: Ramp", File.ReadAllText(file));

        using (var reading = SharedFile.OpenRead(file)) {
            Assert.Equal('n', reading.ReadByte());
            File.Delete(file);
        }

        Assert.False(File.Exists(file));
    }

    /// <summary>
    ///     Every read asks Windows to let others write and delete. This holds on every platform, so a read that shares
    ///     less fails here on the machine that made it, not only on the Windows runner.
    /// </summary>
    [Fact]
    public void Every_read_shares_write_and_delete() {
        var reading = SharedFile.Reading();

        Assert.Equal(FileShare.ReadWrite | FileShare.Delete, reading.Share);
        Assert.Equal(FileAccess.Read, reading.Access);
        Assert.Equal(FileMode.Open, reading.Mode);
        Assert.Equal(FileShare.ReadWrite | FileShare.Delete, SharedFile.Reading(true).Share);
    }

    /// <summary>The readers give back what the ones they replace did, byte order mark and all.</summary>
    [Fact]
    public async Task The_readers_read_what_the_framework_reads() {
        var file = Path.Combine(folder.FullName, "notes.txt");
        File.WriteAllText(file, "één\ntwee\n", new System.Text.UTF8Encoding(true));

        Assert.Equal(File.ReadAllBytes(file), SharedFile.ReadAllBytes(file));
        Assert.Equal(File.ReadAllText(file), SharedFile.ReadAllText(file));
        Assert.Equal(File.ReadLines(file), SharedFile.ReadLines(file));
        Assert.Equal(
            File.ReadAllText(file),
            await SharedFile.ReadAllTextAsync(file, TestContext.Current.CancellationToken)
        );
        Assert.Throws<FileNotFoundException>(() => SharedFile.ReadAllBytes(Path.Combine(folder.FullName, "gone")));
    }

    /// <summary>
    ///     Nothing the Server, the Worker or the library code runs reads a file the framework's way. Those readers
    ///     share reading only, so each is a sharing violation waiting for an artist's save on Windows; a new one fails
    ///     here.
    /// </summary>
    [Fact]
    public void No_engine_source_reads_a_file_without_sharing_it() {
        var source = Path.Combine(EngineRoot(), "src");
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
                     .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                     .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                     .Where(static f => Path.GetFileName(f) != "SharedFile.cs")
                     .Order(StringComparer.Ordinal)) {
            var lines = File.ReadAllLines(file);
            for (var line = 0; line < lines.Length; line++) {
                if (UnsharedRead().IsMatch(lines[line])) {
                    found.Add($"{Path.GetRelativePath(source, file)}:{line + 1}: {lines[line].Trim()}");
                }
            }
        }

        Assert.True(found.Count == 0, "Read through SharedFile instead:\n" + string.Join('\n', found));
    }

    /// <summary>The framework's readers that share reading only, and any open that asks to read alone.</summary>
    [GeneratedRegex(
        @"(?<![\w.])(System\.IO\.)?File\.(ReadAll(Text|Bytes|Lines)(Async)?|ReadLines(Async)?|OpenRead|OpenText)\s*\("
        + @"|FileAccess\.Read\b(?!Write)"
    )]
    private static partial Regex UnsharedRead();

    static string EngineRoot() {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent) {
            if (File.Exists(Path.Combine(dir.FullName, "Patina.slnx"))) {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the engine folder.");
    }
}
