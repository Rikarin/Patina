using CsCheck;
using Patina.Contracts;
using Patina.Server.Stacks;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

public sealed class StackEditorPropertyTests {
    static readonly IReadOnlyList<float> White = [1f, 1f, 1f, 1f];

    /// <summary>
    ///     Random batches of every command, some of which are refused, then undoing all of them, gives the original
    ///     document and an empty journal; redoing all of them gives the edited document and the same journal.
    /// </summary>
    [Fact]
    [Trait("Category", "Property")]
    public void Random_batches_undone_return_the_original_stack() =>
        Gen.Int.Array[1, 30]
            .Sample(static batches => {
                    var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
                    var original = StackDocuments.Text(editor.Document);
                    var steps = 0;
                    foreach (var seed in batches) {
                        var before = StackDocuments.Text(editor.Document);
                        var random = new Random(seed);
                        var history = editor.Standing.Count;
                        try {
                            editor.Apply(new([..Enumerable.Range(0, random.Next(1, 4)).Select(_ => Pick(editor, random))]));
                            steps++;
                        } catch (StackEditRefusedException) {
                            Assert.Equal(before, StackDocuments.Text(editor.Document));
                            Assert.Equal(history, editor.Standing.Count);
                        }
                    }

                    var edited = StackDocuments.Text(editor.Document);
                    long[] journal = [..editor.Standing.Select(static entry => entry.Id)];
                    for (var step = 0; step < steps; step++) {
                        Assert.NotNull(editor.Undo());
                    }

                    Assert.Null(editor.Undo());
                    Assert.Equal(original, StackDocuments.Text(editor.Document));
                    Assert.Empty(editor.Standing);
                    for (var step = 0; step < steps; step++) {
                        Assert.NotNull(editor.Redo());
                    }

                    Assert.Null(editor.Redo());
                    Assert.Equal(edited, StackDocuments.Text(editor.Document));
                    Assert.Equal(journal, editor.Standing.Select(static entry => entry.Id).ToArray());
                },
                iter: 200,
                threads: 1
            );

    /// <summary>The parts of a drag are one step, and whatever else arrives ends it.</summary>
    [Fact]
    public void The_parts_of_a_drag_are_one_step() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        editor.Apply(new([Stroke("a", true, 2)]));
        editor.Apply(new([Stroke("a", true, 3)]));
        Assert.Equal(5, Assert.Single(editor.Standing).Stamps.Count);
        editor.Apply(new([Stroke("b", false, 1)]));
        Assert.False(editor.HasOpenStroke);
        Assert.Equal(2, editor.Standing.Count);
        Assert.NotNull(editor.Undo());
        Assert.Equal(5, Assert.Single(editor.Standing).Stamps.Count);
    }

    /// <summary>A first stroke names the file, and undoing it takes the name back.</summary>
    [Fact]
    public void A_first_stroke_names_its_paint_file() {
        var editor = new StackEditor("/tmp/Crate.vxlayers", Sample());
        Assert.Equal("", LayerOf(editor).Paint);
        editor.Apply(new([Stroke("a", false, 1)]));
        Assert.Equal("Crate.Body.paint.vxpaint", LayerOf(editor).Paint);
        editor.Undo();
        Assert.Equal("", LayerOf(editor).Paint);
    }

    static LayerAsset LayerOf(StackEditor editor) => editor.Set.Layers.Single(static layer => layer.Id == "paint");

    static StackStroke Stroke(string name, bool open, int stamps) =>
        new(
            name,
            "paint",
            "baseColor",
            new(8f),
            White,
            [..Enumerable.Range(0, stamps).Select(static index => new StackStamp(index, index))],
            open
        );

    static StackCommand Pick(StackEditor editor, Random random) {
        string[] ids = [..editor.Set.Layers.Select(static layer => layer.Id), "absent"];
        var id = ids[random.Next(ids.Length)];
        return random.Next(5) switch {
            0 => new StackAddLayer(random.Next(4) == 0 ? "nonsense" : "fill", $"Layer {random.Next(4)}"),
            1 => new StackRemoveLayer(id),
            2 => new StackMoveLayer(id, random.Next(-2, 3)),
            3 => new StackSetLayer(
                id,
                random.Next(2) == 0 ? "Renamed" : null,
                random.Next(2) == 0,
                random.NextSingle() * 1.2f,
                random.Next(3) == 0 ? "Multiply" : null
            ),
            _ => new StackStroke(
                $"s{random.Next(3)}",
                id,
                random.Next(4) == 0 ? "absent" : "baseColor",
                new(random.Next(3)),
                White,
                [new(random.NextSingle() * 64f, random.NextSingle() * 64f)],
                random.Next(2) == 0,
                random.Next(4) == 0
            )
        };
    }

    static LayerStackAsset Sample() =>
        new() {
            Name = "Crate",
            BaseWidth = 64,
            BaseHeight = 64,
            Sets = [
                new() {
                    Name = "Body",
                    Channels = [new() { Usage = "baseColor", Default = [0.5f, 0.5f, 0.5f, 1f] }],
                    Layers = [
                        new() { Id = "base", Name = "Base", Kind = LayerKind.Fill },
                        new() { Id = "paint", Name = "Paint", Kind = LayerKind.Paint }
                    ]
                }
            ]
        };
}
