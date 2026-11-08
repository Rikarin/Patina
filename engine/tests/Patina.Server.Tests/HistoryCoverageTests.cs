using Patina.Contracts;
using Patina.Server.Graphs;
using Patina.Server.Stacks;
using Vixen.NodeGraph;
using Vixen.TextureGraph;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

/// <summary>
///     The History pane (#193) lists every kind of edit a client can send, under a name a person reads, and a jump to
///     any step gives the document as it stood just after it: new nodes (#167, #172), switching off, relinking, laying
///     out and presets (#181, #182), the brush's new settings (#123, #124), and anchors and flattening (#107, #111).
/// </summary>
public sealed class HistoryCoverageTests {
    static readonly NodeTypeRegistry Registry = Catalogue();
    static readonly IReadOnlyList<float> White = [1f, 1f, 1f, 1f];

    static NodeTypeRegistry Catalogue() {
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        return registry;
    }

    [Fact]
    public void A_graphs_history_names_each_authoring_edit_and_jumps_to_just_after_it() {
        var editor = new GraphEditor(GraphTesting.Sample(), Registry);
        List<string> after = [];
        foreach (var command in (GraphCommand[]) [
                     new GraphAddNode("Analysis/Histogram", 1200, 0),
                     new GraphSetBypass([2], true),
                     new GraphSavePreset("Worn", new Dictionary<string, string> { ["Wear"] = "0.9" }),
                     new GraphArrange([]),
                     new GraphRemoveNodes([2], true)
                 ]) {
            editor.Apply(new([command]));
            after.Add(GraphDocuments.Text(editor.Document));
        }

        var list = Steps(editor);
        Assert.Equal(
            ["Add Histogram", "Bypass", "Save Preset Worn", "Auto-Layout", "Delete and Relink"],
            list.Done.Select(static step => step.Label)
        );

        for (var at = after.Count - 1; at >= 0; at--) {
            UndoHistory.Jump(Steps(editor), list.Done[at].Id, editor.Undo, editor.Redo);
            Assert.Equal(after[at], GraphDocuments.Text(editor.Document));
        }

        UndoHistory.Jump(Steps(editor), list.Done[^1].Id, editor.Undo, editor.Redo);
        Assert.Equal(after[^1], GraphDocuments.Text(editor.Document));
        Assert.Empty(Steps(editor).Undone);
    }

    [Fact]
    public void A_stacks_history_names_strokes_anchors_and_flattening_and_jumps_with_its_journal() {
        var editor = new StackEditor("/tmp/History.vxlayers", Stack());
        StackBrush brush = new(
            6f,
            Aspect: 2f,
            FlowJitter: 0.3f,
            ScatterAxis: "across",
            Accumulate: false,
            SizeSpace: "texture"
        );
        List<(string Text, int Entries)> after = [];
        foreach (var command in (StackCommand[]) [
                     new StackStroke("s", "paint", "baseColor", brush, White, [new(8f, 8f), new(20f, 8f)]),
                     new StackSetLayer("base", AnchorPoint: "Ground"),
                     new StackFlattenLayer("folder")
                 ]) {
            editor.Apply(new([command]));
            after.Add((StackDocuments.Text(editor.Document), editor.Standing.Count));
        }

        var list = Steps(editor);
        Assert.Equal(["Stroke", "Change layer", "Flatten folder"], list.Done.Select(static step => step.Label));

        UndoHistory.Jump(list, list.Done[0].Id, editor.Undo, editor.Redo);
        Assert.Equal(after[0], (StackDocuments.Text(editor.Document), editor.Standing.Count));
        Assert.Equal(brush, Assert.Single(editor.Standing).Brush);
        Assert.Equal(string.Empty, editor.Set.Layers[0].AnchorPoint);

        UndoHistory.Jump(Steps(editor), list.Done[^1].Id, editor.Undo, editor.Redo);
        Assert.Equal(after[^1], (StackDocuments.Text(editor.Document), editor.Standing.Count));
        Assert.Single(editor.Standing, static entry => entry.Capture is not null);

        UndoHistory.Jump(Steps(editor), list.Done[1].Id, editor.Undo, editor.Redo);
        Assert.Equal(after[1], (StackDocuments.Text(editor.Document), editor.Standing.Count));
        Assert.Equal("Ground", editor.Set.Layers[0].AnchorPoint);
        Assert.DoesNotContain(editor.Standing, static entry => entry.Capture is not null);
    }

    static HistoryList Steps(GraphEditor editor) =>
        UndoHistory.List(editor.DoneSteps, editor.UndoneSteps, static step => new(step.Id, step.Label));

    static HistoryList Steps(StackEditor editor) =>
        UndoHistory.List(editor.DoneSteps, editor.UndoneSteps, static step => new(step.Id, step.Label));

    static LayerStackAsset Stack() =>
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
                        new() { Id = "paint", Name = "Paint", Kind = LayerKind.Paint },
                        new() {
                            Id = "folder",
                            Name = "Folder",
                            Kind = LayerKind.Group,
                            Children = [new() { Id = "inner", Name = "Inner", Kind = LayerKind.Fill }]
                        }
                    ]
                }
            ]
        };
}
