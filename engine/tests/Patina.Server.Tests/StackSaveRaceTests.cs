using Patina.Contracts;
using Patina.Server.Stacks;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

/// <summary>What happens to a save or a wait while something else happens to the stack (#299).</summary>
public sealed class StackSaveRaceTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-stack-save-race-");

    public void Dispose() => folder.Delete(true);

    /// <summary>
    ///     Choosing or finishing a bake is no edit of the document. During a save it left the stack unsaved, and Close
    ///     then refused it with a 409.
    /// </summary>
    [Fact]
    public void A_bake_chosen_during_a_save_leaves_the_stack_saved() {
        var session = Opened("Crate");
        session.Apply(new([new StackSetSeed(9)]));

        var (text, taken) = session.PrepareSave(false);
        session.UseMeshMaps(Path.Combine(folder.FullName, "bakes"));
        session.FinishSave(text, taken);

        Assert.False(session.Summary.Dirty);
    }

    /// <summary>An edit made while a save waits for the Worker is not in the file, so the stack stays dirty.</summary>
    [Fact]
    public void An_edit_during_a_save_leaves_the_stack_unsaved() {
        var session = Opened("Crate");
        session.Apply(new([new StackSetSeed(9)]));

        var (text, taken) = session.PrepareSave(false);
        session.Apply(new([new StackSetSeed(11)]));
        session.FinishSave(text, taken);

        Assert.True(session.Summary.Dirty);
    }

    /// <summary>
    ///     An edit made during a save and undone again leaves the document as the file holds it. A count of edits said
    ///     otherwise, so Close refused a stack that had nothing to lose (#306).
    /// </summary>
    [Fact]
    public void An_edit_undone_during_a_save_leaves_the_stack_saved() {
        var session = Opened("Crate");
        session.Apply(new([new StackSetSeed(9)]));

        var (text, taken) = session.PrepareSave(false);
        session.Apply(new([new StackSetSeed(11)]));
        session.Undo();
        session.FinishSave(text, taken);

        Assert.False(session.Summary.Dirty);
        session.Redo();
        Assert.True(session.Summary.Dirty);
    }

    /// <summary>The step saved, undone during the save, is still what the file holds: it is unsaved until redone.</summary>
    [Fact]
    public void A_step_undone_during_a_save_is_saved_once_redone() {
        var session = Opened("Crate");
        session.Apply(new([new StackSetSeed(9)]));

        var (text, taken) = session.PrepareSave(false);
        session.Undo();
        session.FinishSave(text, taken);

        Assert.True(session.Summary.Dirty);
        session.Redo();
        Assert.False(session.Summary.Dirty);
    }

    /// <summary>
    ///     Two stacks waited on at once (a save on one, a sample on the other) stopped each other's evaluations, which
    ///     on a slow GPU repeated forever. A stack nobody waits on still gives way to one that is waited on (ADR 0040).
    /// </summary>
    [Fact]
    public void Two_stacks_waited_on_never_stop_each_other() {
        var crate = Opened("Crate");
        var barrel = Opened("Barrel");
        StackSession[] open = [crate, barrel];

        using (barrel.Reading()) {
            Assert.True(StackEvaluator.Supersedes(crate, crate.Summary.Revision, open));
            using (crate.Reading()) {
                Assert.False(StackEvaluator.Supersedes(crate, crate.Summary.Revision, open));
                Assert.False(StackEvaluator.Supersedes(barrel, barrel.Summary.Revision, open));

                // A newer revision of the stack waited on still stops an older one.
                Assert.True(StackEvaluator.Supersedes(crate, crate.Summary.Revision - 1, open));
            }
        }
    }

    StackSession Opened(string name) {
        var path = Path.Combine(folder.FullName, $"{name}.vxlayers");
        var text = StackDocuments.Text(Sample(name));
        File.WriteAllText(path, text);
        return new(path, text, static () => { });
    }

    static LayerStackAsset Sample(string name) =>
        new() {
            Name = name,
            BaseWidth = 64,
            BaseHeight = 64,
            Sets = [
                new() {
                    Name = "Body",
                    Channels = [new() { Usage = "baseColor", Default = [0.5f, 0.5f, 0.5f, 1f] }],
                    Layers = [new() { Id = "base", Name = "Base", Kind = LayerKind.Fill }]
                }
            ]
        };
}
