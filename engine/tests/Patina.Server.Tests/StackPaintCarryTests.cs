using Patina.Contracts;
using Patina.Ipc;
using Patina.Server.Api;
using Patina.Server.Stacks;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

/// <summary>
///     Paint carried from one of a mesh's UV sets onto the one a texture set is laid out in (#210): every canvas becomes
///     a new one the Worker lays out again, so an undo names the old ones, which nothing touched.
/// </summary>
public sealed class StackPaintCarryTests {
    static readonly IReadOnlyList<float> White = [1f, 1f, 1f, 1f];

    static StackEditor Painted() {
        var editor = new StackEditor("/tmp/Carried.vxlayers", Sample());
        editor.Apply(new([new StackSetModel("ramp.gltf")]));
        editor.Apply(new([new StackStroke("s", "paint", "baseColor", new(4f), White, [new(8f, 8f)])]));
        editor.Apply(new([new StackStroke("m", "paint", "baseColor", new(4f), White, [new(8f, 8f)], Mask: true)]));
        return editor;
    }

    [Fact]
    public void A_new_uv_set_and_its_paint_carried_are_one_step_of_new_canvases() {
        var editor = Painted();
        var before = editor.Set.Layers[1];

        editor.Apply(new([new StackSetSettings(UvSet: 1), new StackCarryPaint(0)]));

        var after = editor.Set.Layers[1];
        Assert.Equal(1, editor.Set.UvSet);
        Assert.NotEqual(before.Paint, after.Paint);
        Assert.NotEqual(before.Mask.Paint, after.Mask.Paint);
        var carried = editor.DoneSteps[^1].Strokes;
        Assert.Equal(
            [(before.Paint, after.Paint), (before.Mask.Paint, after.Mask.Paint)],
            carried.Select(static entry => (entry.From!, entry.Paint))
        );
        Assert.All(carried, static entry => Assert.Equal(new StackCarry(0, 1), entry.Carry));
        Assert.All(carried, static entry => Assert.Equal((64, 64), (entry.Width, entry.Height)));

        editor.Undo();

        Assert.Equal(0, editor.Set.UvSet);
        Assert.Equal(before.Paint, editor.Set.Layers[1].Paint);
        Assert.DoesNotContain(editor.Standing, static entry => entry.Carry is not null);
    }

    [Fact]
    public void Paint_carried_from_the_uv_set_it_is_in_is_refused_with_a_code() {
        var editor = Painted();

        var refused = Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([new StackCarryPaint(0)])));

        Assert.Equal(Refusals.CarrySameUvSet, refused.Code);
        Assert.Equal("1", refused.Values["uvSet"]);
    }

    [Fact]
    public void Paint_is_carried_on_a_mesh_and_not_without_one() {
        var editor = new StackEditor("/tmp/Loose.vxlayers", Sample());

        var refused = Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([new StackCarryPaint(1)])));

        Assert.Equal(Refusals.NoMesh, refused.Code);
    }

    [Fact]
    public void A_set_with_no_canvas_carries_nothing() {
        var editor = new StackEditor("/tmp/Bare.vxlayers", Sample());
        editor.Apply(new([new StackSetModel("ramp.gltf")]));

        editor.Apply(new([new StackSetSettings(UvSet: 1), new StackCarryPaint(0)]));

        Assert.Empty(editor.DoneSteps[^1].Strokes);
        Assert.Equal(string.Empty, editor.Set.Layers[1].Paint.Trim());
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
