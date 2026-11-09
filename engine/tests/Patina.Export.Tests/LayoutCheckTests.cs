using Patina.Contracts;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;

namespace Patina.Export.Tests;

/// <summary>
///     What a texture set's layout and maps measure before export (#159), on shapes whose answer is on paper: a square
///     of two units laid over the whole tile has a quarter of a tile per unit squared, so half the side per unit.
/// </summary>
public sealed class LayoutCheckTests {
    const int Side = 512;

    /// <summary>A square two units across, facing +z, laid over the whole tile in two triangles.</summary>
    static readonly Vector3[] Square = [new(-1f, -1f, 0f), new(1f, -1f, 0f), new(1f, 1f, 0f), new(-1f, 1f, 0f)];

    static readonly Vector2[] Tile = [new(0f, 1f), new(1f, 1f), new(1f, 0f), new(0f, 0f)];

    static readonly int[] Two = [0, 1, 2, 0, 2, 3];

    static readonly BoundingBox Bounds = new(new(-1f, -1f, 0f), new(1f, 1f, 0f));

    static LayoutMeasures Measure(Vector3[] positions, Vector2[] coordinates, int[] indices) =>
        LayoutCheck.Measure(positions, coordinates, indices, Bounds, Side).Measures;

    [Fact]
    public void A_square_over_the_whole_tile_has_half_the_side_per_unit_and_nothing_wrong() {
        var measures = Measure(Square, Tile, Two);

        Assert.Equal(2, measures.Triangles);
        Assert.Equal(4, measures.SurfaceArea, 1e-5);
        Assert.Equal(1, measures.MapArea, 1e-5);
        Assert.Equal(Side / 2d, LayoutCheck.Density(measures, Side), 1e-3);
        Assert.Equal((0, 0, 0), (measures.Outside, measures.Degenerate, measures.Flipped));
        // The diagonal the two triangles share runs through texel centres, which neither of them takes.
        Assert.Equal(0, measures.Overlap);
        Assert.Empty(measures.Spots);
    }

    [Fact]
    public void The_square_laid_twice_overlaps_everywhere_it_covers() {
        var measures = Measure([..Square, ..Square], [..Tile, ..Tile], [..Two, ..Two.Select(static i => i + 4)]);

        Assert.Equal(1, measures.Overlap, 1e-9);
        var spot = Assert.Single(measures.Spots);
        Assert.Equal(CheckCodes.UvOverlap, spot.Code);
        Assert.Equal(2, spot.Triangle);
    }

    [Fact]
    public void A_triangle_turned_over_past_the_tile_and_with_no_area_are_each_counted() {
        Vector2[] turned = [new(0f, 1f), new(1f, 1f), new(1f, 0f), new(0f, 0f), new(0f, 0f), new(1f, 0f), new(1f, 1f)];
        Vector3[] positions = [..Square, new(-1f, -1f, 0f), new(1f, -1f, 0f), new(1f, 1f, 0f)];
        var flipped = Measure(positions, turned, [0, 1, 2, 4, 5, 6]);
        Assert.Equal(1, flipped.Flipped);

        var outside = Measure(Square, [..Tile.Select(static uv => uv + new Vector2(1f, 0f))], Two);
        Assert.Equal(2, outside.Outside);
        Assert.Equal(CheckCodes.UvOutside, Assert.Single(outside.Spots).Code);

        Vector2[] line = [new(0f, 0f), new(0.5f, 0.5f), new(1f, 1f), new(0f, 0f)];
        var degenerate = Measure(Square, line, [0, 1, 2]);
        Assert.Equal(1, degenerate.Degenerate);
    }

    [Fact]
    public void A_spot_looks_at_its_triangle_along_the_way_it_faces() {
        var spot = Assert.Single(Measure(Square, [..Tile.Select(static uv => uv + new Vector2(1f, 0f))], [0, 1, 2]).Spots);
        var camera = spot.Camera!;

        // The square faces +z: a camera at yaw 0 and pitch 0 looks down -z at it.
        Assert.Equal(0, camera.Yaw, 1e-6);
        Assert.Equal(0, camera.Pitch, 1e-6);
        // The triangle's middle is a third of the way to (1, -1) from the centre, in bounding radii of √2.
        Assert.Equal(1 / 3d / Math.Sqrt(2), camera.PanX, 1e-6);
        Assert.Equal(-1 / 3d / Math.Sqrt(2), camera.PanY, 1e-6);
    }

    [Fact]
    public void A_place_on_the_map_is_found_on_the_model() {
        var spot = LayoutCheck.Located(new(CheckCodes.AlbedoDark, -1, 0.9f, 0.5f, null), Square, Tile, Two, Bounds);

        Assert.Equal(0, spot.Triangle);
        Assert.NotNull(spot.Camera);
        // u 0.9 is x 0.8, and v 0.5 is y 0, in units: in radii of √2 from the centre.
        Assert.Equal(0.8 / Math.Sqrt(2), spot.Camera.PanX, 1e-5);
        Assert.Equal(0, spot.Camera.PanY, 1e-5);
    }

    static Bitmap Solid(byte r, byte g, byte b, int side = 8) {
        var pixels = new byte[side * side * 4];
        for (var at = 0; at < pixels.Length; at += 4) {
            (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (r, g, b, 255);
        }

        return new(side, side, pixels);
    }

    [Theory]
    [InlineData(10, 0, CheckCodes.AlbedoDark)]
    [InlineData(250, 0, CheckCodes.AlbedoBright)]
    [InlineData(120, 255, CheckCodes.MetalDark)]
    [InlineData(200, 128, CheckCodes.MetalPartial)]
    public void A_value_no_material_has_is_measured_over_every_texel(byte grey, byte metal, string code) {
        var measure = Assert.Single(MapCheck.Measure(Solid(grey, grey, grey), Solid(metal, metal, metal), null, 0));

        Assert.Equal(code, measure.Code);
        Assert.Equal(1, measure.Share);
        Assert.Equal((1 / 16f, 1 / 16f), (measure.Spot.U, measure.Spot.V));
    }

    [Theory]
    [InlineData(128, 0)]
    [InlineData(31, 0)]
    [InlineData(239, 0)]
    [InlineData(200, 255)]
    public void A_value_a_material_can_have_says_nothing(byte grey, byte metal) =>
        Assert.Empty(MapCheck.Measure(Solid(grey, grey, grey), Solid(metal, metal, metal), null, 0));

    [Fact]
    public void Only_texels_the_layout_covers_count() {
        var colour = Solid(128, 128, 128);
        // The left half is too dark, and only the right half is laid out.
        for (var y = 0; y < 8; y++) {
            for (var x = 0; x < 4; x++) {
                var at = colour.Offset(x, y);
                (colour.Pixels[at], colour.Pixels[at + 1], colour.Pixels[at + 2]) = (5, 5, 5);
            }
        }

        var right = new bool[4 * 4];
        for (var at = 0; at < right.Length; at++) {
            right[at] = at % 4 >= 2;
        }

        Assert.Equal(0.5, Assert.Single(MapCheck.Measure(colour, null, null, 0)).Share);
        Assert.Empty(MapCheck.Measure(colour, null, right, 4));
    }
}
