namespace Patina.Contracts;

/// <summary>Limits a layer to some of the model's named meshes, on top of any mask it has.</summary>
/// <param name="Parts">The named meshes it stays on. Empty is the whole model.</param>
/// <param name="Asset">The picture of where they lie, from <c>POST /stacks/{id}/mesh-mask</c>.</param>
public sealed record StackMeshMask(IReadOnlyList<string> Parts, string Asset);

/// <summary>The body of <c>POST /api/v1/stacks/{id}/mesh-mask</c>.</summary>
/// <param name="Parts">The named meshes to draw.</param>
public sealed record StackMeshMaskRequest(IReadOnlyList<string> Parts);

/// <summary>The named meshes of a stack's model, and the picture of some of them.</summary>
/// <param name="Parts">The named meshes in its texture set.</param>
/// <param name="Asset">The picture, when one was asked for.</param>
public sealed record StackMeshParts(IReadOnlyList<string> Parts, string? Asset = null);

/// <summary>
///     A camera in the mesh's own space, which a stroke keeps because a position on a picture means nothing without
///     it. Any client with a camera can fill this in; <c>POST /viewport/pick</c> gives the viewport's.
/// </summary>
/// <param name="Eye">Where it is: three numbers.</param>
/// <param name="Right">The unit direction a column grows in: three numbers.</param>
/// <param name="Up">The unit direction a row shrinks in: three numbers.</param>
/// <param name="Forward">The unit direction it looks: three numbers.</param>
/// <param name="VerticalFieldOfView">In radians.</param>
/// <param name="Width">The picture's width, in the units stamps and the brush's radius are in.</param>
/// <param name="Height">Its height.</param>
public sealed record StackView(
    IReadOnlyList<float> Eye,
    IReadOnlyList<float> Right,
    IReadOnlyList<float> Up,
    IReadOnlyList<float> Forward,
    float VerticalFieldOfView,
    int Width,
    int Height
);
