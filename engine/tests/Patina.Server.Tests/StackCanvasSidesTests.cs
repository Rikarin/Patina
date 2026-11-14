using Patina.Contracts;
using Patina.Server.Api;
using Patina.Server.Stacks;
using Vixen.Texturing.Painting;

namespace Patina.Server.Tests;

/// <summary>
///     A canvas larger than its texture set (#19): a stroke paints it at its own size, on the model or in the 2D view,
///     whose stamps are carried from the set's texels onto the canvas's.
/// </summary>
public sealed class StackCanvasSidesTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-sides-");

    public void Dispose() => folder.Delete(true);

    string Canvas(int width, int height) {
        const string name = "paint.vxpaint";
        using var stream = File.Create(Path.Combine(folder.FullName, name));
        new PaintCanvas(width, height).Write(stream);
        return name;
    }

    [Fact]
    public void A_larger_canvas_of_the_sets_shape_is_painted_at_its_own_size() {
        var sides = new StackCanvasSides(folder.FullName);

        Assert.Equal((2048, 2048), sides.Check("Paint", Canvas(2048, 2048), 1024, 1024));
    }

    [Fact]
    public void A_larger_canvas_of_another_shape_is_refused() {
        var sides = new StackCanvasSides(folder.FullName);

        var refused = Assert.Throws<StackEditRefusedException>(() => sides.Check(
                "Paint",
                Canvas(2048, 1024),
                1024,
                1024
            )
        );

        Assert.Equal(Refusals.CanvasLarger, refused.Code);
    }

    [Fact]
    public void A_canvas_the_sets_size_or_none_is_painted_at_the_sets_size() {
        var sides = new StackCanvasSides(folder.FullName);

        Assert.Equal((512, 256), sides.Check("Paint", "none.vxpaint", 512, 256));
        Assert.Equal((1024, 1024), sides.Check("Paint", Canvas(1024, 1024), 1024, 1024));
    }

    /// <summary>
    ///     Everything a stroke in the 2D view measures in the set's texels is multiplied onto the canvas's: a texel's
    ///     middle at 10.5 on a set of 512 is at 21 on a canvas of 1024, the middle of the same place.
    /// </summary>
    [Fact]
    public void A_stroke_in_the_2d_view_is_carried_onto_the_canvas_texels() {
        var stroke = new StackStroke(
            "s",
            "paint",
            "baseColor",
            new(8f, Spacing: 0.2f),
            [1f, 0f, 0f, 1f],
            [new(10.5f, 20f, 0.5f)],
            Stencil: new("stencil.png", [30f, 40f], 2f),
            Resume: [4f, 6f],
            Source: new("clone", Offset: [3f, -5f])
        );

        var scaled = StackStrokeScale.Scaled(stroke, 2f);

        Assert.Equal(16f, scaled.Brush.Radius);
        Assert.Equal(0.2f, scaled.Brush.Spacing);
        Assert.Equal(new(21f, 40f, 0.5f), Assert.Single(scaled.Stamps));
        Assert.Equal([60f, 80f], scaled.Stencil!.Centre);
        Assert.Equal(4f, scaled.Stencil.Scale);
        Assert.Equal([8f, 12f], scaled.Resume!);
        Assert.Equal([6f, -10f], scaled.Source!.Offset!);
    }

    [Fact]
    public void A_brush_sized_by_the_model_keeps_its_size() {
        var stroke = new StackStroke("s", "paint", "baseColor", new(5f, SizeSpace: "model"), [1f, 0f, 0f, 1f], []);

        Assert.Equal(5f, StackStrokeScale.Scaled(stroke, 4f).Brush.Radius);
    }
}
