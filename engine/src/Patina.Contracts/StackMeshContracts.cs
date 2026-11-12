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
