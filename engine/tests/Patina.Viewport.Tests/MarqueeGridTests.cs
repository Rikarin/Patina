using Vixen.Core.Mathematics;

namespace Patina.Viewport.Tests;

/// <summary>
///     Where a marquee looks for meshes (#110): points a few pixels apart across the rectangle, whichever corner it was
///     dragged from, and none outside the picture.
/// </summary>
public sealed class MarqueeGridTests {
    /// <summary>A rectangle dragged either way covers the same points, inside it, at most the spacing apart.</summary>
    [Fact]
    public void A_marquee_covers_its_rectangle_from_either_corner() {
        Vector2[] down = [..MarqueeGrid.Points(new(0.25f, 0.5f), new(0.75f, 1f), 400, 200)];
        Vector2[] up = [..MarqueeGrid.Points(new(0.75f, 1f), new(0.25f, 0.5f), 400, 200)];

        Assert.Equal(down, up);
        Assert.All(down, static point => Assert.InRange(point.X, 100f, 300f));
        Assert.All(down, static point => Assert.InRange(point.Y, 100f, 200f));
        var columns = down.Select(static point => point.X).Distinct().Order().ToArray();
        Assert.True(columns[0] - 100f <= MarqueeGrid.Spacing, $"The first column is at {columns[0]}.");
        Assert.All(
            columns.Zip(columns.Skip(1)),
            static pair => Assert.True(pair.Second - pair.First <= MarqueeGrid.Spacing)
        );
    }

    /// <summary>A click is one point, a marquee stops at the picture's edge, and a huge one is capped.</summary>
    [Fact]
    public void A_marquee_is_at_least_a_point_and_at_most_the_picture() {
        var point = Assert.Single(MarqueeGrid.Points(new(0.5f, 0.5f), new(0.5f, 0.5f), 100, 100));
        Assert.Equal(new(50f, 50f), point);

        Vector2[] past = [..MarqueeGrid.Points(new(-1f, -1f), new(2f, 2f), 100, 100)];
        Assert.All(past, static at => Assert.InRange(at.X, 0f, 100f));
        Assert.All(past, static at => Assert.InRange(at.Y, 0f, 100f));

        Assert.Equal(
            MarqueeGrid.MostAcross * MarqueeGrid.MostAcross,
            MarqueeGrid.Points(Vector2.Zero, Vector2.One, 4000, 4000).Count()
        );
    }
}
