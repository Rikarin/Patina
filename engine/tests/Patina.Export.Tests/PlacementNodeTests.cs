using CsCheck;
using Patina.Contracts;
using Patina.Export.Materials;

namespace Patina.Export.Tests;

/// <summary>
///     A glTF texture transform as a <c>Space/Transform 2D</c> node: the node's own mapping, written out as its shader
///     does it, reads the source where glTF's matrix says, at every point of the picture.
/// </summary>
public sealed class PlacementNodeTests {
    /// <summary>Where <c>Transform2D.rvn</c> reads its source for an output point, on a square picture.</summary>
    static (float U, float V) NodeReads(Transform2DValues node, float u, float v) {
        var (c, s) = (MathF.Cos(node.Rotation), MathF.Sin(node.Rotation));
        var a00 = c * node.ScaleX - s * node.ShearY * node.ScaleX;
        var a01 = c * node.ShearX * node.ScaleY - s * node.ScaleY;
        var a10 = s * node.ScaleX + c * node.ShearY * node.ScaleX;
        var a11 = s * node.ShearX * node.ScaleY + c * node.ScaleY;
        var det = a00 * a11 - a01 * a10;
        var (dx, dy) = (u - 0.5f - node.OffsetX, v - 0.5f - node.OffsetY);
        return ((a11 * dx - a01 * dy) / det + 0.5f, (-a10 * dx + a00 * dy) / det + 0.5f);
    }

    /// <summary>Where glTF's <c>KHR_texture_transform</c> reads the picture: scaled, turned, moved.</summary>
    static (float U, float V) GltfReads(ImportedPlacement placement, float u, float v) {
        var (c, s) = (MathF.Cos(placement.Rotation), MathF.Sin(placement.Rotation));
        var (x, y) = (u * placement.ScaleX, v * placement.ScaleY);
        return (c * x + s * y + placement.OffsetX, -s * x + c * y + placement.OffsetY);
    }

    static void AssertSameEverywhere(ImportedPlacement placement) {
        var node = PlacementNode.Of(placement);
        Assert.NotNull(node);
        foreach (var (u, v) in (IEnumerable<(float, float)>)
                 [(0f, 0f), (1f, 0f), (0f, 1f), (0.3f, 0.8f), (0.5f, 0.5f)]) {
            var expected = GltfReads(placement, u, v);
            var read = NodeReads(node, u, v);
            Assert.Equal(expected.U, read.U, 3);
            Assert.Equal(expected.V, read.V, 3);
        }
    }

    [Fact]
    public void AMoveAloneIsTheNodesOffsetTheOtherWay() {
        var node = PlacementNode.Of(new(0.25f, -0.5f, 0f, 1f, 1f))!;

        Assert.Equal(-0.25f, node.OffsetX, 5);
        Assert.Equal(0.5f, node.OffsetY, 5);
        Assert.Equal(0f, node.ShearX, 5);
        Assert.Equal(0f, node.ShearY, 5);
    }

    /// <summary>Unequal scales under a turn are what the shear is for; without it this placement draws wrong.</summary>
    [Fact]
    public void AnUnevenScaleUnderATurnIsDrawnExactly() => AssertSameEverywhere(new(0.25f, -0.5f, 0.7f, 2f, 3f));

    [Fact]
    public void AMirrorIsDrawnToo() => AssertSameEverywhere(new(0.1f, 0.2f, 1.2f, -1f, 2f));

    [Fact]
    public void AScaleOfZeroIsNotDrawn() => Assert.Null(PlacementNode.Of(new(0f, 0f, 0f, 0f, 1f)));

    [Fact]
    [Trait("Category", "Property")]
    public void EveryPlacementIsDrawnWhereGltfReads() =>
        Gen.Select(
                Gen.Float[-2f, 2f],
                Gen.Float[-2f, 2f],
                Gen.Float[-MathF.PI, MathF.PI],
                Gen.Float[0.25f, 4f],
                Gen.Float[0.25f, 4f]
            )
            .Sample(static (x, y, turn, scaleX, scaleY) => AssertSameEverywhere(new(x, y, turn, scaleX, scaleY)));
}
