using Patina.Library;
using System.Text.RegularExpressions;

namespace Patina.Server.Tests;

/// <summary>
///     Letting a document go while its autosave is still being written (#162, and the save texture-sets saw fail).
/// </summary>
public sealed partial class AutosaveGateTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-autosave-gate-");

    public void Dispose() => folder.Delete(true);

    /// <summary>
    ///     A save lets go of the document's autosaves while the Worker is still writing paint into the newest. The
    ///     finished ones go at once; the one being written is left to the autosave, which deletes it when it finds it
    ///     let go. Deleting it under the Worker is what failed a save with "Directory not empty".
    /// </summary>
    [Fact]
    public void Letting_go_leaves_the_autosave_being_written_to_whoever_writes_it() {
        var root = folder.FullName;
        var file = Path.Combine(root, "projects", "crate", "crate.vxlayers");
        var gate = new AutosaveGate();
        Assert.True(gate.Finish(root, file, gate.Begin(root, file), gate.Mark(), "older", 3));
        Assert.Equal(1, Recovery.Count(root, file));

        var mark = gate.Mark();
        var writing = gate.Begin(root, file);
        File.WriteAllText(Path.Combine(writing, "early.vxpaint"), "paint");
        gate.Forget(root, file);

        Assert.True(Directory.Exists(writing));
        Assert.Equal(0, Recovery.Count(root, file));

        File.WriteAllText(Path.Combine(writing, "late.vxpaint"), "paint");
        Assert.False(gate.Finish(root, file, writing, mark, "autosaved", 3));
        Assert.False(Directory.Exists(Recovery.Folder(root, file)));
    }

    /// <summary>
    ///     Only the gate lets go of autosaves (#239). A letting go anywhere else can delete one a Worker is still
    ///     writing, or let one begun before it land after it; it is easy to copy, so a new one fails here. Recovery
    ///     itself only declares it.
    /// </summary>
    [Fact]
    public void Nothing_but_the_gate_lets_go_of_autosaves() {
        var source = Path.Combine(EngineRoot(), "src");
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
                     .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                     .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                     .Where(static f => Path.GetFileName(f) != "AutosaveGate.cs")
                     .Order(StringComparer.Ordinal)) {
            var own = Path.GetFileName(file) == "Recovery.cs";
            var lines = File.ReadAllLines(file);
            for (var line = 0; line < lines.Length; line++) {
                if ((own ? ClearWithin() : ClearOfRecovery()).IsMatch(lines[line])) {
                    found.Add($"{Path.GetRelativePath(source, file)}:{line + 1}: {lines[line].Trim()}");
                }
            }
        }

        Assert.True(found.Count == 0, "Let go through AutosaveGate.Forget instead:\n" + string.Join('\n', found));
    }

    /// <summary>A call of <see cref="Recovery.Clear" /> from outside it.</summary>
    [GeneratedRegex(@"\bRecovery\s*\.\s*Clear\s*\(")]
    private static partial Regex ClearOfRecovery();

    /// <summary>A call of Clear inside Recovery, which is anything but its declaration.</summary>
    [GeneratedRegex(@"(?<!\bvoid\s+)\bClear\s*\(")]
    private static partial Regex ClearWithin();

    static string EngineRoot() {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent) {
            if (File.Exists(Path.Combine(dir.FullName, "Patina.slnx"))) {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the engine folder.");
    }
}
