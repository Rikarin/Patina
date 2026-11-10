using Patina.Library;

namespace Patina.Server.Tests;

/// <summary>Letting a document go while its autosave is still being written (#162, and the save texture-sets saw fail).</summary>
public sealed class AutosaveGateTests : IDisposable {
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
}
