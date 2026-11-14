using Patina.Server.Api;
using Patina.Server.Stacks;
using Vixen.Texturing.Painting;

namespace Patina.Server.Tests;

/// <summary>
///     A canvas larger than its texture set (#19): a stroke on the model paints it at its own size, and one in the 2D
///     view, whose stamps are the set's texels, is still refused.
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
    public void A_stroke_on_the_model_paints_a_larger_canvas_at_its_own_size() {
        var sides = new StackCanvasSides(folder.FullName);

        Assert.Equal((2048, 2048), sides.Check("Paint", Canvas(2048, 2048), 1024, 1024, true));
    }

    [Fact]
    public void A_stroke_in_the_2d_view_on_a_larger_canvas_is_refused() {
        var sides = new StackCanvasSides(folder.FullName);

        var refused = Assert.Throws<StackEditRefusedException>(() => sides.Check(
                "Paint",
                Canvas(2048, 2048),
                1024,
                1024,
                false
            )
        );

        Assert.Equal(Refusals.CanvasLarger, refused.Code);
    }

    [Fact]
    public void A_larger_canvas_of_another_shape_is_refused_on_the_model_too() {
        var sides = new StackCanvasSides(folder.FullName);

        Assert.Throws<StackEditRefusedException>(() => sides.Check("Paint", Canvas(2048, 1024), 1024, 1024, true));
    }

    [Fact]
    public void A_canvas_the_sets_size_or_none_is_painted_at_the_sets_size() {
        var sides = new StackCanvasSides(folder.FullName);

        Assert.Equal((512, 256), sides.Check("Paint", "none.vxpaint", 512, 256, true));
        Assert.Equal((1024, 1024), sides.Check("Paint", Canvas(1024, 1024), 1024, 1024, false));
    }
}
